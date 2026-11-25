# MOTOVELO — Roadmap & Completed Milestones

## Phase 1 — Initial WebGL2 & Android Prototype (Completed)
- [x] Inspect reference architecture via REA MCP and establish 100% original asset/code pipeline
- [x] Scaffold initial Three.js + TypeScript prototype (`motovelo-web`) & Android wrapper (`motovelo-android`)

## Phase 2 — Native Unity 6.3 LTS (`6000.3.29f1`) + URP Migration (Completed)
- [x] Sculpt 2026 F1 Replica chassis (`formula_car.fbx`), wheel (`formula_wheel.fbx`), and standing driver (`driver_standing.fbx`) in Blender 5.2 mapped to `2026 LIVERY-TEMPLATE.PNG`
- [x] Import all 11 2026 F1 team liveries, driver helmets, race suits, Pirelli tyre compounds, and 20 real-world GPS circuit GeoJSON layouts into `MotoveloUnity`
- [x] Configure Android IL2CPP ARM64 build (`UnityPlayerActivity` + `OpenGLES3` + unified URP `PC_RPAsset` + embedded `MotoveloFont.ttf` + `link.xml`) resolving mobile blank-screen startup issues
- [x] Configure simultaneous Unity WebGL Gzip decompression-fallback browser build (`MOTOVELO-WebGL`)

## Phase 3 — F1 Stadium Circuit, Physics, AI, Graphics & Audio Overhaul (Completed)
- [x] Inject authentic 3D circuit elevation profiles (`15m–44m` climbs/plunges) and progressive corner banking (`9.5°–16.5°`)
- [x] Enclose circuits with double-sided `3.8m` FIA wire-mesh debris catch-fencing, 360° mountain ring, stadium grandstands, floodlight towers, and strict 960-sample track-clearance prop guards
- [x] Upgrade trackside Blender assets (`grandstand.fbx`, `pit_building.fbx`, `start_gantry.fbx`, `light_tower.fbx`, `tree_cluster.fbx`) and add procedural URP skybox + 4× MSAA
- [x] Tune smooth progressive throttle, 8-speed gearbox (`309.3 KPH` top speed), marked green DRS / cyan ERS zones, and `-42°` animated DRS rear-wing flap
- [x] Upgrade 21 AI cars to full `300+ KPH` race pace with zero Lap-1 grid stalling
- [x] Rewrite `ProceduralV6Audio.cs` with band-limited PolyBLEP oscillator + 3-pole warm low-pass filter + HUD `SFX ON / MUTED` button

## Phase 4 — Physical 3D FIA Pit Lane, 10-Man Pit Crew & Sporting Regulations (Completed)
- [x] Sculpt `pit_crew_member.fbx` and `pit_boom.fbx` in Blender 5.2 and import into `MotoveloUnity`
- [x] Build physical 3D Pit Lane road with Starting-Region (`+14m..+82m`) and Lap-End (`-225m..-120m`) Pit Entry Mouths, Concrete Pit Wall, `PIT IN` chevrons, `PIT`/`80` boards, and Solid White Pit Exit Line
- [x] Implement `PitStopCrewController.cs` (`2.50s` 10-man team-suited pit stop: Front/Rear Jackmen `+0.15m` lift, 4 Wheel Gunners + sparks, 4 Tyre Carriers, real-time Pirelli sidewall swap, overhead Stop/Go boom)
- [x] Enforce FIA Pit Regulations (`80 KPH` Pit Speed Limiter, `+3.0s` speeding penalty, Mandatory 2-Compound Dry Tyre `+10.0s` rule, Pit Exit white-line warning, AI mid-race pit stops)
- [x] Replace fake HUD pit button with `NEXT: MED` (`1–5`/`P`) pre-pit compound radio + live FIA Pit Crew Stopwatch banner and rebuild both `MOTOVELO-WebGL` & signed `MOTOVELO.apk`
