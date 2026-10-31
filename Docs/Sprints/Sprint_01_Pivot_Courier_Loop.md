# Sprint 01 – Pivot: Courier Mission Loop (Bolt Food Style)

> **Sprint Goal:** Transform the original endless runner with fixed 3 lanes into a mission‑based courier loop. The player starts in a parked car (Lobby), selects an order in the courier app, drives the route following the dashboard GPS while balancing a FOMO meter with phone reels, handles intersections, and receives a financial evaluation added to their wallet.

---

## 👥 Team Composition & Responsibilities

| Role | Developer | Primary Sprint Responsibility |
|------|-----------|--------------------------------|
| **Dev 1** | Game Flow & State Machine | Implement the state machine (`GameManager`), delivery lifecycle, and scene orchestration. |
| **Dev 2** | Orders & Economy | Create the `DeliveryOrder` data model, procedural `OrderManager`, and wallet UI in the phone app. |
| **Dev 3** | Vehicle Dynamics & Physics | Implement smooth horizontal steering, realistic steering wheel rotation, car body tilt, and soft curb contact. |
| **Dev 4** | Road Network & Navigation Backend | Generate intersections in `EnvironmentSpawner`, implement `TurnTrigger`, and handle route recalculation (*Recalculating*). |
| **Dev 5** | Dashboard UI & Final Evaluation | Build the dashboard GPS UI with audio cues and the delivery summary UI. |

---

## 🔗 Task Dependency Matrix

```mermaid
flowchart TD
    D2_1["Dev 2: DeliveryOrder Model"] --> D1_1["Dev 1: GameManager (StartDelivery)"]
    D2_1 --> D4_1["Dev 4: Spawner & Intersections"]
    D2_1 --> D5_1["Dev 5: Dashboard GPS UI"]
    D4_1 --> D4_2["Dev 4: TurnTrigger & Recalculating"]
    D4_2 --> D5_1
    D3_1["Dev 3: Smooth Steering"] --> D3_2["Dev 3: Body Roll & Curb Bounce"]
    D1_1 --> D1_2["Dev 1: Scene & Reset Lifecycle"]
    D2_2["Dev 2: WalletManager & Phone UI"] --> D5_2["Dev 5: Delivery Summary UI"]
    D1_2 --> D5_2
```

---

## 📋 Detailed Trello Card Breakdown

### 👤 DEV 1 – Game Flow & State Machine
- **Card 1.1 – Implement Game State Machine**
  - Replace the simple `isGameOver` boolean with a full state machine handling the entire courier loop.
  - Define states:
    - `OrderSelection`: Car stationary, engine idle, phone showing order list.
    - `DeliveryActive`: Car accelerates, world moves, timer runs, intersections and FOMO drain activate.
    - `DeliveryCompleted`: Destination reached within time limit, world stops, evaluation panel shows.
    - `DeliveryFailed`: Car destroyed (0 HP) or time expires, game over.
  - Add C# events (`OnGameStateChanged`, `OnDeliveryStarted`, `OnDeliveryFinished`) for UI, audio, and spawner integration.
  - **Key Files:** `Assets/_Game/Scripts/Core/GameManager.cs`, `Assets/_Game/Scripts/Core/DeliveryProgressTracker.cs`, `Assets/_Game/Scripts/Core/WorldSpeedController.cs`, `Assets/_Game/Scripts/Core/PauseController.cs`, `Assets/_Game/Scripts/UI/GameUIController.cs`, `Assets/_Game/Scripts/Core/WorldMover.cs`.
  - **Definition of Done:** Game starts in `OrderSelection` in `EnviromentScene`; calling `GameManager.Instance.StartDelivery(selectedOrder)` smoothly accelerates the world and starts the timer; `CompleteDelivery()` or `FailDelivery()` stops the world and notifies subsystems; returning to `OrderSelection` does not require reloading the scene.

- **Card 1.2 – Delivery Lifecycle & Scene Orchestration**
  - Ensure a clean restart between deliveries (reset distance travelled, temporary car damage, remove old obstacles, keep wallet balance).
  - Adjust the pause system (`TogglePause`) to work correctly during delivery (pause timer, freeze `Time.timeScale`, mute audio).
  - Connect engine audio states (idle in lobby, acceleration sound when delivery starts).
  - **Key Files:** `Assets/_Game/Scripts/Core/GameManager.cs`, `Assets/_Game/Scripts/Player/` (autonomous resets in `VehicleMovement.cs`, `VehicleHealth.cs`, `VehicleAudio.cs`, `VehicleVFX.cs`), `Assets/_Game/Scripts/Audio/AudioManager.cs`.
  - **Definition of Done:** No memory leaks between deliveries; car health resets to max; wallet balance persists; pause works reliably during gameplay.

---

### 👤 DEV 2 – Order System & Wallet
- **Card 2.1 – DeliveryOrder Data Model & Procedural Offer Generator**
  - Create `DeliveryOrder` representing a delivery:
    - Restaurant & customer names.
    - Total distance (meters).
    - Time limit for maximum tip.
    - Sequence of required turns.
    - Base payout and max tip.
  - Implement `OrderManager` that generates three distinct offers (easy, medium, hard) when entering `OrderSelection`.
  - **Key Files:** `Assets/_Game/Scripts/Economy/DeliveryOrder.cs`, `Assets/_Game/Scripts/Economy/OrderManager.cs`, data templates in `Assets/_Game/Data/Orders/`.
  - **Definition of Done:** Each return to `OrderSelection` produces three unique offers with balanced length, difficulty, and reward; selected order accessible via `OrderManager.CurrentOrder`.

- **Card 2.2 – Wallet & Phone UI**
  - Implement `WalletManager` to store player funds with persistent saving (`PlayerPrefs` or encrypted JSON).
  - Create `CourierAppUI` script for the phone display showing order cards, wallet balance, and an "Accept Order" button.
  - In `OrderSelection`, display the courier app UI; on order acceptance, call `GameManager.Instance.StartDelivery(order)`.
  - Update `PhoneSystem` to switch between app UI and reels mode.
  - **Key Files:** `Assets/_Game/Scripts/Economy/WalletManager.cs`, `Assets/_Game/Scripts/Phone/CourierAppUI.cs`, `Assets/_Game/Scripts/Phone/PhoneSystem.cs`, modify `carInterior.prefab`.
  - **Definition of Done:** Phone UI shows interactive order cards; accepting an order starts the delivery; wallet updates correctly and persists after game restart.

---

### 👤 DEV 3 – Vehicle Handling & Physics
- **Card 3.1 – Continuous Analog Steering & Wheel Sync**
  - Remove discrete lane logic; implement continuous X‑axis movement driven by New Input System (`controls.Player.Move.ReadValue<Vector2>().x`).
  - Clamp X position between road margins (`minRoadX`, `maxRoadX`).
  - Add smooth acceleration/deceleration and steering speed.
  - Synchronise steering wheel rotation (`steeringWheel.localRotation`) with input.
  - **Key Files:** `Assets/_Game/Scripts/Player/VehicleMovement.cs`, input bindings in `Assets/_Game/Data/Input/InputSystem_Actions.inputactions`, `carInterior.prefab`.
  - **Definition of Done:** Car moves fluidly across the full road width; steering works with keyboard (A/D, arrows) and gamepad; wheel rotates proportionally and returns to neutral when input released.

- **Card 3.2 – Body Roll, Tire Screech, and Curb Bounce**
  - Compute dynamic body tilt based on lateral acceleration for realistic tilt.
  - Replace hard curb collisions with a soft bounce back: slight camera shake, minor speed reduction, tire screech sound.
  - Trigger tire screech only on sharp steering at high speed.
  - **Key Files:** `Assets/_Game/Scripts/Player/VehicleMovement.cs`, `Assets/_Game/Scripts/Player/VehicleAudio.cs`, `Assets/_Game/Scripts/Player/VehicleCollisionHandler.cs`, `Assets/_Game/Scripts/Core/CameraShake.cs`, audio sources on `carInterior.prefab`.
  - **Definition of Done:** Car tilts naturally in turns; curb contact produces a gentle bounce and appropriate audio/visual feedback; driving feels smooth and arcade‑like.

---

### 👤 DEV 4 – Road Network, Intersections & Dynamic Route Extension
- **Card 4.1 – Procedural Intersection Generation**
  - Extend `EnvironmentSpawner` to generate road chunks based on the selected `DeliveryOrder` profile.
  - Create prefabs for left turn, right turn, and T‑junction/straight chunks.
  - Ensure spawner places intersections at the correct distances defined by the order.
  - **Key Files:** `Assets/_Game/Scripts/Environment/EnvironmentSpawner.cs`, new `Assets/_Game/Scripts/Environment/RoadChunk.cs`, prefabs in `Assets/_Game/Prefabs/Environment/`.
  - **Definition of Done:** `EnvironmentSpawner` inserts intersections at the right points; road segments connect seamlessly without gaps or collision issues; world movement and despawning work as with straight chunks.

- **Card 4.2 – Turn Detection & Recalculating**
  - Create `TurnTrigger` component on intersection prefabs.
  - When the car passes a trigger, check X position and wheel angle:
    - **Correct turn:** Player follows GPS cue → advance to next route point.
    - **Missed turn / wrong turn:** Activate *Recalculating* state.
  - In *Recalculating*: fire `OnMissedTurn`, add 2‑3 extra road chunks (+~180 m), record penalty.
  - **Key Files:** `Assets/_Game/Scripts/Navigation/TurnTrigger.cs`, `Assets/_Game/Scripts/Navigation/RouteController.cs`, integrate with `EnvironmentSpawner.cs` and `GameManager.cs`.
  - **Definition of Done:** System reliably distinguishes correct vs. missed turns; missed turns extend the route and apply penalties; works robustly for repeated misses.

---

### 👤 DEV 5 – Dashboard GPS UI & Delivery Summary
- **Card 5.1 – Dashboard GPS Display & Early Turn Warning**
  - Build a world‑space canvas or RenderTexture on the dashboard showing:
    - Arrow for next maneuver (left/right/straight).
    - Live distance countdown to the turn (e.g., "200 m → 10 m").
    - Recalculating warning (orange) when the player misses a turn.
  - Add an audio cue (GPS beep) ~70 m before the turn.
  - **Key Files:** `Assets/_Game/Scripts/UI/DashboardNavigationUI.cs`, modify `carInterior.prefab`, add audio assets to `Assets/_Game/Audio/`.
  - **Definition of Done:** GPS UI is clearly visible from the driver’s perspective; distance number updates smoothly; audio cue triggers at the correct distance; Recalculating mode switches instantly.

- **Card 5.2 – Delivery Summary UI**
  - Create a summary panel displayed after a successful delivery showing:
    - Base fee, time tip, damage penalties, missed‑turn penalties, net earnings.
    - Animated tally of the final amount added to the wallet.
    - "Continue" button returning to `OrderSelection`.
  - **Key Files:** `Assets/_Game/Scripts/UI/DeliverySummaryUI.cs`, prefab `Assets/_Game/Prefabs/UI/DeliverySummaryCanvas.prefab`, connect to `WalletManager.cs` and `GameManager.cs`.
  - **Definition of Done:** On `OnDeliveryCompleted`, the panel fades in with animated numbers; amounts match the actual gameplay stats; pressing "Continue" hides the panel, adds money to the wallet, and returns to the lobby.

---

## 📁 Sprint Documentation Location
All sprint documentation resides in `Docs/Sprints/`. The English version of this file is now available at:

- `Docs/Sprints/Sprint_01_Pivot_Courier_Loop.md`

---

> **Note:** The permanent architectural manual and project rules are in the repository root file [`AGENTS.md`](../AGENTS.md).
