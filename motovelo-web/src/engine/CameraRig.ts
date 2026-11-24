import * as THREE from 'three';
import { BuiltTrack } from './TrackBuilder';
import { FormulaCar } from './VehiclePhysics';

export type CameraMode = 'CHASE' | 'TCAM' | 'COCKPIT' | 'NOSE' | 'TRACKSIDE';
export const CAMERA_MODES: CameraMode[] = ['CHASE', 'TCAM', 'COCKPIT', 'NOSE', 'TRACKSIDE'];

export class CameraRig {
  public modeIndex = 0;
  private smoothPos = new THREE.Vector3();
  private smoothLook = new THREE.Vector3();
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
  }

  update(camera: THREE.PerspectiveCamera, car: FormulaCar, track: BuiltTrack, dt: number) {
    const fwd = new THREE.Vector3(Math.sin(car.yaw), 0, Math.cos(car.yaw));
    const speedNorm = Math.min(1, car.speed / 95);

    const desiredPos = new THREE.Vector3();
    const desiredLook = new THREE.Vector3();
    let targetFov = 64;

    switch (this.mode) {
      case 'CHASE': {
        desiredPos.copy(car.position).addScaledVector(fwd, -7.2 - speedNorm * 1.4);
        desiredPos.y += 2.65;
        desiredLook.copy(car.position).addScaledVector(fwd, 9.0);
        desiredLook.y += 0.9;
        targetFov = 62 + speedNorm * 18 + (car.ersActive ? 6 : 0);
        break;
      }
      case 'TCAM': {
        // Iconic F1 broadcast T-Cam mounted above the airbox
        desiredPos.copy(car.position).addScaledVector(fwd, -0.55);
        desiredPos.y += 1.48;
        desiredLook.copy(car.position).addScaledVector(fwd, 24.0);
        desiredLook.y += 0.72;
        targetFov = 68 + speedNorm * 12;
        break;
      }
      case 'COCKPIT': {
        // Inside driver helmet looking over the Halo
        desiredPos.copy(car.position).addScaledVector(fwd, 0.12);
        desiredPos.y += 0.92;
        desiredLook.copy(car.position).addScaledVector(fwd, 28.0);
        desiredLook.y += 0.74;
        targetFov = 70 + speedNorm * 10;
        break;
      }
      case 'NOSE': {
        // Low-slung front wing nose camera
        desiredPos.copy(car.position).addScaledVector(fwd, 2.55);
        desiredPos.y += 0.56;
        desiredLook.copy(car.position).addScaledVector(fwd, 32.0);
        desiredLook.y += 0.52;
        targetFov = 74 + speedNorm * 14;
        break;
      }
      case 'TRACKSIDE': {
        // Nearest TV broadcast trackside camera tracking the car
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
        targetFov = 46;
        break;
      }
    }

    if (!this.initialized || this.mode === 'TCAM' || this.mode === 'COCKPIT' || this.mode === 'NOSE') {
      this.smoothPos.copy(desiredPos);
      this.smoothLook.copy(desiredLook);
      this.initialized = true;
    } else if (this.mode === 'TRACKSIDE') {
      this.smoothPos.copy(desiredPos);
      this.smoothLook.lerp(desiredLook, Math.min(1, dt * 14));
    } else {
      const posAlpha = Math.min(1, dt * 10);
      const lookAlpha = Math.min(1, dt * 14);
      this.smoothPos.lerp(desiredPos, posAlpha);
      this.smoothLook.lerp(desiredLook, lookAlpha);
    }

    camera.position.copy(this.smoothPos);
    camera.lookAt(this.smoothLook);
    camera.fov += (targetFov - camera.fov) * Math.min(1, dt * 8);
    camera.updateProjectionMatrix();
  }
}
