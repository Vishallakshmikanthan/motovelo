import * as THREE from 'three';
import { CIRCUITS, CircuitDef } from '../data/circuits';
import {
  DRIVERS,
  DriverEntry,
  F1_POINTS,
  FormulaTeam,
  getDriversForTeam,
  getTeamById,
  HELMET_LIST,
  SUIT_LIST,
  TEAMS,
  TYRE_COMPOUNDS,
  TyreCompound,
} from '../data/teams';
import { AIController } from './AIController';
import { AssetLoader } from './AssetLoader';
import { AudioEngine } from './AudioEngine';
import { CameraRig } from './CameraRig';
import { GarageScene } from './GarageScene';
import { buildCircuit, BuiltTrack } from './TrackBuilder';
import { CarInput, CarSetup, FormulaCar } from './VehiclePhysics';

export type GameState =
  | 'LOADING'
  | 'MENU'
  | 'CAR_SELECT'
  | 'DRIVER_SELECT'
  | 'TRACK_SELECT'
  | 'CAR_SETUP'
  | 'GRID_INTRO'
  | 'COUNTDOWN'
  | 'RACING'
  | 'PAUSED'
  | 'RESULTS';

export type RaceMode = 'QUICK' | 'CHAMPIONSHIP' | 'TIMETRIAL';

export interface GameSettings {
  autoGear: boolean;
  quality: 'low' | 'high';
  audio: boolean;
  steerSensitivity: number;
  accelPressure: number;
  brakePressure: number;
}

export class GameEngine {
  private renderer: THREE.WebGLRenderer;
  private raceScene = new THREE.Scene();
  private raceCamera = new THREE.PerspectiveCamera(66, 16 / 9, 0.12, 1400);
  private assetLoader = new AssetLoader();
  public garageScene!: GarageScene;
  private cameraRig = new CameraRig();
  public audio = new AudioEngine();

  public state: GameState = 'LOADING';
  public mode: RaceMode = 'QUICK';
  public selectedTeamIndex = 0;   // Default Redbull Racing
  public selectedDriverIndex = 0; // Index within team drivers or DRIVERS
  public customHelmetIdx = 0;
  public customSuitIdx = 0;
  public selectedCircuitIndex = 0; // Default Dune Coast (Netherlands)
  public selectedLaps = 5;
  public difficulty = 1;
  public championshipPoints = 0;
  public championshipRound = 0;
  public fullTower = false;

  public carSetup: CarSetup = {
    wings: -20,
    brakeBalance: -16,
    suspension: 0,
    compound: TYRE_COMPOUNDS[0],
  };

  public settings: GameSettings = {
    autoGear: true,
    quality: 'high',
    audio: true,
    steerSensitivity: 1.0,
    accelPressure: 1.0,
    brakePressure: 1.0,
  };

  // Active race state
  private track: BuiltTrack | null = null;
  private cars: FormulaCar[] = [];
  private aiControllers = new Map<string, AIController>();
  public playerCar: FormulaCar | null = null;
  private raceTime = 0;
  private gridIntroTimer = 0;
  private countdownTimer = 0;
  private lastLitPod = 0;
  private bannerTimer = 0;
  private pitStopTimer = 0;
  private yellowFlagSector = 0;
  private fastestLapTime = Infinity;
  private fastestLapBannerTimer = 0;
  private accumulator = 0;
  private lastFrameTime = performance.now();
  private hudTick = 0;

  public playerInput: CarInput = {
    throttle: 0,
    brake: 0,
    steer: 0,
    ers: false,
    drs: false,
    shiftUp: false,
    shiftDown: false,
  };

  private minimapCtx: CanvasRenderingContext2D;
  private rpmLedEls: HTMLElement[] = [];

  constructor(private canvas: HTMLCanvasElement) {
    this.loadSavedState();

    this.renderer = new THREE.WebGLRenderer({
      canvas: this.canvas,
      antialias: this.settings.quality === 'high',
      powerPreference: 'high-performance',
    });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, this.settings.quality === 'high' ? 2 : 1));
    this.renderer.setSize(window.innerWidth, window.innerHeight);
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.08;
    this.renderer.shadowMap.enabled = this.settings.quality === 'high';
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    const minimap = document.getElementById('minimap-canvas') as HTMLCanvasElement;
    this.minimapCtx = minimap.getContext('2d')!;

    // Build 11 RPM dots matching Monoposto cluster
    const ledsWrap = document.getElementById('rpm-leds')!;
    ledsWrap.innerHTML = '';
    for (let i = 0; i < 11; i++) {
      const el = document.createElement('div');
      el.className = 'rpm-dot';
      ledsWrap.appendChild(el);
      this.rpmLedEls.push(el);
    }

    window.addEventListener('resize', () => this.onResize());
  }

  get currentTeam(): FormulaTeam {
    return TEAMS[this.selectedTeamIndex];
  }

  get currentDriver(): DriverEntry {
    const teamDrivers = getDriversForTeam(this.currentTeam.id);
    return teamDrivers[this.selectedDriverIndex % teamDrivers.length] || DRIVERS[0];
  }

  get currentHelmetId(): string {
    return HELMET_LIST[this.customHelmetIdx % HELMET_LIST.length].id;
  }

  get currentSuitId(): string {
    return SUIT_LIST[this.customSuitIdx % SUIT_LIST.length].id;
  }

  private loadSavedState() {
    try {
      const raw = localStorage.getItem('motovelo_save_v2');
      if (raw) {
        const d = JSON.parse(raw);
        if (typeof d.selectedTeamIndex === 'number') this.selectedTeamIndex = d.selectedTeamIndex % TEAMS.length;
        if (typeof d.championshipPoints === 'number') this.championshipPoints = d.championshipPoints;
        if (d.settings) Object.assign(this.settings, d.settings);
      }
    } catch {
      // ignore
    }
    this.audio.setEnabled(this.settings.audio);
  }

  public saveState() {
    try {
      localStorage.setItem(
        'motovelo_save_v2',
        JSON.stringify({
          selectedTeamIndex: this.selectedTeamIndex,
          championshipPoints: this.championshipPoints,
          settings: this.settings,
        })
      );
    } catch {
      // ignore
    }
  }

  async init() {
    const fill = document.getElementById('loader-fill')!;
    const txt = document.getElementById('loader-text')!;

    await this.assetLoader.loadAll((pct, label) => {
      fill.style.width = `${pct}%`;
      txt.textContent = label;
    });

    this.garageScene = new GarageScene(this.assetLoader);
    this.syncTeamDriverDefaults();
    this.onResize();

    this.setState('MENU');
    this.loop();
  }

  public syncTeamDriverDefaults() {
    const drv = this.currentDriver;
    const hIdx = HELMET_LIST.findIndex((h) => h.id === drv.helmetId);
    const sIdx = SUIT_LIST.findIndex((s) => s.id === drv.suitId);
    this.customHelmetIdx = hIdx >= 0 ? hIdx : 0;
    this.customSuitIdx = sIdx >= 0 ? sIdx : 0;
    this.refreshShowroomModels();
  }

  public refreshShowroomModels() {
    if (!this.garageScene) return;
    this.garageScene.setCar(this.currentTeam, this.currentHelmetId, this.currentSuitId, this.carSetup.compound);
    this.garageScene.setDriver(this.currentSuitId, this.currentHelmetId);
    this.updateMenuPills();
  }

  public selectTeam(index: number) {
    this.selectedTeamIndex = ((index % TEAMS.length) + TEAMS.length) % TEAMS.length;
    this.selectedDriverIndex = 0;
    this.syncTeamDriverDefaults();
    this.saveState();
  }

  public cycleDriver(delta: number) {
    const list = getDriversForTeam(this.currentTeam.id);
    this.selectedDriverIndex = ((this.selectedDriverIndex + delta) % list.length + list.length) % list.length;
    this.syncTeamDriverDefaults();
  }

  public cycleHelmet() {
    this.customHelmetIdx = (this.customHelmetIdx + 1) % HELMET_LIST.length;
    this.refreshShowroomModels();
  }

  public cycleSuit() {
    this.customSuitIdx = (this.customSuitIdx + 1) % SUIT_LIST.length;
    this.refreshShowroomModels();
  }

  public selectCompound(compound: TyreCompound) {
    this.carSetup.compound = compound;
    this.refreshShowroomModels();
  }

  onResize() {
    const w = window.innerWidth;
    const h = window.innerHeight;
    this.renderer.setSize(w, h);
    this.raceCamera.aspect = w / h;
    this.raceCamera.updateProjectionMatrix();
    if (this.garageScene) this.garageScene.resize(w, h);
  }

  applyQuality(q: 'low' | 'high') {
    this.settings.quality = q;
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, q === 'high' ? 2 : 1));
    this.renderer.shadowMap.enabled = q === 'high';
    this.saveState();
  }

  updateMenuPills() {
    const teamPill = document.getElementById('menu-team-pill');
    const champPill = document.getElementById('menu-champ-pill');
    if (teamPill) teamPill.textContent = this.currentTeam.name.toUpperCase();
    if (champPill) champPill.textContent = `CHAMP: ${this.championshipPoints} PTS`;
  }

  setState(next: GameState) {
    this.state = next;
    const ids = [
      'screen-loading',
      'screen-menu',
      'screen-car-select',
      'screen-driver-select',
      'screen-track-select',
      'screen-car-setup',
      'screen-grid-intro',
      'screen-hud',
      'screen-pause',
      'screen-results',
    ];
    ids.forEach((id) => document.getElementById(id)?.classList.remove('active'));

    if (next === 'LOADING') document.getElementById('screen-loading')?.classList.add('active');
    if (next === 'MENU') {
      document.getElementById('screen-menu')?.classList.add('active');
      this.garageScene?.setMode('MENU');
      this.audio.updateEngine(0, 0, 0, false);
      this.updateMenuPills();
    }
    if (next === 'CAR_SELECT') {
      document.getElementById('screen-car-select')?.classList.add('active');
      this.garageScene.zoomedIn = false;
      this.garageScene.setMode('CAR_SELECT');
      this.audio.updateEngine(0, 0, 0, false);
    }
    if (next === 'DRIVER_SELECT') {
      document.getElementById('screen-driver-select')?.classList.add('active');
      this.garageScene.zoomedIn = false;
      this.garageScene.setMode('DRIVER_SELECT');
      this.audio.updateEngine(0, 0, 0, false);
    }
    if (next === 'TRACK_SELECT') {
      document.getElementById('screen-track-select')?.classList.add('active');
      this.audio.updateEngine(0, 0, 0, false);
    }
    if (next === 'CAR_SETUP') {
      document.getElementById('screen-car-setup')?.classList.add('active');
      this.audio.updateEngine(0, 0, 0, false);
    }
    if (next === 'GRID_INTRO') {
      document.getElementById('screen-grid-intro')?.classList.add('active');
    }
    if (next === 'COUNTDOWN' || next === 'RACING') {
      document.getElementById('screen-hud')?.classList.add('active');
    }
    if (next === 'PAUSED') {
      document.getElementById('screen-hud')?.classList.add('active');
      document.getElementById('screen-pause')?.classList.add('active');
      this.audio.updateEngine(0, 0, 0, false);
    }
    if (next === 'RESULTS') {
      document.getElementById('screen-results')?.classList.add('active');
      this.audio.updateEngine(0, 0, 0, false);
    }

    const paddles = document.getElementById('gear-paddles');
    if (paddles) paddles.style.display = this.settings.autoGear ? 'none' : 'flex';
  }

  public skipGridIntro() {
    if (this.state === 'GRID_INTRO') {
      this.beginCountdown();
    }
  }

  private beginCountdown() {
    this.countdownTimer = 0;
    this.lastLitPod = 0;
    this.updateStartLights(0);
    document.getElementById('start-lights')?.classList.remove('hidden');

    const circuit = CIRCUITS[this.selectedCircuitIndex];
    document.getElementById('banner-track-name')!.textContent = circuit.name;
    document.getElementById('banner-country')!.textContent = `(${circuit.country})`;
    document.getElementById('banner-mode')!.textContent =
      this.mode === 'TIMETRIAL' ? 'Time Trial' : this.mode === 'CHAMPIONSHIP' ? 'Championship' : 'Race';
    document.getElementById('race-banner')?.classList.remove('hidden');

    this.setState('COUNTDOWN');
  }

  startRace(mode: RaceMode) {
    this.audio.unlock();
    this.mode = mode;

    if (mode === 'CHAMPIONSHIP') {
      this.selectedCircuitIndex = this.championshipRound % CIRCUITS.length;
    }

    const circuitDef: CircuitDef = CIRCUITS[this.selectedCircuitIndex];
    this.setupRaceScene(circuitDef);

    this.cars = [];
    this.aiControllers.clear();

    const playerTeam = this.currentTeam;
    const playerDrv = this.currentDriver;
    const gridEntries: { isPlayer: boolean; team: FormulaTeam; driver: DriverEntry }[] = [];

    if (mode === 'TIMETRIAL') {
      gridEntries.push({ isPlayer: true, team: playerTeam, driver: playerDrv });
    } else {
      // Full 22-car 2026 F1 grid! Player starts P8 for exciting overtakes through the pack!
      const rivalDrivers = DRIVERS.filter((d) => d.id !== playerDrv.id);
      const playerGridSlot = 7;
      for (let i = 0; i < 22; i++) {
        if (i === playerGridSlot) {
          gridEntries.push({ isPlayer: true, team: playerTeam, driver: playerDrv });
        } else {
          const r = rivalDrivers.shift();
          if (r) gridEntries.push({ isPlayer: false, team: getTeamById(r.teamId), driver: r });
        }
      }
    }

    const track = this.track!;
    gridEntries.forEach((entry, gridIdx) => {
      const comp = entry.isPlayer ? this.carSetup.compound : TYRE_COMPOUNDS[gridIdx % 3];
      const helmetId = entry.isPlayer ? this.currentHelmetId : entry.driver.helmetId;
      const suitId = entry.isPlayer ? this.currentSuitId : entry.driver.suitId;

      const rig = this.assetLoader.createFormulaCar(entry.team, helmetId, suitId, comp, entry.isPlayer);
      this.raceScene.add(rig.root);

      const setup: CarSetup = entry.isPlayer
        ? { ...this.carSetup }
        : { wings: 0, brakeBalance: 0, suspension: 0, compound: comp };

      const car = new FormulaCar(
        `car_${gridIdx}`,
        entry.isPlayer,
        entry.team,
        entry.driver.name,
        entry.driver.code,
        rig,
        setup
      );

      const distBehind = 10 + gridIdx * 7.8;
      const smp = track.getSampleAtDistance(track.totalLength - distBehind);
      const sideOffset = gridIdx % 2 === 0 ? -2.9 : 2.9;
      const startPos = smp.pos.clone().addScaledVector(smp.right, sideOffset);
      const startYaw = Math.atan2(smp.tangent.x, smp.tangent.z);

      car.resetAt(startPos, startYaw, track);
      this.cars.push(car);

      if (entry.isPlayer) {
        this.playerCar = car;
      } else {
        this.aiControllers.set(car.id, new AIController(gridIdx, this.difficulty));
      }
    });

    this.raceTime = 0;
    this.gridIntroTimer = 0;
    this.countdownTimer = 0;
    this.lastLitPod = 0;
    this.bannerTimer = 0;
    this.fastestLapTime = Infinity;
    this.fastestLapBannerTimer = 0;
    this.cameraRig.reset();

    // Populate Starting Grid Intro Card
    (document.getElementById('grid-driver-portrait') as HTMLImageElement).src = playerDrv.portrait;
    (document.getElementById('grid-team-logo') as HTMLImageElement).src = playerTeam.logoTex;
    document.getElementById('grid-pos-num')!.textContent = mode === 'TIMETRIAL' ? '1' : '8';
    document.getElementById('grid-driver-name')!.textContent = playerDrv.name;
    document.getElementById('grid-team-name')!.textContent = playerTeam.name;
    document.getElementById('grid-quali-time')!.textContent = '01:24.812';
    this.pitStopTimer = 0;
    this.yellowFlagSector = 0;
    document.getElementById('touch-pit')?.classList.remove('armed');

    document.getElementById('fastest-lap-banner')?.classList.add('hidden');
    document.getElementById('hud-session')!.textContent =
      mode === 'TIMETRIAL' ? 'HOTLAP' : 'RACE';

    // Reset sector boxes
    ['sec-s1', 'sec-s2', 'sec-s3'].forEach((id, i) => {
      const el = document.getElementById(id);
      if (el) {
        el.className = 'sec-cell';
        el.textContent = `S${i + 1}`;
      }
    });

    this.setState('GRID_INTRO');
  }

  private setupRaceScene(def: CircuitDef) {
    while (this.raceScene.children.length > 0) {
      this.raceScene.remove(this.raceScene.children[0]);
    }

    this.raceScene.background = new THREE.Color(def.skyHorizon);
    this.raceScene.fog = new THREE.FogExp2(def.fogColor, 0.0013);

    const skyGeo = new THREE.SphereGeometry(950, 24, 16);
    const skyMat = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      uniforms: {
        topColor: { value: new THREE.Color(def.skyTop) },
        bottomColor: { value: new THREE.Color(def.skyHorizon) },
      },
      vertexShader: `
        varying vec3 vWorldPosition;
        void main() {
          vec4 wp = modelMatrix * vec4(position, 1.0);
          vWorldPosition = wp.xyz;
          gl_Position = projectionMatrix * viewMatrix * wp;
        }
      `,
      fragmentShader: `
        uniform vec3 topColor;
        uniform vec3 bottomColor;
        varying vec3 vWorldPosition;
        void main() {
          float h = clamp(normalize(vWorldPosition).y * 1.4, 0.0, 1.0);
          gl_FragColor = vec4(mix(bottomColor, topColor, pow(h, 0.7)), 1.0);
        }
      `,
    });
    this.raceScene.add(new THREE.Mesh(skyGeo, skyMat));

    const hemi = new THREE.HemisphereLight(def.skyHorizon, def.groundColor, 1.1);
    this.raceScene.add(hemi);

    const sun = new THREE.DirectionalLight(def.sunColor, def.sunIntensity);
    sun.position.set(160, 220, 110);
    sun.castShadow = this.settings.quality === 'high';
    sun.shadow.mapSize.width = 2048;
    sun.shadow.mapSize.height = 2048;
    sun.shadow.camera.near = 20;
    sun.shadow.camera.far = 700;
    const d = 240;
    sun.shadow.camera.left = -d;
    sun.shadow.camera.right = d;
    sun.shadow.camera.top = d;
    sun.shadow.camera.bottom = -d;
    this.raceScene.add(sun);

    this.track = buildCircuit(def, this.assetLoader.assets);
    this.raceScene.add(this.track.group);
  }

  private updateStartLights(litCount: number) {
    const pods = document.querySelectorAll('#start-lights .mono-pod');
    pods.forEach((pod, idx) => {
      if (idx < litCount) pod.classList.add('lit');
      else pod.classList.remove('lit');
    });

    if (this.track) {
      this.track.startLightMats.forEach((mat, idx) => {
        if (!mat) return;
        if (idx < litCount) {
          mat.color.setHex(0xff1828);
          mat.emissive.setHex(0xff1020);
          mat.emissiveIntensity = 8.0;
        } else {
          mat.color.setHex(0x280508);
          mat.emissive.setHex(0x000000);
          mat.emissiveIntensity = 0.0;
        }
      });
    }
  }

  cycleCamera() {
    this.cameraRig.cycle();
    this.audio.playClick();
  }

  setLookBack(active: boolean) {
    this.cameraRig.lookBack = active;
  }

  togglePitRequest() {
    if (!this.playerCar || this.state !== 'RACING') return;
    this.playerCar.pitRequested = !this.playerCar.pitRequested;
    document.getElementById('touch-pit')?.classList.toggle('armed', this.playerCar.pitRequested);
    this.audio.playClick();
  }

  toggleTower() {
    this.fullTower = !this.fullTower;
    this.audio.playClick();
  }

  private updateSlipstreamAndFlags() {
    this.yellowFlagSector = 0;
    for (const car of this.cars) {
      car.slipstream = 0;
      if (this.raceTime > 6 && !car.isPlayer && car.speed < 14) {
        this.yellowFlagSector = car.projection.t < 0.33 ? 1 : car.projection.t < 0.66 ? 2 : 3;
      }
      const fwdX = Math.sin(car.yaw);
      const fwdZ = Math.cos(car.yaw);
      for (const other of this.cars) {
        if (other.id === car.id) continue;
        const dx = other.position.x - car.position.x;
        const dz = other.position.z - car.position.z;
        const dist = Math.hypot(dx, dz);
        if (dist > 4.0 && dist < 36.0) {
          const dot = (dx * fwdX + dz * fwdZ) / dist;
          if (dot > 0.94) {
            const tow = 1.0 - (dist - 4.0) / 32.0;
            if (tow > car.slipstream) car.slipstream = tow;
          }
        }
      }
    }
  }

  private resolveCarCollisions() {
    for (let i = 0; i < this.cars.length; i++) {
      for (let j = i + 1; j < this.cars.length; j++) {
        const a = this.cars[i];
        const b = this.cars[j];
        const dx = b.position.x - a.position.x;
        const dz = b.position.z - a.position.z;
        const dist = Math.hypot(dx, dz);
        const minDist = 2.6;
        if (dist > 0.001 && dist < minDist) {
          const nx = dx / dist;
          const nz = dz / dist;
          const push = (minDist - dist) * 0.55;
          a.position.x -= nx * push;
          a.position.z -= nz * push;
          b.position.x += nx * push;
          b.position.z += nz * push;
          a.speed *= 0.94;
          b.speed *= 0.94;
          if (a.isPlayer || b.isPlayer) {
            this.audio.playCollision(0.45);
          }
        }
      }
    }
  }

  private triggerFastestLapBanner(car: FormulaCar, prevBest: number) {
    const fl = document.getElementById('fastest-lap-banner');
    if (!fl) return;
    document.getElementById('fl-driver')!.textContent = car.driverName;
    document.getElementById('fl-time')!.textContent = formatTime(car.lastLapTime);
    const delta = Number.isFinite(prevBest) ? `-${(prevBest - car.lastLapTime).toFixed(3)}` : '';
    document.getElementById('fl-delta')!.textContent = delta;
    (document.getElementById('fl-logo') as HTMLImageElement).src = car.team.logoTex;
    fl.classList.remove('hidden');
    this.fastestLapBannerTimer = 4.5;
  }

  private stepPhysics(dt: number) {
    if (!this.track || !this.playerCar) return;

    if (this.state === 'COUNTDOWN') {
      this.countdownTimer += dt;
      const lit = Math.min(5, Math.floor(this.countdownTimer / 0.72) + 1);
      if (this.countdownTimer < 3.6) {
        if (lit > this.lastLitPod) {
          this.lastLitPod = lit;
          this.updateStartLights(lit);
          this.audio.playStartBeep(false);
        }
      } else if (this.countdownTimer >= 4.25) {
        this.updateStartLights(0);
        document.getElementById('start-lights')?.classList.add('hidden');
        document.getElementById('race-banner')?.classList.add('hidden');
        this.audio.playStartBeep(true);
        this.state = 'RACING';
      }
    } else if (this.state === 'RACING') {
      this.raceTime += dt;
      if (this.bannerTimer > 0) {
        this.bannerTimer -= dt;
        if (this.bannerTimer <= 0) {
          document.getElementById('race-banner')?.classList.add('hidden');
        }
      }
      if (this.fastestLapBannerTimer > 0) {
        this.fastestLapBannerTimer -= dt;
        if (this.fastestLapBannerTimer <= 0) {
          document.getElementById('fastest-lap-banner')?.classList.add('hidden');
        }
      }
    }

    const canMove = this.state === 'RACING';

    if (canMove) {
      this.updateSlipstreamAndFlags();
      // Handle Player Pit Stop when armed and passing main straight pit window
      if (this.playerCar.pitRequested && !this.playerCar.inPit && (this.playerCar.projection.t > 0.94 || this.playerCar.projection.t < 0.04)) {
        this.playerCar.inPit = true;
        this.playerCar.pitRequested = false;
        this.pitStopTimer = 2.2;
        document.getElementById('touch-pit')?.classList.remove('armed');
      }
      if (this.playerCar.inPit) {
        this.pitStopTimer -= dt;
        this.playerCar.speed *= Math.exp(-5 * dt);
        if (this.pitStopTimer <= 0) {
          this.playerCar.inPit = false;
          this.playerCar.pitStops++;
          this.playerCar.tyreHealth = 100;
          this.playerCar.carHealth = 100;
          this.playerCar.tyreTemp = 88;
          // Swap to next dry compound for authentic strategy
          const nextIdx = (TYRE_COMPOUNDS.findIndex((c) => c.id === this.playerCar!.setup.compound.id) + 1) % 3;
          this.playerCar.setup.compound = TYRE_COMPOUNDS[nextIdx];
          this.audio.playStartBeep(true);
        }
      }
    }

    for (const car of this.cars) {
      let input: CarInput;
      if (car.isPlayer) {
        input = car.inPit ? { throttle: 0, brake: 1, steer: 0, ers: false, drs: false } : this.playerInput;
      } else {
        const ai = this.aiControllers.get(car.id)!;
        input = canMove ? ai.computeInput(car, this.cars, this.track, dt) : { throttle: 0.4, brake: 0, steer: 0, ers: false, drs: false };
      }

      const res = car.step(
        dt,
        input,
        this.track,
        car.isPlayer ? this.settings.autoGear : true,
        this.settings.steerSensitivity,
        car.isPlayer ? this.settings.accelPressure : 1.0,
        car.isPlayer ? this.settings.brakePressure : 1.0,
        this.raceTime,
        this.selectedLaps,
        canMove
      );

      if (res.crossedLap && car.lastLapTime < this.fastestLapTime) {
        const prev = this.fastestLapTime;
        this.fastestLapTime = car.lastLapTime;
        this.triggerFastestLapBanner(car, prev);
      }

      if (car.isPlayer) {
        if (res.shifted) this.audio.playShiftPop();
        if (car.collisionImpulse > 0.1) this.audio.playCollision(car.collisionImpulse);
      }
    }

    this.playerInput.shiftUp = false;
    this.playerInput.shiftDown = false;

    if (canMove) {
      this.resolveCarCollisions();
    }

    if (this.playerCar.finished && this.state === 'RACING') {
      this.showResults();
    }
  }

  private getSortedStandings(): FormulaCar[] {
    return [...this.cars].sort((a, b) => {
      if (a.finished && b.finished) return a.finishTime - b.finishTime;
      if (a.finished) return -1;
      if (b.finished) return 1;
      return b.totalProgress - a.totalProgress;
    });
  }

  private showResults() {
    const standings = this.getSortedStandings();
    const playerPos = standings.findIndex((c) => c.isPlayer) + 1;
    const pts = this.mode === 'TIMETRIAL' ? 0 : F1_POINTS[playerPos - 1] ?? 0;

    if (this.mode === 'CHAMPIONSHIP') {
      this.championshipPoints += pts;
      this.championshipRound = (this.championshipRound + 1) % CIRCUITS.length;
      this.saveState();
    }

    document.getElementById('res-title')!.textContent =
      playerPos === 1 ? 'P1 — GRAND PRIX VICTORY!' : `P${playerPos} — FINISHED`;
    document.getElementById('res-subtitle')!.textContent =
      `${CIRCUITS[this.selectedCircuitIndex].name} // ${this.mode}`;
    document.getElementById('res-best-lap')!.textContent =
      `BEST LAP: ${formatTime(this.playerCar!.bestLapTime)}`;
    document.getElementById('btn-res-next')!.style.display =
      this.mode === 'CHAMPIONSHIP' ? 'inline-flex' : 'none';

    const table = document.getElementById('res-table')!;
    table.innerHTML = '';
    standings.forEach((car, idx) => {
      const row = document.createElement('div');
      row.className = `res-row ${car.isPlayer ? 'player' : ''}`;
      const gap =
        idx === 0
          ? formatTime(car.finishTime || this.raceTime)
          : `+${(idx * 1.15 + Math.abs(standings[0].totalProgress - car.totalProgress) * 18).toFixed(3)}s`;
      const rowPts = this.mode === 'TIMETRIAL' ? '—' : `+${F1_POINTS[idx] ?? 0} PTS`;
      row.innerHTML = `<span>P${idx + 1}</span><img src="${car.team.logoTex}" class="tower-logo" /><span>${car.driverName}</span><span style="font-family:monospace">${gap}</span><span style="text-align:right;color:#f5b700">${rowPts}</span>`;
      table.appendChild(row);
    });

    this.setState('RESULTS');
  }

  private updateHUD() {
    if (!this.playerCar || !this.track) return;
    const car = this.playerCar;

    const kmh = Math.round(car.speed * 3.6);
    const mph = Math.round(kmh * 0.621371);
    document.getElementById('hud-kph')!.textContent = String(kmh);
    document.getElementById('hud-mph')!.textContent = String(mph);
    document.getElementById('hud-gear')!.textContent =
      this.state === 'COUNTDOWN' || car.speed < 0.4 ? 'N' : String(car.gear);

    const standings = this.getSortedStandings();
    const playerIdx = standings.findIndex((c) => c.isPlayer);
    const pos = playerIdx + 1;

    document.getElementById('hud-lap')!.textContent = `LAP ${car.lap}/${this.selectedLaps}`;
    document.getElementById('hud-time')!.textContent = formatTime(this.raceTime - car.lapStartTime);
    document.getElementById('hud-best')!.textContent = formatTime(car.bestLapTime);
    document.getElementById('hud-fastest')!.textContent = formatTime(this.fastestLapTime);

    const deltaVal =
      Number.isFinite(car.bestLapTime) && this.raceTime - car.lapStartTime > 2
        ? `${(this.raceTime - car.lapStartTime - car.bestLapTime * Math.max(0.05, car.projection.t)).toFixed(3)}`
        : '--.---';
    document.getElementById('hud-delta')!.textContent = deltaVal;

    // Sector cells
    car.sectorTimes.forEach((st, idx) => {
      const el = document.getElementById(`sec-s${idx + 1}`);
      if (el && st !== null) {
        el.className = 'sec-cell green';
        el.textContent = formatTime(st);
      }
    });

    // Tyre temp, ring color, fuel & ERS bars
    document.getElementById('hud-tyre-temp')!.textContent = `${Math.round(car.tyreTemp)}°`;
    const ring = document.getElementById('hud-tyre-ring') as HTMLElement;
    ring.style.borderColor = car.tyreTemp > 96 ? '#ff4433' : car.tyreTemp > 84 ? '#22ee55' : '#22d8ff';
    (document.getElementById('hud-ers-bar') as HTMLElement).style.width = `${Math.round(car.ersCharge * 100)}%`;
    (document.getElementById('hud-fuel-bar') as HTMLElement).style.width = `${Math.round(car.fuelLevel)}%`;
    (document.getElementById('hud-tyre-bar') as HTMLElement).style.width = `${Math.round(car.tyreHealth)}%`;

    // 11 RPM LEDs
    const rpmFrac = Math.max(0, Math.min(1, (car.rpm - 5000) / 8200));
    const litLeds = Math.round(rpmFrac * 11);
    for (let i = 0; i < 11; i++) {
      const el = this.rpmLedEls[i];
      if (i < litLeds) {
        el.className = i < 4 ? 'rpm-dot g' : i < 8 ? 'rpm-dot r' : 'rpm-dot b';
      } else {
        el.className = 'rpm-dot';
      }
    }

    // Update Leaderboard Tower, Yellow Flag Header, DRS glow & Cockpit Display at ~10Hz
    this.hudTick++;
    if (this.hudTick % 6 === 0) {
      const hdr = document.getElementById('tower-header');
      if (hdr) {
        if (this.yellowFlagSector > 0) {
          hdr.textContent = `YELLOW FLAG S${this.yellowFlagSector}`;
          hdr.classList.add('yellow-flag');
        } else {
          hdr.textContent = 'MOTOVELO';
          hdr.classList.remove('yellow-flag');
        }
      }
      document.getElementById('touch-drs')?.classList.toggle('active', car.drsActive);

      const rowsEl = document.getElementById('tower-rows')!;
      let slice: { car: FormulaCar; rank: number }[] = [];
      if (this.fullTower || standings.length <= 4) {
        slice = standings.map((c, idx) => ({ car: c, rank: idx + 1 }));
      } else {
        const start = Math.max(0, Math.min(standings.length - 3, playerIdx - 1));
        for (let i = start; i < Math.min(standings.length, start + 3); i++) {
          slice.push({ car: standings[i], rank: i + 1 });
        }
      }
      const leaderProg = standings[0]?.totalProgress ?? 0;
      rowsEl.innerHTML = slice
        .map(({ car: c, rank }) => {
          const gapSec = Math.max(0, (leaderProg - c.totalProgress) * 58);
          const gapText = c.inPit ? `PIT (${c.pitStops + 1}) P` : rank === 1 ? `LAP ${c.lap}` : `+${gapSec.toFixed(3)}`;
          return `<div class="tower-row ${c.isPlayer ? 'player' : ''}">
            <span class="tyre-dot" style="border-color:${c.setup.compound.color}">${c.pitStops}</span>
            <span>${String(rank).padStart(2, '0')}</span>
            <span>${c.driverCode}</span>
            <img src="${c.team.logoTex}" class="tower-logo" />
            <span class="tower-gap">${gapText}</span>
          </div>`;
        })
        .join('');

      car.updateCockpitDisplay(pos, this.selectedLaps, deltaVal);
    }

    this.drawMinimap();
  }

  private drawMinimap() {
    if (!this.track) return;
    const ctx = this.minimapCtx;
    const w = 124;
    const h = 124;
    ctx.clearRect(0, 0, w, h);

    let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity;
    for (const s of this.track.samples) {
      if (s.pos.x < minX) minX = s.pos.x;
      if (s.pos.x > maxX) maxX = s.pos.x;
      if (s.pos.z < minZ) minZ = s.pos.z;
      if (s.pos.z > maxZ) maxZ = s.pos.z;
    }
    const pad = 16;
    const scale = Math.min((w - pad * 2) / Math.max(1, maxX - minX), (h - pad * 2) / Math.max(1, maxZ - minZ));
    const ox = (w - (maxX - minX) * scale) * 0.5;
    const oz = (h - (maxZ - minZ) * scale) * 0.5;
    const toX = (x: number) => ox + (x - minX) * scale;
    const toY = (z: number) => oz + (z - minZ) * scale;

    // Dark border + light grey road outline
    ctx.strokeStyle = '#111';
    ctx.lineWidth = 6;
    ctx.beginPath();
    this.track.samples.forEach((s, i) => {
      if (i === 0) ctx.moveTo(toX(s.pos.x), toY(s.pos.z));
      else ctx.lineTo(toX(s.pos.x), toY(s.pos.z));
    });
    ctx.closePath();
    ctx.stroke();

    ctx.strokeStyle = '#aeb6c4';
    ctx.lineWidth = 3.5;
    ctx.stroke();

    // Car dots
    for (const car of this.cars) {
      if (car.isPlayer) continue;
      ctx.fillStyle = '#' + car.team.primary.toString(16).padStart(6, '0');
      ctx.beginPath();
      ctx.arc(toX(car.position.x), toY(car.position.z), 3, 0, Math.PI * 2);
      ctx.fill();
    }

    // Green player arrow
    if (this.playerCar) {
      const px = toX(this.playerCar.position.x);
      const py = toY(this.playerCar.position.z);
      ctx.save();
      ctx.translate(px, py);
      ctx.rotate(-this.playerCar.yaw + Math.PI);
      ctx.fillStyle = '#22ff44';
      ctx.strokeStyle = '#062008';
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.moveTo(0, -6);
      ctx.lineTo(5, 5);
      ctx.lineTo(-5, 5);
      ctx.closePath();
      ctx.fill();
      ctx.stroke();
      ctx.restore();
    }
  }

  private loop = () => {
    requestAnimationFrame(this.loop);
    const now = performance.now();
    const frameDt = Math.min(0.05, (now - this.lastFrameTime) / 1000);
    this.lastFrameTime = now;

    if (
      this.state === 'MENU' ||
      this.state === 'CAR_SELECT' ||
      this.state === 'DRIVER_SELECT' ||
      this.state === 'TRACK_SELECT' ||
      this.state === 'CAR_SETUP'
    ) {
      this.garageScene.update(frameDt);
      this.renderer.render(this.garageScene.scene, this.garageScene.camera);
      return;
    }

    if (this.state === 'GRID_INTRO') {
      this.gridIntroTimer += frameDt;
      if (this.playerCar) {
        // Dramatic grid fly-over camera looking down the starting grid!
        const p = this.playerCar.position;
        const fwd = new THREE.Vector3(Math.sin(this.playerCar.yaw), 0, Math.cos(this.playerCar.yaw));
        const right = new THREE.Vector3(Math.cos(this.playerCar.yaw), 0, -Math.sin(this.playerCar.yaw));
        const camPos = p
          .clone()
          .addScaledVector(fwd, 9 - this.gridIntroTimer * 2.2)
          .addScaledVector(right, 4.2);
        camPos.y += 2.3;
        this.raceCamera.position.copy(camPos);
        this.raceCamera.lookAt(p.x, p.y + 0.8, p.z - 8);
      }
      this.renderer.render(this.raceScene, this.raceCamera);
      if (this.gridIntroTimer >= 2.8) {
        this.beginCountdown();
      }
      return;
    }

    if (this.state === 'COUNTDOWN' || this.state === 'RACING') {
      const FIXED_DT = 1 / 60;
      this.accumulator += frameDt;
      while (this.accumulator >= FIXED_DT) {
        this.stepPhysics(FIXED_DT);
        this.accumulator -= FIXED_DT;
      }

      if (this.playerCar && this.track) {
        this.cameraRig.update(this.raceCamera, this.playerCar, this.track, frameDt);
        this.audio.updateEngine(this.playerCar.rpm, this.playerInput.throttle, this.playerCar.slipAmount, true);
        this.updateHUD();
      }
      this.renderer.render(this.raceScene, this.raceCamera);
      return;
    }

    if (this.state === 'PAUSED' || this.state === 'RESULTS') {
      this.renderer.render(this.raceScene, this.raceCamera);
    }
  };
}

function formatTime(sec: number): string {
  if (!Number.isFinite(sec) || sec < 0) return '--:--.---';
  const mins = Math.floor(sec / 60);
  const rem = sec % 60;
  const whole = Math.floor(rem);
  const ms = Math.floor((rem - whole) * 1000);
  return `${String(mins).padStart(2, '0')}:${String(whole).padStart(2, '0')}.${String(ms).padStart(3, '0')}`;
}
