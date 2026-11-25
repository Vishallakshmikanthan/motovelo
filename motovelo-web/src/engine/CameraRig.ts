import * as THREE from 'three';
import { BuiltTrack } from './TrackBuilder';
import { FormulaCar } from './VehiclePhysics';

export type CameraMode = 'TCAM' | 'COCKPIT' | 'CHASE' | 'CHASE_CLOSE' | 'TRACKSIDE';
export const CAMERA_MODES: CameraMode[] = ['TCAM', 'COCKPIT', 'CHASE', 'CHASE_CLOSE', 'TRACKSIDE'];

export class CameraRig {
  public modeIndex = 0; // Default Monoposto T-Cam!
  public lookBack = false;
  private smoothPos = new THREE.Vector3();
  private smoothLook = new THREE.Vector3();
  private smoothRoll = 0;
  private initialized = false;

  get mode(): CameraMode {
    return CAMERA_MODES[this.modeIndex];
  }

  cycle(): CameraMode {
    this.modeIndex = (this.modeIndex + 1) % CAMERA_MODES.length;
    this.initialized = false;
    return this.mode;
  }

  reset() {
    this.initialized = false;
    this.smoothRoll = 0;
  }

  update(camera: THREE.PerspectiveCamera, car: FormulaCar, track: BuiltTrack, dt: number) {
    const fwd = new THREE.Vector3(Math.sin(car.yaw), 0, Math.cos(car.yaw));
    const right = new THREE.Vector3(Math.cos(car.yaw), 0, -Math.sin(car.yaw));
    const speedNorm = Math.min(1, car.speed / 95);

    // Subtle high-frequency kerb/speed vibration for visceral Monoposto speed feel!
    const tNow = performance.now() * 0.055;
    const shakeAmp = car.kerbRumble * 0.018 + (speedNorm > 0.78 ? (speedNorm - 0.78) * 0.012 : 0);
    const shakeX = Math.sin(tNow * 1.3) * shakeAmp;
    const shakeY = Math.cos(tNow * 1.9) * shakeAmp;

    const desiredPos = new THREE.Vector3();
    const desiredLook = new THREE.Vector3();
    let targetFov = 68;
    let targetRoll = 0;

    if (this.lookBack) {
      // Exact Monoposto Rear-View T-Cam looking back over the rear wing (frame 1232.2s in video!)
      desiredPos.copy(car.position).addScaledVector(fwd, -0.35);
      desiredPos.y += 1.24;
      desiredLook.copy(car.position).addScaledVector(fwd, -22.0);
      desiredLook.y += 0.65;
      targetFov = 74 + speedNorm * 8;
    } else {
      switch (this.mode) {
        case 'TCAM': {
          // Exact Monoposto T-Cam: subtle lateral G lean + dynamic speed FOV punch (68 -> 83 deg!)
          const gShift = Math.max(-0.08, Math.min(0.08, -car.lateralG * 0.018));
          desiredPos.copy(car.position).addScaledVector(fwd, -0.36).addScaledVector(right, gShift);
          desiredPos.y += 1.24 + shakeY;
          desiredPos.x += shakeX;
          desiredLook.copy(car.position).addScaledVector(fwd, 13.0).addScaledVector(right, -car.steerAngle * 2.2);
          desiredLook.y += 0.36 - car.pitch * 3.0;
          targetFov = 68 + speedNorm * 14 + (car.drsActive ? 3 : 0) + (car.ersActive ? 3 : 0);
          targetRoll = Math.max(-0.04, Math.min(0.04, car.lateralG * 0.008));
          break;
        }
        case 'COCKPIT': {
          desiredPos.copy(car.position).addScaledVector(fwd, 0.02);
          desiredPos.y += 0.80 + shakeY;
          desiredPos.x += shakeX;
          desiredLook.copy(car.position).addScaledVector(fwd, 15.0).addScaledVector(right, -car.steerAngle * 2.4);
          desiredLook.y += 0.56 - car.pitch * 2.5;
          targetFov = 68 + speedNorm * 11 + (car.ersActive ? 3 : 0);
          targetRoll = Math.max(-0.05, Math.min(0.05, car.lateralG * 0.010));
          break;
        }
        case 'CHASE': {
          desiredPos.copy(car.position).addScaledVector(fwd, -6.2 - speedNorm * 1.2);
          desiredPos.y += 2.2 + shakeY;
          desiredLook.copy(car.position).addScaledVector(fwd, 10.0);
          desiredLook.y += 0.85;
          targetFov = 64 + speedNorm * 16 + (car.ersActive ? 5 : 0);
          break;
        }
        case 'CHASE_CLOSE': {
          desiredPos.copy(car.position).addScaledVector(fwd, -4.2 - speedNorm * 0.7);
          desiredPos.y += 1.46 + shakeY;
          desiredLook.copy(car.position).addScaledVector(fwd, 11.0);
          desiredLook.y += 0.72;
          targetFov = 67 + speedNorm * 13;
          break;
        }
        case 'TRACKSIDE': {
          let nearest = track.tracksideCameras[0];
          let bestD2 = Infinity;
          for (const c of track.tracksideCameras) {
            const d2 = c.distanceToSquared(car.position);
            if (d2 < bestD2) {
              bestD2 = d2;
              nearest = c;
            }
          }
          desiredPos.copy(nearest);
          desiredLook.copy(car.position);
          desiredLook.y += 0.6;
          targetFov = 45;
          break;
        }
      }
    }

    if (!this.initialized || this.lookBack || this.mode === 'TCAM' || this.mode === 'COCKPIT') {
      this.smoothPos.copy(desiredPos);
      this.smoothLook.copy(desiredLook);
      this.initialized = true;
    } else if (this.mode === 'TRACKSIDE') {
      this.smoothPos.copy(desiredPos);
      this.smoothLook.lerp(desiredLook, Math.min(1, dt * 14));
    } else {
      this.smoothPos.lerp(desiredPos, Math.min(1, dt * 11));
      this.smoothLook.lerp(desiredLook, Math.min(1, dt * 15));
    }

    camera.position.copy(this.smoothPos);
    camera.lookAt(this.smoothLook);
    this.smoothRoll += (targetRoll - this.smoothRoll) * Math.min(1, dt * 9);
    if (Math.abs(this.smoothRoll) > 0.0005) {
      camera.rotateZ(this.smoothRoll);
    }
    camera.fov += (targetFov - camera.fov) * Math.min(1, dt * 8);
    camera.updateProjectionMatrix();
  }
}
