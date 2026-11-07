# Dev 4 – Road Network, Procedural Intersections & Navigation System

Branch: `feature/procedural-junction-spawner`

## Overview

Dev 4 delivers the procedural road network and dynamic navigation architecture for **DoomDriver**. It bridges the economy (`DeliveryOrder`), the procedural world generator (`EnvironmentSpawner`), the physical vehicle (`VehicleMovement`), and the dashboard GPS display (`DashboardNavigationSource`).

The driver no longer travels on an endless straight highway. Instead, the game generates an authentic urban courier route featuring 90° turns (left, right, and 4-way intersections). The player must follow dashboard navigation cues while balancing road awareness with smartphone distraction.

---

## 1. Key Assets & Components

### A. Road Prefabs & Geometry
* `Assets/_Game/Prefabs/Environment/RoadChunk_TurnLeft.prefab`: T-junction branching left (`chunkLength = 44.75m`, `intersectionCenterZ = 19.0m`, `branchExitDistance = 25.8m`).
* `Assets/_Game/Prefabs/Environment/RoadChunk_TurnRight.prefab`: T-junction branching right (`chunkLength = 44.75m`, `intersectionCenterZ = 19.0m`, `branchExitDistance = 25.8m`).
* `Assets/_Game/Prefabs/Environment/RoadChunk_Crossroads.prefab`: 4-way crossroad supporting both left and right maneuvers.
* `Assets/_Game/Prefabs/Environment/RoadChunk_01.prefab`, `RoadChunk_02.prefab`, `RoadChunk_03.prefab`: Straight street variants with diverse buildings, street lights, and scenery.

### B. Scripts & Architecture
* [`RoadChunk.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Environment/RoadChunk.cs): Identifies segment type (`Straight`, `TurnLeft`, `TurnRight`, `Crossroads`, `TJunction`), configures length, intersection pivot center, and holds pre-attached preview branch chunks.
* [`EnvironmentSpawner.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Environment/EnvironmentSpawner.cs): Procedurally spawns, pools, and recycles chunks along the route. Synchronizes directly with the active `DeliveryOrder` and attaches preview chunks to branch exits to eliminate visual void during turns.
* [`TurnTrigger.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Navigation/TurnTrigger.cs): Placed on intersection chunks. Evaluates vehicle lateral position (`transform.position.x`) and steering angle (`steeringWheel.localRotation`) when the car crosses the junction threshold.
* [`RouteController.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Navigation/RouteController.cs): Singleton managing route execution, GPS cue broadcasting (`DashboardNavigationSource`), world 90° pivot rotations, missed-turn penalties, and dynamic route extensions (+134m / 3 chunks).

---

## 2. System Mechanics

### Card 4.1 – Procedural Intersection Generation
1. **Order Synchronization:**
   * When an order is accepted (via `OrderManager.OnOrderSelected` or `Enter`), `EnvironmentSpawner.HandleDeliveryStarted()` reads the order's `TotalDistance` and `TurnSequence` (0 = Straight, 1 = Left, 2 = Right).
   * `ConfigureRouteFromTurns()` distributes the requested intersections evenly across the route distance, interspersed with straight road segments.
2. **Seamless Pre-attached Branch Preview Chunks:**
   * When an intersection chunk is spawned, straight road chunks are automatically pre-instantiated and attached to its left and/or right exits (rotated ±90° locally).
   * While the car approaches and turns, the preview chunk rotates synchronously with the intersection, completely eliminating visual empty voids or pop-in artifacts.
   * Upon turn completion, `EnvironmentSpawner.ResumeGenerationAfterTurn()` seamlessly detaches the preview chunk into world space and promotes it to the active road list.

### Card 4.2 – Turn Detection, Closed-Form Pivot & Recalculating
1. **Turn Detection (`TurnTrigger`):**
   * As the vehicle enters the intersection center zone, `TurnTrigger` checks the vehicle's lateral position:
     * **Left Turn:** `carX < -steerThreshold` (-1.5m).
     * **Right Turn:** `carX > steerThreshold` (+1.5m).
     * **Straight:** `-steerThreshold <= carX <= steerThreshold`.
   * If the driver steers in the commanded direction, a **Successful Turn** is triggered.
   * If the driver ignores the cue, steers in the wrong direction, or fails to initiate the turn in time, a **Missed Turn** is triggered.
2. **Mathematical 90° World Pivot:**
   * To keep vehicle physics stable and aligned with the single-axis `WorldMover` (moving along -Z), `RouteController.ExecuteWorldTurn()` pivots the entire active world by 90° around the intersection center `(0, 0, pivotZ)`:
     ```csharp
     // Closed-form mathematical rotation:
     Vector3 offset = chunk.transform.position - pivotPoint;
     Vector3 rotatedOffset = (direction == Maneuver.Left)
         ? new Vector3(-offset.z, offset.y, offset.x)
         : new Vector3(offset.z, offset.y, -offset.x);
     chunk.transform.position = pivotPoint + rotatedOffset;
     ```
   * The vehicle's lateral position is smoothly recentered onto the new road corridor over 0.3s.
3. **Dynamic Route Recalculation:**
   * On a missed turn, `RouteController.TriggerMissedTurn()` immediately sets the dashboard GPS to **Recalculating** (orange warning).
   * A financial penalty (**5.00 €**) is recorded into `RouteController.TotalMissedTurnPenalty`.
   * The route is dynamically extended: 3 additional straight chunks (~134m) are injected before another compensatory turn is scheduled.
   * The GPS announces a new recalculated maneuver once the detour route is prepared.

---

## 3. Integration Contracts

### With Dev 2 (Courier App & Order Loop)
* `EnvironmentSpawner` subscribes to `OrderManager.Instance.OnOrderSelected`.
* The debug input bridge in [`GameManager.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Core/GameManager.cs) supports both mouse clicking on the courier app and pressing `Enter` / `Numpad Enter` in the parked lobby (auto-selecting `OrderManager.Instance.CurrentOffers[0]`).

### With Dev 5 (Dashboard GPS & Delivery Summary)
* `RouteController` publishes live maneuver updates and remaining distances to `DashboardNavigationSource.Instance.Publish(id, maneuver, remainingMeters)`.
* At ~70m before a turn, the GPS plays an audio beep and spoken instruction.
* In [`DeliveryProgressTracker.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Core/DeliveryProgressTracker.cs), `FinalizeDeliveryReward()` queries `RouteController.Instance.TotalMissedTurnPenalty` so that all missed-turn deductions appear accurately on the final `DeliverySummaryUI` payout screen.

---

## 4. Manual Verification Procedure (Unity Editor)

1. Open `Assets/_Game/Scenes/EnviromentScene.unity`.
2. Press **Play**. You start in the parked car lobby with the smartphone displayed.
3. Select an order by clicking **"Prijať"** on the phone screen, OR press **`Enter`** / **`Keypad Enter`**.
4. The car accelerates forward. Note the dashboard GPS displaying the next turn (e.g. *Turn Left in 200m*).
5. Watch the GPS distance count down. At ~70m, verify the audio beep and direction announcement.
6. **Test Successful Turn:**
   * When reaching the intersection, steer into the requested direction (`A` or `D`).
   * Verify the world smoothly rotates 90° with no empty gaps or stutter.
   * Verify the car is aligned with the new road and generation continues seamlessly.
7. **Test Missed Turn (Recalculating):**
   * At the next intersection, continue driving straight without turning.
   * Verify the GPS immediately switches to orange **Recalculating**.
   * Verify the route is extended and a new turn is scheduled.
8. Press **`C`** (or complete the route):
   * Verify `DeliverySummaryUI` opens and displays the **Missed-turn penalties** item corresponding to the misses made.
   * Press **Continue**: payout is credited to wallet and state returns to `OrderSelection`.

