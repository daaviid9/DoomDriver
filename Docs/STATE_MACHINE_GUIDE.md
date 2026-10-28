# DoomDriver – Game State Machine Control Guide (Debug & Testing)

This document serves as a quick reference guide for testing the core gameplay loop (Game State Machine) and vehicle controls in the current build.

---

## 🎮 Keybindings Overview

| Key | When Active | Action / Effect |
| :--- | :--- | :--- |
| **`Enter`** | In **OrderSelection** (Lobby) | **Start Delivery (`StartDelivery`)** – Vehicle accelerates onto the road, transitions to `DeliveryActive`. |
| **`A` / `D`** or **`←` / `→`** | Only during **DeliveryActive** | **Lane Switching / Steering** (controls are locked during lobby and evaluation states). |
| **`Space` (Hold)** | Only during **DeliveryActive** | **Raise Phone & Scroll Reels** – Refills FOMO (+20/s), plays reel video and audio, but blocks the driver's view of the road and GPS. |
| **`Space` (Release)** | Only during **DeliveryActive** | **Lower Phone** – Returns focus to the road. FOMO begins draining (-8/s). |
| **`C`** *(Debug)* | Only during **DeliveryActive** | **Complete Delivery (`CompleteDelivery`)** – Simulates reaching the route destination. Vehicle brakes to a stop and transitions to `DeliveryCompleted`. |
| **`R`** *(Debug)* | In **DeliveryCompleted** or **DeliveryFailed** | **Return to Lobby (`ReturnToOrderSelection`)** – Resets vehicle HP to 100%, re-centers position, clears obstacles, and returns the camera to the phone in the parked car. |
| **`ESC`** | At any time | **Pause Game (`TogglePause`)** – Freezes time (`timeScale = 0`), halts mission timers, and mutes vehicle audio. |

---

## 🔄 Detailed States & Behaviors

### 1. `OrderSelection` (Parked Car Lobby)
* **Vehicle Movement:** The vehicle is parked (`WorldMover.moveSpeed = 0`). Lane changing and tire screech audio are locked.
* **Camera & Smartphone:**
  * The driver's hand holds the smartphone in front of them.
  * The camera pitches down by **17.6°** and zooms to **FOV 40.8°** to naturally frame the phone screen.
  * The background cockpit (steering wheel, dashboard, city outside) is blurred using **Depth of Field Blur** with subtle side darkening (**Vignette 0.45**) to focus attention on the phone.
* **Audio & FOMO:**
  * Engine runs at quiet idle pitch (0.8).
  * Distraction notifications (Instagram, Messenger, iPhone sounds) are **completely disabled**.
  * The FOMO meter is locked at 100% (does not drain).
* **How to proceed:** Press **`Enter`**.

---

### 2. `DeliveryActive` (Active Delivery Driving)
* **Vehicle Movement:**
  * The world accelerates into motion (starting at 12 m/s and gradually increasing).
  * Traveled distance counter updates in real time on the top right (`xx m`).
  * Obstacles dynamically spawn on the road (cones, barricades, barrels, traffic).
* **Camera & Smartphone:**
  * The phone hand automatically lowers out of view.
  * Camera levels horizontally towards the windshield and widens to **FOV 60°**.
  * Depth of field blur turns off immediately for crystal-clear road visibility.
* **FOMO & Distractions:**
  * The FOMO meter constantly drains. When FOMO drops too low, tunnel vision darkening and desaturation activate.
  * Random notifications (social media pings) play to tempt the driver.
  * Holding **`Space`** raises the phone to stream short reels, refilling FOMO at the cost of road awareness.
* **Damage & Collisions:**
  * Hitting obstacles damages car HP and applies a speed penalty.
  * If HP drops to 0, the car is totaled and transitions to `DeliveryFailed`.
* **How to proceed:**
  * Success (debug): Press **`C`**.
  * Failure: Collide with obstacles until HP reaches 0 (or allow time to expire).

---

### 3. `DeliveryCompleted` (Delivery Success)
* **Vehicle Movement:** The vehicle smoothly decelerates to a complete stop (`WorldMover.moveSpeed = 0`).
* **Economy & Persistence:**
  * Delivery summary displays final distance traveled.
  * Payout is credited to player wallet (`PlayerBalance`, persisted via `PlayerPrefs`).
  * Remaining obstacles on the road are cleared from the scene.
* **How to proceed:** Press **`R`** to return to the parked car lobby and choose the next delivery order.

---

### 4. `DeliveryFailed` (Vehicle Totaled / Mission Failed)
* **Vehicle Movement:** Vehicle stops immediately.
* **Effects:** Engine smoke, fire particles, and radiator hissing sounds activate.
* **How to proceed:** Press **`R`** to tow/repair the vehicle (restores 100 HP, extinguishes smoke) and return to `OrderSelection`.
