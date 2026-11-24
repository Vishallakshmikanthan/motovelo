# MOTOVELO — TODO & Roadmap

## P0 — Complete Playable Game
- [x] Inspect environment, Android SDK, Blender 5.2, and reference APK via REA MCP
- [x] Create `FEATURE_MATRIX.md`, `DEVELOPMENT_LOG.md`, `TODO.md`, `README.md`
- [x] Produce original 3D Formula car, wheels, grandstands, pit building, and start gantry `.glb` assets in Blender 5.2
- [x] Scaffold Vite + Three.js game project (`motovelo-web`)
- [x] Implement MOTOVELO branding, loading screen, and main menu
- [x] Implement 60Hz Formula vehicle physics (engine, 8-speed gearbox, aero downforce/drag, tire slip, collisions)
- [x] Implement 3D circuit generator + Autodromo Veloce GP circuit with kerbs, barriers, grandstands, pit lane, gantry
- [x] Implement 5 camera modes (Chase, T-Cam, Cockpit, Nose, Trackside)
- [x] Implement 7 competitive AI Formula cars (racing line, braking zones, overtaking, collision avoidance)
- [x] Implement race loop (8-car grid, 5-red-light F1 start, checkpoints, lap timing, position tracking, finish & results, restart)
- [x] Implement broadcast racing HUD & responsive landscape multi-touch mobile controls
- [x] Build native Android wrapper project (`motovelo-android`) and compile `MOTOVELO.apk`
- [x] Verify compiled `MOTOVELO.apk` package integrity (manifest, v2 signature, bundled `.glb` & JS/CSS assets)
- [ ] On-device / emulator interactive gameplay verification (requires launching AVD `Pixel_7` or connecting a physical Android device via ADB)

## P1 — Game Completeness
- [x] Implement 3D Garage with 6 original Formula teams/liveries & interactive turntable
- [x] Add 2 additional circuits (Port Azzurro Harbor GP & Fujiyama Technical GP)
- [x] Implement Championship Mode & Time Trial Mode
- [x] Implement procedural Web Audio V6 Turbo F1 engine, shift, tire screech, collision, start-light & menu audio
- [x] Implement Settings (auto/manual gear, graphics quality, audio toggle, steering sensitivity) & Credits
