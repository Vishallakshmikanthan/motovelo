import './style.css';
import { CIRCUITS } from './data/circuits';
import { TYRE_COMPOUNDS } from './data/teams';
import { GameEngine, RaceMode } from './engine/GameEngine';

const canvas = document.getElementById('game-canvas') as HTMLCanvasElement;
const engine = new GameEngine(canvas);

let pendingMode: RaceMode = 'QUICK';

// Sync Car Selection UI
function syncCarSelectUI() {
  const t = engine.currentTeam;
  (document.getElementById('car-team-logo') as HTMLImageElement).src = t.logoTex;
  document.getElementById('car-team-name')!.textContent = t.name;
}

// Sync Driver Selection UI
function syncDriverSelectUI() {
  const d = engine.currentDriver;
  (document.getElementById('driver-portrait-img') as HTMLImageElement).src = d.portrait;
  document.getElementById('driver-name-banner')!.textContent = d.name;
}

// Build Monoposto Track Selection Card Carousel
function renderTrackCarousel() {
  const wrap = document.getElementById('track-carousel')!;
  wrap.innerHTML = '';
  CIRCUITS.forEach((c, idx) => {
    const card = document.createElement('div');
    card.className = `track-card ${idx === engine.selectedCircuitIndex ? 'selected' : ''}`;
    card.innerHTML = `
      <div class="flag-badge flag-${c.flagCode}"></div>
      <div class="track-card-inner">
        <div class="track-silver-band"></div>
        <img class="track-photo" src="${c.cardImg}" alt="${c.name}" />
        <div class="track-card-footer">
          <div class="track-card-name">${c.name}</div>
          <div class="track-card-country">${c.country}</div>
        </div>
      </div>
    `;
    card.onclick = () => {
      engine.audio.unlock();
      engine.audio.playClick();
      engine.selectedCircuitIndex = idx;
      renderTrackCarousel();
    };
    wrap.appendChild(card);
  });
}

// Build Monoposto Car Setup 5 Tyre Compound Cards
function renderTyreCards() {
  const row = document.getElementById('tyre-cards-row')!;
  row.innerHTML = '';
  const bgGrads: Record<string, string> = {
    soft: 'linear-gradient(180deg, #ff2836 0%, #8c0610 100%)',
    mid: 'linear-gradient(180deg, #ffd020 0%, #9c6e04 100%)',
    hard: 'linear-gradient(180deg, #a8b0bc 0%, #444a54 100%)',
    inter: 'linear-gradient(180deg, #52e816 0%, #247808 100%)',
    wet: 'linear-gradient(180deg, #249cff 0%, #0b4a8c 100%)',
  };
  TYRE_COMPOUNDS.forEach((comp) => {
    const card = document.createElement('div');
    card.className = `tyre-card ${engine.carSetup.compound.id === comp.id ? 'selected' : ''}`;
    card.innerHTML = `
      <div class="tyre-card-box" style="background:${bgGrads[comp.id]}">
        <img src="${comp.tex}" alt="${comp.label}" />
      </div>
      <div class="tyre-count">${comp.sets}</div>
      <div class="tyre-label">${comp.label}</div>
    `;
    card.onclick = () => {
      engine.audio.unlock();
      engine.audio.playClick();
      engine.selectCompound(comp);
      renderTyreCards();
    };
    row.appendChild(card);
  });
}

// Main Menu -> Monoposto Flow (Car Select -> Driver Select -> Track Select -> Car Setup -> Grid Intro -> Race!)
document.getElementById('btn-quick-race')!.onclick = () => {
  engine.audio.unlock();
  engine.audio.playClick();
  pendingMode = 'QUICK';
  syncCarSelectUI();
  engine.setState('CAR_SELECT');
};
document.getElementById('btn-championship')!.onclick = () => {
  engine.audio.unlock();
  engine.audio.playClick();
  pendingMode = 'CHAMPIONSHIP';
  syncCarSelectUI();
  engine.setState('CAR_SELECT');
};
document.getElementById('btn-timetrial')!.onclick = () => {
  engine.audio.unlock();
  engine.audio.playClick();
  pendingMode = 'TIMETRIAL';
  syncCarSelectUI();
  engine.setState('CAR_SELECT');
};
document.getElementById('btn-customize')!.onclick = () => {
  engine.audio.unlock();
  engine.audio.playClick();
  pendingMode = 'QUICK';
  syncCarSelectUI();
  engine.setState('CAR_SELECT');
};

const lapOptions = [3, 5, 10, 25];
document.getElementById('btn-laps-cycle')!.onclick = (e) => {
  engine.audio.unlock();
  engine.audio.playClick();
  const next = lapOptions[(lapOptions.indexOf(engine.selectedLaps) + 1) % lapOptions.length];
  engine.selectedLaps = next;
  (e.currentTarget as HTMLElement).textContent = `LAPS: ${next}`;
};

// CAR SELECTION CONTROLS
document.getElementById('btn-car-back')!.onclick = () => { engine.audio.playClick(); engine.setState('MENU'); };
document.getElementById('btn-car-prev')!.onclick = () => { engine.audio.playClick(); engine.selectTeam(engine.selectedTeamIndex - 1); syncCarSelectUI(); };
document.getElementById('btn-car-next')!.onclick = () => { engine.audio.playClick(); engine.selectTeam(engine.selectedTeamIndex + 1); syncCarSelectUI(); };
document.getElementById('btn-car-zoom')!.onclick = () => { engine.audio.playClick(); engine.garageScene.zoomedIn = !engine.garageScene.zoomedIn; };
document.getElementById('btn-car-edit')!.onclick = () => { engine.audio.playClick(); syncDriverSelectUI(); engine.setState('DRIVER_SELECT'); };
document.getElementById('btn-car-options')!.onclick = () => { engine.audio.playClick(); document.getElementById('screen-settings')!.classList.add('active'); };
document.getElementById('btn-car-continue')!.onclick = () => { engine.audio.playClick(); syncDriverSelectUI(); engine.setState('DRIVER_SELECT'); };

// DRIVER SELECTION CONTROLS
document.getElementById('btn-driver-back')!.onclick = () => { engine.audio.playClick(); syncCarSelectUI(); engine.setState('CAR_SELECT'); };
document.getElementById('btn-driver-prev')!.onclick = () => { engine.audio.playClick(); engine.cycleDriver(-1); syncDriverSelectUI(); };
document.getElementById('btn-driver-next')!.onclick = () => { engine.audio.playClick(); engine.cycleDriver(1); syncDriverSelectUI(); };
document.getElementById('btn-driver-zoom')!.onclick = () => { engine.audio.playClick(); engine.garageScene.zoomedIn = !engine.garageScene.zoomedIn; };
document.getElementById('btn-load-helmet')!.onclick = () => { engine.audio.playClick(); engine.cycleHelmet(); };
document.getElementById('btn-load-suit')!.onclick = () => { engine.audio.playClick(); engine.cycleSuit(); };
document.getElementById('btn-driver-continue')!.onclick = () => { engine.audio.playClick(); renderTrackCarousel(); engine.setState('TRACK_SELECT'); };

// TRACK SELECTION CONTROLS
document.getElementById('btn-track-back')!.onclick = () => { engine.audio.playClick(); syncDriverSelectUI(); engine.setState('DRIVER_SELECT'); };
document.getElementById('btn-track-continue')!.onclick = () => { engine.audio.playClick(); renderTyreCards(); engine.setState('CAR_SETUP'); };

// CAR SETUP CONTROLS & SLIDERS
document.getElementById('btn-setup-back')!.onclick = () => { engine.audio.playClick(); renderTrackCarousel(); engine.setState('TRACK_SELECT'); };
document.getElementById('btn-setup-start')!.onclick = () => { engine.audio.playClick(); engine.startRace(pendingMode); };

(document.getElementById('slider-wings') as HTMLInputElement).addEventListener('input', (e) => {
  const v = Number((e.target as HTMLInputElement).value);
  engine.carSetup.wings = v;
  document.getElementById('val-wings')!.textContent = String(v);
});
(document.getElementById('slider-brake') as HTMLInputElement).addEventListener('input', (e) => {
  const v = Number((e.target as HTMLInputElement).value);
  engine.carSetup.brakeBalance = v;
  document.getElementById('val-brake')!.textContent = String(v);
});
(document.getElementById('slider-susp') as HTMLInputElement).addEventListener('input', (e) => {
  const v = Number((e.target as HTMLInputElement).value);
  engine.carSetup.suspension = v;
  document.getElementById('val-susp')!.textContent = String(v);
});

// Tap Starting Grid Intro to jump right to lights!
document.getElementById('screen-grid-intro')!.onclick = () => engine.skipGridIntro();

// Settings Modal
const settingsModal = document.getElementById('screen-settings')!;
document.getElementById('btn-settings')!.onclick = () => { engine.audio.unlock(); engine.audio.playClick(); settingsModal.classList.add('active'); };
document.getElementById('btn-settings-close')!.onclick = () => { engine.audio.playClick(); settingsModal.classList.remove('active'); };

document.querySelectorAll('#set-gearbox .chip').forEach((el) => {
  el.addEventListener('click', () => {
    engine.settings.autoGear = (el as HTMLElement).dataset.val === 'auto';
    document.querySelectorAll('#set-gearbox .chip').forEach((c) => c.classList.remove('active'));
    el.classList.add('active');
    engine.saveState();
  });
});
document.querySelectorAll('#set-quality .chip').forEach((el) => {
  el.addEventListener('click', () => {
    engine.applyQuality((el as HTMLElement).dataset.val as 'low' | 'high');
    document.querySelectorAll('#set-quality .chip').forEach((c) => c.classList.remove('active'));
    el.classList.add('active');
  });
});
document.querySelectorAll('#set-audio .chip').forEach((el) => {
  el.addEventListener('click', () => {
    const on = (el as HTMLElement).dataset.val === 'on';
    engine.settings.audio = on;
    engine.audio.setEnabled(on);
    document.querySelectorAll('#set-audio .chip').forEach((c) => c.classList.remove('active'));
    el.classList.add('active');
    engine.saveState();
  });
});
document.querySelectorAll('#set-diff .chip').forEach((el) => {
  el.addEventListener('click', () => {
    engine.difficulty = Number((el as HTMLElement).dataset.diff);
    document.querySelectorAll('#set-diff .chip').forEach((c) => c.classList.remove('active'));
    el.classList.add('active');
  });
});
(document.getElementById('set-steer-sens') as HTMLInputElement).addEventListener('input', (e) => {
  engine.settings.steerSensitivity = parseFloat((e.target as HTMLInputElement).value);
  engine.saveState();
});
(document.getElementById('set-accel-press') as HTMLInputElement).addEventListener('input', (e) => {
  engine.settings.accelPressure = parseFloat((e.target as HTMLInputElement).value);
  engine.saveState();
});
(document.getElementById('set-brake-press') as HTMLInputElement).addEventListener('input', (e) => {
  engine.settings.brakePressure = parseFloat((e.target as HTMLInputElement).value);
  engine.saveState();
});

// HUD & Pause & Results Actions
document.getElementById('btn-hud-tv')!.onclick = () => engine.toggleTower();
document.getElementById('btn-hud-cam')!.onclick = () => engine.cycleCamera();
document.getElementById('btn-hud-pause')!.onclick = () => {
  if (engine.state === 'RACING' || engine.state === 'COUNTDOWN') engine.setState('PAUSED');
};
document.getElementById('btn-resume')!.onclick = () => engine.setState('RACING');
document.getElementById('btn-restart')!.onclick = () => engine.startRace(engine.mode);
document.getElementById('btn-pause-cam')!.onclick = () => engine.cycleCamera();
document.getElementById('btn-quit')!.onclick = () => engine.setState('MENU');

document.getElementById('btn-res-next')!.onclick = () => engine.startRace('CHAMPIONSHIP');
document.getElementById('btn-res-restart')!.onclick = () => engine.startRace(engine.mode);
document.getElementById('btn-res-menu')!.onclick = () => engine.setState('MENU');

// Responsive Multi-Touch Mobile Controls + Keyboard State
const held = { left: false, right: false, gas: false, brake: false, ers: false, drs: false };

function syncInputs() {
  engine.playerInput.steer = (held.right ? 1 : 0) - (held.left ? 1 : 0);
  engine.playerInput.throttle = held.gas ? 1 : 0;
  engine.playerInput.brake = held.brake ? 1 : 0;
  engine.playerInput.ers = held.ers;
  engine.playerInput.drs = held.drs;
}

function bindHoldButton(id: string, key: keyof typeof held) {
  const el = document.getElementById(id);
  if (!el) return;
  const down = (e: Event) => {
    e.preventDefault();
    engine.audio.unlock();
    held[key] = true;
    el.classList.add('pressed');
    syncInputs();
  };
  const up = (e: Event) => {
    e.preventDefault();
    held[key] = false;
    el.classList.remove('pressed');
    syncInputs();
  };
  el.addEventListener('pointerdown', down);
  el.addEventListener('pointerup', up);
  el.addEventListener('pointercancel', up);
  el.addEventListener('pointerleave', up);
}

bindHoldButton('touch-left', 'left');
bindHoldButton('touch-right', 'right');
bindHoldButton('touch-gas', 'gas');
bindHoldButton('touch-brake', 'brake');
bindHoldButton('touch-ers-l', 'ers');
bindHoldButton('touch-ers-r', 'ers');
bindHoldButton('touch-drs', 'drs');

document.getElementById('touch-pit')?.addEventListener('pointerdown', (e) => {
  e.preventDefault();
  engine.togglePitRequest();
});

const rearBtn = document.getElementById('touch-rearview');
if (rearBtn) {
  const rDown = (e: Event) => { e.preventDefault(); rearBtn.classList.add('pressed'); engine.setLookBack(true); };
  const rUp = (e: Event) => { e.preventDefault(); rearBtn.classList.remove('pressed'); engine.setLookBack(false); };
  rearBtn.addEventListener('pointerdown', rDown);
  rearBtn.addEventListener('pointerup', rUp);
  rearBtn.addEventListener('pointercancel', rUp);
  rearBtn.addEventListener('pointerleave', rUp);
}

document.getElementById('touch-shift-up')!.addEventListener('pointerdown', (e) => {
  e.preventDefault();
  engine.playerInput.shiftUp = true;
});
document.getElementById('touch-shift-dn')!.addEventListener('pointerdown', (e) => {
  e.preventDefault();
  engine.playerInput.shiftDown = true;
});

window.addEventListener('keydown', (e) => {
  if (e.repeat) return;
  engine.audio.unlock();
  switch (e.code) {
    case 'KeyW': case 'ArrowUp': held.gas = true; break;
    case 'KeyS': case 'ArrowDown': held.brake = true; break;
    case 'KeyA': case 'ArrowLeft': held.left = true; break;
    case 'KeyD': case 'ArrowRight': held.right = true; break;
    case 'Space': held.ers = true; break;
    case 'KeyF': held.drs = true; break;
    case 'KeyR': engine.setLookBack(true); break;
    case 'KeyP': engine.togglePitRequest(); break;
    case 'KeyC': if (engine.state === 'RACING' || engine.state === 'COUNTDOWN') engine.cycleCamera(); break;
    case 'KeyT': if (engine.state === 'RACING' || engine.state === 'COUNTDOWN') engine.toggleTower(); break;
    case 'KeyE': engine.playerInput.shiftUp = true; break;
    case 'KeyQ': engine.playerInput.shiftDown = true; break;
    case 'Escape':
      if (engine.state === 'RACING') engine.setState('PAUSED');
      else if (engine.state === 'PAUSED') engine.setState('RACING');
      break;
  }
  syncInputs();
});

window.addEventListener('keyup', (e) => {
  switch (e.code) {
    case 'KeyW': case 'ArrowUp': held.gas = false; break;
    case 'KeyS': case 'ArrowDown': held.brake = false; break;
    case 'KeyA': case 'ArrowLeft': held.left = false; break;
    case 'KeyD': case 'ArrowRight': held.right = false; break;
    case 'Space': held.ers = false; break;
    case 'KeyF': held.drs = false; break;
    case 'KeyR': engine.setLookBack(false); break;
  }
  syncInputs();
});

(window as unknown as { MOTOVELO: GameEngine }).MOTOVELO = engine;

engine.init();
