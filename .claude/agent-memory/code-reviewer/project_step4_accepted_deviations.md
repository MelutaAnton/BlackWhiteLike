---
name: step4-accepted-deviations
description: Owner-preapproved design decisions for step 4 (Чудеса + Faith) — assess risk only, never re-litigate as violations
metadata:
  type: project
---

Owner pre-accepted (2026-08-28, step 4 review request):

1. Cast is a stateful session in `MiracleService` (`CanCast/BeginCast/UpdateCast/ReleaseCast/CancelCast`); the `ICommand` pattern is deliberately **not** used.
2. `VillagerStimulus.Blasted` shares the `Grabbed` branch of the transition table (both → `Tossed`).
3. `ByPlayer` added to `NeedTriggeredEvent`/`NeedExpiredEvent` (game-design §13 table not yet updated to match).
4. `FaithBarView` + presenter are extra scope beyond the step's brief.
5. `IInputProvider.IsPointerOverUi` as a predicate on the provider (full `InputRouter` with Gameplay/UI contexts is step 5).
6. A `NeedSite` serves exactly one `NeedType`; Fireball only ignites a Fire site.
7. Miracle↔need mapping lives in `MiracleConfig` data (`ResolvesNeeds`/`CreatesNeed`), not in service branches.

**Why:** these follow the project's harder rules (services stay pure C#, no branch-per-type in services, SO holds the mapping) and the step's scope in `docs/cli-workflow.md`.

**How to apply:** treat as settled; report only drift risk. Live risks worth watching in later steps: (3) the §13 table drifting further from the structs; (6) a second need type on one site breaking `NeedSiteController._isNeedActive`; (1) `MiracleService` holding one global session — a second simultaneous caster (creature, step 6+) would need a session per caster. Delivery conventions: [[review-format-and-scope]]. Earlier steps: [[step3-accepted-deviations]].
