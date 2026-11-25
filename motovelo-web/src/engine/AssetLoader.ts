import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { FormulaTeam, HELMET_LIST, SUIT_LIST, TYRE_COMPOUNDS, TyreCompound } from '../data/teams';

export interface CarRig {
  root: THREE.Group;
  bodyGroup: THREE.Group;
  frontLeftPivot: THREE.Group;
  frontRightPivot: THREE.Group;
  wheels: THREE.Object3D[]; // [FL, FR, RL, RR]
  drsFlap: THREE.Object3D | null;
  driverHelmet: THREE.Object3D | null;
  steeringDisplay: THREE.Object3D | null;
  displayCanvas?: HTMLCanvasElement;
  displayCtx?: CanvasRenderingContext2D;
  displayTex?: THREE.CanvasTexture;
}

export interface LoadedAssets {
  carProto: THREE.Group;
  wheelProto: THREE.Group;
  driverProto: THREE.Group;
  grandstandProto: THREE.Group;
  pitBuildingProto: THREE.Group;
  startGantryProto: THREE.Group;
  sponsorTextures: Record<string, THREE.Texture>;
}

export class AssetLoader {
  private loader = new GLTFLoader();
  private texLoader = new THREE.TextureLoader();
  private texCache = new Map<string, THREE.Texture>();
  public assets!: LoadedAssets;

  public getTexture(url: string, flipY = false): THREE.Texture {
    const key = `${url}|${flipY}`;
    const existing = this.texCache.get(key);
    if (existing) return existing;
    const tex = this.texLoader.load(url);
    tex.flipY = flipY;
    tex.colorSpace = THREE.SRGBColorSpace;
    tex.anisotropy = 8;
    this.texCache.set(key, tex);
    return tex;
  }

  async loadAll(onProgress?: (pct: number, label: string) => void): Promise<LoadedAssets> {
    const items: [keyof Omit<LoadedAssets, 'sponsorTextures'>, string, string][] = [
      ['carProto', './models/formula_car.glb', 'LOADING 2026 FORMULA CHASSIS...'],
      ['wheelProto', './models/formula_wheel.glb', 'LOADING PIRELLI P-ZERO TYRES...'],
      ['driverProto', './models/driver_standing.glb', 'LOADING 3D DRIVER & SUIT MODEL...'],
      ['grandstandProto', './models/grandstand.glb', 'LOADING STADIUM GRANDSTANDS...'],
      ['pitBuildingProto', './models/pit_building.glb', 'LOADING PIT LANE COMPLEX...'],
      ['startGantryProto', './models/start_gantry.glb', 'LOADING F1 START GANTRY...'],
    ];

    const result: Partial<LoadedAssets> = {};
    for (let i = 0; i < items.length; i++) {
      const [key, url, label] = items[i];
      onProgress?.(Math.round(((i + 0.3) / (items.length + 1)) * 100), label);
      const gltf = await this.loader.loadAsync(url);
      gltf.scene.traverse((obj) => {
        if ((obj as THREE.Mesh).isMesh) {
          const m = obj as THREE.Mesh;
          m.castShadow = true;
          m.receiveShadow = true;
        }
      });
      result[key] = gltf.scene;
      onProgress?.(Math.round(((i + 1) / (items.length + 1)) * 100), label);
    }

    onProgress?.(92, 'LOADING 2026 TRACKSIDE SPONSORS...');
    const sponsorNames = ['aramco', 'tagheuer', 'louisvuitton', 'aws', 'f1madrid'];
    const sponsorTextures: Record<string, THREE.Texture> = {};
    for (const s of sponsorNames) {
      const t = this.getTexture(`./textures/sponsors/${s}.png`, true);
      t.wrapS = THREE.RepeatWrapping;
      t.wrapT = THREE.ClampToEdgeWrapping;
      sponsorTextures[s] = t;
    }
    result.sponsorTextures = sponsorTextures;
    onProgress?.(100, 'READY TO RACE');

    this.assets = result as LoadedAssets;
    return this.assets;
  }

  createFormulaCar(
    team: FormulaTeam,
    helmetId?: string,
    suitId?: string,
    compound: TyreCompound = TYRE_COMPOUNDS[0],
    withLiveDisplay = false
  ): CarRig {
    const root = new THREE.Group();
    const bodyGroup = new THREE.Group();
    root.add(bodyGroup);

    const chassis = this.assets.carProto.clone(true);
    let drsFlap: THREE.Object3D | null = null;
    let driverHelmet: THREE.Object3D | null = null;
    let steeringDisplay: THREE.Object3D | null = null;

    const liveryTex = this.getTexture(team.liveryTex, false);
    const helmetObj = HELMET_LIST.find((h) => h.id === helmetId) || HELMET_LIST[0];
    const helmetTex = this.getTexture(helmetObj.tex, false);
    const suitObj = SUIT_LIST.find((s) => s.id === (suitId || team.id)) || SUIT_LIST[0];
    const suitTex = this.getTexture(suitObj.tex, false);

    let displayCanvas: HTMLCanvasElement | undefined;
    let displayCtx: CanvasRenderingContext2D | undefined;
    let displayTex: THREE.CanvasTexture | undefined;

    if (withLiveDisplay) {
      displayCanvas = document.createElement('canvas');
      displayCanvas.width = 256;
      displayCanvas.height = 128;
      displayCtx = displayCanvas.getContext('2d')!;
      displayTex = new THREE.CanvasTexture(displayCanvas);
      displayTex.colorSpace = THREE.SRGBColorSpace;
    }

    chassis.traverse((obj) => {
      if (obj.name.includes('RearWingDRS')) {
        drsFlap = obj;
      }
      if (obj.name.includes('DriverHelmet')) {
        driverHelmet = obj;
      }
      if (obj.name.includes('SteeringDisplay')) {
        steeringDisplay = obj;
      }
      if ((obj as THREE.Mesh).isMesh) {
        const mesh = obj as THREE.Mesh;
        const origMat = mesh.material as THREE.MeshStandardMaterial;
        if (origMat && origMat.name) {
          const mat = origMat.clone();
          if (mat.name.startsWith('CarLivery')) {
            mat.color.setHex(0xffffff);
            mat.map = liveryTex;
            mat.metalness = 0.28;
            mat.roughness = 0.24;
            mat.needsUpdate = true;
          } else if (mat.name.startsWith('DriverHelmetMat')) {
            mat.color.setHex(0xffffff);
            mat.map = helmetTex;
            mat.metalness = 0.25;
            mat.roughness = 0.16;
            mat.needsUpdate = true;
          } else if (mat.name.startsWith('DriverSuitMat')) {
            mat.color.setHex(0xffffff);
            mat.map = suitTex;
            mat.metalness = 0.05;
            mat.roughness = 0.65;
            mat.needsUpdate = true;
          } else if (mat.name.startsWith('SteeringDisplay') && displayTex) {
            mat.color.setHex(0xffffff);
            mat.map = displayTex;
            mat.emissive.setHex(0xffffff);
            mat.emissiveMap = displayTex;
            mat.emissiveIntensity = 0.9;
            mat.needsUpdate = true;
          }
          mesh.material = mat;
        }
      }
    });
    bodyGroup.add(chassis);

    const tyreTex = this.getTexture(compound.tex, true);

    const makeWheel = (isRear: boolean): THREE.Object3D => {
      const w = this.assets.wheelProto.clone(true);
      if (isRear) {
        w.scale.set(1.18, 1.04, 1.04);
      }
      w.traverse((obj) => {
        if ((obj as THREE.Mesh).isMesh) {
          const mesh = obj as THREE.Mesh;
          const origMat = mesh.material as THREE.MeshStandardMaterial;
          if (origMat && origMat.name.startsWith('TireSidewall')) {
            const mat = origMat.clone();
            mat.color.setHex(0xffffff);
            mat.map = tyreTex;
            mat.transparent = true;
            mat.alphaTest = 0.15;
            mat.roughness = 0.65;
            mat.needsUpdate = true;
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
      driverHelmet,
      steeringDisplay,
      displayCanvas,
      displayCtx,
      displayTex,
    };
  }

  createStandingDriver(suitId: string, helmetId: string): THREE.Group {
    const model = this.assets.driverProto.clone(true);
    const suitObj = SUIT_LIST.find((s) => s.id === suitId) || SUIT_LIST[0];
    const helmetObj = HELMET_LIST.find((h) => h.id === helmetId) || HELMET_LIST[0];
    const suitTex = this.getTexture(suitObj.tex, false);
    const helmetTex = this.getTexture(helmetObj.tex, false);

    model.traverse((obj) => {
      if ((obj as THREE.Mesh).isMesh) {
        const mesh = obj as THREE.Mesh;
        const origMat = mesh.material as THREE.MeshStandardMaterial;
        if (origMat && origMat.name) {
          const mat = origMat.clone();
          if (mat.name.startsWith('DriverSuitMat')) {
            mat.color.setHex(0xffffff);
            mat.map = suitTex;
            mat.roughness = 0.58;
            mat.needsUpdate = true;
          } else if (mat.name.startsWith('DriverHelmetMat')) {
            mat.color.setHex(0xffffff);
            mat.map = helmetTex;
            mat.roughness = 0.16;
            mat.metalness = 0.25;
            mat.needsUpdate = true;
          }
          mesh.material = mat;
        }
      }
    });
    return model;
  }
}
