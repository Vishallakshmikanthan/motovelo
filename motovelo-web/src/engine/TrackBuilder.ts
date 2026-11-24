import * as THREE from 'three';
import { CircuitDef } from '../data/circuits';
import { LoadedAssets } from './AssetLoader';

export interface TrackSample {
  pos: THREE.Vector3;
  tangent: THREE.Vector3;
  right: THREE.Vector3;
  s: number;        // cumulative distance along track (m)
  t: number;        // normalized 0..1
  curvature: number;
}

export interface TrackProjection {
  sampleIndex: number;
  s: number;
  t: number;
  lateralOffset: number; // +right, -left
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
  ctx.fillStyle = '#25272c';
  ctx.fillRect(0, 0, 512, 512);

  // Fine asphalt grain
  for (let i = 0; i < 14000; i++) {
    const x = Math.random() * 512;
    const y = Math.random() * 512;
    const v = 28 + Math.floor(Math.random() * 26);
    ctx.fillStyle = `rgb(${v},${v},${v + 2})`;
    ctx.fillRect(x, y, 2, 2);
  }

  // Dark rubbered-in racing groove
  const grad = ctx.createLinearGradient(0, 0, 512, 0);
  grad.addColorStop(0.0, 'rgba(0,0,0,0)');
  grad.addColorStop(0.28, 'rgba(10,10,12,0.28)');
  grad.addColorStop(0.5, 'rgba(12,12,15,0.34)');
  grad.addColorStop(0.72, 'rgba(10,10,12,0.28)');
  grad.addColorStop(1.0, 'rgba(0,0,0,0)');
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, 512, 512);

  // Crisp white track limit lines
  ctx.fillStyle = '#e8ecf2';
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
  ctx.fillStyle = '#e01b24';
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
  const tex = new THREE.CanvasTexture(c);
  return tex;
}

function createBarrierTexture(): THREE.CanvasTexture {
  const c = document.createElement('canvas');
  c.width = 512;
  c.height = 64;
  const ctx = c.getContext('2d')!;
  ctx.fillStyle = '#14171f';
  ctx.fillRect(0, 0, 512, 64);
  ctx.fillStyle = '#e6192e';
  ctx.fillRect(0, 0, 512, 10);
  ctx.fillStyle = '#f5b700';
  ctx.fillRect(0, 56, 512, 8);
  ctx.fillStyle = '#ffffff';
  ctx.font = 'italic 900 28px Arial';
  ctx.fillText('MOTOVELO  //  GP26', 24, 42);
  ctx.fillText('MOTOVELO  //  GP26', 310, 42);
  const tex = new THREE.CanvasTexture(c);
  tex.wrapS = THREE.RepeatWrapping;
  tex.wrapT = THREE.ClampToEdgeWrapping;
  return tex;
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
  height: number,
  flipSide: boolean
): THREE.BufferGeometry {
  const n = samples.length;
  const positions: number[] = [];
  const uvs: number[] = [];
  const indices: number[] = [];

  for (let i = 0; i <= n; i++) {
    const smp = samples[i % n];
    const s = i === n ? samples[n - 1].s + 2 : smp.s;
    const x = smp.pos.x + smp.right.x * offset;
    const y = smp.pos.y;
    const z = smp.pos.z + smp.right.z * offset;

    positions.push(x, y, z, x, y + height, z);
    uvs.push(s * 0.08, 0, s * 0.08, 1);

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
  const curve = new THREE.CatmullRomCurve3(controlPts, true, 'catmullrom', 0.45);

  const NUM_SAMPLES = 420;
  const rawPts = curve.getSpacedPoints(NUM_SAMPLES);
  rawPts.pop(); // remove duplicate end point

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

  // 1. Ground Terrain Plane
  const groundMat = new THREE.MeshStandardMaterial({
    color: def.groundColor,
    roughness: 0.95,
    metalness: 0.02,
  });
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(1800, 1800), groundMat);
  ground.rotation.x = -Math.PI / 2;
  ground.position.y = -0.35;
  ground.receiveShadow = true;
  group.add(ground);

  // Optional Harbor Water for Port Azzurro
  if (def.hasWater) {
    const waterMat = new THREE.MeshStandardMaterial({
      color: 0x0d4f7a,
      roughness: 0.18,
      metalness: 0.55,
    });
    const water = new THREE.Mesh(new THREE.PlaneGeometry(900, 900), waterMat);
    water.rotation.x = -Math.PI / 2;
    water.position.set(280, -0.22, 60);
    group.add(water);
  }

  // 2. Asphalt Track Ribbon
  const asphaltTex = createAsphaltTexture();
  const asphaltMat = new THREE.MeshStandardMaterial({
    map: asphaltTex,
    roughness: 0.72,
    metalness: 0.08,
  });
  const roadGeo = buildRibbonGeometry(samples, -halfW, halfW, 0.02, 0.08);
  const roadMesh = new THREE.Mesh(roadGeo, asphaltMat);
  roadMesh.receiveShadow = true;
  group.add(roadMesh);

  // 3. Red & White Formula Kerbs (Left & Right)
  const kerbTex = createKerbTexture();
  const kerbMat = new THREE.MeshStandardMaterial({ map: kerbTex, roughness: 0.6 });
  const kerbL = new THREE.Mesh(buildRibbonGeometry(samples, -halfW - 1.35, -halfW + 0.05, 0.05, 0.22), kerbMat);
  const kerbR = new THREE.Mesh(buildRibbonGeometry(samples, halfW - 0.05, halfW + 1.35, 0.05, 0.22), kerbMat);
  kerbL.receiveShadow = true;
  kerbR.receiveShadow = true;
  group.add(kerbL, kerbR);

  // 4. Runoff Aprons & MOTOVELO Safety Barriers
  const runoffMat = new THREE.MeshStandardMaterial({ color: 0x3b4048, roughness: 0.9 });
  const runoffL = new THREE.Mesh(buildRibbonGeometry(samples, -halfW - 5.2, -halfW - 1.3, 0.0, 0.05), runoffMat);
  const runoffR = new THREE.Mesh(buildRibbonGeometry(samples, halfW + 1.3, halfW + 5.2, 0.0, 0.05), runoffMat);
  group.add(runoffL, runoffR);

  const barrierTex = createBarrierTexture();
  const barrierMat = new THREE.MeshStandardMaterial({
    map: barrierTex,
    roughness: 0.45,
    metalness: 0.2,
    side: THREE.DoubleSide,
  });
  const wallL = new THREE.Mesh(buildWallGeometry(samples, -halfW - 5.0, 1.35, false), barrierMat);
  const wallR = new THREE.Mesh(buildWallGeometry(samples, halfW + 5.0, 1.35, true), barrierMat);
  wallL.castShadow = true;
  wallR.castShadow = true;
  group.add(wallL, wallR);

  // 5. Checkered Start/Finish Line at sample[0]
  const startSmp = samples[0];
  const startYaw = Math.atan2(startSmp.tangent.x, startSmp.tangent.z);
  const lineMesh = new THREE.Mesh(
    new THREE.PlaneGeometry(def.trackWidth - 0.8, 2.8),
    new THREE.MeshStandardMaterial({ map: createStartLineTexture(), roughness: 0.5 })
  );
  lineMesh.rotation.x = -Math.PI / 2;
  lineMesh.rotation.z = -startYaw;
  lineMesh.position.set(startSmp.pos.x, startSmp.pos.y + 0.06, startSmp.pos.z);
  group.add(lineMesh);

  // 6. Blender Start Gantry + Extract 5 Dynamic Light Materials
  const gantry = assets.startGantryProto.clone(true);
  const scaleGantry = (def.trackWidth + 5.5) / 21.6;
  gantry.scale.set(scaleGantry, 1, 1);
  gantry.position.copy(startSmp.pos);
  gantry.rotation.y = startYaw;
  group.add(gantry);

  const startLightMats: THREE.MeshStandardMaterial[] = new Array(5);
  gantry.traverse((obj) => {
    if ((obj as THREE.Mesh).isMesh) {
      const m = obj as THREE.Mesh;
      const mat = m.material as THREE.MeshStandardMaterial;
      if (mat && mat.name.startsWith('StartLight_')) {
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

  // 7. Blender Pit Building alongside main straight
  const pitSmp = samples[Math.floor(NUM_SAMPLES * 0.035)];
  const pitYaw = Math.atan2(pitSmp.tangent.x, pitSmp.tangent.z);
  const pit = assets.pitBuildingProto.clone(true);
  pit.position.copy(pitSmp.pos).addScaledVector(pitSmp.right, halfW + 13.5);
  pit.rotation.y = pitYaw - Math.PI / 2;
  group.add(pit);

  // 8. Blender Grandstands around key sections
  const standFractions = [0.02, 0.09, 0.26, 0.52, 0.78, 0.94];
  standFractions.forEach((frac, idx) => {
    const smp = samples[Math.floor(NUM_SAMPLES * frac) % NUM_SAMPLES];
    const yaw = Math.atan2(smp.tangent.x, smp.tangent.z);
    const side = idx % 2 === 0 ? -1 : 1;
    const gs = assets.grandstandProto.clone(true);
    gs.position.copy(smp.pos).addScaledVector(smp.right, side * (halfW + 14.0));
    gs.rotation.y = yaw + (side < 0 ? Math.PI / 2 : -Math.PI / 2);
    group.add(gs);
  });

  // 9. Procedural Trees / Stadium Light Towers for rich speed cues
  const trunkGeo = new THREE.CylinderGeometry(0.35, 0.5, 4.5, 6);
  const crownGeo = new THREE.ConeGeometry(2.8, 7.5, 7);
  const trunkMat = new THREE.MeshStandardMaterial({ color: 0x4d3423, roughness: 0.9 });
  const crownMat = new THREE.MeshStandardMaterial({ color: 0x1d4d26, roughness: 0.85 });

  for (let i = 0; i < NUM_SAMPLES; i += 7) {
    const smp = samples[i];
    for (const side of [-1, 1]) {
      const dist = halfW + 18 + ((i * 13) % 24);
      const pos = smp.pos.clone().addScaledVector(smp.right, side * dist);
      // Avoid overlapping pit/grandstands near main straight
      if (i < 48 || i > NUM_SAMPLES - 30) continue;
      const tree = new THREE.Group();
      const trunk = new THREE.Mesh(trunkGeo, trunkMat);
      trunk.position.y = 2.25;
      const crown = new THREE.Mesh(crownGeo, crownMat);
      crown.position.y = 7.2;
      tree.add(trunk, crown);
      tree.position.copy(pos);
      const sc = 0.85 + ((i * 7) % 10) * 0.05;
      tree.scale.setScalar(sc);
      group.add(tree);
    }
  }

  // 10. Trackside TV Broadcast Cameras
  const tracksideCameras: THREE.Vector3[] = [];
  for (let i = 0; i < 12; i++) {
    const smp = samples[Math.floor((i / 12) * NUM_SAMPLES)];
    const side = i % 2 === 0 ? 1 : -1;
    const camPos = smp.pos.clone().addScaledVector(smp.right, side * (halfW + 7.5));
    camPos.y += 4.2;
    tracksideCameras.push(camPos);
  }

  // Fast projection helper
  const projectToTrack = (x: number, z: number, hintIdx?: number): TrackProjection => {
    let bestIdx = 0;
    let bestD2 = Infinity;

    if (hintIdx !== undefined) {
      const window = 28;
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

    // Fallback full scan if far from hint
    if (bestD2 > 1400) {
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

  const getSampleAtDistance = (s: number): TrackSample => {
    const wrapped = ((s % totalLength) + totalLength) % totalLength;
    const idx = Math.floor((wrapped / totalLength) * NUM_SAMPLES) % NUM_SAMPLES;
    return samples[idx];
  };

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
