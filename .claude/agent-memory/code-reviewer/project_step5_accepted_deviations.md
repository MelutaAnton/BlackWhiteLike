---
name: step5-accepted-deviations
description: Owner-preapproved design decisions for step 5 (Worship + win) — assess risk only, never re-litigate as violations
metadata:
  type: project
---

Owner pre-accepted (2026-08-29, step 5 review request):

1. Awe/Fear values come only from `WorshipConfig`; `MiracleConfig.WorshipYield` was deleted.
2. `AlignmentShift` is applied by outcome, via `CauseMiracle` carried on
   `NeedTriggeredEvent`/`NeedResolvedEvent` (valid only when `ByPlayer`).
3. Pause is real: `PausedState` + `ITimeScaleProvider` + `PauseOverlayView`.
4. Worship popups anchor to the HUD — no world coordinates in Worship events.
5. Stall predicate = Faith≈0 ∧ no active needs ∧ no pending needs ∧ Worship < target.
6. FSM states are assembled from `IEnumerable<IGameStateBinding>`; LevelSelect/TempleMeta/
   WelcomeBack are `NullState` placeholders.
7. `BootstrapSettings.InitialState`, wired per scene through `GameLifetimeScope._initialState`.
8. `InputRouter` (Gameplay ⇔ LevelPlaying) gates hand / camera / miracle targeting.
9. Static `*Rules` classes stay static — see [[false-positive-static-rules-tables]].

**Why:** they follow the project's harder rules (services stay pure C#, SO holds the data, one
source per identifier) and the step's scope in `docs/cli-workflow.md`.

**How to apply:** treat as settled; report only drift risk. Live risks worth watching later:
(2) `Blasted` shares the `Grabbed`→`Tossed` branch (see [[step4-accepted-deviations]]), so the
`Tossed→Stunned` predicate cannot tell a hand throw from a Fireball blast — `HandTerrorAlignmentShift`
and `VillagerHardLanding` Fear stack on top of the miracle's own shift; (6) the binding collection
is where the step-5 blockers lived (multi-registration lifetime + a cycle back to `GameFlowService`,
see [[vcontainer-review-false-positives]]); (8) `InputRouter` snapshots `IGameFlowService.Current`
in its ctor while `GameFlowService.Start()` deliberately publishes no event — any consumer resolved
before bootstrap latches the wrong context. Delivery conventions: [[review-format-and-scope]].
