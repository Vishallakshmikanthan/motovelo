export interface DriverEntry {
  id: string;
  name: string;
  code: string;
  number: number;
  country: string;
  flag: string;
  teamId: string;
  portrait: string;
  cutout: string;
  helmetId: string;
  suitId: string;
}

export interface FormulaTeam {
  id: string;
  name: string;
  shortName: string;
  driverName: string;
  driverCode: string;
  driverNumber: number;
  primary: number;
  secondary: number;
  accent: number;
  helmet: number;
  tireStripe: number;
  liveryTex: string;
  logoTex: string;
  suitTex: string;
  power: number;   // 0.96 .. 1.04
  aero: number;    // 0.96 .. 1.04
  grip: number;    // 0.96 .. 1.04
  agility: number; // 0.96 .. 1.04
}

export interface TyreCompound {
  id: 'soft' | 'mid' | 'hard' | 'inter' | 'wet';
  label: string;
  short: string;
  color: string;
  hex: number;
  gripMult: number;
  wearRate: number;
  sets: number;
  tex: string;
}

export const TYRE_COMPOUNDS: TyreCompound[] = [
  { id: 'soft',  label: 'SOFT',  short: 'S', color: '#ff2432', hex: 0xff2432, gripMult: 1.06, wearRate: 1.45, sets: 3, tex: './textures/tyres/soft.png' },
  { id: 'mid',   label: 'MID',   short: 'M', color: '#f5cc16', hex: 0xf5cc16, gripMult: 1.01, wearRate: 1.00, sets: 2, tex: './textures/tyres/mid.png' },
  { id: 'hard',  label: 'HARD',  short: 'H', color: '#e8ecf2', hex: 0xe8ecf2, gripMult: 0.97, wearRate: 0.65, sets: 2, tex: './textures/tyres/hard.png' },
  { id: 'inter', label: 'INTER', short: 'I', color: '#22d44c', hex: 0x22d44c, gripMult: 0.93, wearRate: 0.85, sets: 4, tex: './textures/tyres/inter.png' },
  { id: 'wet',   label: 'WET',   short: 'W', color: '#1b8cff', hex: 0x1b8cff, gripMult: 0.89, wearRate: 0.75, sets: 3, tex: './textures/tyres/wet.png' },
];

export const HELMET_LIST = [
  { id: 'verstappen', label: 'Verstappen 2026', tex: './textures/helmets/verstappen.webp' },
  { id: 'verstappen_cota', label: 'Verstappen USA', tex: './textures/helmets/verstappen_cota.webp' },
  { id: 'hadjar', label: 'Hadjar RB26', tex: './textures/helmets/hadjar.webp' },
  { id: 'leclerc', label: 'Leclerc #16', tex: './textures/helmets/leclerc.webp' },
  { id: 'hamilton', label: 'Hamilton Gold', tex: './textures/helmets/hamilton.webp' },
  { id: 'russell', label: 'Russell #63', tex: './textures/helmets/russell.webp' },
  { id: 'antonelli', label: 'Antonelli #12', tex: './textures/helmets/antonelli.webp' },
  { id: 'norris', label: 'Norris #1', tex: './textures/helmets/norris.webp' },
  { id: 'piastri', label: 'Piastri #81', tex: './textures/helmets/piastri.webp' },
  { id: 'alonso', label: 'Alonso SG26', tex: './textures/helmets/alonso.webp' },
  { id: 'stroll', label: 'Stroll #18', tex: './textures/helmets/stroll.webp' },
  { id: 'williams', label: 'Williams 2026', tex: './textures/helmets/williams.webp' },
  { id: 'alpine', label: 'Alpine 2026', tex: './textures/helmets/alpine.webp' },
  { id: 'haas', label: 'Haas 2026', tex: './textures/helmets/haas.webp' },
  { id: 'vcarb', label: 'VCARB 2026', tex: './textures/helmets/vcarb.webp' },
];

export const SUIT_LIST = [
  { id: 'redbull', label: 'Red Bull Austin', tex: './textures/suits/redbull.webp' },
  { id: 'ferrari', label: 'Ferrari SF26', tex: './textures/suits/ferrari.webp' },
  { id: 'mercedes', label: 'Mercedes Petronas', tex: './textures/suits/mercedes.webp' },
  { id: 'mclaren', label: 'McLaren Papaya', tex: './textures/suits/mclaren.webp' },
  { id: 'astonmartin', label: 'Aston Martin', tex: './textures/suits/astonmartin.webp' },
  { id: 'alpine', label: 'Alpine BWT', tex: './textures/suits/alpine.webp' },
  { id: 'williams', label: 'Williams Atlassian', tex: './textures/suits/williams.webp' },
  { id: 'vcarb', label: 'VCARB White', tex: './textures/suits/vcarb.webp' },
  { id: 'audi', label: 'Audi Revolut', tex: './textures/suits/audi.webp' },
  { id: 'haas', label: 'Haas TGR', tex: './textures/suits/haas.webp' },
  { id: 'cadillac', label: 'Cadillac TWG', tex: './textures/suits/cadillac.webp' },
];

export const TEAMS: FormulaTeam[] = [
  {
    id: 'redbull',
    name: 'Redbull Racing',
    shortName: 'RBR',
    driverName: 'Max Verstappen',
    driverCode: 'MAX',
    driverNumber: 3,
    primary: 0x0f1d45,
    secondary: 0x090d1a,
    accent: 0xffc400,
    helmet: 0xe6192e,
    tireStripe: 0xff2432,
    liveryTex: './textures/liveries/redbull.webp',
    logoTex: './textures/logos/redbull.png',
    suitTex: './textures/suits/redbull.webp',
    power: 1.04,
    aero: 1.04,
    grip: 1.03,
    agility: 1.03,
  },
  {
    id: 'ferrari',
    name: 'Scuderia Ferrari',
    shortName: 'FER',
    driverName: 'Charles Leclerc',
    driverCode: 'LEC',
    driverNumber: 16,
    primary: 0xdc1428,
    secondary: 0x111318,
    accent: 0xffd000,
    helmet: 0xdc1428,
    tireStripe: 0xff2432,
    liveryTex: './textures/liveries/ferrari.webp',
    logoTex: './textures/logos/ferrari.png',
    suitTex: './textures/suits/ferrari.webp',
    power: 1.04,
    aero: 1.02,
    grip: 1.02,
    agility: 1.02,
  },
  {
    id: 'mclaren',
    name: 'McLaren Racing',
    shortName: 'MCL',
    driverName: 'Oscar Piastri',
    driverCode: 'PIA',
    driverNumber: 81,
    primary: 0xff7800,
    secondary: 0x12151c,
    accent: 0x00c2ff,
    helmet: 0xff8800,
    tireStripe: 0xf5cc16,
    liveryTex: './textures/liveries/mclaren.webp',
    logoTex: './textures/logos/mclaren.png',
    suitTex: './textures/suits/mclaren.webp',
    power: 1.03,
    aero: 1.04,
    grip: 1.04,
    agility: 1.03,
  },
  {
    id: 'mercedes',
    name: 'Mercedes-AMG',
    shortName: 'MER',
    driverName: 'Kimi Antonelli',
    driverCode: 'ANT',
    driverNumber: 12,
    primary: 0x141a20,
    secondary: 0xa6b2be,
    accent: 0x00d2be,
    helmet: 0x00d2be,
    tireStripe: 0xf5cc16,
    liveryTex: './textures/liveries/mercedes.webp',
    logoTex: './textures/logos/mercedes.png',
    suitTex: './textures/suits/mercedes.webp',
    power: 1.03,
    aero: 1.02,
    grip: 1.02,
    agility: 1.01,
  },
  {
    id: 'astonmartin',
    name: 'Aston Martin',
    shortName: 'AST',
    driverName: 'Fernando Alonso',
    driverCode: 'ALO',
    driverNumber: 14,
    primary: 0x00594f,
    secondary: 0x0b1514,
    accent: 0xc2ff00,
    helmet: 0x00a896,
    tireStripe: 0xf5cc16,
    liveryTex: './textures/liveries/astonmartin.webp',
    logoTex: './textures/logos/astonmartin.png',
    suitTex: './textures/suits/astonmartin.webp',
    power: 1.00,
    aero: 1.01,
    grip: 1.01,
    agility: 1.01,
  },
  {
    id: 'alpine',
    name: 'Alpine F1 Team',
    shortName: 'ALP',
    driverName: 'Pierre Gasly',
    driverCode: 'GAS',
    driverNumber: 10,
    primary: 0x0078d4,
    secondary: 0x101522,
    accent: 0xff4fa3,
    helmet: 0xff4fa3,
    tireStripe: 0xff2432,
    liveryTex: './textures/liveries/alpine.webp',
    logoTex: './textures/logos/alpine.png',
    suitTex: './textures/suits/alpine.webp',
    power: 0.99,
    aero: 1.00,
    grip: 1.00,
    agility: 1.01,
  },
  {
    id: 'williams',
    name: 'Williams Racing',
    shortName: 'WIL',
    driverName: 'Carlos Sainz',
    driverCode: 'SAI',
    driverNumber: 55,
    primary: 0x0046c8,
    secondary: 0x0c1328,
    accent: 0x38b6ff,
    helmet: 0x0066ff,
    tireStripe: 0xe8ecf2,
    liveryTex: './textures/liveries/williams.webp',
    logoTex: './textures/logos/williams.png',
    suitTex: './textures/suits/williams.webp',
    power: 1.00,
    aero: 0.99,
    grip: 1.00,
    agility: 1.00,
  },
  {
    id: 'vcarb',
    name: 'Racing Bulls',
    shortName: 'VCB',
    driverName: 'Liam Lawson',
    driverCode: 'LAW',
    driverNumber: 30,
    primary: 0xe8ecf5,
    secondary: 0x132870,
    accent: 0xe6192e,
    helmet: 0x2255ff,
    tireStripe: 0xe8ecf2,
    liveryTex: './textures/liveries/vcarb.webp',
    logoTex: './textures/logos/vcarb.png',
    suitTex: './textures/suits/vcarb.webp',
    power: 0.99,
    aero: 1.00,
    grip: 0.99,
    agility: 1.01,
  },
  {
    id: 'audi',
    name: 'Audi F1 Team',
    shortName: 'AUD',
    driverName: 'Nico Hulkenberg',
    driverCode: 'NIC',
    driverNumber: 27,
    primary: 0xc8ccd4,
    secondary: 0x141518,
    accent: 0xe6192e,
    helmet: 0xe6192e,
    tireStripe: 0xf5cc16,
    liveryTex: './textures/liveries/audi.webp',
    logoTex: './textures/logos/audi.png',
    suitTex: './textures/suits/audi.webp',
    power: 0.99,
    aero: 0.99,
    grip: 0.99,
    agility: 1.00,
  },
  {
    id: 'haas',
    name: 'Haas F1 Team',
    shortName: 'HAS',
    driverName: 'Ollie Bearman',
    driverCode: 'OLI',
    driverNumber: 87,
    primary: 0xe8eaef,
    secondary: 0x14161b,
    accent: 0xe6192e,
    helmet: 0xe0e4ec,
    tireStripe: 0xf5cc16,
    liveryTex: './textures/liveries/haas.webp',
    logoTex: './textures/logos/haas.png',
    suitTex: './textures/suits/haas.webp',
    power: 0.98,
    aero: 0.99,
    grip: 0.99,
    agility: 0.99,
  },
  {
    id: 'cadillac',
    name: 'Cadillac F1 Team',
    shortName: 'CAD',
    driverName: 'Valtteri Bottas',
    driverCode: 'BOT',
    driverNumber: 77,
    primary: 0x16181e,
    secondary: 0xdce0e8,
    accent: 0xc9a227,
    helmet: 0xdce0e8,
    tireStripe: 0xe8ecf2,
    liveryTex: './textures/liveries/cadillac.webp',
    logoTex: './textures/logos/cadillac.png',
    suitTex: './textures/suits/cadillac.webp',
    power: 0.98,
    aero: 0.98,
    grip: 0.99,
    agility: 0.99,
  },
];

export const DRIVERS: DriverEntry[] = [
  { id: 'verstappen', name: 'Max Verstappen',  code: 'MAX', number: 3,  country: 'NETHERLANDS', flag: '🇳🇱', teamId: 'redbull',     portrait: './textures/drivers/verstappen.webp', cutout: './textures/drivers_cutout/verstappen.webp', helmetId: 'verstappen', suitId: 'redbull' },
  { id: 'hadjar',     name: 'Isack Hadjar',    code: 'HAD', number: 6,  country: 'FRANCE',      flag: '🇫🇷', teamId: 'redbull',     portrait: './textures/drivers/hadjar.webp',     cutout: './textures/drivers_cutout/hadjar.webp',     helmetId: 'hadjar',     suitId: 'redbull' },
  { id: 'leclerc',    name: 'Charles Leclerc', code: 'LEC', number: 16, country: 'MONACO',      flag: '🇲🇨', teamId: 'ferrari',     portrait: './textures/drivers/leclerc.webp',    cutout: './textures/drivers_cutout/leclerc.webp',    helmetId: 'leclerc',    suitId: 'ferrari' },
  { id: 'hamilton',   name: 'Lewis Hamilton',  code: 'HAM', number: 44, country: 'UK',          flag: '🇬🇧', teamId: 'ferrari',     portrait: './textures/drivers/hamilton.webp',   cutout: './textures/drivers_cutout/hamilton.webp',   helmetId: 'hamilton',   suitId: 'ferrari' },
  { id: 'piastri',    name: 'Oscar Piastri',   code: 'PIA', number: 81, country: 'AUSTRALIA',   flag: '🇦🇺', teamId: 'mclaren',     portrait: './textures/drivers/piastri.webp',    cutout: './textures/drivers_cutout/piastri.webp',    helmetId: 'piastri',    suitId: 'mclaren' },
  { id: 'norris',     name: 'Lando Norris',    code: 'NOR', number: 1,  country: 'UK',          flag: '🇬🇧', teamId: 'mclaren',     portrait: './textures/drivers/norris.webp',     cutout: './textures/drivers_cutout/norris.webp',     helmetId: 'norris',     suitId: 'mclaren' },
  { id: 'antonelli',  name: 'Kimi Antonelli',  code: 'ANT', number: 12, country: 'ITALY',       flag: '🇮🇹', teamId: 'mercedes',    portrait: './textures/drivers/antonelli.webp',  cutout: './textures/drivers_cutout/antonelli.webp',  helmetId: 'antonelli',  suitId: 'mercedes' },
  { id: 'russell',    name: 'George Russell',  code: 'RUS', number: 63, country: 'UK',          flag: '🇬🇧', teamId: 'mercedes',    portrait: './textures/drivers/russell.webp',    cutout: './textures/drivers_cutout/russell.webp',    helmetId: 'russell',    suitId: 'mercedes' },
  { id: 'alonso',     name: 'Fernando Alonso', code: 'ALO', number: 14, country: 'SPAIN',       flag: '🇪🇸', teamId: 'astonmartin', portrait: './textures/drivers/alonso.webp',     cutout: './textures/drivers_cutout/alonso.webp',     helmetId: 'alonso',     suitId: 'astonmartin' },
  { id: 'stroll',     name: 'Lance Stroll',    code: 'STR', number: 18, country: 'CANADA',      flag: '🇨🇦', teamId: 'astonmartin', portrait: './textures/drivers/stroll.webp',     cutout: './textures/drivers_cutout/stroll.webp',     helmetId: 'stroll',     suitId: 'astonmartin' },
  { id: 'gasly',      name: 'Pierre Gasly',    code: 'GAS', number: 10, country: 'FRANCE',      flag: '🇫🇷', teamId: 'alpine',      portrait: './textures/drivers/gasly.webp',      cutout: './textures/drivers_cutout/gasly.webp',      helmetId: 'alpine',     suitId: 'alpine' },
  { id: 'colapinto',  name: 'Franco Colapinto',code: 'COL', number: 43, country: 'ARGENTINA',   flag: '🇦🇷', teamId: 'alpine',      portrait: './textures/drivers/colapinto.webp',  cutout: './textures/drivers_cutout/colapinto.webp',  helmetId: 'alpine',     suitId: 'alpine' },
  { id: 'albon',      name: 'Alex Albon',      code: 'ALB', number: 23, country: 'THAILAND',    flag: '🇹🇭', teamId: 'williams',    portrait: './textures/drivers/albon.webp',      cutout: './textures/drivers_cutout/albon.webp',      helmetId: 'williams',   suitId: 'williams' },
  { id: 'sainz',      name: 'Carlos Sainz',    code: 'SAI', number: 55, country: 'SPAIN',       flag: '🇪🇸', teamId: 'williams',    portrait: './textures/drivers/sainz.webp',      cutout: './textures/drivers_cutout/sainz.webp',      helmetId: 'williams',   suitId: 'williams' },
  { id: 'lawson',     name: 'Liam Lawson',     code: 'LAW', number: 30, country: 'NEW ZEALAND', flag: '🇳🇿', teamId: 'vcarb',       portrait: './textures/drivers/lawson.webp',     cutout: './textures/drivers_cutout/lawson.webp',     helmetId: 'vcarb',      suitId: 'vcarb' },
  { id: 'lindblad',   name: 'Arvid Lindblad',  code: 'ARV', number: 41, country: 'UK',          flag: '🇬🇧', teamId: 'vcarb',       portrait: './textures/drivers/lindblad.webp',   cutout: './textures/drivers_cutout/lindblad.webp',   helmetId: 'vcarb',      suitId: 'vcarb' },
  { id: 'hulkenberg', name: 'Nico Hulkenberg', code: 'NIC', number: 27, country: 'GERMANY',     flag: '🇩🇪', teamId: 'audi',        portrait: './textures/drivers/hulkenberg.webp', cutout: './textures/drivers_cutout/hulkenberg.webp', helmetId: 'haas',       suitId: 'audi' },
  { id: 'bortoleto',  name: 'Gabriel Bortoleto',code:'GAB', number: 5,  country: 'BRAZIL',      flag: '🇧🇷', teamId: 'audi',        portrait: './textures/drivers/bortoleto.webp',  cutout: './textures/drivers_cutout/bortoleto.webp',  helmetId: 'piastri',    suitId: 'audi' },
  { id: 'ocon',       name: 'Esteban Ocon',    code: 'EST', number: 31, country: 'FRANCE',      flag: '🇫🇷', teamId: 'haas',        portrait: './textures/drivers/ocon.webp',       cutout: './textures/drivers_cutout/ocon.webp',       helmetId: 'haas',       suitId: 'haas' },
  { id: 'bearman',    name: 'Ollie Bearman',   code: 'OLI', number: 87, country: 'UK',          flag: '🇬🇧', teamId: 'haas',        portrait: './textures/drivers/bearman.webp',    cutout: './textures/drivers_cutout/bearman.webp',    helmetId: 'haas',       suitId: 'haas' },
  { id: 'bottas',     name: 'Valtteri Bottas', code: 'BOT', number: 77, country: 'FINLAND',     flag: '🇫🇮', teamId: 'cadillac',    portrait: './textures/drivers/bottas.webp',     cutout: './textures/drivers_cutout/bottas.webp',     helmetId: 'russell',    suitId: 'cadillac' },
  { id: 'perez',      name: 'Sergio Perez',    code: 'SER', number: 11, country: 'MEXICO',      flag: '🇲🇽', teamId: 'cadillac',    portrait: './textures/drivers/perez.webp',      cutout: './textures/drivers_cutout/perez.webp',      helmetId: 'verstappen', suitId: 'cadillac' },
];

export function getTeamById(id: string): FormulaTeam {
  return TEAMS.find((t) => t.id === id) || TEAMS[0];
}

export function getDriversForTeam(teamId: string): DriverEntry[] {
  return DRIVERS.filter((d) => d.teamId === teamId);
}

export const F1_POINTS = [25, 18, 15, 12, 10, 8, 6, 4, 2, 1];
