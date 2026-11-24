import * as THREE from 'three';
import { FormulaTeam } from '../data/teams';
import { CarRig } from './AssetLoader';
import { BuiltTrack, TrackProjection } from './TrackBuilder';

export interface CarInput {
  throttle: number; // 0..1
  brake: number;    // 0..1
  steer: number;    // -1 (left) .. +1 (right)
  ers: boolean;
  shiftUp?: boolean;
  shiftDown?: boolean;
}

// 8-speed F1 sequential gearbox top speeds in m/s (~348 km/h max)
const GEAR_MAX_SPEED = [0, 29, 43, 55, 67, 78, 88, 95, 102];

export class FormulaCar {
  public readonly id: string;
  public readonly isPlayer: boolean;
  public readonly team: FormulaTeam;
  public readonly driverName: string;
  public readonly rig: CarRig;

  // Spatial state
  public position = new THREE.Vector3();
  public yaw = 0; // radians, 0 = +Z
  public speed = 0; // forward m/s
  public lateralVel = 0; // sideways slip m/s
  public steerAngle = 0;
  public wheelSpin = 0;

  // Powertrain & Aero
  public gear = 1; // 1..8
  public rpm = 4800; // 4500..13500
  public ersCharge = 1.0; // 0..1
  public ersActive = false;
  public slipAmount = 0; // 0..1 for audio/VFX
  public collisionImpulse = 0;

  // Suspension visual state
  private pitch = 0;
  private roll = 0;

  // Race progress
  public trackHintIdx = 0;
  public projection!: TrackProjection;
  public lap = 1;
  public maxSectorReached = 0; // 0, 1, 2 checkpoints
  public totalProgress = 0;
  public lapStartTime = 0;
  public bestLapTime = Infinity;
  public finished = false;
  public finishTime = 0;

  constructor(id: string, isPlayer: boolean, team: FormulaTeam, driverName: string, rig: CarRig) {
    this.id = id;
    this.isPlayer = isPlayer;
    this.team = team;
    this.driverName = driverName;
    this.rig = rig;
  }

  resetAt(pos: THREE.Vector3, yaw: number, track: BuiltTrack) {
    this.position.copy(pos);
    this.yaw = yaw;
    this.speed = 0;
    this.lateralVel = 0;
    this.steerAngle = 0;
    this.gear = 1;
    this.rpm = 5200;
    this.ersCharge = 1.0;
    this.ersActive = false;
    this.slipAmount = 0;
    this.collisionImpulse = 0;
    this.lap = 1;
    this.maxSectorReached = 0;
    this.totalProgress = 0;
    this.lapStartTime = 0;
    this.bestLapTime = Infinity;
    this.finished = false;
    this.finishTime = 0;
    this.projection = track.projectToTrack(pos.x, pos.z);
    this.trackHintIdx = this.projection.sampleIndex;
    this.syncVisuals(0.016);
  }

  step(
    dt: number,
    input: CarInput,
    track: BuiltTrack,
    autoGear: boolean,
    steerSensitivity: number,
    raceTime: number,
    totalLaps: number,
    canMove: boolean
  ): { shifted: boolean; crossedLap: boolean } {
    let shifted = false;
    let crossedLap = false;
    this.collisionImpulse = 0;

    if (!canMove) {
      // Rev engine on the grid while waiting for lights out!
      const targetRpm = 4800 + input.throttle * 7600;
      this.rpm += (targetRpm - this.rpm) * Math.min(1, dt * 10);
      this.syncVisuals(dt);
      return { shifted, crossedLap };
    }

    // Track surface detection
    const proj = track.projectToTrack(this.position.x, this.position.z, this.trackHintIdx);
    this.projection = proj;
    this.trackHintIdx = proj.sampleIndex;

    const absLat = Math.abs(proj.lateralOffset);
    const onKerb = absLat > track.halfWidth - 0.6 && absLat <= track.halfWidth + 1.4;
    const offTrack = absLat > track.halfWidth + 1.4;
    const surfaceGrip = offTrack ? 0.58 : onKerb ? 0.88 : 1.0;
    const surfaceDrag = offTrack ? 0.48 : 0.0;

    // ERS / DRS logic
    if (input.ers && this.ersCharge > 0.04 && input.throttle > 0.25 && this.speed > 18) {
      this.ersActive = true;
      this.ersCharge = Math.max(0, this.ersCharge - dt * 0.22);
    } else {
      this.ersActive = false;
      const regen = input.brake > 0.1 ? 0.14 : 0.035;
      this.ersCharge = Math.min(1, this.ersCharge + dt * regen);
    }

    // Manual or Auto Gearbox
    const prevGear = this.gear;
    if (!autoGear) {
      if (input.shiftUp && this.gear < 8) this.gear++;
      if (input.shiftDown && this.gear > 1) this.gear--;
    } else {
      const upshiftSpd = GEAR_MAX_SPEED[this.gear] * 0.94;
      const downshiftSpd = this.gear > 1 ? GEAR_MAX_SPEED[this.gear - 1] * 0.72 : -1;
      if (this.speed > upshiftSpd && this.gear < 8) this.gear++;
      else if (this.speed < downshiftSpd && this.gear > 1) this.gear--;
    }
    if (this.gear !== prevGear) shifted = true;

    // Engine thrust & Aerodynamics (F1 downforce ~ v^2, drag ~ v^2)
    const speedNorm = Math.min(1, Math.max(0, this.speed / 100));
    const downforce = (1.0 + 1.55 * speedNorm * speedNorm) * this.team.aero;
    const ersBoost = this.ersActive ? 1.18 : 1.0;
    const drsDragFactor = this.ersActive ? 0.82 : 1.0;

    const gearTop = GEAR_MAX_SPEED[this.gear] * this.team.power * (this.ersActive ? 1.06 : 1.0);
    const accelBase = (28.5 - this.gear * 1.85) * this.team.power * ersBoost;
    const revFactor = this.speed < gearTop ? 1.0 : 0.08;

    const forwardAccel = input.throttle * accelBase * revFactor;
    const brakeDecel = input.brake * (44.0 + 14.0 * speedNorm) * surfaceGrip;
    const aeroDrag = (0.0021 * this.speed * this.speed * drsDragFactor) + 0.08 * this.speed + surfaceDrag * this.speed;

    const prevSpeed = this.speed;
    this.speed += (forwardAccel - brakeDecel - aeroDrag) * dt;
    if (this.speed < 0) this.speed = 0;

    // Speed-sensitive Formula steering
    const targetSteer = -input.steer * steerSensitivity * (0.58 / (1 + this.speed * 0.022));
    this.steerAngle += (targetSteer - this.steerAngle) * Math.min(1, dt * 13);

    // Yaw rate & lateral tire grip/slip model
    const maxLatAccel = (22.0 * downforce * this.team.grip * surfaceGrip);
    const desiredYawRate = this.speed > 0.5 ? (this.speed / 3.4) * Math.tan(this.steerAngle) * (1.28 * this.team.agility) : 0;
    const latAccelNeeded = Math.abs(desiredYawRate * this.speed);
    const gripRatio = latAccelNeeded > maxLatAccel ? maxLatAccel / latAccelNeeded : 1.0;

    const actualYawRate = desiredYawRate * gripRatio;
    this.yaw += actualYawRate * dt;

    // Slip calculation for realistic feel & tire screech audio
    const slip = Math.max(0, 1 - gripRatio);
    this.slipAmount = Math.min(1, slip * 2.2 + (input.brake > 0.7 && this.speed > 25 ? 0.45 : 0));
    this.lateralVel += (desiredYawRate - actualYawRate) * this.speed * 0.14 * dt;
    this.lateralVel *= Math.exp(-8.5 * dt);

    // Integrate world position
    const fwdX = Math.sin(this.yaw);
    const fwdZ = Math.cos(this.yaw);
    const rightX = Math.cos(this.yaw);
    const rightZ = -Math.sin(this.yaw);

    this.position.x += (fwdX * this.speed + rightX * this.lateralVel) * dt;
    this.position.z += (fwdZ * this.speed + rightZ * this.lateralVel) * dt;
    this.position.y += (proj.y - this.position.y) * Math.min(1, dt * 16);

    // Barrier collision clamp
    const barrierLimit = track.halfWidth + 4.1;
    const postProj = track.projectToTrack(this.position.x, this.position.z, this.trackHintIdx);
    if (Math.abs(postProj.lateralOffset) > barrierLimit) {
      const sign = Math.sign(postProj.lateralOffset);
      const excess = Math.abs(postProj.lateralOffset) - barrierLimit;
      this.position.x -= postProj.right.x * sign * excess;
      this.position.z -= postProj.right.z * sign * excess;
      this.collisionImpulse = Math.min(1, this.speed / 45);
      this.speed *= 0.62;
      // Gently align car along barrier tangent
      const trackYaw = Math.atan2(postProj.tangent.x, postProj.tangent.z);
      let dYaw = trackYaw - this.yaw;
      while (dYaw > Math.PI) dYaw -= Math.PI * 2;
      while (dYaw < -Math.PI) dYaw += Math.PI * 2;
      this.yaw += dYaw * 0.35;
    }

    // RPM calculation
    const lowSpd = this.gear === 1 ? 0 : GEAR_MAX_SPEED[this.gear - 1] * 0.72;
    const highSpd = GEAR_MAX_SPEED[this.gear];
    const ratio = Math.max(0, Math.min(1.06, (this.speed - lowSpd) / Math.max(1, highSpd - lowSpd)));
    const targetRpm = 4800 + ratio * 8400 + (this.speed < 2 ? input.throttle * 4200 : 0);
    this.rpm += (targetRpm - this.rpm) * Math.min(1, dt * 18);

    // Suspension pitch & roll
    const longAccel = (this.speed - prevSpeed) / Math.max(0.001, dt);
    const targetPitch = Math.max(-0.045, Math.min(0.045, -longAccel * 0.0012));
    const targetRoll = Math.max(-0.06, Math.min(0.06, -actualYawRate * this.speed * 0.0022));
    this.pitch += (targetPitch - this.pitch) * Math.min(1, dt * 10);
    this.roll += (targetRoll - this.roll) * Math.min(1, dt * 10);

    // Wheel spin
    this.wheelSpin += (this.speed / 0.36) * dt;

    // Checkpoint & Lap Counting (3 sectors: 0..0.33, 0.33..0.66, 0.66..1.0)
    const t = postProj.t;
    if (t > 0.25 && t < 0.55 && this.maxSectorReached === 0) {
      this.maxSectorReached = 1;
    } else if (t > 0.55 && t < 0.85 && this.maxSectorReached === 1) {
      this.maxSectorReached = 2;
    } else if (t < 0.15 && this.maxSectorReached === 2) {
      // Completed a full lap!
      const lapDuration = raceTime - this.lapStartTime;
      if (lapDuration > 8 && lapDuration < this.bestLapTime) {
        this.bestLapTime = lapDuration;
      }
      this.lapStartTime = raceTime;
      this.maxSectorReached = 0;
      crossedLap = true;
      if (this.lap >= totalLaps) {
        if (!this.finished) {
          this.finished = true;
          this.finishTime = raceTime;
        }
      } else {
        this.lap++;
      }
    }

    // Continuous race progress for live standings
    const lapFrac = this.maxSectorReached === 0 && t > 0.85 ? t - 1 : t;
    this.totalProgress = (this.finished ? totalLaps + 1 : this.lap - 1) + lapFrac;

    this.syncVisuals(dt);
    return { shifted, crossedLap };
  }

  public syncVisuals(_dt: number) {
    this.rig.root.position.copy(this.position);
    this.rig.root.rotation.set(0, this.yaw, 0);
    this.rig.bodyGroup.rotation.set(this.pitch, 0, this.roll);

    // Front wheel steering & all 4 wheels spinning
    this.rig.frontLeftPivot.rotation.y = this.steerAngle * 1.35;
    this.rig.frontRightPivot.rotation.y = this.steerAngle * 1.35;
    for (const w of this.rig.wheels) {
      w.rotation.x = this.wheelSpin;
    }

    // DRS Rear Wing flap visual opening
    if (this.rig.drsFlap) {
      const targetAngle = this.ersActive ? -0.32 : 0.0;
      this.rig.drsFlap.rotation.x += (targetAngle - this.rig.drsFlap.rotation.x) * 0.2;
    }
  }
}
