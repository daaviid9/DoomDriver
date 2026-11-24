# Dev 4 – Road Network, Procedural Intersections & Navigation System

Branches: `feature/procedural-junction-spawner`, `turning-fork`

## Overview

Dev 4 delivers the procedural road network and dynamic navigation architecture for **DoomDriver**. It bridges the economy (`DeliveryOrder`), the procedural world generator (`EnvironmentSpawner`), the physical vehicle (`VehicleMovement`), and the dashboard GPS display (`DashboardNavigationSource`).

The driver no longer travels on an endless straight highway. Legacy abrupt 90° T-junctions have been completely replaced with extended 45° **Y-fork** highway splits (`RoadChunk_YFork.prefab`). The player must follow dashboard fork guidance ("Keep Left / Keep Right") while balancing road awareness with smartphone distraction.

---

## 1. Key Assets & Components

### A. Road Prefabs & Geometry
* [`Assets/_Game/Prefabs/Environment/RoadChunk_YFork.prefab`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Prefabs/Environment/RoadChunk_YFork.prefab): Active 45° Y-junction / fork road segment (`chunkLength = 45.456m`, `intersectionCenterZ = 18.37m`) splitting smoothly into Left ($X = -27.086\text{m}, Y = -45^\circ$) and Right ($X = +27.086\text{m}, Y = +45^\circ$) branches. Includes central divider island, apex crash attenuator (`Road_Barrier_Crash`), inner guardrails, island props, and flanking city buildings.
* `Assets/_Game/Prefabs/Environment/RoadChunk_01.prefab`, `RoadChunk_02.prefab`, `RoadChunk_03.prefab`: Straight street variants with diverse buildings, street lights, and scenery.
* *(Legacy / Removed from active spawning)*: `RoadChunk_TurnLeft.prefab`, `RoadChunk_TurnRight.prefab`, `RoadChunk_Crossroads.prefab` — replaced completely by `RoadChunk_YFork.prefab`.

### B. Scripts & Architecture
* [`RoadChunk.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Environment/RoadChunk.cs): Identifies segment type (`Straight`, `YFork`, plus legacy enum values), configures `chunkLength`, `intersectionCenterZ`, per-chunk `turnDuration`, and holds dual pre-attached preview branch chunks.
* [`EnvironmentSpawner.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Environment/EnvironmentSpawner.cs): Procedurally spawns, pools, and recycles straight and Y-fork chunks along the route. Attaches preview chunks to both Y-fork exits and stops forward straight spawning behind active Y-forks (`HasActiveForkAhead()`).
* [`TurnTrigger.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Navigation/TurnTrigger.cs): Placed on Y-fork chunks with an elongated trigger corridor. Evaluates smooth lateral positioning (`transform.position.x`) as the divider island approaches to determine Left vs. Right branch selection.
* [`ForkCrashBarrier.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Navigation/ForkCrashBarrier.cs): Mounted on the central apex crash barrier (`Road_Barrier_Crash`) to handle fatal head-on impacts when a distracted driver fails to steer into either fork branch.
* [`RouteController.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Navigation/RouteController.cs): Singleton managing route execution, fork GPS cue broadcasting (`Maneuver.ForkLeft` / `Maneuver.ForkRight`), analytical 45° world pivot rotations with Inspector-configurable `forkTurnDuration`, missed-turn penalties, and dynamic route extensions (+134m / 3 chunks).
* [`DashboardNavigationSource.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Navigation/DashboardNavigationSource.cs) & [`DashboardNavigationUI.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/UI/DashboardNavigationUI.cs): Supports `Maneuver.ForkLeft` and `Maneuver.ForkRight` with fork-specific arrows, `"Keep Left" / "Keep Right"` dashboard prompts, and early voice/beep cues.
* [`YForkPrefabBuilder.cs`](file:///c:/Users/kikom/Skola/3/gamedev/game-jam/Assets/_Game/Scripts/Editor/YForkPrefabBuilder.cs): Editor tool used to assemble and update `RoadChunk_YFork.prefab` with exact branch exit anchors, guardrails, crash barrier, and framing buildings.

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

### Card 4.3 – Extended Y-Fork Road Prefabs & Branch Geometry
1. **High-Speed 45° Split Geometry (`chunkLength = 45.456m`, `intersectionCenterZ = 18.37m`):**
   * Replaces abrupt 90° T-junctions with a smooth, authentic $45^\circ$ Y-fork avenue segment.
   * The avenue splits into dual diverging paths:
     * **Left Fork:** Exits at $X = -27.086\text{m}, Z = 45.456\text{m}$ (rotation $Y = -45.0^\circ$).
     * **Right Fork:** Exits at $X = +27.086\text{m}, Z = 45.456\text{m}$ (rotation $Y = +45.0^\circ$).
   * Span between branch centers at exit: $\Delta X = 54.172\text{m}$.
2. **Analytical 45° World Turn Pivot & Inspector Tuning (`RouteController` & `RoadChunk`):**
   * Rotation angle is dynamically configured via `intersectionChunk.TurnAngle` ($45^\circ$ for `YFork` vs $90^\circ$ for standard intersections).
   * Pivoting around analytical center $C_z = E_z - |E_x| = 45.456 - 27.086 = 18.37\text{m}$ ensures the chosen branch exit aligns **identically at $X = 0.0000\text{m}$ and $0.0^\circ$ world rotation**.
   * **Inspector Turn Tuning:**
     * Per-prefab / chunk override: `RoadChunk.TurnDuration` (`turnDuration` on `RoadChunk_YFork.prefab`, default 0.95s).
     * Global scene defaults: `RouteController.ForkTurnDuration` (0.95s) and `SharpTurnDuration` (1.1s) serialized on `GameManagers` in `EnviromentScene.unity`.
   * `VehicleMovement.StartTurnManeuver` adapts with natural weight transfer (roll $\pm 0.5^\circ$), gentle wheel steering ($\approx 31^\circ$), zero drift yaw, and no tire screeching for smooth highway cornering.
3. **Dual Exit Anchors & Forward Straight Chunk Elimination (`EnvironmentSpawner`):**
   * `RoadChunk.cs` defines `RoadChunkType.YFork` and dual exit anchors:
     * `GetBranchExitLocalPosition(direction)` returns the exact local position ($\pm 27.086\text{m}, 0\text{m}, 45.456\text{m}$).
     * `GetBranchExitLocalRotation(direction)` returns the matching branch rotation ($\pm 45.0^\circ$).
   * When `EnvironmentSpawner` spawns a Y-fork, `AttachSideBranchPreviews()` attaches preview chunks to **both** left and right exits simultaneously.
   * **No Straight Forward Chunks Behind Y-Fork:**
     * `InitializeRoad()`, `SpawnNextChunkInSequence()`, and `ResumeGenerationAfterTurn()` stop forward spawning as soon as a Y-fork is encountered.
     * `HasActiveForkAhead()` blocks forward spawning while a Y-fork is in front of the car, ensuring the road splits **exclusively into Left and Right** with no phantom straight road behind the barrier.
   * Promoted preview chunks seamlessly continue straight along the $+Z$ axis without gaps or stutter.
   * **Bifurcation Hazard Rule (Card 4.3 Update):**
     * On a Y-fork, there is no straight continuation path. The road splits exclusively into Left ($-45^\circ$) and Right ($+45^\circ$).
     * The player **only survives if steering into Left or Right**.
     * If the player continues straight (or fails to steer into either fork branch, e.g. due to phone doomscrolling), they crash directly into the central divider barrier `Road_Barrier_Crash` (equipped with `BoxCollider` and `ForkCrashBarrier.cs`), triggering fatal damage (100 HP loss), explosion VFX/SFX, heavy camera shake, and `DeliveryFailed`.
     * Steering into the wrong branch (e.g. Right instead of GPS Left) still executes the smooth $45^\circ$ world turn to preserve motion, but triggers route recalculation (+150m road penalty) and financial deduction.
     * **Game Over UI & Cursor Control:** Upon `DeliveryFailed`, `GameUIController` unlocks and shows the cursor (`Cursor.lockState = CursorLockMode.None`), and `DeliveryFailedPanel` / `DeliveryExpiredPanel` present the failure summary with functional Retry and Exit buttons.

### Card 4.4 – Fork Navigation, Steering Splitting & Recalculating (`turning-fork`)
1. **Elongated Fork Corridor Evaluation (`TurnTrigger.cs`):**
   * Supports an elongated trigger corridor along the Y-fork approach so lateral car position (`transform.position.x`) is continuously evaluated as the central divider island approaches.
2. **Fork-Specific GPS Maneuvers (`DashboardNavigationSource.cs` & `DashboardNavigationUI.cs`):**
   * Added `Maneuver.ForkLeft` and `Maneuver.ForkRight` with dedicated fork arrows, **"Keep Left" / "Keep Right"** dashboard labels, and audio cues approaching the split.
3. **Y-Fork-Only Spawner Pipeline (`EnvironmentSpawner.cs` & `EnviromentScene.unity`):**
   * Removed legacy 90° intersection prefab slots (`turnLeftPrefabs`, `turnRightPrefabs`, `crossroadsPrefabs`) from `EnvironmentSpawner`; all procedural junctions now spawn exclusively from `yForkPrefabs` (`RoadChunk_YFork.prefab`).
   * Verified `EnviromentScene.unity` `EnvironmentSpawner` references `RoadChunk_YFork.prefab` with `debugTurnSequence` set strictly to `RoadChunkType.YFork`.

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
6. **Test Successful Y-Fork Turn:**
   * When reaching the Y-fork, steer into the requested branch (`A` for Left, `D` for Right).
   * Verify the world smoothly rotates $45^\circ$ with no empty gaps or stutter.
   * Verify the car is aligned with the chosen branch and generation continues seamlessly.
7. **Test Wrong Branch Selection (Recalculating):**
   * If the GPS asks for Left, steer Right instead into the other branch.
   * Verify the car smoothly enters the right branch ($45^\circ$ turn), and GPS switches to orange **Recalculating** with penalty.
8. **Test Fatal Central Crash (Going Straight into Y-Fork):**
   * At the Y-fork, drive straight ahead without steering left or right (e.g. distracted by phone).
   * Verify the car smashes directly into the central divider/attenuator at the fork center.
   * Verify fatal damage (car destroyed, explosion/smoke VFX, heavy camera shake) and `DeliveryFailed` screen triggers immediately.
9. Press **`C`** (or complete the route):
   * Verify `DeliverySummaryUI` opens and displays the **Missed-turn penalties** item corresponding to the misses made.
   * Press **Continue**: payout is credited to wallet and state returns to `OrderSelection`.

