# MOTOVELO — Feature Matrix

Reference APK inspected via **REA (Reverse Engineer Anything) MCP**: `Monoposto.apk` (`com.gabama.monopostolite` v6.74, Unity IL2CPP/Burst).
All MOTOVELO code, 3D models (produced in Blender 5.2), shaders, UI, and audio are **100% original**.

| System | Feature | Priority | Status | Implementation Details |
|---|---|---|---|---|
| **Branding & UI** | MOTOVELO Logo & Identity | P0 | Implemented | Original motorsport red/carbon/gold visual identity + custom vector APK icon |
| **Branding & UI** | Loading Screen | P0 | Implemented | Animated telemetry loading bar + GLB preload (`AssetLoader.ts`) |
| **Main Menu** | Play / Quick Race | P0 | Implemented | Instant 8-car grid race with selectable circuit, laps (2/3/5), & AI difficulty |
| **Main Menu** | Championship Mode | P1 | Implemented | 3-round points series (F1 25-18-15-12-10-8-6-4) with localStorage persistence |
| **Main Menu** | Time Trial Mode | P1 | Implemented | Solo hotlap session with lap & best-lap timing |
| **Main Menu** | Garage & Vehicle Selection | P1 | Implemented | 3D turntable showroom with 6 original Formula teams/liveries & spec bars |
| **Main Menu** | Settings & Credits | P1 | Implemented | Auto/Manual gearbox, High/Low graphics, Audio toggle, Steering sensitivity |
| **Vehicle Physics** | Formula Car Dynamics | P0 | Implemented | 60Hz fixed-step physics: throttle, braking, speed-sensitive steering, downforce ($v^2$), drag |
| **Vehicle Physics** | 8-Speed Sequential Gearbox | P0 | Implemented | RPM curve (4,800–13,500), rev limiter, shift-up/down paddles, auto & manual modes |
| **Vehicle Physics** | Tire Grip & Slip Model | P0 | Implemented | Lateral grip limit, slip angle, kerb/off-track grip & drag modifiers |
| **Vehicle Physics** | DRS / ERS Boost | P1 | Implemented | Rechargeable ERS battery, boost acceleration, animated rear-wing DRS flap |
| **Vehicle Physics** | Collisions & Wheel Spin | P0 | Implemented | Track barrier response, car-to-car collision impulse, spinning & steering front wheels |
| **Camera System** | 5 Racing Cameras | P0 | Implemented | Chase, T-Cam, Cockpit (Halo), Front Nose, Trackside Broadcast (`CameraRig.ts`) |
| **Circuits** | Autodromo Veloce GP | P0 | Implemented | High-speed Monza-inspired circuit with kerbs, barriers, pit building, grandstands, gantry |
| **Circuits** | Port Azzurro GP | P1 | Implemented | Golden-hour harbor street circuit with water, elevation, tight barriers |
| **Circuits** | Fujiyama Technical GP | P1 | Implemented | Twilight technical S-curves & sweeping corners |
| **Racing System** | Grid, Lights, Laps & Timing | P0 | Implemented | 8-car grid, 5-red-light F1 start sequence (HUD + 3D gantry), 3-sector lap timing, live standings |
| **Racing System** | Results & Restart | P0 | Implemented | Classification table, gaps, F1 points, best lap, instant restart & next race |
| **AI Competitors** | Autonomous Racers | P0 | Implemented | 7 AI Formula cars with curvature braking zones, overtaking, collision avoidance, ERS |
| **Racing HUD** | Broadcast Telemetry HUD | P0 | Implemented | Speed (KM/H), 15-LED RPM bar, Gear, Position, Lap, Lap/Best Time, Live Minimap, ERS bar |
| **Mobile Controls** | Landscape Touch Controls | P0 | Implemented | Multi-touch pointer controls (Steer, Gas, Brake, ERS/DRS, G+/G-, Camera, Pause) + keyboard |
| **3D Assets** | Blender 5.2 GLB Pipeline | P0 | Implemented | 5 exported `.glb` models (`formula_car`, `formula_wheel`, `grandstand`, `pit_building`, `start_gantry`) |
| **Audio** | Procedural Web Audio F1 Synth | P1 | Implemented | V6 Turbo engine harmonics, turbo whine, shift pop, tire screech, collisions, start beeps |
| **Android Build** | Native Android APK (`MOTOVELO.apk`) | P0 | Built & Signed | Hardware-accelerated Android `WebViewAssetLoader` wrapper, immersive landscape, verified via `aapt2` & `apksigner` |
