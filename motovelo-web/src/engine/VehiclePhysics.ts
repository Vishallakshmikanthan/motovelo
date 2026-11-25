import * as THREE from 'three';
import { FormulaTeam, TYRE_COMPOUNDS, TyreCompound } from '../data/teams';
import { CarRig } from './AssetLoader';
import { BuiltTrack, TrackProjection } from './TrackBuilder';

export interface CarInput {
  throttle: number; // 0..1
  brake: number;    // 0..1
  steer: number;    // -1 (left) .. +1 (right)
  ers: boolean;
  drs?: boolean;
  shiftUp?: boolean;
  shiftDown?: boolean;
}

export interface CarSetup {
  wings: number;        // -50 (Speed) .. +50 (Grip)
  brakeBalance: number; // -50 (Front) .. +50 (Rear)
  suspension: number;   // -50 (Low) .. +50 (High)
  compound: TyreCompound;
}

// 8-speed 2026 F1 gearbox ratios in m/s (328 KPH normal, ~346 KPH DRS+ERS+Tow!)
const GEAR_MAX_SPEED = [0, 32, 46, 59, 71, 82, 91, 98, 106];

export class FormulaCar {
  public readonly id: string;
  public readonly isPlayer: boolean;
  public readonly team: FormulaTeam;
  public readonly driverName: string;
  public readonly driverCode: string;
  public readonly rig: CarRig;
  public setup: CarSetup;

  // Spatial state
  public position = new THREE.Vector3();
  public yaw = 0;
  public speed = 0;
  public lateralVel = 0;
  public smoothSteer = 0;
  public steerAngle = 0;
  public wheelSpin = 0;

  // Powertrain, Aero, Tyres & Damage
  public gear = 1;
  public rpm = 5200;
  public ersCharge = 1.0;
  public ersActive = false;
  public drsActive = false;
  public slipstream = 0;   // 0..1 aerodynamic tow from car ahead
  public slipAmount = 0;
  public kerbRumble = 0;   // 0..1 for camera shake
  public offTrack = false;
  public collisionImpulse = 0;
  public tyreTemp = 86;
  public tyreHealth = 100;
  public carHealth = 100;
  public fuelLevel = 100;
  public pitStops = 0;
  public inPit = false;
  public pitRequested = false;

  // Suspension & G-force visual state
  public pitch = 0;
  public roll = 0;
  public lateralG = 0;

  // Race progress & Sector Times
  public trackHintIdx = 0;
  public projection!: TrackProjection;
  public lap = 1;
  public maxSectorReached = 0;
  public totalProgress = 0;
  public lapStartTime = 0;
  public sectorStartTime = 0;
  public sectorTimes: [number | null, number | null, number | null] = [null, null, null];
  public lastLapTime = Infinity;
  public bestLapTime = Infinity;
  public finished = false;
  public finishTime = 0;

  constructor(
    id: string,
    isPlayer: boolean,
    team: FormulaTeam,
    driverName: string,
    driverCode: string,
    rig: CarRig,
    setup?: CarSetup
  ) {
    this.id = id;
    this.isPlayer = isPlayer;
    this.team = team;
    this.driverName = driverName;
    this.driverCode = driverCode;
    this.rig = rig;
    this.setup = setup ?? {
      wings: 0,
      brakeBalance: 0,
      suspension: 0,
      compound: TYRE_COMPOUNDS[0],
    };
  }

  resetAt(pos: THREE.Vector3, yaw: number, track: BuiltTrack) {
    this.position.copy(pos);
    this.yaw = yaw;
    this.speed = 0;
    this.lateralVel = 0;
    this.smoothSteer = 0;
    this.steerAngle = 0;
    this.gear = 1;
    this.rpm = 5400;
    this.ersCharge = 1.0;
    this.ersActive = false;
    this.drsActive = false;
    this.slipstream = 0;
    this.slipAmount = 0;
    this.kerbRumble = 0;
    this.offTrack = false;
    this.collisionImpulse = 0;
    this.tyreTemp = 86;
    this.tyreHealth = 100;
    this.carHealth = 100;
    this.fuelLevel = 100;
    this.pitStops = 0;
    this.inPit = false;
    this.pitRequested = false;
    this.lap = 1;
    this.maxSectorReached = 0;
    this.totalProgress = 0;
    this.lapStartTime = 0;
    this.sectorStartTime = 0;
    this.sectorTimes = [null, null, null];
    this.lastLapTime = Infinity;
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
    accelPressure: number,
    brakePressure: number,
    raceTime: number,
    totalLaps: number,
    canMove: boolean
  ): { shifted: boolean; crossedLap: boolean; newBestLap: boolean } {
    let shifted = false;
    let crossedLap = false;
    let newBestLap = false;
    this.collisionImpulse = 0;

    if (!canMove) {
      const targetRpm = 5200 + input.throttle * 7600;
      this.rpm += (targetRpm - this.rpm) * Math.min(1, dt * 11);
      this.syncVisuals(dt);
      return { shifted, crossedLap, newBestLap };
    }

    const proj = track.projectToTrack(this.position.x, this.position.z, this.trackHintIdx);
    this.projection = proj;
    this.trackHintIdx = proj.sampleIndex;

    const absLat = Math.abs(proj.lateralOffset);
    const onKerb = absLat > track.halfWidth - 0.7 && absLat <= track.halfWidth + 1.5;
    const offTrack = absLat > track.halfWidth + 1.5;
    this.offTrack = offTrack;
    this.kerbRumble = onKerb ? Math.min(1, this.speed / 35) : offTrack ? Math.min(0.6, this.speed / 45) : 0;

    const surfaceGrip = offTrack ? 0.66 : onKerb ? 0.92 : 1.0;
    const surfaceDrag = offTrack ? 0.36 : 0.0;

    // Car Setup modifiers (Monoposto sliders genuinely tune speed vs cornering!)
    const wingNorm = this.setup.wings / 50; // -1 (Speed) .. +1 (Grip)
    const speedSetupMult = 1.0 - wingNorm * 0.045;
    const gripSetupMult =
      (1.0 + wingNorm * 0.09) *
      this.setup.compound.gripMult *
      (0.84 + 0.16 * (this.tyreHealth / 100));

    // ERS (Electric MGU-K Overtake) & DRS (Rear Wing Drag Reduction)
    if (input.ers && this.ersCharge > 0.03 && input.throttle > 0.2 && this.speed > 14) {
      this.ersActive = true;
      this.ersCharge = Math.max(0, this.ersCharge - dt * 0.18);
    } else {
      this.ersActive = false;
      const regen = input.brake > 0.1 ? 0.16 : 0.045;
      this.ersCharge = Math.min(1, this.ersCharge + dt * regen);
    }

    // DRS opens when requested (or auto-assisted with ERS on straight) and not braking
    const wantDrs = Boolean(input.drs || input.ers);
    this.drsActive = wantDrs && input.brake < 0.05 && this.speed > 38 && Math.abs(this.smoothSteer) < 0.55;

    // 8-Speed Sequential Gearbox
    const prevGear = this.gear;
    if (!autoGear) {
      if (input.shiftUp && this.gear < 8) this.gear++;
      if (input.shiftDown && this.gear > 1) this.gear--;
    } else {
      const upshiftSpd = GEAR_MAX_SPEED[this.gear] * 0.93;
      const downshiftSpd = this.gear > 1 ? GEAR_MAX_SPEED[this.gear - 1] * 0.74 : -1;
      if (this.speed > upshiftSpd && this.gear < 8) this.gear++;
      else if (this.speed < downshiftSpd && this.gear > 1) this.gear--;
    }
    if (this.gear !== prevGear) shifted = true;

    // Authentic 2026 F1 Acceleration, Aerodynamic Downforce, DRS & Slipstream Tow
    const speedNorm = Math.min(1, Math.max(0, this.speed / 100));
    const downforce = (1.08 + 1.85 * speedNorm * speedNorm) * this.team.aero;
    const ersBoost = this.ersActive ? 1.20 : 1.0;
    const drsDragFactor = this.drsActive ? 0.76 : 1.0;
    const towDragFactor = 1.0 - this.slipstream * 0.22;

    const gearTop =
      GEAR_MAX_SPEED[this.gear] *
      this.team.power *
      speedSetupMult *
      (this.drsActive ? 1.045 : 1.0) *
      (this.ersActive ? 1.035 : 1.0) *
      (1.0 + this.slipstream * 0.045);

    // Punchy low-end F1 launch (0-100 in ~2.4s, 0-200 in ~4.6s, 330+ KPH top!)
    const accelBase = (33.5 - this.gear * 2.05) * this.team.power * ersBoost;
    const revFactor = this.speed < gearTop ? 1.0 : 0.06;

    const throttleEff = Math.min(1, input.throttle * accelPressure);
    const brakeEff = Math.min(1, input.brake * brakePressure);

    const forwardAccel = throttleEff * accelBase * revFactor;
    // F1 Carbon-Ceramic Brakes: 5G+ deceleration at high speed!
    const brakeDecel = brakeEff * (48.0 + 26.0 * speedNorm) * surfaceGrip;
    const aeroDrag =
      0.00175 * this.speed * this.speed * drsDragFactor * towDragFactor +
      0.065 * this.speed +
      surfaceDrag * this.speed;

    const prevSpeed = this.speed;
    this.speed += (forwardAccel - brakeDecel - aeroDrag) * dt;
    if (this.speed < 0) this.speed = 0;

    // Progressive Monoposto Steering (smooth ramp-in, crisp self-centering, speed-adaptive lock!)
    const steerRate = Math.abs(input.steer) > Math.abs(this.smoothSteer) ? 9.5 : 14.5;
    this.smoothSteer += (input.steer - this.smoothSteer) * Math.min(1, dt * steerRate);

    // Speed-sensitive lock: agile at hairpins, surgical at 300 KPH!
    const maxLock = (0.64 / (1 + this.speed * 0.020)) * steerSensitivity;
    const targetSteer = -this.smoothSteer * maxLock;
    this.steerAngle += (targetSteer - this.steerAngle) * Math.min(1, dt * 16);

    // High-Downforce Cornering Grip & Controlled Slip
    const maxLatAccel = 27.5 * downforce * this.team.grip * gripSetupMult * surfaceGrip;
    const desiredYawRate =
      this.speed > 0.5 ? (this.speed / 3.35) * Math.tan(this.steerAngle) * (1.34 * this.team.agility) : 0;
    const latAccelNeeded = Math.abs(desiredYawRate * this.speed);
    const gripRatio = latAccelNeeded > maxLatAccel ? maxLatAccel / latAccelNeeded : 1.0;

    const actualYawRate = desiredYawRate * gripRatio;
    this.yaw += actualYawRate * dt;
    this.lateralG = (actualYawRate * this.speed) / 9.81;

    // Subtle straight-line stabilization when player releases steering
    if (this.isPlayer && Math.abs(input.steer) < 0.05 && this.speed > 20) {
      const trackYaw = Math.atan2(proj.tangent.x, proj.tangent.z);
      let dYaw = trackYaw - this.yaw;
      while (dYaw > Math.PI) dYaw -= Math.PI * 2;
      while (dYaw < -Math.PI) dYaw += Math.PI * 2;
      if (Math.abs(dYaw) < 0.16) {
        this.yaw += dYaw * Math.min(1, dt * 1.4);
      }
    }

    const slip = Math.max(0, 1 - gripRatio);
    this.slipAmount = Math.min(1, slip * 2.2 + (brakeEff > 0.75 && this.speed > 28 ? 0.38 : 0));
    this.lateralVel += (desiredYawRate - actualYawRate) * this.speed * 0.11 * dt;
    this.lateralVel *= Math.exp(-10.5 * dt);

    // Tyre temp, wear & fuel simulation
    const targetTemp = 76 + speedNorm * 22 + this.slipAmount * 14 + Math.min(8, Math.abs(this.lateralG) * 1.5);
    this.tyreTemp += (targetTemp - this.tyreTemp) * Math.min(1, dt * 0.8);
    this.tyreHealth = Math.max(15, this.tyreHealth - dt * (0.06 + this.slipAmount * 0.35) * this.setup.compound.wearRate);
    this.fuelLevel = Math.max(5, this.fuelLevel - dt * (0.04 + throttleEff * 0.09));

    const fwdX = Math.sin(this.yaw);
    const fwdZ = Math.cos(this.yaw);
    const rightX = Math.cos(this.yaw);
    const rightZ = -Math.sin(this.yaw);

    this.position.x += (fwdX * this.speed + rightX * this.lateralVel) * dt;
    this.position.z += (fwdZ * this.speed + rightZ * this.lateralVel) * dt;
    this.position.y += (proj.y - this.position.y) * Math.min(1, dt * 16);

    // Smooth Glancing Barrier Collision (never gets stuck!)
    const barrierLimit = track.halfWidth + 4.8;
    const postProj = track.projectToTrack(this.position.x, this.position.z, this.trackHintIdx);
    if (Math.abs(postProj.lateralOffset) > barrierLimit) {
      const sign = Math.sign(postProj.lateralOffset);
      const excess = Math.abs(postProj.lateralOffset) - barrierLimit;
      this.position.x -= postProj.right.x * sign * (excess + 0.12);
      this.position.z -= postProj.right.z * sign * (excess + 0.12);
      this.collisionImpulse = Math.min(1, this.speed / 50);
      this.carHealth = Math.max(35, this.carHealth - this.collisionImpulse * 5);
      this.speed *= 0.76;
      const trackYaw = Math.atan2(postProj.tangent.x, postProj.tangent.z);
      let dYaw = trackYaw - this.yaw;
      while (dYaw > Math.PI) dYaw -= Math.PI * 2;
      while (dYaw < -Math.PI) dYaw += Math.PI * 2;
      this.yaw += dYaw * 0.48;
    }

    // RPM curve
    const lowSpd = this.gear === 1 ? 0 : GEAR_MAX_SPEED[this.gear - 1] * 0.72;
    const highSpd = GEAR_MAX_SPEED[this.gear];
    const ratio = Math.max(0, Math.min(1.06, (this.speed - lowSpd) / Math.max(1, highSpd - lowSpd)));
    const targetRpm = 5000 + ratio * 8400 + (this.speed < 2 ? throttleEff * 4200 : 0);
    this.rpm += (targetRpm - this.rpm) * Math.min(1, dt * 18);

    // Suspension pitch & roll
    const longAccel = (this.speed - prevSpeed) / Math.max(0.001, dt);
    const targetPitch = Math.max(-0.048, Math.min(0.048, -longAccel * 0.0011));
    const targetRoll = Math.max(-0.058, Math.min(0.058, -actualYawRate * this.speed * 0.0019));
    this.pitch += (targetPitch - this.pitch) * Math.min(1, dt * 11);
    this.roll += (targetRoll - this.roll) * Math.min(1, dt * 11);

    this.wheelSpin += (this.speed / 0.36) * dt;

    // 3-Sector timing & Lap counting
    const t = postProj.t;
    if (t > 0.33 && t < 0.60 && this.maxSectorReached === 0) {
      this.maxSectorReached = 1;
      this.sectorTimes[0] = raceTime - this.sectorStartTime;
      this.sectorStartTime = raceTime;
    } else if (t > 0.66 && t < 0.90 && this.maxSectorReached === 1) {
      this.maxSectorReached = 2;
      this.sectorTimes[1] = raceTime - this.sectorStartTime;
      this.sectorStartTime = raceTime;
    } else if (t < 0.15 && this.maxSectorReached === 2) {
      this.sectorTimes[2] = raceTime - this.sectorStartTime;
      this.sectorStartTime = raceTime;
      const lapDuration = raceTime - this.lapStartTime;
      this.lastLapTime = lapDuration;
      if (lapDuration > 12 && lapDuration < this.bestLapTime) {
        this.bestLapTime = lapDuration;
        newBestLap = true;
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

    const lapFrac = this.maxSectorReached === 0 && t > 0.85 ? t - 1 : t;
    this.totalProgress = (this.finished ? totalLaps + 1 : this.lap - 1) + lapFrac;

    this.syncVisuals(dt);
    return { shifted, crossedLap, newBestLap };
  }

  public updateCockpitDisplay(pos: number, totalLaps: number, deltaStr: string) {
    const ctx = this.rig.displayCtx;
    const tex = this.rig.displayTex;
    if (!ctx || !tex) return;

    ctx.fillStyle = '#090c10';
    ctx.fillRect(0, 0, 256, 128);
    ctx.strokeStyle = this.drsActive ? '#22ff44' : '#33404e';
    ctx.lineWidth = 3;
    ctx.strokeRect(2, 2, 252, 124);

    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 28px monospace';
    ctx.fillText(String(Math.round(this.speed * 3.6)).padStart(3, ' '), 12, 32);
    ctx.fillStyle = '#9aa5b1';
    ctx.font = '10px sans-serif';
    ctx.fillText('KPH', 72, 30);

    ctx.fillStyle = this.drsActive ? '#22ff44' : '#f5d000';
    ctx.font = 'bold 15px monospace';
    ctx.fillText(this.drsActive ? 'DRS ON' : deltaStr, 130, 26);

    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 54px sans-serif';
    ctx.fillText(this.speed < 0.5 ? 'N' : String(this.gear), 110, 86);

    ctx.font = 'bold 13px monospace';
    ctx.fillStyle = '#ffe040';
    ctx.fillText(`${Math.round(this.tyreTemp)}° T.Temp`, 10, 62);
    ctx.fillStyle = '#a0e060';
    ctx.fillText(`${Math.round(this.tyreHealth)} Tyres%`, 10, 80);
    ctx.fillStyle = '#40e880';
    ctx.fillText(`${Math.round(this.carHealth)} Car %`, 10, 98);

    const erPct = Math.round(this.ersCharge * 100);
    ctx.fillStyle = '#38d6ff';
    ctx.fillText(`${erPct} ER%`, 176, 72);
    ctx.fillStyle = '#222830';
    ctx.fillRect(228, 42, 14, 52);
    ctx.fillStyle = '#22e050';
    const barH = Math.round(52 * this.ersCharge);
    ctx.fillRect(228, 94 - barH, 14, barH);

    ctx.fillStyle = '#e5e9f0';
    ctx.font = 'bold 14px monospace';
    ctx.fillText(`Pos ${pos}`, 14, 120);
    ctx.fillText(`Lap ${this.lap}/${totalLaps}`, 146, 120);

    tex.needsUpdate = true;
  }

  public syncVisuals(_dt: number) {
    this.rig.root.position.copy(this.position);
    this.rig.root.rotation.set(0, this.yaw, 0);
    this.rig.bodyGroup.rotation.set(this.pitch, 0, this.roll);

    // Front wheel steering & all 4 wheels spinning
    this.rig.frontLeftPivot.rotation.y = this.steerAngle * 1.45;
    this.rig.frontRightPivot.rotation.y = this.steerAngle * 1.45;
    for (const w of this.rig.wheels) {
      w.rotation.x = this.wheelSpin;
    }

    // Driver helmet subtly looks into the apex of every corner (Monoposto T-Cam feel!)
    if (this.rig.driverHelmet) {
      this.rig.driverHelmet.rotation.y = this.steerAngle * 0.55;
      this.rig.driverHelmet.rotation.z = -this.roll * 1.4;
    }

    // DRS Rear Wing flap visual opening
    if (this.rig.drsFlap) {
      const targetAngle = this.drsActive ? -0.34 : 0.0;
      this.rig.drsFlap.rotation.x += (targetAngle - this.rig.drsFlap.rotation.x) * 0.25;
    }
  }
}
