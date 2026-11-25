# MOTOVELO — Feature Matrix (Unity 6.3 LTS URP + WebGL + Android)

Reference architecture benchmarked against `Monoposto.apk` (`com.gabama.monopostolite`, Unity IL2CPP/Burst).
All MOTOVELO C# scripts, Blender 5.2 `.fbx`/`.glb` 3D models, URP materials, native uGUI layouts, and DSP audio synthesizers are **100% original**.

| System | Feature | Priority | Status | Implementation Details |
| :--- | :--- | :--- | :--- | :--- |
| **Engine & Targets** | Unity 6.3 LTS (`6000.3.29f1`) + URP | P0 | Verified | Simultaneous **Android IL2CPP ARM64 APK** (`OpenGLES3`, `UnityPlayerActivity`) + **WebGL Gzip** browser build (`MotoveloBuilder.cs`) |
| **Showroom & Roster** | 2026 11-Team / 22-Driver F1 Grid | P0 | Verified | Mirror-floor 3D Showroom Hub, Driver/Suit/Helmet selector, Car Aero Setup, and 20-Circuit GPS selector (`MotoveloData.cs`) |
| **2026 F1 Visuals** | Sculpted Multi-Part 2026 F1 Car | P0 | Verified | UV-mapped to `2026 LIVERY-TEMPLATE.PNG`, `-42°` animated DRS rear-wing flap + green wingtip LEDs, jack lift, live Pirelli tyre swap (`FormulaCarVisuals.cs`) |
| **3D Circuits** | Steep Elevation, Banking & Enclosures | P0 | Verified | 20 real GPS circuits with `15m–44m` 3D climbs/plunges, `9.5°–16.5°` progressive corner banking, `3.8m` double-sided FIA catch-fencing, 360° mountain ring, zero-intrusion prop guard (`SplineCircuit.cs`) |
| **DRS & ERS Zones** | Marked Trackside & Surface Zones | P0 | Verified | Glowing green **DRS DETECTION / ACTIVATION** strips & boards (`100%` straight activation) + cyan **ERS HARVEST** braking chevrons (`SplineCircuit.cs`) |
| **3D Pit Stops** | Physical Starting-Region & Lap-End Pit Lane | P0 | Verified | Paved parallel Pit Lane road, open Starting-Region (`+14m..+82m`) & Lap-End (`-225m..-120m`) mouths, Concrete Pit Wall, `PIT IN` chevrons, `PIT`/`80` boards (`SplineCircuit.cs`) |
| **Pit Crew** | 10-Man Team-Suited Pit Crew & Boom | P0 | Verified | Front/Rear Jackmen (`+0.15m` lift), 4 Wheel Gunners + blue sparks, 4 Tyre Carriers + spare Pirelli wheels, overhead pneumatic Stop/Go boom (`PitStopCrewController.cs`) |
| **FIA Regulations** | 80 KPH Limiter, Speeding & 2-Compound Rules | P0 | Verified | `80 KPH` pit limiter, `+3.0s` speeding penalty, Mandatory 2-Compound rule (`+10.0s` penalty if skipped in `>=3` laps), white pit-exit line check (`FormulaCarPhysics.cs`) |
| **Powertrain Physics** | Smooth 60Hz Aero & 8-Speed Gearbox | P0 | Verified | Progressive `pow(1.35)` throttle curve, 8-speed seamless gearbox (`309+ KPH` verified), corner-entry trail-braking assist without straight-line speed cuts (`FormulaCarPhysics.cs`) |
| **AI Pack Racing** | 21 Competitive 300+ KPH Rivals + AI Pits | P0 | Verified | Zero Lap-1 stalling, curvature-lookahead braking, lateral slipstream overtakes, and scheduled mid-race pit-lane tyre stops (`FormulaAICar.cs`) |
| **Camera System** | 6 Broadcast & Onboard Cameras | P0 | Verified | T-Cam, Cockpit, Chase, Close Chase, Trackside TV, Rear Look-Back + automatic **Broadcast Pit-Box Camera** during pit stops (`MonopostoCameraRig.cs`) |
| **Audio DSP** | PolyBLEP V6 Hybrid Synth + Mute Toggle | P0 | Verified | Band-limited PolyBLEP sawtooth + 3-pole `18 dB/oct` warm low-pass filter, subtle MGU-H shimmer, shift cut, kerb rumble, HUD `SFX ON / MUTED` (`ProceduralV6Audio.cs`) |
| **Native uGUI HUD** | Strategy Radio, Steward Banner & Touch | P0 | Verified | `NEXT: MED` (`1–5`/`P`) compound selector, live FIA Pit Stopwatch & Steward Notice banner, timing tower, GPS radar, multi-touch pedals (`MotoveloUIManager.cs`) |
