import * as THREE from 'three';
import { FormulaTeam, TYRE_COMPOUNDS, TyreCompound } from '../data/teams';
import { AssetLoader, CarRig } from './AssetLoader';

export type ShowroomMode = 'MENU' | 'CAR_SELECT' | 'DRIVER_SELECT';

export class GarageScene {
  public readonly scene = new THREE.Scene();
  public readonly camera = new THREE.PerspectiveCamera(38, 16 / 9, 0.1, 120);

  private carGroup = new THREE.Group();
  private reflectionGroup = new THREE.Group();
  private driverGroup = new THREE.Group();

  private currentCar: CarRig | null = null;
  private reflectedCar: CarRig | null = null;
  private currentDriver: THREE.Group | null = null;

  private studioFloor!: THREE.Mesh;
  private studioBack!: THREE.Mesh;
  private darkStreakFloor!: THREE.Mesh;
  private keyLight!: THREE.DirectionalLight;
  private fillLight!: THREE.HemisphereLight;

  private mode: ShowroomMode = 'MENU';
  private carAngle = -Math.PI * 0.5; // Side-profile hero view like Monoposto CAR SELECTION!
  private driverAngle = 0;
  public zoomedIn = false;

  constructor(private assetLoader: AssetLoader) {
    // Bright studio hemisphere + directional lighting for clean Monoposto CAR SELECTION
    this.fillLight = new THREE.HemisphereLight(0xffffff, 0xb8bec8, 1.45);
    this.scene.add(this.fillLight);

    this.keyLight = new THREE.DirectionalLight(0xffffff, 2.2);
    this.keyLight.position.set(4, 10, 7);
    this.keyLight.castShadow = true;
    this.keyLight.shadow.mapSize.set(1024, 1024);
    this.scene.add(this.keyLight);

    const rimLight = new THREE.DirectionalLight(0xdce8ff, 1.1);
    rimLight.position.set(-6, 5, -6);
    this.scene.add(rimLight);

    // 1. Bright Monoposto White-Silver Glossy Studio Floor (semi-transparent so flipped reflection shows beneath!)
    const floorMat = new THREE.MeshStandardMaterial({
      color: 0xe6e9ef,
      roughness: 0.18,
      metalness: 0.08,
      transparent: true,
      opacity: 0.80,
    });
    this.studioFloor = new THREE.Mesh(new THREE.PlaneGeometry(46, 46), floorMat);
    this.studioFloor.rotation.x = -Math.PI / 2;
    this.studioFloor.position.y = 0.0;
    this.studioFloor.receiveShadow = true;
    this.scene.add(this.studioFloor);

    // Subtle soft cyclorama backdrop
    const backMat = new THREE.MeshBasicMaterial({ color: 0xb6bbc4 });
    this.studioBack = new THREE.Mesh(new THREE.PlaneGeometry(60, 24), backMat);
    this.studioBack.position.set(0, 8, -14);
    this.scene.add(this.studioBack);

    // 2. Dark Carbon Floor for DRIVER SELECTION / MENU
    this.darkStreakFloor = new THREE.Mesh(
      new THREE.PlaneGeometry(40, 40),
      new THREE.MeshStandardMaterial({ color: 0x0c0e14, roughness: 0.35, metalness: 0.55 })
    );
    this.darkStreakFloor.rotation.x = -Math.PI / 2;
    this.darkStreakFloor.position.y = -0.01;
    this.scene.add(this.darkStreakFloor);

    // Mirror reflection group (inverted Y directly under the glossy floor!)
    this.reflectionGroup.scale.set(1, -1, 1);
    this.reflectionGroup.position.y = -0.02;
    this.scene.add(this.reflectionGroup);
    this.scene.add(this.carGroup);
    this.scene.add(this.driverGroup);
  }

  setMode(mode: ShowroomMode) {
    this.mode = mode;
    if (mode === 'CAR_SELECT') {
      // Exact bright light-grey studio with glossy mirror floor like Monoposto CAR SELECTION!
      this.scene.background = new THREE.Color(0xc5cad2);
      this.scene.fog = new THREE.Fog(0xc5cad2, 16, 38);
      this.studioFloor.visible = true;
      this.studioBack.visible = true;
      this.darkStreakFloor.visible = false;
      this.carGroup.visible = true;
      this.reflectionGroup.visible = true;
      this.driverGroup.visible = false;
      this.fillLight.intensity = 1.55;
      this.keyLight.intensity = 2.1;
      this.carAngle = -Math.PI * 0.5; // Crisp side profile facing left!
    } else if (mode === 'DRIVER_SELECT') {
      // Dark carbon studio with subtle rim light like Monoposto DRIVER SELECTION!
      this.scene.background = new THREE.Color(0x0b0c10);
      this.scene.fog = new THREE.Fog(0x0b0c10, 12, 30);
      this.studioFloor.visible = false;
      this.studioBack.visible = false;
      this.darkStreakFloor.visible = true;
      this.carGroup.visible = false;
      this.reflectionGroup.visible = false;
      this.driverGroup.visible = true;
      this.fillLight.intensity = 1.25;
      this.keyLight.intensity = 2.0;
      this.driverAngle = 0;
    } else {
      // MENU hero background (dark glossy motorsport showroom)
      this.scene.background = new THREE.Color(0x0a0b10);
      this.scene.fog = new THREE.Fog(0x0a0b10, 12, 32);
      this.studioFloor.visible = false;
      this.studioBack.visible = false;
      this.darkStreakFloor.visible = true;
      this.carGroup.visible = true;
      this.reflectionGroup.visible = false;
      this.driverGroup.visible = false;
      this.fillLight.intensity = 1.15;
      this.keyLight.intensity = 2.2;
    }
  }

  setCar(team: FormulaTeam, helmetId: string, suitId: string, compound: TyreCompound = TYRE_COMPOUNDS[0]) {
    if (this.currentCar) this.carGroup.remove(this.currentCar.root);
    if (this.reflectedCar) this.reflectionGroup.remove(this.reflectedCar.root);

    this.currentCar = this.assetLoader.createFormulaCar(team, helmetId, suitId, compound, false);
    this.carGroup.add(this.currentCar.root);

    // Mirror reflection beneath glossy studio floor!
    this.reflectedCar = this.assetLoader.createFormulaCar(team, helmetId, suitId, compound, false);
    this.reflectionGroup.add(this.reflectedCar.root);
  }

  setDriver(suitId: string, helmetId: string) {
    if (this.currentDriver) this.driverGroup.remove(this.currentDriver);
    this.currentDriver = this.assetLoader.createStandingDriver(suitId, helmetId);
    this.driverGroup.add(this.currentDriver);
  }

  resize(w: number, h: number) {
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  update(dt: number) {
    if (this.mode === 'CAR_SELECT') {
      // Gentle showcase sway around crisp side-profile (or slow orbit when zoomed in)
      if (this.zoomedIn) {
        this.carAngle += dt * 0.42;
      } else {
        this.carAngle = -Math.PI * 0.5 + Math.sin(performance.now() * 0.00055) * 0.14;
      }
      this.carGroup.rotation.y = this.carAngle;
      this.reflectionGroup.rotation.y = this.carAngle;

      const dist = this.zoomedIn ? 5.6 : 7.6;
      const targetY = this.zoomedIn ? 1.25 : 1.08;
      this.camera.position.lerp(new THREE.Vector3(0, targetY, dist), Math.min(1, dt * 7));
      this.camera.lookAt(0, 0.42, 0);
    } else if (this.mode === 'DRIVER_SELECT') {
      this.driverAngle = Math.sin(performance.now() * 0.0008) * 0.25;
      this.driverGroup.rotation.y = this.driverAngle;
      const dist = this.zoomedIn ? 2.1 : 3.25;
      const camY = this.zoomedIn ? 1.48 : 1.02;
      const lookY = this.zoomedIn ? 1.42 : 0.94;
      this.camera.position.lerp(new THREE.Vector3(0, camY, dist), Math.min(1, dt * 7));
      this.camera.lookAt(0, lookY, 0);
    } else {
      // MENU
      this.carAngle += dt * 0.24;
      this.carGroup.rotation.y = this.carAngle;
      this.camera.position.lerp(new THREE.Vector3(3.4, 1.85, 5.4), Math.min(1, dt * 5));
      this.camera.lookAt(-0.7, 0.55, 0);
    }
  }
}
