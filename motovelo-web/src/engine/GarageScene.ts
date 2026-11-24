import * as THREE from 'three';
import { FormulaTeam } from '../data/teams';
import { AssetLoader, CarRig } from './AssetLoader';

export class GarageScene {
  public readonly scene = new THREE.Scene();
  public readonly camera = new THREE.PerspectiveCamera(45, 16 / 9, 0.1, 120);
  private turntable = new THREE.Group();
  private currentCar: CarRig | null = null;
  private angle = 0.65;

  constructor(private assetLoader: AssetLoader) {
    this.scene.background = new THREE.Color(0x0b0e16);
    this.scene.fog = new THREE.FogExp2(0x0b0e16, 0.032);

    // Studio lighting
    const amb = new THREE.AmbientLight(0x8899bb, 0.9);
    this.scene.add(amb);

    const key = new THREE.SpotLight(0xffffff, 85, 35, Math.PI / 4, 0.5, 1.4);
    key.position.set(5, 9, 6);
    key.castShadow = true;
    this.scene.add(key);

    const rimRed = new THREE.PointLight(0xe6192e, 32, 20);
    rimRed.position.set(-5, 3.5, -4);
    this.scene.add(rimRed);

    const rimCyan = new THREE.PointLight(0x00c2ff, 22, 20);
    rimCyan.position.set(5, 3.0, -5);
    this.scene.add(rimCyan);

    // Reflective showroom floor & turntable plinth
    const floor = new THREE.Mesh(
      new THREE.CircleGeometry(28, 48),
      new THREE.MeshStandardMaterial({ color: 0x0f121b, roughness: 0.25, metalness: 0.7 })
    );
    floor.rotation.x = -Math.PI / 2;
    floor.receiveShadow = true;
    this.scene.add(floor);

    const plinth = new THREE.Mesh(
      new THREE.CylinderGeometry(3.4, 3.65, 0.16, 48),
      new THREE.MeshStandardMaterial({ color: 0x181c28, roughness: 0.3, metalness: 0.8 })
    );
    plinth.position.y = 0.08;
    plinth.receiveShadow = true;
    this.turntable.add(plinth);

    const ring = new THREE.Mesh(
      new THREE.TorusGeometry(3.52, 0.045, 12, 64),
      new THREE.MeshStandardMaterial({ color: 0xe6192e, emissive: 0xe6192e, emissiveIntensity: 1.8 })
    );
    ring.rotation.x = Math.PI / 2;
    ring.position.y = 0.16;
    this.turntable.add(ring);

    this.scene.add(this.turntable);
  }

  setTeam(team: FormulaTeam) {
    if (this.currentCar) {
      this.turntable.remove(this.currentCar.root);
    }
    this.currentCar = this.assetLoader.createFormulaCar(team);
    this.currentCar.root.position.set(0, 0.16, 0);
    this.turntable.add(this.currentCar.root);
  }

  resize(w: number, h: number) {
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  update(dt: number, isGarageFocus: boolean) {
    this.angle += dt * (isGarageFocus ? 0.35 : 0.22);
    this.turntable.rotation.y = this.angle;
    const targetX = isGarageFocus ? 0.0 : -0.6;
    this.camera.position.set(targetX + 4.4, 2.05, 5.2);
    this.camera.lookAt(targetX, 0.65, 0);
  }
}
