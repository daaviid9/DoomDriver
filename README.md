# DoomDriver – Game Jam Project Documentation & Guide

---

## 1. Quick Start (How to Run the Game)

- **Render Pipeline:** Universal Render Pipeline (URP).
- **Input System:** Unity New Input System.

### Scenes to launch:
1. **Main Game (recommended):**
   - Open scene: `Assets/_Game/Scenes/LoadingScreenMenu.unity`
   - Press **Play** ▶️. You will see the loading screen / main menu with zoom, music, and a start button.
2. **Direct Gameplay Testing:**
   - Open scene: `Assets/_Game/Scenes/EnviromentScene.unity`
   - Press **Play** ▶️ for an immediate jump into the driving experience.
3. **Isolated Tests:**
   - In `Assets/_Game/Scenes/_Tests/` you will find scenes used for experimental prototypes (particles, sounds, test prototypes) where you can experiment without affecting the main game.

### Controls while testing in the editor:
- **A / D** or **Left / Right Arrow** – change driving lane (left, centre, right).
- **Space / Jump** – hold to raise the phone, watch reels and recharge the FOMO meter.
- **Escape** – pause the game (Pause menu).

---

## 2. Project Organization & Folder Structure

All content under the `Assets/` folder is split into two main parts: **our own code and assets (`_Game/`)** and **downloaded asset packages (`ThirdParty/`)**.

```text
Assets/
├── _Game/                      <-- ALL CONTENT CREATED BY OUR TEAM
│   ├── Audio/                  <-- Sound effects (engine, collisions, notifications, menu)
│   ├── Data/
│   │   └── Input/              <-- New Input System settings (.inputactions)
│   ├── Materials/              <-- Game materials (interior, screens, road, obstacles)
│   ├── Models/                 <-- 3D models (car interior, tachometer)
│   ├── Prefabs/                <-- Prefabs grouped by category:
│   │   ├── Cars/               <-- Traffic (ambulance, police, taxi, cars...)
│   │   ├── Core/               <-- Manager prefabs (AudioManager...)
│   │   ├── Environment/        <-- Road chunks (RoadChunk_01, 02, 03)
│   │   └── Player/             <-- Player prefab (carInterior)
│   ├── Reels/                  <-- Brain‑rot videos (.mp4) and audio tracks (.mp3)
│   ├── RenderTextures/         <-- Render textures for phone display
│   ├── Scenes/                 <-- Production scenes (LoadingScreenMenu, EnviromentScene)
│   │   └── _Tests/             <-- Sandbox for test scenes
│   ├── Scripts/                <-- C# scripts organised into subfolders:
│   │   ├── Audio/              <-- Audio management and music
│   │   ├── Core/               <-- GameManager, world movement, speed/pause controllers, progress tracker
│   │   ├── Economy/            <-- Order data models, procedural orders, wallet management
│   │   ├── Environment/        <-- Road and obstacle spawning
│   │   ├── Phone/              <-- Phone logic, reels system, hand animation
│   │   ├── Player/             <-- Modular vehicle subsystems (movement, collisions, health, audio, VFX)
│   │   └── UI/                 <-- Game UI controller, menus, health bar, VHS filter and effects
│   ├── Settings/               <-- URP assets and post‑processing profiles
│   ├── Textures/               <-- Tachometer and clock textures
│   └── UI/
│       ├── Fonts/              <-- Fonts and TextMeshPro font assets
│       ├── LoadingScreen/      <-- Loading screen backgrounds (1.png‑5.png)
│       └── Sprites/            <-- UI sprites (phone icon)
│
├── ThirdParty/                 <-- DOWNLOADED PACKAGES FROM ASSET STORE
│   ├── Customizable Skybox/
│   ├── FencesPack/
│   ├── [Free] Phone/
│   ├── Low Poly Simple Urban City 3D Asset Pack/
│   ├── Polygonal Particles/
│   ├── SimpleHands/
│   └── SimpleSky/
│
├── TextMesh Pro/               <-- Standard Unity TextMeshPro resources
└── TutorialInfo/               <-- Template data
```

---

## 3. Important Team Rules (Git & Unity Workflow)

To avoid lost data, missing materials, or broken scripts, follow these rules:

### 1. The Golden `.meta` Rule
- In Unity **every file and folder** has its own `.meta` file with a unique GUID.
- **Never** move or delete files via the Windows Explorer without their `.meta` file! The safest way is to manipulate files directly in Unity's Project window, which automatically moves the `.meta`.
- In Git, **always commit `.meta` files together with their assets**. If you commit an asset without its `.meta`, Unity will generate a new GUID on another machine and all references in scenes will break.

### 2. Keep the Root `Assets/` Clean
- **Never** place loose files or new folders directly in the root of `Assets/`.
- When you create a new model, script, or texture, store it in the appropriate sub‑folder under `_Game/`.
- If you download a new package from the Asset Store, place it in `ThirdParty/`.

### 3. Prevent Git Conflicts in Scenes
- Unity `.unity` scenes are notoriously hard to merge when multiple people edit them.
- **Rule:** When adding new functionality, cars, obstacles, or UI elements, **wrap them in a Prefab** under `Assets/_Game/Prefabs/`. Then place the prefab in the scene – this minimises changes to the scene file itself.
- If you need to experiment, create a new scene under `_Game/Scenes/_Tests/`.

---

The above content provides a clear English version of the project documentation.
