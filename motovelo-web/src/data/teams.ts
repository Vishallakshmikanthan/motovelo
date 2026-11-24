export interface FormulaTeam {
  id: string;
  name: string;
  shortName: string;
  driverName: string;
  driverNumber: number;
  primary: number;
  secondary: number;
  accent: number;
  helmet: number;
  tireStripe: number;
  power: number;   // 0.94 .. 1.04
  aero: number;    // 0.94 .. 1.04
  grip: number;    // 0.94 .. 1.04
  agility: number; // 0.94 .. 1.04
}

export const TEAMS: FormulaTeam[] = [
  {
    id: 'scuderia-veloce',
    name: 'Scuderia Veloce',
    shortName: 'VEL',
    driverName: 'L. Moretti',
    driverNumber: 16,
    primary: 0xdc1428,
    secondary: 0x111318,
    accent: 0xffc800,
    helmet: 0xf4f4f8,
    tireStripe: 0xff2222,
    power: 1.03,
    aero: 0.99,
    grip: 1.01,
    agility: 1.00,
  },
  {
    id: 'apex-bull',
    name: 'Apex Corsa Racing',
    shortName: 'APX',
    driverName: 'M. Lindqvist',
    driverNumber: 1,
    primary: 0x101c44,
    secondary: 0x0b0e18,
    accent: 0xffb800,
    helmet: 0xffd000,
    tireStripe: 0xff2222,
    power: 1.02,
    aero: 1.04,
    grip: 1.02,
    agility: 1.03,
  },
  {
    id: 'silver-arrow',
    name: 'Silberpfeil GP',
    shortName: 'SLB',
    driverName: 'K. Richter',
    driverNumber: 44,
    primary: 0x181c24,
    secondary: 0xaeb6c2,
    accent: 0x00d2be,
    helmet: 0x00e5cc,
    tireStripe: 0xf5d000,
    power: 1.01,
    aero: 1.01,
    grip: 1.02,
    agility: 1.00,
  },
  {
    id: 'papaya-gt',
    name: 'Solaris Papaya',
    shortName: 'SOL',
    driverName: 'O. Vance',
    driverNumber: 4,
    primary: 0xff6e00,
    secondary: 0x12151c,
    accent: 0x00c2ff,
    helmet: 0xff8800,
    tireStripe: 0xff2222,
    power: 1.00,
    aero: 1.03,
    grip: 1.03,
    agility: 1.02,
  },
  {
    id: 'emerald-works',
    name: 'British Emerald F1',
    shortName: 'EMR',
    driverName: 'A. Sterling',
    driverNumber: 14,
    primary: 0x00594f,
    secondary: 0x0c1414,
    accent: 0xb6ff00,
    helmet: 0xc8ff00,
    tireStripe: 0xf5d000,
    power: 0.99,
    aero: 1.00,
    grip: 1.00,
    agility: 1.01,
  },
  {
    id: 'cobalt-alpine',
    name: 'Cobalt Aero Team',
    shortName: 'COB',
    driverName: 'E. Laurent',
    driverNumber: 10,
    primary: 0x0055cc,
    secondary: 0x111520,
    accent: 0xff3b9a,
    helmet: 0x2288ff,
    tireStripe: 0xffffff,
    power: 0.98,
    aero: 0.99,
    grip: 0.99,
    agility: 1.02,
  },
];

export const EXTRA_AI_DRIVERS = [
  { name: 'R. Takahashi', number: 22, teamIdx: 1 },
  { name: 'C. Mendoza', number: 55, teamIdx: 0 },
];

export const F1_POINTS = [25, 18, 15, 12, 10, 8, 6, 4];
