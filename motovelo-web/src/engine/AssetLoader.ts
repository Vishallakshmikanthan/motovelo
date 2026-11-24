import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { FormulaTeam } from '../data/teams';

export interface CarRig {
  root: THREE.Group;
  bodyGroup: THREE.Group;
  frontLeftPivot: THREE.Group;
  frontRightPivot: THREE.Group;
  wheels: THREE.Object3D[]; // [FL, FR, RL, RR]
  drsFlap: THREE.Object3D | null;
}

export interface LoadedAssets {
  carProto: THREE.Group;
  wheelProto: THREE.Group;
  grandstandProto: THREE.Group;
  pitBuildingProto: THREE.Group;
  startGantryProto: THREE.Group;
}

export class AssetLoader {
  private loader = new GLTFLoader();
  public assets!: LoadedAssets;

  async loadAll(onProgress?: (pct: number, label: string) => void): Promise<LoadedAssets> {
    const items: [keyof LoadedAssets, string, string][] = [
      ['carProto', './models/formula_car.glb', 'LOADING BLENDER FORMULA CHASSIS...'],
      ['wheelProto', './models/formula_wheel.glb', 'LOADING PIRELLI-STYLE SLICK TIRES...'],
      ['grandstandProto', './models/grandstand.glb', 'LOADING STADIUM GRANDSTANDS...'],
      ['pitBuildingProto', './models/pit_building.glb', 'LOADING PIT LANE COMPLEX...'],
      ['startGantryProto', './models/start_gantry.glb', 'LOADING F1 START GANTRY...'],
    ];

    const result: Partial<LoadedAssets> = {};
    for (let i = 0; i < items.length; i++) {
      const [key, url, label] = items[i];
      onProgress?.(Math.round(((i + 0.3) / items.length) * 100), label);
      const gltf = await this.loader.loadAsync(url);
      gltf.scene.traverse((obj) => {
        if ((obj as THREE.Mesh).isMesh) {
          const m = obj as THREE.Mesh;
          m.castShadow = true;
          m.receiveShadow = true;
        }
      });
      result[key] = gltf.scene;
      onProgress?.(Math.round(((i + 1) / items.length) * 100), label);
    }

    this.assets = result as LoadedAssets;
    return this.assets;
  }

  createFormulaCar(team: FormulaTeam): CarRig {
    const root = new THREE.Group();
    const bodyGroup = new THREE.Group();
    root.add(bodyGroup);

    const chassis = this.assets.carProto.clone(true);
    let drsFlap: THREE.Object3D | null = null;

    chassis.traverse((obj) => {
      if (obj.name.includes('RearWingDRS')) {
        drsFlap = obj;
      }
      if ((obj as THREE.Mesh).isMesh) {
        const mesh = obj as THREE.Mesh;
        const origMat = mesh.material as THREE.MeshStandardMaterial;
        if (origMat && origMat.name) {
          const mat = origMat.clone();
          if (mat.name.startsWith('BodyPrimary')) {
            mat.color.setHex(team.primary);
          } else if (mat.name.startsWith('BodySecondary')) {
            mat.color.setHex(team.secondary);
          } else if (mat.name.startsWith('Accent')) {
            mat.color.setHex(team.accent);
          } else if (mat.name.startsWith('Helmet')) {
            mat.color.setHex(team.helmet);
          }
          mesh.material = mat;
        }
      }
    });
    bodyGroup.add(chassis);

    // Build 4 articulated wheels from Blender wheel asset
    // In glTF Y-up: Blender -Y (front) -> glTF +Z!
    // Front axle at z = +1.75, Rear axle at z = -1.65, Wheel radius = 0.36
    const makeWheel = (isRear: boolean): THREE.Object3D => {
      const w = this.assets.wheelProto.clone(true);
      if (isRear) {
        w.scale.set(1.18, 1.04, 1.04); // Wider rear slicks
      }
      w.traverse((obj) => {
        if ((obj as THREE.Mesh).isMesh) {
          const mesh = obj as THREE.Mesh;
          const origMat = mesh.material as THREE.MeshStandardMaterial;
          if (origMat && origMat.name.startsWith('TireStripe')) {
            const mat = origMat.clone();
            mat.color.setHex(team.tireStripe);
            mesh.material = mat;
          }
        }
      });
      return w;
    };

    const frontLeftPivot = new THREE.Group();
    frontLeftPivot.position.set(0.88, 0.36, 1.75);
    const wFL = makeWheel(false);
    frontLeftPivot.add(wFL);
    root.add(frontLeftPivot);

    const frontRightPivot = new THREE.Group();
    frontRightPivot.position.set(-0.88, 0.36, 1.75);
    const wFR = makeWheel(false);
    frontRightPivot.add(wFR);
    root.add(frontRightPivot);

    const wRL = makeWheel(true);
    wRL.position.set(0.92, 0.37, -1.65);
    root.add(wRL);

    const wRR = makeWheel(true);
    wRR.position.set(-0.92, 0.37, -1.65);
    root.add(wRR);

    return {
      root,
      bodyGroup,
      frontLeftPivot,
      frontRightPivot,
      wheels: [wFL, wFR, wRL, wRR],
      drsFlap,
    };
  }
}
