# Physical 3D FIA Pit Lane, 10-Man Pit Crew & Sporting Regulations

This document details the architecture and FIA rule enforcement implemented across `SplineCircuit.cs`, `PitStopCrewController.cs`, `FormulaCarPhysics.cs`, `FormulaCarVisuals.cs`, `FormulaAICar.cs`, `MonopostoCameraRig.cs`, `MotoveloGameManager.cs`, and `MotoveloUIManager.cs`.

---

## 1. Physical 3D Pit Lane Geometry (`SplineCircuit.cs`)

Instead of an arcade HUD teleport button, every circuit features a paved, physically drivable **3D FIA Pit Lane** running parallel to the main start/finish straight on the clear paddock side (`pitSide = +1` right or `-1` left, offset `halfWidth + 6.0m`):

| Track Signed Distance (`signedS`) | Pit Complex Feature |
| :--- | :--- |
| **`-235m .. +270m`** | Smooth `+11.5m` outer FIA catch-fence & barrier expansion creating an enclosed paddock bay |
| **`-225m .. -120m`** | **Lap-End Pit Entry Mouth** — open kerb break with glowing amber `PIT IN` asphalt chevrons |
| **`-120m`** | Lap-End **White `80 KPH` Pit Speed Limit Line** + trackside `PIT` & red-ringed `80` boards |
| **`-120m .. +14m`** | **Concrete FIA Pit Wall Segment A** separating the pit lane from the main straight |
| **`+14m .. +82m`** | **Starting-Region Pit Entry Mouth** — immediate open entry right past the Start Gantry with glowing amber `PIT IN` chevrons so drivers can pit right in the starting region |
| **`+82m`** | Starting-Region **White `80 KPH` Pit Speed Limit Line** + `80` KPH board |
| **`+82m .. +198m`** | **Concrete FIA Pit Wall Segment B** protecting the active Pit Box service lane |
| **`+138m` (`pitBoxSignedS`)** | **Painted Team Pit Box**, **3-Story Pit Garage Building**, **Overhead Pneumatic Boom**, and **Waiting 10-Man Pit Crew** |
| **`+198m`** | **Pit Exit Marshal Post** with glowing green `PIT EXIT` light; Pit Speed Limiter disengages |
| **`+198m .. +260m`** | **FIA Solid White Pit Exit Line** separating rejoining cars from 300+ KPH main-straight traffic |

> **Arc-Length Accuracy**: `SplineCircuit.GetSampleAtDistance(float s)` uses an exact binary search over cumulative arc-length `samples[mid].s` so all pit walls, speed-limit lines, and pit-box coordinates align to sub-meter precision regardless of GeoJSON control-point spacing.

---

## 2. 10-Man Team-Suited Pit Crew & `2.50s` Choreography (`PitStopCrewController.cs`)

Sculpted in Blender 5.2 (`pit_crew_member.fbx` & `pit_boom.fbx`) and dynamically textured in the player's chosen 2026 constructor suit & helmet livery:

1. **Overhead Pneumatic Pit Boom**: Cantilevered steel gantry arms suspending compressed-air wheel-gun hoses and a live **Red (`STOP`) / Green (`GO!`)** LED status pod.
2. **Front Jackman (`+2.85m`) & Rear Jackman (`-2.65m`)**: Step into the nosecone and rear diffuser as the car stops, pivoting their hydraulic jacks to lift `FormulaCarVisuals.bodyRoot` **`+0.150m`** (`0.00s–0.38s`), holding steady during wheel service (`0.38s–2.12s`), and dropping the car cleanly (`2.12s–2.50s`).
3. **4 Wheel Gunners (`FL`, `FR`, `RL`, `RR`)**: Rush inward (`0.42m` travel) onto each wheel hub, firing animated **blue impact-wrench sparks** while loosening and re-torquing center-lock wheel nuts.
4. **4 Tyre Carriers (`FL`, `FR`, `RL`, `RR`)**: Hold 3D Pirelli spare wheels pre-wrapped in the compound selected via the player's `NEXT` radio button. At **`1.25s`** (`50%` through the stop), `FormulaCarVisuals.SwapTyreCompound` swaps all four mounted tyre sidewalls in real time, restores `tyreHealth = 100%`, and resets optimal `90°C` operating temperature.
5. **Broadcast Pit-Box Camera (`MonopostoCameraRig.cs`)**: Automatically frames an elevated front-3/4 F1 TV pit-box angle during `PitLanePhase.ServicingInBox` so all 10 mechanics, jacks, sparks, and compound swaps are clearly visible.

---

## 3. FIA Sporting Regulations (`FormulaCarPhysics.cs`)

- **Pit Lane State Machine (`PitLanePhase`)**: `OnTrack` -> `EnteringPitLane` -> `ServicingInBox` -> `ExitingPitLane` -> `OnTrack`.
- **80 KPH Pit Speed Limiter & Speeding Penalty**: Crossing the `80 KPH` line engages `pitLimiterActive` (`22.22 m/s`). Entering across the line above the FIA speed threshold (`> 112 KPH`) triggers an immediate **`+3.0s` FIA Stewards Time Penalty** and HUD steward notice.
- **Mandatory 2-Compound Dry Tyre Rule**: `usedCompoundsMask` tracks distinct tyre compounds fitted during the race. In races of **`>= 3` laps**, finishing on a single compound incurs an official **`+10.0s` FIA Post-Race Penalty** applied to `finishTime` in final standings.
- **Solid White Pit Exit Line Warning**: Crossing back across `halfWidth` between `+199m` and `+252m` after pitting raises an official FIA Steward warning.
- **AI Pit Strategy (`FormulaAICar.cs`)**: AI competitors monitor tyre wear (`< 46%`) and mandatory compound rules, physically steering into the pit lane on scheduled middle laps to swap compounds.
