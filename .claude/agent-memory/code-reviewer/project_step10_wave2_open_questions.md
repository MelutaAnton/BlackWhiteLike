---
name: step10-wave2-open-questions
description: Step 10 wave 2 (worship popup positions, alignment UI tint, localization) — the one rule call that needs the owner, plus verified facts not to re-derive
metadata:
  type: project
---

Raised at the wave-2 review (2026-09-12), **not yet decided by the owner** — do not treat as accepted:

- **`LocalizedTextApplyEntryPoint` uses `Object.FindObjectsByType<LocalizedTextView>(Include)` in `Start()`.**
  Grep-verified: this is the ONLY runtime (non-Editor) `Find*ByType` in the whole codebase. CLAUDE.md
  bans `FindObjectOfType` in runtime code ("допустимо только в Editor-скриптах"); `FindObjectsByType`
  is the same family, so this is a new precedent, not an existing one.
  **Why:** ambiguous-rule memory ([[feedback-ambiguous-rule-ask-first]] in the user-memory index) says
  the first reading of a two-way CLAUDE.md phrase is the owner's call.
  **How to apply:** if the owner keeps it, stop reporting it; if not, the alternatives are
  `builder.RegisterComponentInHierarchy<LocalizedTextView>()` or a SerializeField list on the scope.
  Side effect either way: UI instantiated **after** `Start()` (pooled popups, dynamically built cards)
  never gets localized — nothing needs it today, and nothing checks it.

Verified facts from this wave — reuse, don't re-derive:

- `StateMachine.Pop()` returns to the underlying state **without** re-calling `Enter()`
  (`Core/StateMachine/StateMachine.cs`). So any `IState.Enter` that mutates global state (timeScale)
  is NOT restored when a pushed state pops off above it — `PausedState`'s "previous was 0 → restore 1"
  safety net actively unfreezes the world in that case. Today unreachable over `LevelComplete`: the
  overlay's full-screen `Dim` (raycastTarget, `SetAsLastSibling`) covers the pause button and
  `IInputProvider.PausePressed` has **no consumer**. Re-check the moment a keyboard/Esc pause path lands.
- timeScale safety nets, both present: `SceneLoaderService.Load` writes `Time.timeScale = 1f` before
  every `LoadScene`, and `UnityTimeScaleProvider.Dispose` restores 1 on scope close.
- `WorshipPopupView.Update` animates on **scaled** `Time.deltaTime` — in-flight popups freeze whenever
  a state zeroes timeScale (now `LevelComplete`, not just `Paused`).
- `PopupRoot` is a 1×1 rect, pivot/anchors (0.5, 1), so
  `ScreenPointToLocalPointInRectangle(popupRoot, ...)` → `localPoint` ≈ child `anchoredPosition`
  (error ≤ 0.5 px). The world→screen→canvas conversion is byte-for-byte the accepted
  `TutorialView.ShowPointerAtScreenPoint` form (worldCamera only for non-Overlay canvases).
- `WorshipService` anchors Fear to the **current** event's `WorldPosition` while taking `cause` from
  the remembered `_tossedCauseByVillager` — i.e. the popup lands at the act (landing/strike), not at
  the grab. That asymmetry is intentional and correct; don't flag it.
