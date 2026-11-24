export interface CircuitDef {
  id: string;
  name: string;
  country: string;
  description: string;
  trackWidth: number;
  skyTop: number;
  skyHorizon: number;
  fogColor: number;
  sunColor: number;
  sunIntensity: number;
  ambientColor: number;
  groundColor: number;
  hasWater?: boolean;
  points: [number, number, number][]; // [x, y, z] closed loop control points
}

export const CIRCUITS: CircuitDef[] = [
  {
    id: 'veloce-gp',
    name: 'AUTODROMO VELOCE',
    country: 'ITALY',
    description: 'High-speed temple of velocity with long full-throttle straights, sweeping chicanes, and a fast final parabolic curve.',
    trackWidth: 16,
    skyTop: 0x1c5aa6,
    skyHorizon: 0x9ec9f2,
    fogColor: 0x9ec9f2,
    sunColor: 0xfff5dc,
    sunIntensity: 2.6,
    ambientColor: 0x6b82a8,
    groundColor: 0x2d5a27,
    points: [
      [0, 0, 0],
      [0, 0, 140],
      [12, 0, 240],
      [75, 0, 290],
      [155, 1, 275],
      [185, 2, 200],
      [145, 2, 130],
      [170, 1, 45],
      [235, 0, -25],
      [230, 0, -135],
      [155, 0, -205],
      [55, 0, -195],
      [-5, 0, -135],
    ],
  },
  {
    id: 'port-azzurro',
    name: 'PORT AZZURRO',
    country: 'MONACO',
    description: 'Tight golden-hour harbor street circuit demanding surgical braking, kerb precision, and fearless barrier commitment.',
    trackWidth: 14,
    skyTop: 0x2a2456,
    skyHorizon: 0xf08c55,
    fogColor: 0xd98057,
    sunColor: 0xffb36b,
    sunIntensity: 2.3,
    ambientColor: 0x7c6882,
    groundColor: 0x3b424d,
    hasWater: true,
    points: [
      [0, 0, 0],
      [0, 0, 115],
      [35, 3, 185],
      [110, 6, 195],
      [150, 5, 140],
      [115, 2, 75],
      [165, 0, 15],
      [180, 0, -85],
      [120, 0, -150],
      [45, 0, -125],
      [5, 0, -165],
      [-45, 0, -120],
      [-20, 0, -55],
    ],
  },
  {
    id: 'fujiyama-gp',
    name: 'FUJIYAMA TECHNICAL',
    country: 'JAPAN',
    description: 'Flowing technical Grand Prix with rhythmic S-curves, a heavy-braking hairpin, and high-downforce twilight sweepers.',
    trackWidth: 15.5,
    skyTop: 0x0d1b38,
    skyHorizon: 0x45689c,
    fogColor: 0x344e78,
    sunColor: 0xd8e6ff,
    sunIntensity: 2.0,
    ambientColor: 0x526b94,
    groundColor: 0x24472c,
    points: [
      [0, 0, 0],
      [0, 0, 130],
      [45, 1, 215],
      [120, 3, 225],
      [150, 4, 165],
      [105, 3, 105],
      [155, 2, 40],
      [215, 2, -20],
      [195, 1, -115],
      [115, 0, -175],
      [45, 0, -135],
      [-25, 0, -165],
      [-55, 0, -90],
    ],
  },
];
