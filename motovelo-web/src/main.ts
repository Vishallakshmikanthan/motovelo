import './style.css';
import { CIRCUITS } from './data/circuits';
import { TEAMS } from './data/teams';
import { GameEngine } from './engine/GameEngine';

const canvas = document.getElementById('game-canvas') as HTMLCanvasElement;
const engine = new GameEngine(canvas);

// Populate Circuit Selector
const circuitSelector = document.getElementById('circuit-selector')!;
const circuitDesc = document.getElementById('circuit-desc')!;

function renderCircuits() {
  circuitSelector.innerHTML = '';
  CIRCUITS.forEach((c, idx) => {
    const btn = document.createElement('button');
    btn.className = `chip ${idx === engine.selectedCircuitIndex ? 'active' : ''}`;
    btn.textContent = c.name;
    btn.onclick = () => {
      engine.audio.unlock();
      engine.audio.playClick();
      engine.selectedCircuitIndex = idx;
      renderCircuits();
    };
    circuitSelector.appendChild(btn);
  });
  circuitDesc.textContent = CIRCUITS[engine.selectedCircuitIndex].description;
}
renderCircuits();

// Laps & Difficulty Chips
document.querySelectorAll('#laps-selector .chip').forEach((el) => {
  el.addEventListener('click', () => {
    engine.audio.unlock();
    engine.audio.playClick();
    engine.selectedLaps = Number((el as HTMLElement).dataset.laps);
    document.querySelectorAll('#laps-selector .chip').forEach((c) => c.classList.remove('active'));
    el.classList.add('active');
  });
});

document.querySelectorAll('#diff-selector .chip').forEach((el) => {
  el.addEventListener('click', () => {
    engine.audio.unlock();
    engine.audio.playClick();
    engine.difficulty = Number((el as HTMLElement).dataset.diff);
    document.querySelectorAll('#diff-selector .chip').forEach((c) => c.classList.remove('active'));
    el.classList.add('active');
  });
});

// Garage Team List & Specs
const teamListEl = document.getElementById('garage-team-list')!;
function renderGarage() {
  teamListEl.innerHTML = '';
  TEAMS.forEach((t, idx) => {
    const btn = document.createElement('button');
    btn.className = `team-item ${idx === engine.selectedTeamIndex ? 'active' : ''}`;
    const hex = '#' + t.primary.toString(16).padStart(6, '0');
    btn.innerHTML = `
      <div class="team-swatch" style="background:${hex}"></div>
      <div>
        <div class="team-item-name">${t.name}</div>
        <div class="team-item-sub">#${t.driverNumber} ${t.driverName}</div>
      </div>
    `;
    btn.onclick = () => {
      engine.audio.unlock();
      engine.audio.playClick();
      engine.selectTeam(idx);
      renderGarage();
    };
    teamListEl.appendChild(btn);
  });

  const active = TEAMS[engine.selectedTeamIndex];
  document.getElementById('spec-team-name')!.textContent = active.name.toUpperCase();
  document.getElementById('spec-driver')!.textContent = `DRIVER #${active.driverNumber} // ${active.driverName.toUpperCase()}`;
  const toPct = (v: number) => `${Math.round(((v - 0.90) / 0.15) * 100)}%`;
  (document.getElementById('stat-power') as HTMLElement).style.width = toPct(active.power);
  (document.getElementById('stat-aero') as HTMLElement).style.width = toPct(active.aero);
  (document.getElementById('stat-grip') as HTMLElement).style.width = toPct(active.grip);
  (document.getElementById('stat-agility') as HTMLElement).style.width = toPct(active.agility);
}

// Main Menu Navigation
document.getElementById('btn-play')!.onclick = () => { engine.audio.unlock(); engine.audio.playClick(); engine.startRace('QUICK'); };
document.getElementById('btn-championship')!.onclick = () => { engine.audio.unlock(); engine.audio.playClick(); engine.startRace('CHAMPIONSHIP'); };
document.getElementById('btn-timetrial')!.onclick = () => { engine.audio.unlock(); engine.audio.playClick(); engine.startRace('TIMETRIAL'); };
document.getElementById('btn-garage')!.onclick = () => { engine.audio.unlock(); engine.audio.playClick(); renderGarage(); engine.setState('GARAGE'); };
document.getElementById('btn-garage-back')!.onclick = () => { engine.audio.playClick(); engine.setState('MENU'); };
document.getElementById('btn-garage-race')!.onclick = () => { engine.audio.playClick(); engine.startRace('QUICK'); };

// Settings & Credits Modals
const settingsModal = document.getElementById('screen-settings')!;
const creditsModal = document.getElementById('screen-credits')!;

document.getElementById('btn-settings')!.onclick = () => { engine.audio.unlock(); engine.audio.playClick(); settingsModal.classList.add('active'); };
document.getElementById('btn-settings-close')!.onclick = () => { engine.audio.playClick(); settingsModal.classList.remove('active'); };
document.getElementById('btn-credits')!.onclick = () => { engine.audio.unlock(); engine.audio.playClick(); creditsModal.classList.add('active'); };
document.getElementById('btn-credits-close')!.onclick = () => { engine.audio.playClick(); creditsModal.classList.remove('active'); };

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
(document.getElementById('set-steer-sens') as HTMLInputElement).addEventListener('input', (e) => {
  engine.settings.steerSensitivity = parseFloat((e.target as HTMLInputElement).value);
  engine.saveState();
});

// HUD & Pause & Results Actions
document.getElementById('btn-hud-cam')!.onclick = () => engine.cycleCamera();
document.getElementById('btn-hud-pause')!.onclick = () => { if (engine.state === 'RACING' || engine.state === 'COUNTDOWN') engine.setState('PAUSED'); };
document.getElementById('btn-resume')!.onclick = () => engine.setState('RACING');
document.getElementById('btn-restart')!.onclick = () => engine.startRace(engine.mode);
document.getElementById('btn-pause-cam')!.onclick = () => engine.cycleCamera();
document.getElementById('btn-quit')!.onclick = () => engine.setState('MENU');

document.getElementById('btn-res-next')!.onclick = () => engine.startRace('CHAMPIONSHIP');
document.getElementById('btn-res-restart')!.onclick = () => engine.startRace(engine.mode);
document.getElementById('btn-res-menu')!.onclick = () => engine.setState('MENU');

// Responsive Multi-Touch Mobile Controls + Keyboard State
const held = { left: false, right: false, gas: false, brake: false, ers: false };

function syncInputs() {
  engine.playerInput.steer = (held.right ? 1 : 0) - (held.left ? 1 : 0);
  engine.playerInput.throttle = held.gas ? 1 : 0;
  engine.playerInput.brake = held.brake ? 1 : 0;
  engine.playerInput.ers = held.ers;
}

function bindHoldButton(id: string, key: keyof typeof held) {
  const el = document.getElementById(id)!;
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
bindHoldButton('touch-ers', 'ers');

document.getElementById('touch-shift-up')!.addEventListener('pointerdown', (e) => {
  e.preventDefault();
  engine.playerInput.shiftUp = true;
});
document.getElementById('touch-shift-dn')!.addEventListener('pointerdown', (e) => {
  e.preventDefault();
  engine.playerInput.shiftDown = true;
});

// Keyboard Controls (Desktop & Emulator Testing)
window.addEventListener('keydown', (e) => {
  if (e.repeat) return;
  engine.audio.unlock();
  switch (e.code) {
    case 'KeyW': case 'ArrowUp': held.gas = true; break;
    case 'KeyS': case 'ArrowDown': held.brake = true; break;
    case 'KeyA': case 'ArrowLeft': held.left = true; break;
    case 'KeyD': case 'ArrowRight': held.right = true; break;
    case 'Space': held.ers = true; break;
    case 'KeyC': if (engine.state === 'RACING' || engine.state === 'COUNTDOWN') engine.cycleCamera(); break;
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
  }
  syncInputs();
});

// Expose engine for automated smoke verification
(window as unknown as { MOTOVELO: GameEngine }).MOTOVELO = engine;

engine.init();
