import * as THREE from 'three';
import { CIRCUITS, CircuitDef } from '../data/circuits';
import { EXTRA_AI_DRIVERS, F1_POINTS, FormulaTeam, TEAMS } from '../data/teams';
import { AIController } from './AIController';
import { AssetLoader } from './AssetLoader';
import { AudioEngine } from './AudioEngine';
import { CameraRig } from './CameraRig';
import { GarageScene } from './GarageScene';
import { buildCircuit, BuiltTrack } from './TrackBuilder';
import { CarInput, FormulaCar } from './VehiclePhysics';

export type GameState = 'LOADING' | 'MENU' | 'GARAGE' | 'COUNTDOWN' | 'RACING' | 'PAUSED' | 'RESULTS';
export type RaceMode = 'QUICK' | 'CHAMPIONSHIP' | 'TIMETRIAL';

export interface GameSettings {
  autoGear: boolean;
  quality: 'low' | 'high';
  audio: boolean;
  steerSensitivity: number;
}

export class GameEngine {
  private renderer: THREE.WebGLRenderer;
  private raceScene = new THREE.Scene();
  private raceCamera = new THREE.PerspectiveCamera(65, 16 / 9, 0.15, 1400);
  private assetLoader = new AssetLoader();
  private garageScene!: GarageScene;
  private cameraRig = new CameraRig();
  public audio = new AudioEngine();

  public state: GameState = 'LOADING';
  public mode: RaceMode = 'QUICK';
  public selectedTeamIndex = 0;
  public selectedCircuitIndex = 0;
  public selectedLaps = 3;
  public difficulty = 1; // 0 Rookie, 1 Pro, 2 Legend
  public championshipPoints = 0;
  public championshipRound = 0;

  public settings: GameSettings = {
    autoGear: true,
    quality: 'high',
    audio: true,
    steerSensitivity: 1.0,
  };

  // Active race state
  private track: BuiltTrack | null = null;
  private cars: FormulaCar[] = [];
  private aiControllers = new Map<string, AIController>();
  private playerCar: FormulaCar | null = null;
  private raceTime = 0;
  private countdownTimer = 0;
  private lastLitPod = 0;
  private bannerTimer = 0;
  private accumulator = 0;
  private lastFrameTime = performance.now();

  // Player live input
  public playerInput: CarInput = {
    throttle: 0,
    brake: 0,
    steer: 0,
    ers: false,
    shiftUp: false,
    shiftDown: false,
  };

  // UI Hooks
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

    // Build 15 RPM LEDs
    const ledsWrap = document.getElementById('rpm-leds')!;
    ledsWrap.innerHTML = '';
    for (let i = 0; i < 15; i++) {
      const el = document.createElement('div');
      el.className = 'rpm-led';
      ledsWrap.appendChild(el);
      this.rpmLedEls.push(el);
    }

    window.addEventListener('resize', () => this.onResize());
  }

  private loadSavedState() {
    try {
      const raw = localStorage.getItem('motovelo_save_v1');
      if (raw) {
        const d = JSON.parse(raw);
        if (typeof d.selectedTeamIndex === 'number') this.selectedTeamIndex = d.selectedTeamIndex % TEAMS.length;
        if (typeof d.championshipPoints === 'number') this.championshipPoints = d.championshipPoints;
        if (d.settings) Object.assign(this.settings, d.settings);
      }
    } catch {
      // Ignore storage errors
    }
    this.audio.setEnabled(this.settings.audio);
  }

  public saveState() {
    try {
      localStorage.setItem(
        'motovelo_save_v1',
        JSON.stringify({
          selectedTeamIndex: this.selectedTeamIndex,
          championshipPoints: this.championshipPoints,
          settings: this.settings,
        })
      );
    } catch {
      // Ignore storage errors
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
    this.garageScene.setTeam(TEAMS[this.selectedTeamIndex]);
    this.onResize();

    this.setState('MENU');
    this.loop();
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

  selectTeam(index: number) {
    this.selectedTeamIndex = index;
    if (this.garageScene) this.garageScene.setTeam(TEAMS[index]);
    this.saveState();
    this.updateMenuPills();
  }

  updateMenuPills() {
    const teamPill = document.getElementById('menu-team-pill');
    const champPill = document.getElementById('menu-champ-pill');
    if (teamPill) teamPill.textContent = TEAMS[this.selectedTeamIndex].name.toUpperCase();
    if (champPill) champPill.textContent = `CHAMP: ${this.championshipPoints} PTS`;
  }

  setState(next: GameState) {
    this.state = next;
    const ids = ['screen-loading', 'screen-menu', 'screen-garage', 'screen-hud', 'screen-pause', 'screen-results'];
    ids.forEach((id) => document.getElementById(id)?.classList.remove('active'));

    if (next === 'LOADING') document.getElementById('screen-loading')?.classList.add('active');
    if (next === 'MENU') {
      document.getElementById('screen-menu')?.classList.add('active');
      this.audio.updateEngine(0, 0, 0, false);
      this.updateMenuPills();
    }
    if (next === 'GARAGE') {
      document.getElementById('screen-garage')?.classList.add('active');
      this.audio.updateEngine(0, 0, 0, false);
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

    // Show or hide manual gear paddles
    const paddles = document.getElementById('gear-paddles');
    if (paddles) paddles.style.display = this.settings.autoGear ? 'none' : 'flex';
  }

  startRace(mode: RaceMode) {
    this.audio.unlock();
    this.mode = mode;

    if (mode === 'CHAMPIONSHIP') {
      this.selectedCircuitIndex = this.championshipRound % CIRCUITS.length;
    }

    const circuitDef: CircuitDef = CIRCUITS[this.selectedCircuitIndex];
    this.setupRaceScene(circuitDef);

    // Spawn Grid (1 car in Time Trial, 8 cars in Quick Race / Championship)
    this.cars = [];
    this.aiControllers.clear();

    const playerTeam: FormulaTeam = TEAMS[this.selectedTeamIndex];
    const gridEntries: { isPlayer: boolean; team: FormulaTeam; driver: string }[] = [];

    if (mode === 'TIMETRIAL') {
      gridEntries.push({ isPlayer: true, team: playerTeam, driver: 'YOU' });
    } else {
      // Build 7 AI rivals + Player starting P4 for exciting overtakes
      const rivals: { team: FormulaTeam; driver: string }[] = [];
      TEAMS.forEach((t, idx) => {
        if (idx !== this.selectedTeamIndex) rivals.push({ team: t, driver: t.driverName });
      });
      EXTRA_AI_DRIVERS.forEach((ex) => {
        rivals.push({ team: TEAMS[ex.teamIdx], driver: ex.name });
      });

      for (let i = 0; i < 8; i++) {
        if (i === 3) {
          gridEntries.push({ isPlayer: true, team: playerTeam, driver: `YOU (${playerTeam.shortName})` });
        } else {
          const r = rivals.shift()!;
          gridEntries.push({ isPlayer: false, team: r.team, driver: r.driver });
        }
      }
    }

    const track = this.track!;
    gridEntries.forEach((entry, gridIdx) => {
      const rig = this.assetLoader.createFormulaCar(entry.team);
      this.raceScene.add(rig.root);
      const car = new FormulaCar(`car_${gridIdx}`, entry.isPlayer, entry.team, entry.driver, rig);

      // Staggered 2-wide F1 starting grid behind start/finish line
      const distBehind = 10 + gridIdx * 7.5;
      const smp = track.getSampleAtDistance(track.totalLength - distBehind);
      const sideOffset = gridIdx % 2 === 0 ? -2.8 : 2.8;
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
    this.countdownTimer = 0;
    this.lastLitPod = 0;
    this.bannerTimer = 0;
    this.cameraRig.reset();

    // Reset 5-red-light HUD & 3D Gantry
    this.updateStartLights(0);
    document.getElementById('start-lights')?.classList.remove('hidden');
    document.getElementById('race-banner')?.classList.add('hidden');
    const camBtn = document.getElementById('btn-hud-cam');
    if (camBtn) camBtn.textContent = `CAM: ${this.cameraRig.mode}`;

    this.setState('COUNTDOWN');
  }

  private setupRaceScene(def: CircuitDef) {
    // Clear previous scene objects
    while (this.raceScene.children.length > 0) {
      this.raceScene.remove(this.raceScene.children[0]);
    }

    this.raceScene.background = new THREE.Color(def.skyHorizon);
    this.raceScene.fog = new THREE.FogExp2(def.fogColor, 0.0014);

    // Sky dome gradient
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

    // Lighting
    const hemi = new THREE.HemisphereLight(def.skyHorizon, def.groundColor, 1.05);
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
    const pods = document.querySelectorAll('#start-lights .light-pod');
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
    const mode = this.cameraRig.cycle();
    const camBtn = document.getElementById('btn-hud-cam');
    if (camBtn) camBtn.textContent = `CAM: ${mode}`;
    this.audio.playClick();
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
        // LIGHTS OUT AND AWAY WE GO!
        this.updateStartLights(0);
        document.getElementById('start-lights')?.classList.add('hidden');
        const banner = document.getElementById('race-banner');
        if (banner) {
          banner.textContent = 'LIGHTS OUT!';
          banner.classList.remove('hidden');
        }
        this.bannerTimer = 1.6;
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
    }

    const canMove = this.state === 'RACING';

    for (const car of this.cars) {
      let input: CarInput;
      if (car.isPlayer) {
        input = this.playerInput;
      } else {
        const ai = this.aiControllers.get(car.id)!;
        input = canMove ? ai.computeInput(car, this.cars, this.track, dt) : { throttle: 0.4, brake: 0, steer: 0, ers: false };
      }

      const res = car.step(
        dt,
        input,
        this.track,
        car.isPlayer ? this.settings.autoGear : true,
        this.settings.steerSensitivity,
        this.raceTime,
        this.selectedLaps,
        canMove
      );

      if (car.isPlayer) {
        if (res.shifted) this.audio.playShiftPop();
        if (car.collisionImpulse > 0.1) this.audio.playCollision(car.collisionImpulse);
        if (res.crossedLap && !car.finished) {
          const banner = document.getElementById('race-banner');
          if (banner) {
            banner.textContent = car.lap === this.selectedLaps ? 'FINAL LAP!' : `LAP ${car.lap}/${this.selectedLaps}`;
            banner.classList.remove('hidden');
            this.bannerTimer = 1.8;
          }
        }
      }
    }

    // Consume one-shot manual shift triggers
    this.playerInput.shiftUp = false;
    this.playerInput.shiftDown = false;

    if (canMove) {
      this.resolveCarCollisions();
    }

    // Check race completion for player
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

    const title = document.getElementById('res-title')!;
    const sub = document.getElementById('res-subtitle')!;
    const bestEl = document.getElementById('res-best-lap')!;
    const table = document.getElementById('res-table')!;
    const nextBtn = document.getElementById('btn-res-next')!;

    title.textContent = playerPos === 1 ? 'P1 — GRAND PRIX VICTORY!' : `P${playerPos} — FINISHED`;
    sub.textContent = `${CIRCUITS[this.selectedCircuitIndex].name} // ${this.mode}`;
    bestEl.textContent = `BEST LAP: ${formatTime(this.playerCar!.bestLapTime)}`;
    nextBtn.style.display = this.mode === 'CHAMPIONSHIP' ? 'flex' : 'none';

    table.innerHTML = '';
    standings.forEach((car, idx) => {
      const row = document.createElement('div');
      row.className = `res-row ${car.isPlayer ? 'player' : ''}`;
      const gap = idx === 0 ? formatTime(car.finishTime || this.raceTime) : `+${((idx * 1.4) + Math.abs(standings[0].totalProgress - car.totalProgress) * 18).toFixed(2)}s`;
      const rowPts = this.mode === 'TIMETRIAL' ? '—' : `+${F1_POINTS[idx] ?? 0} PTS`;
      row.innerHTML = `<span>P${idx + 1}</span><span>${car.driverName} <small style="color:#98a2b3">(${car.team.shortName})</small></span><span>${gap}</span><span style="text-align:right;color:#f5b700">${rowPts}</span>`;
      table.appendChild(row);
    });

    this.setState('RESULTS');
  }

  private updateHUD() {
    if (!this.playerCar || !this.track) return;
    const car = this.playerCar;

    const kmh = Math.round(car.speed * 3.6);
    document.getElementById('hud-speed')!.textContent = String(kmh);
    document.getElementById('hud-gear')!.textContent = this.state === 'COUNTDOWN' ? '1' : String(car.gear);

    const standings = this.getSortedStandings();
    const pos = standings.findIndex((c) => c.isPlayer) + 1;
    document.getElementById('hud-pos')!.innerHTML = `${pos}<small>/${this.cars.length}</small>`;
    document.getElementById('hud-lap')!.innerHTML = `${car.lap}<small>/${this.selectedLaps}</small>`;
    document.getElementById('hud-time')!.textContent = formatTime(this.raceTime - car.lapStartTime);
    document.getElementById('hud-best')!.textContent = formatTime(car.bestLapTime);

    const ersEl = document.getElementById('hud-ers')!;
    ersEl.style.width = `${Math.round(car.ersCharge * 100)}%`;

    // RPM LEDs
    const rpmFrac = Math.max(0, Math.min(1, (car.rpm - 5000) / 8200));
    const litLeds = Math.round(rpmFrac * 15);
    for (let i = 0; i < 15; i++) {
      const el = this.rpmLedEls[i];
      if (i < litLeds) {
        el.className = i < 5 ? 'rpm-led g' : i < 10 ? 'rpm-led r' : 'rpm-led b';
      } else {
        el.className = 'rpm-led';
      }
    }

    // Minimap
    this.drawMinimap();
  }

  private drawMinimap() {
    if (!this.track) return;
    const ctx = this.minimapCtx;
    const w = 136;
    const h = 136;
    ctx.clearRect(0, 0, w, h);

    let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity;
    for (const s of this.track.samples) {
      if (s.pos.x < minX) minX = s.pos.x;
      if (s.pos.x > maxX) maxX = s.pos.x;
      if (s.pos.z < minZ) minZ = s.pos.z;
      if (s.pos.z > maxZ) maxZ = s.pos.z;
    }
    const pad = 14;
    const scale = Math.min((w - pad * 2) / Math.max(1, maxX - minX), (h - pad * 2) / Math.max(1, maxZ - minZ));
    const ox = (w - (maxX - minX) * scale) * 0.5;
    const oz = (h - (maxZ - minZ) * scale) * 0.5;

    const toX = (x: number) => ox + (x - minX) * scale;
    const toY = (z: number) => oz + (z - minZ) * scale;

    // Track outline
    ctx.strokeStyle = 'rgba(255,255,255,0.45)';
    ctx.lineWidth = 4;
    ctx.beginPath();
    this.track.samples.forEach((s, i) => {
      if (i === 0) ctx.moveTo(toX(s.pos.x), toY(s.pos.z));
      else ctx.lineTo(toX(s.pos.x), toY(s.pos.z));
    });
    ctx.closePath();
    ctx.stroke();

    // Cars
    for (const car of this.cars) {
      ctx.fillStyle = car.isPlayer ? '#ff1e2d' : '#f5b700';
      ctx.beginPath();
      ctx.arc(toX(car.position.x), toY(car.position.z), car.isPlayer ? 4.5 : 3.0, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  private loop = () => {
    requestAnimationFrame(this.loop);
    const now = performance.now();
    const frameDt = Math.min(0.05, (now - this.lastFrameTime) / 1000);
    this.lastFrameTime = now;

    if (this.state === 'MENU' || this.state === 'GARAGE') {
      this.garageScene.update(frameDt, this.state === 'GARAGE');
      this.renderer.render(this.garageScene.scene, this.garageScene.camera);
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
