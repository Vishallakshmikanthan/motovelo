import { BuiltTrack } from './TrackBuilder';
import { CarInput, FormulaCar } from './VehiclePhysics';

export class AIController {
  private laneOffset: number;
  private skillFactor: number;
  private currentAvoid = 0;

  constructor(gridIndex: number, difficulty: number) {
    // difficulty: 0=Rookie, 1=Pro, 2=Legend
    const baseSkill = difficulty === 0 ? 0.78 : difficulty === 1 ? 0.90 : 0.99;
    // Slight spread across the 22-car F1 field so realistic battles & overtakes form!
    const fieldSpread = ((21 - Math.min(21, gridIndex)) / 21) * 0.075;
    this.skillFactor = baseSkill + fieldSpread;
    this.laneOffset = (gridIndex % 2 === 0 ? -1 : 1) * (1.2 + (gridIndex % 4) * 0.85);
  }

  computeInput(car: FormulaCar, allCars: FormulaCar[], track: BuiltTrack, dt: number): CarInput {
    // Dynamic look-ahead scaled for real 2.5-4.3km F1 circuits
    const lookDist = Math.max(18, Math.min(58, car.speed * 0.58));
    const targetSample = track.getSampleAtDistance(car.projection.s + lookDist);

    // Multi-point braking look-ahead (30m, 70m, 125m) so AI brakes smoothly into hairpins!
    let maxCurv = targetSample.curvature;
    for (const d of [30, 65, 110, 155]) {
      const s = track.getSampleAtDistance(car.projection.s + d);
      const weight = d <= 65 ? 1.0 : d <= 110 ? 0.85 : 0.65;
      maxCurv = Math.max(maxCurv, s.curvature * weight);
    }

    // Overtaking & pack avoidance across all 22 cars
    let targetAvoid = 0;
    let emergencyBrake = 0;
    const fwdX = Math.sin(car.yaw);
    const fwdZ = Math.cos(car.yaw);

    for (const other of allCars) {
      if (other.id === car.id) continue;
      const dx = other.position.x - car.position.x;
      const dz = other.position.z - car.position.z;
      const dist = Math.hypot(dx, dz);
      if (dist < 18 && dist > 0.1) {
        const dotFwd = (dx * fwdX + dz * fwdZ) / dist;
        if (dotFwd > 0.60) {
          const latDiff = other.projection.lateralOffset - car.projection.lateralOffset;
          if (Math.abs(latDiff) < 3.4) {
            // Pull out cleanly to the open side to overtake!
            const preferLeft = other.projection.lateralOffset > 0;
            targetAvoid = preferLeft ? -4.2 : 4.2;
            if (dist < 6.0 && other.speed < car.speed + 2) {
              emergencyBrake = 0.55;
            }
          }
        }
      }
    }

    this.currentAvoid += (targetAvoid - this.currentAvoid) * Math.min(1, dt * 4.5);

    const desiredOffset = Math.max(
      -track.halfWidth + 2.0,
      Math.min(track.halfWidth - 2.0, this.laneOffset + this.currentAvoid)
    );

    const aimX = targetSample.pos.x + targetSample.right.x * desiredOffset;
    const aimZ = targetSample.pos.z + targetSample.right.z * desiredOffset;

    const desiredYaw = Math.atan2(aimX - car.position.x, aimZ - car.position.z);
    let yawErr = desiredYaw - car.yaw;
    while (yawErr > Math.PI) yawErr -= Math.PI * 2;
    while (yawErr < -Math.PI) yawErr += Math.PI * 2;

    const steer = Math.max(-1, Math.min(1, -yawErr * 2.5));

    // Corner target speed based on real F1 downforce grip curve
    const cornerSpeed = Math.max(22, Math.min(102, Math.sqrt(26.0 / (maxCurv + 0.0024)) * this.skillFactor));

    let throttle = 1.0;
    let brake = emergencyBrake;

    if (car.speed > cornerSpeed + 4.5) {
      throttle = 0.0;
      brake = Math.max(brake, Math.min(1.0, (car.speed - cornerSpeed) / 16));
    } else if (car.speed > cornerSpeed) {
      throttle = 0.42;
    }

    const onStraight = maxCurv < 0.0055 && car.speed > 48;
    const drs = onStraight && brake < 0.05;
    const ers = onStraight && car.ersCharge > 0.35 && (car.slipstream > 0.2 || Math.abs(this.currentAvoid) > 1.0);

    return { throttle, brake, steer, ers, drs };
  }
}
