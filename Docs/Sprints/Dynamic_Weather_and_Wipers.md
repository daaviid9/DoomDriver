# Dynamic Weather Variations (Rain, Fog, Wet Roads & Windshield Wipers)

Branch: `alternating-weather`

## Overview

This feature introduces dynamic weather atmosphere presets to **DoomDriver**, linking exterior falling rain, realistic raindrops on the car's windshield and side windows, automatic windshield wipers, wet asphalt reflections, and volumetric-style road fog to the **Daily Shift Briefing**.

The system operates seamlessly across the existing courier loop (`OrderSelection` → `DeliveryActive` → `DeliveryCompleted` / `DeliveryFailed`) with smooth transitions, zero per-frame allocations in `Update()`, and full Unity 6 / URP compatibility.

---

## 1. Key Assets & Components

### A. Scripts
* [`WeatherPreset.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Environment/WeatherPreset.cs): Defines the `WeatherCondition` enum (`Clear`, `Foggy`, `LightRain`, `RainAndFog`) and `WeatherPreset` `ScriptableObject` storing fog settings, rain emission/speed/tint, wet road reflection parameters, lighting multipliers, and daily briefing headlines. Includes built-in runtime fallbacks if no asset is assigned.
* [`WeatherManager.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Environment/WeatherManager.cs): Central singleton controller mounted on `GameManagers` in `EnviromentScene.unity`. Smoothly interpolates `RenderSettings.fog` (`fogDensity`, `fogColor`, `fogMode`), directional light intensity, camera horizon color, wet road reflections, exterior rain, glass raindrop shader parameters, and automatic wipers. Supports cycling presets at runtime via **`F7`** (Unity New Input System).
* [`DailyShiftBriefing.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Core/DailyShiftBriefing.cs): Manages daily shift progression (day number, weekday name, dispatcher headline, active weather condition) and synchronizes weather automatically with `WeatherManager`. Exposes `SetDispatcherBriefing(string)` and `AdvanceToNextDay()` as a decoupled integration contract for Dev 5's `ShiftBriefingUI`.
* [`WindshieldRainEffect.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Environment/WindshieldRainEffect.cs): Attached to `WindshieldRainParticles` under `Main Camera`. Emits exterior falling rain streaks ahead of the hood (`localEmitterOffset = (0, 5.0, 15.5)`) and enforces a strict cabin exclusion filter (`exteriorMinZ = 4.2m` in `LateUpdate()` using a pre-allocated buffer) so raindrops **never** enter the car interior. Dynamically slants rain streaks based on `WorldSpeedController.Instance.CurrentSpeed`.
* [`WindshieldWiperController.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Environment/WindshieldWiperController.cs): Mounted on `GameManagers`. Automatically activates dual windshield wipers during rain (`currentRainIntensity > 0.05f`), scales sweep speed from `lightRainSweepSpeed` to `heavyRainSweepSpeed`, and smoothly parks blades at the bottom edge when dry. Auto-calibrates pivot positions from `CarConstruction` bounds and `Main Camera` height, with live Inspector tuning fields.
* [`WeatherSetupTool.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Editor/WeatherSetupTool.cs): Editor menu utility (`DoomDriver/Setup Dynamic Weather`) to regenerate presets, materials, and scene bindings.

### B. Shaders, Materials & Presets
* [`WindshieldRainGlass.shader`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Materials/WindshieldRainGlass.shader) & [`WindshieldRainGlass.mat`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Materials/WindshieldRainGlass.mat): Custom URP transparent HLSL shader assigned to **Element 5 (`Glass`)** of `CarConstruction` in [`carInterior.prefab`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Prefabs/Player/carInterior.prefab).
* [`Road_Wet.mat`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Materials/Road_Wet.mat): URP Lit wet asphalt reflection preset (`_Smoothness = 0.88`, `_Metallic = 0.32`, darkened wet asphalt tint, specular & environment reflections enabled).
* [`RainStreak.mat`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Materials/RainStreak.mat): URP `Particles/Unlit` transparent material for exterior rain streaks.
* Weather Preset Assets (`Assets/_Game/Data/Weather/`):
  * [`Weather_Clear.asset`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Data/Weather/Weather_Clear.asset): Dry road, no fog (`fogDensity = 0`), no rain.
  * [`Weather_Foggy.asset`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Data/Weather/Weather_Foggy.asset): Dense morning mist (`fogDensity = 0.024`), no rain, matte road.
  * [`Weather_LightRain.asset`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Data/Weather/Weather_LightRain.asset): Light rain (`150` emission), subtle haze (`fogDensity = 0.014`), wet reflective road, wipers on.
  * [`Weather_RainAndFog.asset`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Data/Weather/Weather_RainAndFog.asset): Heavy drizzle (`260` emission), thick road fog (`fogDensity = 0.032`), high-gloss wet road, faster wipers.

---

## 2. Technical Mechanics

### A. Rain-on-Glass Shader & Car Tilt Synchronization
* **Pure Object-Space Projection:** `WindshieldRainGlass.shader` projects droplets using `IN.positionOS` and `IN.normalOS` so raindrops remain rigidly attached to the car glass and tilt/roll identically with the vehicle body during steering and curb bounces.
* **Directional Physics:**
  * **Front Windshield (`abs(normalOS.z) >= abs(normalOS.x)`):** Uses C#-accumulated `_FlowTime` (eliminating `_Time.y * speed` reversal glitches). When parked or braking, gravity pulls droplets **down**; when accelerating, airflow pushes droplets **up/backward** along the windshield.
  * **Side Windows (`abs(normalOS.x) > abs(normalOS.z)`):** Blends smoothly via `_SpeedFactor` between vertical downward trickling (`_SideDownTime`) when slow and horizontal backward streaming (`_SideBackTime` along `+Z_os` = `-Z_ws` due to `carInterior`'s 180° Y rotation) when accelerating — streaming **left on the left window** and **right on the right window** from the driver's seat.

### B. Automatic Windshield Wipers & Droplet Clearing Arc
* Rendered directly in `WindshieldRainGlass.shader` on the front windshield (`isFrontGlass`) so wiper blades sit flush on the slanted glass without clipping the dashboard or pillars.
* Uses a mathematically continuous turnaround distance formula (`distSinceWipe`) strictly bounded to the rubber blade span (`0.20 * len` to `1.00 * len`) and sweep limits (`_WiperMinAngle` to `_WiperMaxAngle`):
  * Inside the wiped arc, droplets are cleared to `0.0` immediately behind the blade and gradually reform over time.
  * Outside the wiper arc (upper windshield corners and side windows), droplets remain 100% untouched.
* Runs purely automatically when raining; parks smoothly and fades out when dry.

### C. Wet Road Reflections on Modular Road Chunks
* Swaps the scene ground plane renderer between `Ground.mat` and `Road_Wet.mat`.
* Subscribes to `EnvironmentSpawner.Instance.OnChunkSpawned` and `OnChunkDespawned` to track road `MeshRenderer`s on all spawned straight and Y-fork chunks, applying `_Smoothness`, `_Metallic`, and `_BaseColor` via a reusable `MaterialPropertyBlock` (never mutating shared FBX materials).

### D. Daily Shift Briefing Integration
* `DailyShiftBriefing` and `WeatherManager` are ensured automatically by `GameManager.EnsureCompanionControllers()`.
* When returning to the lobby after a delivery, `DailyShiftBriefing` advances to the next day and rotates the weather preset.
* Dev 5's `ShiftBriefingUI` can call `DailyShiftBriefing.Instance.SetDispatcherBriefing(message)` (or read `CurrentBriefingHeadline` / subscribe to `OnBriefingUpdated`) to parse keywords (`rain`, `drizzle`, `storm`, `fog`, `mist`, `clear`) and adjust fog density, rain, and wet road reflections automatically.

---

## 3. Manual Verification Procedure (Unity Editor)

1. Open [`Assets/_Game/Scenes/EnviromentScene.unity`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scenes/EnviromentScene.unity) and press **Play**.
2. Verify the scene starts in `LightRain` with exterior rain falling strictly outside the car, droplets trickling on the windshield/side windows, automatic wipers clearing only their swept arc, wet road reflections, and subtle road fog.
3. Accept an order and drive:
   * **Accelerate (`W`):** Front windshield droplets blow upward; side-window droplets stream horizontally backward (left on left window, right on right window).
   * **Brake (`S`):** Droplets slow down and trickle downward by gravity.
   * **Steer (`A`/`D`):** Raindrops and wiper blades tilt rigidly with the car frame.
4. Press **`F7`** to cycle through all 4 weather presets (`Clear` → `Foggy` → `LightRain` → `RainAndFog`):
   * Verify fog density adjusts smoothly (`0` in Clear, thick mist in Foggy & RainAndFog).
   * Verify wipers automatically park and stop in `Clear` and `Foggy`, and resume faster in `RainAndFog`.
   * Verify road surfaces switch cleanly between dry matte asphalt and reflective wet asphalt.
