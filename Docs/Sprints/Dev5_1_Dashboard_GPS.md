# Dev 5.1 – Dashboard GPS

Branch: `Dashboard-GPS-UI-and-Delivery-Summary`

## Assets

- `Assets/_Game/Prefabs/Navigation/DashboardNavigationCanvas.prefab`: world-space TMP display, geometric arrow, AudioSource.
- `Assets/_Game/Prefabs/Player/carInterior.prefab`: wide GPS panel fitted into the centre fascia, replacing the two centre vents.
- `Assets/_Game/Audio/gps_beep.wav`: original synthesized double beep: two 140 ms tones at 880 Hz, separated by 90 ms, with attack/release envelopes (420 ms clip).
- `Assets/_Game/Scenes/_Tests/DashboardGpsTest.unity`: isolated car/camera/demo scene; no GameManager or economy dependencies.
- `DashboardNavigationSource`: event-driven presentation input for the route backend.
- `DashboardNavigationUI`: game lifecycle, rendering, and warning playback.
- `DashboardWarningGate` (in `DashboardNavigationUI.cs`): once-per-maneuver warning policy.
- `DashboardNavigationDemo`: Inspector-driven test source, attached only in the test scene.

`GameUIController` ignores world-space canvases when finding the screen HUD, so the new GPS does not intercept existing HUD binding.

## Manual verification (Unity 6000.3.5f2)

1. Allow scripts to compile. Open `DashboardGpsTest.unity` from the Project window and press Play. Save any unrelated scene work first if Unity prompts.
2. In Game view, check that the GPS is visible above the radio. A left arrow and countdown from 200 m should appear. At 70 m, listen for the spoken instruction for the current direction (about 11 seconds after starting, at 12 m/s).
3. Select `GPS Demo - Inspector context menu` in Hierarchy. Open the three-dot menu on its **Dashboard Navigation Demo** component. Its `GPS` submenu contains the test commands.
4. Choose **Next maneuver (200 m)** twice to test right and straight arrows. Each new maneuver should announce its own direction.
5. Choose **Recalculating**. Orange text must immediately replace the arrow and distance. Choose **Next maneuver** to restore navigation.
6. For exact thresholds, turn off **Auto Countdown**, set **Remaining Meters** to 72 and choose **Apply Inspector distance and direction**. Then set it to 68 and apply: one spoken instruction. Apply 68 again: no repeated instruction. Use a new maneuver if this one has already beeped.
7. Choose **Next maneuver**, then **Toggle pause**. Set 50 m and apply: no spoken instruction. Toggle pause off: one spoken instruction. Repeated pause/resume must not repeat it.
8. While below 70 m after a beep, disable and re-enable **Dashboard Navigation UI** on the nested GPS object: no repeated beep.
9. Choose **End delivery**: neutral display, no warnings. **New delivery** resets IDs and starts at 200 m; warnings work again. A new delivery also clears demo pause.
10. Set 0 or negative meters and apply: no warning and no negative distance shown.
11. Exit Play. Run **DoomDriver → GPS → Validate GPS assets**: Console should report checks PASSED.
12. In the main game, verify GPS placement from the actual driver camera and phone occlusion. Until Dev 4 connects the route source, the production display correctly says **Waiting for route** even during a delivery.

The demo pause only controls its own countdown and audio. The production display observes `PauseController`; it does not change the game's timescale.

## Integration contract for Dev 4

Cache a reference to the `DashboardNavigationSource` component on the car's nested GPS prefab. Publish distance-to-next-maneuver from the route backend, rather than passing total traveled distance:

```csharp
gpsSource.Publish("delivery-17/route-2/turn-3",
    DashboardNavigationSource.Maneuver.Left, remainingMeters);

// Immediately after detecting a missed turn:
gpsSource.SetRecalculating();

// Once a new route is ready, publish its next maneuver with a new ID.
// When no maneuver remains:
gpsSource.ClearRoute();
```

IDs must be stable across distance updates and unique for each maneuver, including reroutes. The UI keeps warning history until the next lobby entry. A new maneuver first received at 1–70 m warns immediately, 0 m does not. Paused/inactive/recalculating updates do not consume the warning. Invalid IDs or non-finite distances clear the route.

The source exposes its current snapshot and `Changed` event, so enabling the display does not require waiting for the next route event. The source owns no route generation, turn detection, or payout logic.

## Authoring and validation

**DoomDriver → GPS → Create missing GPS assets** creates missing assets and installs the nested prefab once. It does not overwrite an existing display, replace a tuned placement, or edit the main scene. The isolated scene is built in a preview scene so unsaved editor scenes are preserved.

Validation checks serialized references, world-space rendering, prefab installation, snapshot events, recalculation, invalid distances, the 72→68 threshold crossing, duplicate IDs, paused eligibility, exact 70 m, 0 m, reroute IDs, and delivery reset.

Final acceptance still requires the actual route backend and visual/audio checks in Play mode. Card 5.2 and economy changes are outside this implementation.

## Integrated fascia and voice guidance

The floating unit and stand have been removed. The panel replaces the centre vents in the car prefab; the vents remain in the prefab but are disabled. The radio and driver-side vent remain. The screen uses a wide 600 x 300 layout, scale 0.00075, a thin black frame, enlarged arrow and 120-point distance text (auto-fit down to 96 for long values) and 44-point direction text.

Placement was fitted to mesh raycast measurements of the actual cockpit, then checked with camera renders: local position (0.15, 0.46003494, 0.35697078), rotation (23.7, 180, 0). Do not revert to the earlier guessed 30-degree tilt: it intersected the dashboard brow. The screen and text use depth-tested materials. The original shared font material is unchanged.

Voice guidance is enabled by default. At the warning threshold (70 m), play one direction-specific instruction:

- `gps_turn_left.wav`: At the next junction, turn left.
- `gps_turn_right.wav`: At the next junction, turn right.
- `gps_straight.wav`: At the next junction, continue straight.
- `gps_recalculating.wav`: Recalculating route.

The recordings were synthesized locally using Windows SAPI / Microsoft Zira Desktop. They are ordinary WAV assets: builds require neither Windows TTS nor internet access. The wording does not promise an incorrect exact distance if the backend first reports the maneuver below 70 m.

Recalculating interrupts the old direction and is announced once per recalculation episode. A new route interrupts an obsolete announcement. Pause defers new announcements; listener pause pauses ongoing audio. The fallback double beep remains available if a direction clip is missing or Use Voice Guidance is disabled in the Dashboard Navigation UI component.

### Verify this revision

1. Refresh Unity and open DashboardGpsTest. Play, and confirm the larger display is fitted into the centre fascia with no stand or floating base.
2. At 70 m, expect a spoken left-turn instruction. Test Next maneuver for right and straight instructions.
3. Trigger Recalculating while a direction is speaking: the old instruction stops and the recalculation phrase plays once. Repeated updates in that state must not repeat the phrase.
4. Pause before reaching the threshold, publish a distance under 70 m, and resume: exactly one instruction. Repeated pause/resume must not repeat it.
5. Verify the screen in Game view and from the side in Scene view with the GPS deselected. Its face should follow the fascia slope; nearer opaque objects should occlude it.
6. Run DoomDriver > GPS > Validate GPS assets. This includes the three voice-to-direction mappings and existing navigation/warning checks.

DoomDriver > GPS > Capture current cockpit saves the actual current camera view and a geometry report under Temp. Preview active navigation renders an isolated copy with a left arrow and 125 m; it does not modify or save the open scene. These tools run only when selected from the menu.
The latest height increase is 260 to 300 (+15.4%). The lower edge is preserved exactly by moving the centre along the panel's local up axis by half the added height. Only the top edge extends upward.

## Additional checklist: blinking and missed exit reports

- Below 50 m (strictly 0 < distance < 50), the direction arrow blinks slowly at 1.25 Hz. At 50 m and above it stays visible. Pause freezes the blink phase; a new maneuver resets it. Recalculating hides it.
- In DashboardGpsTest use the demo component menu **GPS > 8 Blink test (49 m)**. Try **Toggle pause**, then set 50 m and **Apply Inspector distance and direction** to restore a steady arrow.
- Use **GPS > 7 Miss turn (report wrong exit)**: this reports the wrong exit to the source; the source automatically enters Recalculating, and UI becomes amber with voice guidance. Next maneuver restores navigation.
- The junction backend must call `source.ReportTurnTaken(maneuverId, actualDirection)` once it detects the exit actually taken. Stale IDs, duplicate outcomes and invalid directions are ignored. A correct exit clears the completed instruction; a wrong exit enters Recalculating. The backend then publishes the next/replacement maneuver and owns route extensions and penalties.
- There are still no production intersections/TurnTrigger/RouteController in this checkout. This is a working result-reporting boundary, not a claim that physical missed-turn detection now exists.
- Validate GPS assets now includes blink phase/pause checks, stale and duplicate reports, correct exits and automatic amber recalculation on wrong exits.
