# MOTOVELO — Formula Motorsport Racing

**MOTOVELO** is an original, high-octane 3D Formula racing game for Android (and WebGL2), featuring Blender-produced 3D assets, 60Hz Formula aerodynamics & tire physics, competitive AI racers, 5 broadcast/cockpit cameras, procedural V6 Turbo Hybrid audio, 3 circuits, 6 teams, Quick Race, Time Trial, and Championship modes.

## Project Structure
- `motovelo-assets/` — Blender 5.2 Python generator scripts & exported `.glb` 3D models
- `motovelo-web/` — Core 3D game engine (Three.js, Physics, AI, Circuits, HUD, Audio, Garage)
- `motovelo-android/` — Native Android Gradle project (`com.motovelo.racing`) with `WebViewAssetLoader` & immersive landscape mode
- `MOTOVELO.apk` — Final compiled Android APK

## Build Instructions
```bash
# 1. Build web game bundle
cd motovelo-web
npm install
npm run build

# 2. Copy bundle into Android assets and build APK
cd ../motovelo-android
./gradlew assembleDebug
```
