# MOTOVELO — 2026 Formula 1 Native Racing Simulation (Unity 6.3 LTS + URP)

**MOTOVELO** is an original, F1-grade 3D open-wheel motorsport game built natively in **Unity 6.3 LTS (`6000.3.29f1`)** with the **Universal Render Pipeline (URP)**, **IL2CPP ARM64** native Android compilation, and simultaneous **WebGL Browser** deployment.

## Key Gameplay & Simulation Highlights
- **2026 11-Team / 22-Driver Grid & Sculpted Replica Car**: Multi-part 2026 F1 chassis (`formula_car.fbx`) UV-mapped to `2026 LIVERY-TEMPLATE.PNG` with articulated double-wishbone wheels, apex-tracking driver helmet, and animated `-42°` DRS rear-wing flap + glowing green wingtip LEDs.
- **20 Real-World F1 GPS Circuits with 3D Elevation & Banking**: 960-sample Catmull-Rom 3D splines featuring circuit-authentic elevation changes (`15m–44m` climbs/plunges), progressive corner banking (`9.5°–16.5°`), enclosed `3.8m` double-sided FIA wire-mesh catch-fencing, stadium grandstands, floodlight towers, and marked glowing green **DRS** & cyan **ERS** zones.
- **Physical 3D FIA Pit Lane & 10-Man Pit Crew**:
  - Open physical **Starting-Region Pit Entry** (`+12m..+84m`) and **Lap-End Pit Entry** (`-225m..-118m`) separated from the main straight by a solid Concrete FIA Pit Wall, `PIT IN` chevrons, and `PIT` / `80` KPH speed-limit boards.
  - Waiting **10-man team-suited Pit Crew** + **Overhead Pneumatic Pit Boom** performing a live `2.50s` tyre swap (`+0.15m` front/rear jack lift, 4 wheel gunners with blue impact sparks, 4 tyre carriers, real-time Pirelli sidewall swap, and Broadcast Pit-Box Camera).
  - Full **FIA Sporting Regulations**: `80 KPH` Pit Speed Limiter, `+3.0s` FIA Stewards speeding penalty, Mandatory 2-Compound Dry Tyre rule (`+10.0s` post-race penalty if skipped in `>= 3` lap races), Solid White Pit Exit Line warning, and `NEXT: MED` (`1–5` / `P`) pre-pit compound radio.
- **60Hz Powertrain & Competitive 300+ KPH AI Pack**: Smooth progressive throttle curve, seamless 8-speed automatic/manual gearbox (`309+ KPH` top speed), MGU-K ERS deploy/harvest, slipstream tow, and 21 competitive AI rivals that launch cleanly off the grid and execute mid-race pit strategies.
- **Warm PolyBLEP V6 Turbo Hybrid Audio**: Band-limited PolyBLEP sawtooth + sub-harmonic rumble + 3-pole `18 dB/oct` low-pass filter (`ProceduralV6Audio.cs`) with instant HUD `SFX ON / MUTED` toggle.

## Repository Layout
- `MotoveloUnity/` — Primary **Unity 6.3 LTS (`6000.3.29f1`) URP** project (`Assets/Scripts/Motovelo/`, `Assets/Editor/MotoveloBuilder.cs`, `Assets/Resources/`)
- `MOTOVELO-WebGL/` — Compiled Unity WebGL browser build (served locally with Gzip decompression fallback)
- `MOTOVELO.apk` — Compiled & v2-signed native Android ARM64 IL2CPP APK (`com.motovelo.racing`)
- `motovelo-assets/` — Blender 5.2 Python procedural sculpting scripts (`build_2026_f1_replica.py`, `upgrade_trackside_assets.py`), `.glb`/`.fbx` exports, and `unity_staging/` mirror
- `docs/PIT_STOP_AND_REGULATIONS.md` — Detailed technical specification of the 3D Pit Lane, 10-man Pit Crew choreography, and FIA Sporting Regulations
- `motovelo-web/` & `motovelo-android/` — Initial Session 1 Three.js WebGL2 prototype & Android wrapper

## Building & Running (Unity 6.3 LTS CLI)

```powershell
# 1. Build WebGL Browser Preview -> MOTOVELO-WebGL/
unity build .\MotoveloUnity --target WebGL --execute-method Motovelo.Editor.MotoveloBuilder.BuildWebGL --output .\MOTOVELO-WebGL

# 2. Serve WebGL Preview Locally on http://localhost:8088
python -m http.server 8088 --directory .\MOTOVELO-WebGL

# 3. Build Signed Native Android ARM64 APK -> MOTOVELO.apk
unity build .\MotoveloUnity --target Android --execute-method Motovelo.Editor.MotoveloBuilder.BuildAndroidApk --output .\MOTOVELO.apk
```
