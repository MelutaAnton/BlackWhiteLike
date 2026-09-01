---
name: step7-accepted-deviations
description: Owner-preapproved design decisions for step 7 (campaign of 5 levels, root scope, LevelSelect, progress persistence) — assess risk only, never re-litigate as violations
metadata:
  type: project
---

Owner pre-accepted (2026-09-01, step 7 review request):

1. **Root↔scene is NOT VContainer parenting** but a controlled `static RootLifetimeScope.Instance`.
   Rationale from the package source: a `parentReference` whose parent is absent parks the scope
   forever, and test scenes without a root must still run standalone. Scene-side registrations in
   `GameLifetimeScope.RegisterCampaignFlow` are conditional with local fallbacks + `LogWarning`.
2. One baked village: `LevelConfig_02..05` all point at the shared `VillageLayout_01`; progression
   is data only (worshipTarget / villagerCount / coinsReward / timeline).
3. `EventBus` stays per-scene; progress is written by the scene-side `LevelCompletionRecorder`
   (`IStartable`+`IDisposable`) forwarding `LevelCompletedEvent` to the root `ILevelProgressService`.
4. Persistence via `IProfileStorage` + `ProfileKeys.CompletedLevels` (`;`-joined enum names).
5. LevelSelect is a panel inside `MainMenu.unity` (sibling of MainMenuPanel), FSM binding
   `LevelSelect → LevelSelectState`.
6. `RegisterEntryPoint<GameBootstrap>()` moved to right after `GameFlowService` so the FSM start
   state exists before presenters read `IGameFlowService.Current` in their `Start()`.

**Why:** they follow the project's harder rules and the step scope in `docs/cli-workflow.md`.

**How to apply:** settled — report drift risk only.

## Verified-correct facts worth reusing (don't re-derive)

- `EntryPointDispatcher.Dispatch` resolves `IReadOnlyList<IStartable>` from the container, so
  **`IStartable.Start()` runs in registration order**. Moving `GameBootstrap` earlier is therefore a
  real ordering change, and it does NOT disturb `ITickable` order (GameBootstrap is not tickable).
- Moving `GameBootstrap` first is safe *today* only because `GameFlowService.Start(initial)`
  deliberately publishes no `GameStateChangedEvent` and `LevelPlayingState.Enter()` →
  `LevelFlowService.StartLevel()` only sets flags (`_elapsed/_isRunning/_isCompleted/_isStalled`),
  publishing nothing. **The moment any `IState.Enter()` starts publishing events, every startable
  registered after `GameBootstrap` will miss them.** Re-check this on every new state.
- `RegisterInstance`-provided singletons are NOT disposed when the child scope closes
  (`ExistingInstanceProvider`), which is exactly why passing root services into the scene scope by
  `RegisterInstance` is safe — the root services survive scene unload.
- Worship math (needed for any balance judgement): decay is **off**
  (`_worshipDecayPerSecond: 0`), Awe is Fire 20 / Disease 20 / Hunger 15, so Awe available on a
  level = sum over its timeline. The remainder of `WorshipTarget` must come from Fear (terror acts,
  8–25 each), and Fear farming scales with `villagerCount`.

Delivery conventions: [[review-format-and-scope]]. Defect classes: [[recurring-defects]].
