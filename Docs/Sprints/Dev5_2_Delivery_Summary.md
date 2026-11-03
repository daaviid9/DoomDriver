# Dev 5 — Card 5.2: Delivery Summary

## Rýchly test — rovnaký postup ako pri GPS 5.1

1. V Unity použi **Assets → Refresh** a počkaj na dokončenie kompilácie.
2. V okne **Project** otvor priečinok `Assets/_Game/Scenes/_Tests` a dvojklikom otvor **DeliverySummaryTest**. Ulož vlastné rozpracované zmeny scény, ak sa Unity opýta.
3. Stlač **Play**. Nič ďalšie netreba nastavovať ani šoférovať. Summary sa otvorí samo: Base fee **15.00**, Time tip **3.50**, Damage penalties **2.00**, Missed-turn penalties **1.00**, Net earnings **15.50 €**. Čísla sa dopočítajú za približne sekundu.
4. Vľavo vidíš **Test wallet: €50.00**. Klikni **Continue**: panel sa skryje, stav bude **OrderSelection**, peňaženka bude **€65.50**.
5. V **Hierarchy** vyber **Summary Demo - Inspector context menu**. V **Inspectore** nájdi komponent **Delivery Summary Demo** (dole). Klikni na jeho **⋮** v pravom hornom rohu a otvor podmenu **Summary**. Je to rovnaký spôsob ovládania ako `GPS Demo` pri 5.1.

| Príkaz v Summary menu | Čo očakávať |
| --- | --- |
| **1 Show sample (15.50 EUR)** | Znovu otvorí ukážku všetkých položiek. |
| **2 Show fallback (15 EUR)** | Základ 15 €, ostatné nula. Ide o dočasnú testovaciu odmenu. |
| **3 Show high penalties (0 EUR)** | Pokuty 20 + 5 €, výsledok 0 €, vysvetlenie nulovej hranice; Continue nič neodpočíta. |
| **4 Apply Inspector amounts** | Použije čísla Base Fee, Time Tip, Damage Penalty, Missed Turn Penalty z komponentu. |
| **5 Double Continue** | Pri otvorenom paneli simuluje dvojklik: len jedna výplata, vľavo PASS. |
| **6 Reopen panel (no payment)** | Vypne/zapne UI komponent, znovu spustí animáciu bez výplaty. |
| **7 Toggle pause** | Pozastaví čas. Animácia sumy pokračuje; Continue vráti nepozastavenú lobby. |
| **8 Fail delivery (no summary)** | Stav DeliveryFailed, žiadny úspešný panel ani výplata. |
| **9 Run automatic checks** | Spustí sériu kontrol; asi po 2 sekundách vľavo **ALL CHECKS PASSED**, jednotlivé výsledky sú aj v Console. |

Pred ďalšou ukážkou môžeš stlačiť Continue. Ak necháš predchádzajúcu úspešnú ukážku otvorenú, ďalší príkaz ju najprv riadne zúčtuje a až potom začne novú. Zostatok preto počas série testov rastie. Stop → Play ho obnoví na 50 €.

**Táto scéna nemení uložené peniaze ani distance highscore.** Používa skutočný GameManager, WalletManager a summary prefab, ale wallet má vypnuté ukladanie a pohyb sveta/distance tracker sú deaktivované. Údaj času je preto v tomto UI teste 0 s; počiatočné demo poškodenie je skutočných 20 HP. Čas a reálnu jazdu naďalej overuj v hlavnej scéne.

Automatické Play kontroly pokrývajú: žiadnu výplatu pri dokončení/opakovanom dokončení/reopen; zachytenie poškodenia; zákaz obídenia výsledku novou jazdou; presný koniec animácie počas pauzy; skutočný Button callback a dvojklik; skrytie/raycasty/lobby; unpause a reset zdravia pri stabilnom výsledku; skoré Continue a nulu; reset fallbacku v ďalšej jazde; cestu R aj reentrantný wallet callback; zlyhanie bez odmeny.

Scéna je už uložená v projekte; generovacie menu netreba na bežný test používať. **DoomDriver → Summary → Validate summary** kontroluje prefab, účtovné pravidlá a izoláciu testovacej scény bez spustenia Play. Príkaz **Create missing test scene** slúži iba na obnovenie chýbajúcej scény.

## Implementation

The prefab generator creates `DeliverySummaryCanvas.prefab` and nests it in `carInterior.prefab` after Unity imports the new scripts. The main scene and GPS layout are unchanged. `DeliverySummaryUI` listens to `OnDeliveryCompleted`, fades in and animates all five amounts using unscaled time. Continue works during the animation. Disabling/reopening the panel does not pay.

`GameManager` snapshots the result before publishing completion. `ReturnToOrderSelection()` is the only success payout path, shared by Continue and editor R. A guard is set before wallet notification, preventing duplicate/reentrant payouts. Completion itself no longer pays 15 euros. Start/Fail cannot bypass an outstanding successful result. The old GameUIController success panel stays hidden; its HUD lookup excludes the summary canvas.

## Backend boundary and provisional policy

The existing 15 euro fallback is preserved and marked as a placeholder in the result data. Tip and penalties remain zero until the backend provides them; they are not fabricated from GPS demo signals. Elapsed time and lost HP are captured from the real tracker/vehicle before lobby resets. Lost HP means `MaxHealth - CurrentHealth` at completion, capped to valid HP (not cumulative damage).

After starting a delivery and before completing it, the future order/navigation backend calls:

```csharp
GameManager.Instance.SetDeliveryReward(new DeliveryResult(
    baseFee: 15m, timeTip: 3.50m, damagePenalty: 2m, missedTurnPenalty: 1m));
GameManager.Instance.CompleteDelivery();
```

These amounts are an example, not production rates. The backend owns tip/rate calculations and missed-turn counts. Every line is rounded to cents, midpoint away from zero, before summing. Inputs must be nonnegative and at most 1,000,000 euros per line. Current provisional policy floors net earnings at zero; it never creates debt. The panel shows a short minimum-earnings note only when deductions exceed earnings. The existing wallet remains float-based and persists through its existing AddFunds API.

## Unity test procedure

1. Open `EnviromentScene`. Run **Assets > Refresh** and confirm no compile errors. The pending one-shot build request generates and installs the prefab after compilation. If it does not run, use **DoomDriver > Summary > Create missing summary prefab**.
2. In Project open `Assets/_Game/Prefabs/Player/carInterior.prefab`. Confirm `DeliverySummaryCanvas` is a nested prefab with CanvasGroup, GraphicRaycaster and bound DeliverySummaryUI fields. Keep its root active; visibility uses CanvasGroup. The GPS must keep its existing position, dimensions and styling.
3. Enter Play mode. Record `GameManager` object's WalletManager balance (Debug Inspector exposes `currentBalance`). Enter starts a delivery; C completes it. Expect the overlay, fade and tally ending at 15.00 euros, with zero tip and penalties. Wallet must still equal its starting value.
4. Press Continue during the tally. The panel hides, wallet increases by exactly 15, game returns to OrderSelection, health/time reset. Repeat with a rapid double-click. Repeat another delivery using R instead of Continue: exactly one payment each time.
5. Start again, wait several seconds, take nonfatal damage, then use **DoomDriver > Summary > DEMO complete with accounting values**. This explicitly supplies 15 + 3.50 - 2 - 1 = **15.50 euros**. Check every line, final exact total and captured real elapsed time/HP. Continue pays exactly that value. The next ordinary delivery returns to the 15 euro fallback.
6. While the summary is open, disable/re-enable only its DeliverySummaryUI component or canvas root. No payment should occur. Continue pays once. Try Escape/pause while open, then Continue; lobby must be unpaused. Completing while paused must still animate and allow Continue.
7. Destroy the car during an active delivery: failure UI only, no success panel or payout. Check a second delivery starts normally.
8. Check 1920x1080 and 1280x720 Game view, keyboard navigation/Enter, mouse Continue, no clipping, and unchanged GPS / phone / pause behavior during delivery. The fallback EventSystem uses the New Input System only when none exists.
9. **DoomDriver > Summary > Validate summary** checks currency rounding, input limits, zero floor and prefab references. **Render preview** writes `Temp/DeliverySummaryPreview.png` without modifying the scene.

Play tests credit the existing persistent wallet on Continue. Do not clear all PlayerPrefs. Use a disposable test profile if preserving the current balance is necessary.

## Scope

Full production accounting still depends on Dev 2 and Dev 4 providing order amounts and penalties. The summary and settlement integration accept those values now; this card does not implement those backends. User approved the final cockpit palette and authorized the Card 5.2 commit.

## Verification status

- Runtime and editor sources compile with the installed Unity Roslyn compiler.
- Executed checks on DeliveryResult passed: cent rounding, net sum, zero floor, fallback, stat snapshot, negative-input rejection.
- The previous Unity import/build returned PASS; the summary prefab exists and its rendered preview was visually inspected. The new isolated scene and demo compile with Unity Roslyn; the new Play-mode checks still need to run in Unity after Refresh. No Play-mode PASS is claimed before executing them.


## Simplified visual revision and current sprint check

Checked against the current `Sprint_01_Pivot_Courier_Loop.md`, Card 5.2. The required presentation is the five money amounts, animated tally/fade and Continue. Branding, slogan, elapsed time, HP and backend diagnostics are not requested and have been removed from the player panel. Continue has no arrow or extra caption. The panel is now 640 × 660 instead of 800 × 860, with neutral colors and a single divider. The test-scene status is shorter too.

All four breakdown rows remain visible, including zero amounts, because the sprint explicitly requires them. A short minimum-earnings note appears only when the provisional zero-floor policy applies. Placeholder state remains available as `DeliveryResult.IsPlaceholder`; the UI no longer displays development details.

The sprint requires amounts matching actual gameplay stats. Full acceptance of that requirement still depends on Dev 2/Dev 4 supplying real order rewards and penalties through SetDeliveryReward; the existing fallback and demo values are not a completed production accounting backend. The no-debt floor is a provisional implementation choice, not a sprint requirement.

Runtime/editor compilation passed after this visual revision. Refresh and Play DeliverySummaryTest to inspect the new prefab; repeat Summary > 9 Run automatic checks for regression verification. The revised layout has not yet been visually verified in Unity. Commit authorized after final visual approval.
Final palette: charcoal background, neutral light text, white net amount and white Continue with black text, matching LoadingScreenMenu's monochrome controls. No layout or behavior changes.

Palette superseded at user request: colors now follow the cockpit render — dark blue-grey dashboard background, steel-blue Continue, pale blue-grey text and a soft blue net amount. Layout and behavior unchanged.

## Additional checklist: elapsed delivery time

The compact panel now displays **Time mm:ss** below its heading using the immutable real elapsed-time snapshot captured by GameManager. Monetary rows, colors and settlement behavior are unchanged. Time is floored to completed seconds; 60 minutes is displayed as 60:00.

In DeliverySummaryTest open **Summary > 10 Timed delivery (3 seconds)**. After about three seconds the panel shows **Time 00:03**. The test temporarily enables the real progress timer while world speed remains disabled, so it never accumulates distance/highscores. The normal instant demo displays 00:00. Automatic checks now also verify that the real elapsed time is presented correctly. Formatting validation covers 02:05, 60:00, negative and invalid input.
