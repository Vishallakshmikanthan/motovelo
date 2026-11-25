import * as THREE from 'three';
import { CircuitDef } from '../data/circuits';
import { LoadedAssets } from './AssetLoader';

export interface TrackSample {
  pos: THREE.Vector3;
  tangent: THREE.Vector3;
  right: THREE.Vector3;
  s: number;
  t: number;
  curvature: number;
}

export interface TrackProjection {
  sampleIndex: number;
  s: number;
  t: number;
  lateralOffset: number;
  point: THREE.Vector3;
  tangent: THREE.Vector3;
  right: THREE.Vector3;
  y: number;
}

export interface BuiltTrack {
  group: THREE.Group;
  samples: TrackSample[];
  totalLength: number;
  trackWidth: number;
  halfWidth: number;
  startLightMats: THREE.MeshStandardMaterial[];
  tracksideCameras: THREE.Vector3[];
  projectToTrack: (x: number, z: number, hintIdx?: number) => TrackProjection;
  getSampleAtDistance: (s: number) => TrackSample;
}

function createAsphaltTexture(): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = 512;
  c.height = 512;
  const ctx = c.getContext('2d')!;
  ctx.fillStyle = '#2d3038';
  ctx.fillRect(0, 0, 512, 512);

  for (let i = 0; i < 16000; i++) {
    const x = Math.random() * 512;
    const y = Math.random() * 512;
    const v = 36 + Math.floor(Math.random() * 28);
    ctx.fillStyle = `rgb(${v},${v},${v + 2})`;
    ctx.fillRect(x, y, 2, 2);
  }

  const grad = ctx.createLinearGradient(0, 0, 512, 0);
  grad.addColorStop(0.0, 'rgba(0,0,0,0)');
  grad.addColorStop(0.28, 'rgba(10,10,12,0.28)');
  grad.addColorStop(0.5, 'rgba(12,12,15,0.34)');
  grad.addColorStop(0.72, 'rgba(10,10,12,0.28)');
  grad.addColorStop(1.0, 'rgba(0,0,0,0)');
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, 512, 512);

  ctx.fillStyle = '#eef2f6';
  ctx.fillRect(14, 0, 10, 512);
  ctx.fillRect(488, 0, 10, 512);

  const tex = new THREE.CanvasTexture(c);
  tex.wrapS = THREE.ClampToEdgeWrapping;
  tex.wrapT = THREE.RepeatWrapping;
  tex.anisotropy = 8;
  return tex;
}

function createKerbTexture(): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = 64;
  c.height = 256;
  const ctx = c.getContext('2d')!;
  ctx.fillStyle = '#d9281e';
  ctx.fillRect(0, 0, 64, 128);
  ctx.fillStyle = '#f2f4f8';
  ctx.fillRect(0, 128, 64, 128);
  const tex = new THREE.CanvasTexture(c);
  tex.wrapS = THREE.ClampToEdgeWrapping;
  tex.wrapT = THREE.RepeatWrapping;
  return tex;
}

function createStartLineTexture(): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = 256;
  c.height = 64;
  const ctx = c.getContext('2d')!;
  const sz = 16;
  for (let y = 0; y < 4; y++) {
    for (let x = 0; x < 16; x++) {
      ctx.fillStyle = (x + y) % 2 === 0 ? '#ffffff' : '#111111';
      ctx.fillRect(x * sz, y * sz, sz, sz);
    }
  }
  return new THREE.CanvasTexture(c);
}

function createChainFenceTexture(): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = 128;
  c.height = 128;
  const ctx = c.getContext('2d')!;
  ctx.clearRect(0, 0, 128, 128);
  ctx.strokeStyle = 'rgba(210, 218, 228, 0.52)';
  ctx.lineWidth = 2;
  for (let i = -128; i < 256; i += 16) {
    ctx.beginPath();
    ctx.moveTo(i, 0);
    ctx.lineTo(i + 128, 128);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(i + 128, 0);
    ctx.lineTo(i, 128);
    ctx.stroke();
  }
  ctx.fillStyle = 'rgba(130, 138, 148, 0.85)';
  ctx.fillRect(0, 0, 6, 128);
  const tex = new THREE.CanvasTexture(c);
  tex.wrapS = THREE.RepeatWrapping;
  tex.wrapT = THREE.RepeatWrapping;
  return tex;
}

function createChevronTexture(color: string): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = 128;
  c.height = 128;
  const ctx = c.getContext('2d')!;
  ctx.clearRect(0, 0, 128, 128);
  ctx.fillStyle = color;
  ctx.shadowColor = color;
  ctx.shadowBlur = 12;
  ctx.beginPath();
  ctx.moveTo(64, 18);
  ctx.lineTo(116, 78);
  ctx.lineTo(94, 96);
  ctx.lineTo(64, 58);
  ctx.lineTo(34, 96);
  ctx.lineTo(12, 78);
  ctx.closePath();
  ctx.fill();
  return new THREE.CanvasTexture(c);
}

function createBrakeBoardTexture(label: string): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = 128;
  c.height = 96;
  const ctx = c.getContext('2d')!;
  ctx.fillStyle = '#f8fafc';
  ctx.fillRect(0, 0, 128, 96);
  ctx.strokeStyle = '#111827';
  ctx.lineWidth = 8;
  ctx.strokeRect(4, 4, 120, 88);
  ctx.fillStyle = '#0f172a';
  ctx.font = '900 48px Arial';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(label, 64, 52);
  return new THREE.CanvasTexture(c);
}

function buildRibbonGeometry(
  samples: TrackSample[],
  leftOffset: number,
  rightOffset: number,
  yOffset: number,
  vRepeatPerMeter: number
): THREE.BufferGeometry {
  const n = samples.length;
  const positions: number[] = [];
  const uvs: number[] = [];
  const indices: number[] = [];

  for (let i = 0; i <= n; i++) {
    const smp = samples[i % n];
    const s = i === n ? samples[n - 1].s + samples[n - 1].pos.distanceTo(samples[0].pos) : smp.s;
    const lx = smp.pos.x + smp.right.x * leftOffset;
    const ly = smp.pos.y + yOffset;
    const lz = smp.pos.z + smp.right.z * leftOffset;

    const rx = smp.pos.x + smp.right.x * rightOffset;
    const ry = smp.pos.y + yOffset;
    const rz = smp.pos.z + smp.right.z * rightOffset;

    positions.push(lx, ly, lz, rx, ry, rz);
    uvs.push(0, s * vRepeatPerMeter, 1, s * vRepeatPerMeter);

    if (i < n) {
      const a = i * 2;
      const b = a + 1;
      const c = a + 2;
      const d = a + 3;
      indices.push(a, c, b, b, c, d);
    }
  }

  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geo.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
  geo.setIndex(indices);
  geo.computeVertexNormals();
  return geo;
}

function buildWallGeometry(
  samples: TrackSample[],
  offset: number,
  yBase: number,
  height: number,
  flipSide: boolean,
  uRepeatPerMeter = 0.08
): THREE.BufferGeometry {
  const n = samples.length;
  const positions: number[] = [];
  const uvs: number[] = [];
  const indices: number[] = [];

  for (let i = 0; i <= n; i++) {
    const smp = samples[i % n];
    const s = i === n ? samples[n - 1].s + 2 : smp.s;
    const x = smp.pos.x + smp.right.x * offset;
    const y = smp.pos.y + yBase;
    const z = smp.pos.z + smp.right.z * offset;

    positions.push(x, y, z, x, y + height, z);
    uvs.push(s * uRepeatPerMeter, 0, s * uRepeatPerMeter, 1);

    if (i < n) {
      const a = i * 2;
      const b = a + 1;
      const c = a + 2;
      const d = a + 3;
      if (flipSide) {
        indices.push(a, b, c, b, d, c);
      } else {
        indices.push(a, c, b, b, c, d);
      }
    }
  }

  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geo.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
  geo.setIndex(indices);
  geo.computeVertexNormals();
  return geo;
}

export function buildCircuit(def: CircuitDef, assets: LoadedAssets): BuiltTrack {
  const group = new THREE.Group();

  const controlPts = def.points.map(([x, y, z]) => new THREE.Vector3(x, y, z));
  const curve = new THREE.CatmullRomCurve3(controlPts, true, 'catmullrom', 0.35);

  // High-resolution 960 samples for buttery-smooth 3km-4.3km real F1 GeoJSON circuits!
  const NUM_SAMPLES = 960;
  const rawPts = curve.getSpacedPoints(NUM_SAMPLES);
  rawPts.pop();

  const up = new THREE.Vector3(0, 1, 0);
  const samples: TrackSample[] = [];
  let cumDist = 0;

  for (let i = 0; i < NUM_SAMPLES; i++) {
    const p = rawPts[i];
    const pNext = rawPts[(i + 1) % NUM_SAMPLES];
    const pPrev = rawPts[(i - 1 + NUM_SAMPLES) % NUM_SAMPLES];
    if (i > 0) cumDist += p.distanceTo(pPrev);

    const tangent = new THREE.Vector3().subVectors(pNext, pPrev).normalize();
    const right = new THREE.Vector3().crossVectors(up, tangent).normalize();

    const tPrev = new THREE.Vector3().subVectors(p, pPrev).normalize();
    const tNext = new THREE.Vector3().subVectors(pNext, p).normalize();
    const curvature = tPrev.angleTo(tNext) / Math.max(0.5, p.distanceTo(pPrev));

    samples.push({
      pos: p.clone(),
      tangent,
      right,
      s: cumDist,
      t: i / NUM_SAMPLES,
      curvature,
    });
  }
  const totalLength = cumDist + rawPts[NUM_SAMPLES - 1].distanceTo(rawPts[0]);
  const halfW = def.trackWidth / 2;

  // Compute circuit center for ground plane placement
  let cx = 0, cz = 0;
  for (const s of samples) { cx += s.pos.x; cz += s.pos.z; }
  cx /= NUM_SAMPLES;
  cz /= NUM_SAMPLES;

  // 1. Large Ground Terrain Plane centered on circuit
  const groundMat = new THREE.MeshStandardMaterial({
    color: def.groundColor,
    roughness: 0.95,
    metalness: 0.02,
  });
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(4800, 4800), groundMat);
  ground.rotation.x = -Math.PI / 2;
  ground.position.set(cx, -0.45, cz);
  ground.receiveShadow = true;
  group.add(ground);

  if (def.hasWater) {
    const waterMat = new THREE.MeshStandardMaterial({
      color: 0x0d4f7a,
      roughness: 0.18,
      metalness: 0.55,
    });
    const water = new THREE.Mesh(new THREE.PlaneGeometry(2400, 2400), waterMat);
    water.rotation.x = -Math.PI / 2;
    water.position.set(cx + 520, -0.28, cz + 140);
    group.add(water);
  }

  // 2. Asphalt Track Ribbon
  const asphaltTex = createAsphaltTexture();
  const asphaltMat = new THREE.MeshStandardMaterial({
    map: asphaltTex,
    roughness: 0.72,
    metalness: 0.08,
  });
  const roadMesh = new THREE.Mesh(buildRibbonGeometry(samples, -halfW, halfW, 0.02, 0.08), asphaltMat);
  roadMesh.receiveShadow = true;
  group.add(roadMesh);

  // 3. Red & White Formula Kerbs
  const kerbTex = createKerbTexture();
  const kerbMat = new THREE.MeshStandardMaterial({ map: kerbTex, roughness: 0.6 });
  const kerbL = new THREE.Mesh(buildRibbonGeometry(samples, -halfW - 1.45, -halfW + 0.05, 0.05, 0.22), kerbMat);
  const kerbR = new THREE.Mesh(buildRibbonGeometry(samples, halfW - 0.05, halfW + 1.45, 0.05, 0.22), kerbMat);
  kerbL.receiveShadow = true;
  kerbR.receiveShadow = true;
  group.add(kerbL, kerbR);

  // 4. Wide Tarmac Runoff Aprons
  const runoffMat = new THREE.MeshStandardMaterial({
    color: def.runoffColor ?? 0x454a52,
    roughness: 0.88,
  });
  const runoffL = new THREE.Mesh(buildRibbonGeometry(samples, -halfW - 6.2, -halfW - 1.4, 0.0, 0.05), runoffMat);
  const runoffR = new THREE.Mesh(buildRibbonGeometry(samples, halfW + 1.4, halfW + 6.2, 0.0, 0.05), runoffMat);
  group.add(runoffL, runoffR);

  // 5. Real 2026 Sponsor Boards from Download.zip
  const aramcoTex = assets.sponsorTextures['aramco'];
  const f1Tex = assets.sponsorTextures['f1madrid'] || assets.sponsorTextures['aws'];
  const barrierMatL = new THREE.MeshStandardMaterial({ map: aramcoTex, roughness: 0.42, metalness: 0.12, side: THREE.DoubleSide });
  const barrierMatR = new THREE.MeshStandardMaterial({ map: f1Tex, roughness: 0.42, metalness: 0.12, side: THREE.DoubleSide });
  const wallL = new THREE.Mesh(buildWallGeometry(samples, -halfW - 5.9, 0, 1.45, false, 0.10), barrierMatL);
  const wallR = new THREE.Mesh(buildWallGeometry(samples, halfW + 5.9, 0, 1.45, true, 0.10), barrierMatR);
  group.add(wallL, wallR);

  // Chain-Link Catch Fence above barriers
  const fenceTex = createChainFenceTexture();
  const fenceMat = new THREE.MeshBasicMaterial({ map: fenceTex, transparent: true, opacity: 0.72, side: THREE.DoubleSide, depthWrite: false });
  const fenceL = new THREE.Mesh(buildWallGeometry(samples, -halfW - 6.0, 1.45, 2.7, false, 0.24), fenceMat);
  const fenceR = new THREE.Mesh(buildWallGeometry(samples, halfW + 6.0, 1.45, 2.7, true, 0.24), fenceMat);
  group.add(fenceL, fenceR);

  // 6. Dynamic 3D Racing Line Chevrons + 150m / 100m / 50m F1 Braking Marker Boards!
  const chevGeo = new THREE.PlaneGeometry(2.8, 2.8);
  const chevGreen = new THREE.MeshBasicMaterial({ map: createChevronTexture('#18ff4a'), transparent: true, opacity: 0.82, depthWrite: false });
  const chevYellow = new THREE.MeshBasicMaterial({ map: createChevronTexture('#ffe600'), transparent: true, opacity: 0.88, depthWrite: false });
  const chevRed = new THREE.MeshBasicMaterial({ map: createChevronTexture('#ff2424'), transparent: true, opacity: 0.92, depthWrite: false });

  const boardGeo = new THREE.BoxGeometry(1.6, 1.1, 0.15);
  const mat150 = new THREE.MeshStandardMaterial({ map: createBrakeBoardTexture('150') });
  const mat100 = new THREE.MeshStandardMaterial({ map: createBrakeBoardTexture('100') });
  const mat50 = new THREE.MeshStandardMaterial({ map: createBrakeBoardTexture('50') });

  let lastBrakeZoneS = -300;
  for (let i = 6; i < NUM_SAMPLES; i += 6) {
    const smp = samples[i];
    let maxAheadCurv = 0;
    for (let k = 0; k < 22; k++) {
      maxAheadCurv = Math.max(maxAheadCurv, samples[(i + k) % NUM_SAMPLES].curvature);
    }
    const mat = maxAheadCurv > 0.022 ? chevRed : maxAheadCurv > 0.011 ? chevYellow : chevGreen;
    const chev = new THREE.Mesh(chevGeo, mat);
    chev.rotation.x = -Math.PI / 2;
    const yaw = Math.atan2(smp.tangent.x, smp.tangent.z);
    chev.rotation.z = -yaw;
    chev.position.set(smp.pos.x, smp.pos.y + 0.055, smp.pos.z);
    group.add(chev);

    // Place 150 / 100 / 50m braking marker boards before major heavy-braking corners!
    if (smp.curvature > 0.024 && smp.s - lastBrakeZoneS > 240) {
      lastBrakeZoneS = smp.s;
      for (const [distBack, bMat] of [[110, mat150], [75, mat100], [40, mat50]] as [number, THREE.Material][]) {
        const bs = getSampleAt(samples, totalLength, smp.s - distBack);
        const byaw = Math.atan2(bs.tangent.x, bs.tangent.z);
        const board = new THREE.Mesh(boardGeo, bMat);
        board.position.copy(bs.pos).addScaledVector(bs.right, -(halfW + 4.4));
        board.position.y += 1.0;
        board.rotation.y = byaw + Math.PI;
        group.add(board);
      }
    }
  }

  // 7. Checkered Start/Finish Line
  const startSmp = samples[0];
  const startYaw = Math.atan2(startSmp.tangent.x, startSmp.tangent.z);
  const lineMesh = new THREE.Mesh(
    new THREE.PlaneGeometry(def.trackWidth - 0.8, 3.0),
    new THREE.MeshStandardMaterial({ map: createStartLineTexture(), roughness: 0.5 })
  );
  lineMesh.rotation.x = -Math.PI / 2;
  lineMesh.rotation.z = -startYaw;
  lineMesh.position.set(startSmp.pos.x, startSmp.pos.y + 0.06, startSmp.pos.z);
  group.add(lineMesh);

  // 8. Start Gantry with Louis Vuitton Banner + 5 Dynamic Light Pods
  const gantry = assets.startGantryProto.clone(true);
  const scaleGantry = (def.trackWidth + 5.8) / 21.6;
  gantry.scale.set(scaleGantry, 1, 1);
  gantry.position.copy(startSmp.pos);
  gantry.rotation.y = startYaw;
  group.add(gantry);

  const lvTex = assets.sponsorTextures['louisvuitton'];
  const startLightMats: THREE.MeshStandardMaterial[] = new Array(5);
  gantry.traverse((obj) => {
    if ((obj as THREE.Mesh).isMesh) {
      const m = obj as THREE.Mesh;
      const mat = m.material as THREE.MeshStandardMaterial;
      if (mat && mat.name.startsWith('GantryBanner') && lvTex) {
        const cloned = mat.clone();
        cloned.map = lvTex;
        cloned.color.setHex(0xffffff);
        m.material = cloned;
      } else if (mat && mat.name.startsWith('StartLight_')) {
        const idx = parseInt(mat.name.split('_')[1], 10);
        if (!Number.isNaN(idx) && idx >= 0 && idx < 5) {
          if (!startLightMats[idx]) {
            const cloned = mat.clone();
            cloned.color.setHex(0x280508);
            cloned.emissive.setHex(0x000000);
            cloned.emissiveIntensity = 0;
            startLightMats[idx] = cloned;
          }
          m.material = startLightMats[idx];
        }
      }
    }
  });

  // 9. Overhead Sponsor Bridges around the circuit
  const bridgeFracs = [0.18, 0.38, 0.58, 0.78];
  const bridgeSponsors = ['louisvuitton', 'tagheuer', 'aws', 'aramco'];
  bridgeFracs.forEach((frac, idx) => {
    const smp = samples[Math.floor(NUM_SAMPLES * frac) % NUM_SAMPLES];
    const yaw = Math.atan2(smp.tangent.x, smp.tangent.z);
    const bridge = new THREE.Group();
    const span = def.trackWidth + 11.8;
    const pillarGeo = new THREE.BoxGeometry(1.1, 7.6, 1.4);
    const pillarMat = new THREE.MeshStandardMaterial({ color: 0xc8ccd2, roughness: 0.6 });
    const pL = new THREE.Mesh(pillarGeo, pillarMat);
    pL.position.set(-span / 2, 3.8, 0);
    const pR = new THREE.Mesh(pillarGeo, pillarMat);
    pR.position.set(span / 2, 3.8, 0);
    const beamMat = new THREE.MeshStandardMaterial({
      map: assets.sponsorTextures[bridgeSponsors[idx % bridgeSponsors.length]],
      roughness: 0.35,
    });
    const beam = new THREE.Mesh(new THREE.BoxGeometry(span + 1.2, 2.2, 1.8), beamMat);
    beam.position.set(0, 7.4, 0);
    bridge.add(pL, pR, beam);
    bridge.position.copy(smp.pos);
    bridge.rotation.y = yaw;
    group.add(bridge);
  });

  // 10. Pit Complex along main straight
  const pitSmp = samples[Math.floor(NUM_SAMPLES * 0.025)];
  const pitYaw = Math.atan2(pitSmp.tangent.x, pitSmp.tangent.z);
  const pit = assets.pitBuildingProto.clone(true);
  pit.position.copy(pitSmp.pos).addScaledVector(pitSmp.right, halfW + 15.0);
  pit.rotation.y = pitYaw - Math.PI / 2;
  group.add(pit);

  // 11. Grandstands
  const standFractions = [0.015, 0.06, 0.20, 0.36, 0.54, 0.72, 0.88, 0.96];
  standFractions.forEach((frac, idx) => {
    const smp = samples[Math.floor(NUM_SAMPLES * frac) % NUM_SAMPLES];
    const yaw = Math.atan2(smp.tangent.x, smp.tangent.z);
    const side = idx % 2 === 0 ? -1 : 1;
    const gs = assets.grandstandProto.clone(true);
    gs.position.copy(smp.pos).addScaledVector(smp.right, side * (halfW + 16.0));
    gs.rotation.y = yaw + (side < 0 ? Math.PI / 2 : -Math.PI / 2);
    group.add(gs);
  });

  // 12. Trackside Trees for high-speed peripheral cues
  const trunkGeo = new THREE.CylinderGeometry(0.35, 0.5, 4.8, 6);
  const crownGeo = new THREE.ConeGeometry(3.0, 8.2, 7);
  const trunkMat = new THREE.MeshStandardMaterial({ color: 0x4d3423, roughness: 0.9 });
  const crownMat = new THREE.MeshStandardMaterial({ color: 0x1d4d26, roughness: 0.85 });

  for (let i = 0; i < NUM_SAMPLES; i += 12) {
    if (i < 55 || i > NUM_SAMPLES - 40) continue;
    const smp = samples[i];
    for (const side of [-1, 1]) {
      const dist = halfW + 22 + ((i * 17) % 28);
      const pos = smp.pos.clone().addScaledVector(smp.right, side * dist);
      const tree = new THREE.Group();
      const trunk = new THREE.Mesh(trunkGeo, trunkMat);
      trunk.position.y = 2.4;
      const crown = new THREE.Mesh(crownGeo, crownMat);
      crown.position.y = 7.6;
      tree.add(trunk, crown);
      tree.position.copy(pos);
      tree.scale.setScalar(0.9 + ((i * 7) % 10) * 0.06);
      group.add(tree);
    }
  }

  // 13. Trackside Broadcast Cameras
  const tracksideCameras: THREE.Vector3[] = [];
  for (let i = 0; i < 18; i++) {
    const smp = samples[Math.floor((i / 18) * NUM_SAMPLES)];
    const side = i % 2 === 0 ? 1 : -1;
    const camPos = smp.pos.clone().addScaledVector(smp.right, side * (halfW + 8.5));
    camPos.y += 4.5;
    tracksideCameras.push(camPos);
  }

  const projectToTrack = (x: number, z: number, hintIdx?: number): TrackProjection => {
    let bestIdx = 0;
    let bestD2 = Infinity;

    if (hintIdx !== undefined) {
      const window = 44;
      for (let k = -window; k <= window; k++) {
        const idx = (hintIdx + k + NUM_SAMPLES) % NUM_SAMPLES;
        const p = samples[idx].pos;
        const dx = x - p.x;
        const dz = z - p.z;
        const d2 = dx * dx + dz * dz;
        if (d2 < bestD2) {
          bestD2 = d2;
          bestIdx = idx;
        }
      }
    }

    if (bestD2 > 1800) {
      for (let i = 0; i < NUM_SAMPLES; i++) {
        const p = samples[i].pos;
        const dx = x - p.x;
        const dz = z - p.z;
        const d2 = dx * dx + dz * dz;
        if (d2 < bestD2) {
          bestD2 = d2;
          bestIdx = i;
        }
      }
    }

    const smp = samples[bestIdx];
    const dx = x - smp.pos.x;
    const dz = z - smp.pos.z;
    const lateralOffset = dx * smp.right.x + dz * smp.right.z;

    return {
      sampleIndex: bestIdx,
      s: smp.s,
      t: smp.t,
      lateralOffset,
      point: smp.pos,
      tangent: smp.tangent,
      right: smp.right,
      y: smp.pos.y,
    };
  };

  const getSampleAtDistance = (s: number): TrackSample => getSampleAt(samples, totalLength, s);

  return {
    group,
    samples,
    totalLength,
    trackWidth: def.trackWidth,
    halfWidth: halfW,
    startLightMats,
    tracksideCameras,
    projectToTrack,
    getSampleAtDistance,
  };
}

function getSampleAt(samples: TrackSample[], totalLength: number, s: number): TrackSample {
  const n = samples.length;
  const wrapped = ((s % totalLength) + totalLength) % totalLength;
  const idx = Math.floor((wrapped / totalLength) * n) % n;
  return samples[idx];
}
