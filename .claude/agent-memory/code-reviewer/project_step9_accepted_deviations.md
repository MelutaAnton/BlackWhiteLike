---
name: step9-accepted-deviations
description: Step 9 (Mobile polish) decisions already settled by the owner — haptics stub, PinchGestureRules extraction, DI-less SafeAreaFitter; report drift only, never as violations
metadata:
  type: project
---

Settled by the owner for step 9 (Mobile polish, reviewed 2026-09-10). Do NOT re-report these as
violations; report only drift away from them.

1. **`PinchGestureRules` is a static class with no interface and no registration.** Same precedent as
   `VillagerMovementRules` — see [[false-positive-static-rules-tables]]. Extraction was required to be
   behaviour-preserving 1:1, and it is: `PointerInputProvider.Tick` resets `_zoom = 0f` (line 121) before
   `UpdateTouchGestures` and nothing writes `_zoom` in between, so replacing `if (_pinchActive) _zoom = …`
   with an unconditional `_zoom = ComputeZoomDelta(…, pinchWasActive, …)` that returns `0f` on the first
   pinch frame is exactly equivalent. Order of operations and the absence of clamps are unchanged.

2. **`NullHapticsService` (no-op) is registered unconditionally in every scene**, outside the
   `_initialState == GameState.LevelPlaying` gate (`GameLifetimeScope.cs:124`). Deliberate: meta scenes
   (MainMenu/Temple) will need `LightTap`/`Success` too. Nothing is orphaned — all three consumers
   (`GodHandController`, `MiracleTargetingController`, `LevelCompletePresenter`) sit *inside* gates while
   the provider is outside, which is the safe direction of defect class 8.

3. **`SafeAreaFitter` is a MonoBehaviour with no `[Inject] Construct`.** It reads only
   `Screen.safeArea`/`Screen.width`/`Screen.height` — no service to inject. `LateUpdate` + a
   four-float/two-int cache is intentional (safe area changes on rotation and in Device Simulator), and it
   writes only `anchorMin`/`anchorMax`, relying on the editor script having zeroed
   `anchoredPosition`/`sizeDelta` once.

4. **88 canvas units is the owner's hit-zone contract** at reference 1920×1080 (`TouchHitZoneEditor`), even
   though it lands below the platform guidelines (44 pt / 48 dp ≈ 120 px at 400 dpi). Not a finding.

5. **Overlay `Dim` blockers moving inside the safe area is an accepted trade** (documented in
   `SafeAreaSetupEditor`'s class doc). The *opaque* full-screen panel backgrounds are NOT covered by that
   trade — see defect class 12 in [[recurring-defects]].
