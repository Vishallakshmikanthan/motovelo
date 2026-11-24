export class AudioEngine {
  private ctx: AudioContext | null = null;
  private masterGain: GainNode | null = null;
  private engineOsc1: OscillatorNode | null = null;
  private engineOsc2: OscillatorNode | null = null;
  private turboOsc: OscillatorNode | null = null;
  private engineFilter: BiquadFilterNode | null = null;
  private engineGain: GainNode | null = null;
  private turboGain: GainNode | null = null;
  private screechGain: GainNode | null = null;
  private screechFilter: BiquadFilterNode | null = null;

  public enabled = true;
  private running = false;

  unlock() {
    if (!this.ctx) {
      const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      if (!AudioCtx) return;
      this.ctx = new AudioCtx();
      this.masterGain = this.ctx.createGain();
      this.masterGain.gain.value = this.enabled ? 0.35 : 0;
      this.masterGain.connect(this.ctx.destination);
      this.buildEngineNodes();
    }
    if (this.ctx && this.ctx.state === 'suspended') {
      this.ctx.resume().catch(() => {});
    }
  }

  setEnabled(on: boolean) {
    this.enabled = on;
    if (this.masterGain && this.ctx) {
      this.masterGain.gain.setTargetAtTime(on ? 0.35 : 0, this.ctx.currentTime, 0.05);
    }
  }

  private buildEngineNodes() {
    if (!this.ctx || !this.masterGain) return;

    this.engineFilter = this.ctx.createBiquadFilter();
    this.engineFilter.type = 'bandpass';
    this.engineFilter.frequency.value = 320;
    this.engineFilter.Q.value = 1.6;

    this.engineGain = this.ctx.createGain();
    this.engineGain.gain.value = 0.0;

    this.engineOsc1 = this.ctx.createOscillator();
    this.engineOsc1.type = 'sawtooth';
    this.engineOsc1.frequency.value = 110;

    this.engineOsc2 = this.ctx.createOscillator();
    this.engineOsc2.type = 'triangle';
    this.engineOsc2.frequency.value = 220;

    this.engineOsc1.connect(this.engineFilter);
    this.engineOsc2.connect(this.engineFilter);
    this.engineFilter.connect(this.engineGain);
    this.engineGain.connect(this.masterGain);

    // High-pitched F1 turbocharger whine
    this.turboOsc = this.ctx.createOscillator();
    this.turboOsc.type = 'sine';
    this.turboOsc.frequency.value = 1800;
    this.turboGain = this.ctx.createGain();
    this.turboGain.gain.value = 0.0;
    this.turboOsc.connect(this.turboGain);
    this.turboGain.connect(this.masterGain);

    // Tire screech noise
    const bufSize = this.ctx.sampleRate * 2;
    const noiseBuf = this.ctx.createBuffer(1, bufSize, this.ctx.sampleRate);
    const data = noiseBuf.getChannelData(0);
    for (let i = 0; i < bufSize; i++) data[i] = Math.random() * 2 - 1;
    const noise = this.ctx.createBufferSource();
    noise.buffer = noiseBuf;
    noise.loop = true;

    this.screechFilter = this.ctx.createBiquadFilter();
    this.screechFilter.type = 'bandpass';
    this.screechFilter.frequency.value = 1350;
    this.screechFilter.Q.value = 6.5;

    this.screechGain = this.ctx.createGain();
    this.screechGain.gain.value = 0.0;

    noise.connect(this.screechFilter);
    this.screechFilter.connect(this.screechGain);
    this.screechGain.connect(this.masterGain);

    this.engineOsc1.start();
    this.engineOsc2.start();
    this.turboOsc.start();
    noise.start();
    this.running = true;
  }

  updateEngine(rpm: number, throttle: number, slip: number, active: boolean) {
    if (!this.ctx || !this.running || !this.engineOsc1 || !this.engineOsc2 || !this.engineFilter || !this.engineGain || !this.turboOsc || !this.turboGain || !this.screechGain) {
      return;
    }
    const now = this.ctx.currentTime;
    if (!active || !this.enabled) {
      this.engineGain.gain.setTargetAtTime(0, now, 0.06);
      this.turboGain.gain.setTargetAtTime(0, now, 0.06);
      this.screechGain.gain.setTargetAtTime(0, now, 0.06);
      return;
    }

    // V6 F1 fundamental harmonic mapping
    const baseFreq = 68 + (rpm / 13500) * 340;
    this.engineOsc1.frequency.setTargetAtTime(baseFreq, now, 0.03);
    this.engineOsc2.frequency.setTargetAtTime(baseFreq * 1.5, now, 0.03);
    this.engineFilter.frequency.setTargetAtTime(220 + (rpm / 13500) * 1450 + throttle * 420, now, 0.04);
    this.engineGain.gain.setTargetAtTime(0.18 + throttle * 0.24, now, 0.04);

    // Turbo whine
    this.turboOsc.frequency.setTargetAtTime(1400 + (rpm / 13500) * 2600, now, 0.05);
    this.turboGain.gain.setTargetAtTime(throttle * 0.035, now, 0.06);

    // Tire screech on high lateral slip
    this.screechGain.gain.setTargetAtTime(Math.min(0.22, slip * 0.24), now, 0.04);
  }

  playShiftPop() {
    if (!this.ctx || !this.masterGain || !this.enabled) return;
    const now = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = 'square';
    osc.frequency.setValueAtTime(140, now);
    osc.frequency.exponentialRampToValueAtTime(55, now + 0.07);
    g.gain.setValueAtTime(0.22, now);
    g.gain.exponentialRampToValueAtTime(0.001, now + 0.075);
    osc.connect(g);
    g.connect(this.masterGain);
    osc.start(now);
    osc.stop(now + 0.08);
  }

  playCollision(intensity: number) {
    if (!this.ctx || !this.masterGain || !this.enabled || intensity < 0.08) return;
    const now = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = 'triangle';
    osc.frequency.setValueAtTime(125, now);
    osc.frequency.exponentialRampToValueAtTime(32, now + 0.18);
    g.gain.setValueAtTime(Math.min(0.5, intensity * 0.45), now);
    g.gain.exponentialRampToValueAtTime(0.001, now + 0.2);
    osc.connect(g);
    g.connect(this.masterGain);
    osc.start(now);
    osc.stop(now + 0.21);
  }

  playStartBeep(go = false) {
    if (!this.ctx || !this.masterGain || !this.enabled) return;
    const now = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(go ? 980 : 520, now);
    g.gain.setValueAtTime(0.28, now);
    g.gain.exponentialRampToValueAtTime(0.001, now + (go ? 0.35 : 0.14));
    osc.connect(g);
    g.connect(this.masterGain);
    osc.start(now);
    osc.stop(now + (go ? 0.36 : 0.15));
  }

  playClick() {
    if (!this.ctx || !this.masterGain || !this.enabled) return;
    const now = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(660, now);
    osc.frequency.exponentialRampToValueAtTime(420, now + 0.045);
    g.gain.setValueAtTime(0.12, now);
    g.gain.exponentialRampToValueAtTime(0.001, now + 0.05);
    osc.connect(g);
    g.connect(this.masterGain);
    osc.start(now);
    osc.stop(now + 0.055);
  }
}
