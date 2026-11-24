import { BuiltTrack } from './TrackBuilder';
import { CarInput, FormulaCar } from './VehiclePhysics';

export class AIController {
  private laneOffset: number;
  private skillFactor: number;
  private overtakeTimer = 0;

  constructor(gridIndex: number, difficulty: number) {
    // difficulty: 0=Rookie, 1=Pro, 2=Legend
    const baseSkill = difficulty === 0 ? 0.78 : difficulty === 1 ? 0.90 : 0.99;
    this.skillFactor = baseSkill + (gridIndex % 4) * 0.018;
    this.laneOffset = ((gridIndex % 2 === 0 ? -1 : 1) * 2.2) * (0.4 + (gridIndex % 3) * 0.25);
  }

  computeInput(car: FormulaCar, allCars: FormulaCar[], track: BuiltTrack, dt: number): CarInput {
    this.overtakeTimer = Math.max(0, this.overtakeTimer - dt);

    // Look ahead along spline for steering target and corner curvature
    const lookDist = Math.max(16, Math.min(44, car.speed * 0.62));
    const targetSample = track.getSampleAtDistance(car.projection.s + lookDist);
    const brakeSample1 = track.getSampleAtDistance(car.projection.s + lookDist * 1.35);
    const brakeSample2 = track.getSampleAtDistance(car.projection.s + lookDist * 2.1);

    // Check nearby cars ahead for overtaking / collision avoidance
    let avoidOffset = 0;
    let emergencyBrake = 0;
    for (const other of allCars) {
      if (other.id === car.id) continue;
      const dx = other.position.x - car.position.x;
      const dz = other.position.z - car.position.z;
      const dist = Math.hypot(dx, dz);
      if (dist < 14 && dist > 0.1) {
        const fwdX = Math.sin(car.yaw);
        const fwdZ = Math.cos(car.yaw);
        const dotFwd = (dx * fwdX + dz * fwdZ) / dist;
        if (dotFwd > 0.65) {
          // Car directly ahead -> pick opposite side to overtake
          const sideSign = other.projection.lateralOffset >= car.projection.lateralOffset ? -1 : 1;
          avoidOffset = sideSign * 3.4;
          this.overtakeTimer = 1.2;
          if (dist < 5.5) emergencyBrake = 0.65;
        }
      }
    }

    const desiredOffset = Math.max(
      -track.halfWidth + 2.2,
      Math.min(track.halfWidth - 2.2, this.laneOffset + avoidOffset)
    );

    const aimX = targetSample.pos.x + targetSample.right.x * desiredOffset;
    const aimZ = targetSample.pos.z + targetSample.right.z * desiredOffset;

    const desiredYaw = Math.atan2(aimX - car.position.x, aimZ - car.position.z);
    let yawErr = desiredYaw - car.yaw;
    while (yawErr > Math.PI) yawErr -= Math.PI * 2;
    while (yawErr < -Math.PI) yawErr += Math.PI * 2;

    // Positive input.steer turns right (decreases yaw in our +Z forward right-handed coord)
    const steer = Math.max(-1, Math.min(1, -yawErr * 2.4));

    // Corner speed target from upcoming curvature
    const maxCurv = Math.max(targetSample.curvature, brakeSample1.curvature, brakeSample2.curvature);
    const cornerSpeed = Math.max(24, Math.min(98, (2.35 / (maxCurv + 0.018)) * this.skillFactor));

    let throttle = 1.0;
    let brake = emergencyBrake;

    if (car.speed > cornerSpeed + 4) {
      throttle = 0.0;
      brake = Math.max(brake, Math.min(1.0, (car.speed - cornerSpeed) / 18));
    } else if (car.speed > cornerSpeed) {
      throttle = 0.35;
    }

    // Use ERS on long straights
    const ers = maxCurv < 0.008 && car.speed > 45 && car.ersCharge > 0.35;

    return { throttle, brake, steer, ers };
  }
}
