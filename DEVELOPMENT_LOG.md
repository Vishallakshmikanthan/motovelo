# MOTOVELO — Development Log

## Session 1 — 2026-11-24: Reference Analysis & Initial WebGL2 Prototype
- Inspected `Monoposto.apk` via REA MCP (`com.gabama.monopostolite`, Unity IL2CPP/Burst) to establish feature parity targets while keeping 100% original code, Blender 5.2 3D models, UI, and procedural audio.
- Built initial Three.js + TypeScript prototype (`motovelo-web`) and Android `WebViewAssetLoader` wrapper (`motovelo-android`).

---

## Session 2 — 2026-11-25: Native Unity 6.3 LTS (`6000.3.29f1`) + URP Migration & Dual WebGL/APK Setup
- **2026 F1 Replica 3D Pipeline**:
  - Created `motovelo-assets/build_2026_f1_replica.py` in Blender 5.2 to sculpt a multi-part 2026 Formula 1 car (`formula_car.fbx`), Pirelli wheel (`formula_wheel.fbx`), and standing driver (`driver_standing.fbx`) UV-mapped directly to `2026 LIVERY-TEMPLATE.PNG`.
  - Imported all 11 2026 constructors, 22 drivers, helmets, race suits, 5 Pirelli compounds (Soft, Medium, Hard, Inter, Wet), and 20 real-world GPS circuit GeoJSON splines into `MotoveloUnity/`.
- **Android Blank-Screen Root-Cause Fix (`v2.1.0`)**:
  - Switched Android entry point from `UnityPlayerGameActivity` to `AndroidApplicationEntry.Activity` (`com.unity3d.player.UnityPlayerActivity`), forced universal `OpenGLES3`, disabled `m_UseNativeRenderPass` on mobile URP renderer, unified quality levels onto `PC_RPAsset.asset`, set `stripEngineCode = false`, added `Assets/link.xml`, and embedded `MotoveloFont.ttf`.
- **Simultaneous WebGL + Android Build Pipeline**:
  - Added `MotoveloBuilder.BuildWebGL()` (`Gzip` + `decompressionFallback = true`) alongside `BuildAndroidApk()` so every gameplay update outputs both `MOTOVELO-WebGL/` and signed `MOTOVELO.apk`.

---

## Session 3 — 2026-11-25: F1 Stadium Circuit, Powertrain, Pack AI, Graphics & Audio Overhaul
- **Enclosed 3D Stadium Circuits (`SplineCircuit.cs`)**:
  - Added circuit-specific 3D elevation (`15m–44m` climbs and plunges, e.g., `32.3m` Raidillon-style crest on `heritage-circuit`) and smoothed progressive corner banking (`9.5°–16.5°`).
  - Built continuous `3.8m` double-sided FIA wire-mesh debris catch-fencing, a 360° mountain/stadium horizon ring, upgraded multi-tier grandstands (`grandstand.fbx`), 3-story VIP paddock buildings (`pit_building.fbx`), floodlight towers (`light_tower.fbx`), and strict 960-sample `IsClearOfAllTrackSegments` guards preventing any prop from intruding onto the asphalt.
- **Powertrain, DRS Flap Animation & Marked Zones (`FormulaCarPhysics.cs`, `FormulaCarVisuals.cs`)**:
  - Removed straight-line auto-braking speed cuts, smoothed throttle application (`pow(1.35)` + `8.5/s` slew), tuned seamless 8-speed automatic shifting (`309.3 KPH` verified in 8th gear), and animated the rear-wing DRS flap (`-42°` pitch + glowing green wingtip LEDs) across green-painted DRS zones and cyan ERS harvest braking zones.
- **Competitive 300+ KPH AI Pack (`FormulaAICar.cs`)**:
  - Eliminated Lap-1 launch hesitation and raised AI straight-line pace to `300.3 KPH` with dt-scaled wheel-rub collision resolution (`MotoveloGameManager.cs`).
- **Warm PolyBLEP V6 Hybrid Synth (`ProceduralV6Audio.cs`)**:
  - Replaced harsh raw sine oscillators with a band-limited PolyBLEP sawtooth + sub-harmonic rumble + 3-pole cascaded `18 dB/oct` low-pass filter (`~920 Hz`–`3.4 kHz`) and added an instant `SFX ON / MUTED` HUD button.

---

## Session 4 — 2026-11-25: Physical 3D FIA Pit Lane, 10-Man Pit Crew & Sporting Regulations
- **Physical Starting-Region & Lap-End Pit Lane (`SplineCircuit.cs`)**:
  - Upgraded `GetSampleAtDistance` to binary-search cumulative arc-length `samples[mid].s` for exact meter-accurate pit placement.
  - Built a paved parallel 3D Pit Lane (`-230m..+265m`) with **two physical Pit Entry Mouths** — right in the **Starting Region** (`+14m..+82m` past the Start Gantry) and at **Lap End** (`-225m..-120m`) — separated from the track by Concrete FIA Pit Walls, glowing amber `PIT IN` chevrons, `PIT` & `80` KPH speed-limit signs/lines, `PIT EXIT` green marshal light (`+198m`), and a Solid White Pit Exit Line (`+198m..+260m`).
- **10-Man Team-Suited Pit Crew & Pneumatic Boom (`PitStopCrewController.cs`)**:
  - Sculpted `pit_crew_member.fbx` and `pit_boom.fbx` in Blender 5.2 (`upgrade_trackside_assets.py`).
  - Spawned 10 team-liveried mechanics (Front/Rear Jackmen lifting the chassis `+0.150m`, 4 Wheel Gunners firing blue impact sparks, 4 Tyre Carriers holding compound-matched Pirelli spare wheels) + overhead Stop/Go pneumatic boom executing a `2.50s` live tyre change with real-time sidewall swapping and Broadcast Pit-Box Camera framing.
- **FIA Sporting Regulations & Strategy Radio (`FormulaCarPhysics.cs`, `MotoveloUIManager.cs`)**:
  - Implemented `80 KPH` Pit Speed Limiter, `+3.0s` FIA Stewards speeding penalty, Mandatory 2-Compound Dry Tyre rule (`+10.0s` post-race penalty if skipped in `>= 3` lap races), Solid White Pit Exit Line warning, AI mid-race pit stops, and replaced the old fake HUD pit button with a `NEXT: MED` (`1–5`/`P`) strategy radio selector + live FIA Pit Crew Stopwatch banner.
- **Play Mode & Dual Build Verification**:
  - Verified in Unity 6.3 LTS Play Mode (`0` console errors; `snappedService=True`, `jackBodyY=0.150m`, `pitStops=1`, `newCompound=MED`, `tyreHealth=100%`, `mandatoryOk=True`).
  - Rebuilt `MOTOVELO-WebGL/` (`15.1 MB` `.wasm`) and v2-signed `MOTOVELO.apk` (`54.8 MB` ARM64 IL2CPP).
