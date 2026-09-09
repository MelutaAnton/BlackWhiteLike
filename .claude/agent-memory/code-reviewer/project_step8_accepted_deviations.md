---
name: step8-accepted-deviations
description: Owner-preapproved design decisions for step 8 (Meta + idle, Temple scene, economy/upgrades/offline income) — assess risk only, never re-litigate as violations
metadata:
  type: project
---

Owner pre-accepted (2026-09-02, step 8 review request) — do NOT report these as violations:

1. **State lives in root, step logic in scene services.** `PlayerProfile` + `MetaProgressService`
   are root-scoped; `MetaProgressService` implements BOTH `IMetaProgressService` and
   `ILevelProgressService` on one instance (`LevelProgressService.cs` deleted). `EconomyService`/
   `EffectiveStatsService`/`IdleIncomeService` are per-scene. `EventBus` stays per-scene.
2. **The `_initialState == LevelPlaying` gate** in `GameLifetimeScope.Configure` is the intended
   mechanism (verified: `_TestVillage`/`_TestHand`/`Village_Base` are all serialized as
   `LevelPlaying`). The gate itself is approved — its *holes* are still findings (see
   [[recurring-defects]] class 8).
3. **Continue → `ClearSelection()` + load Temple**; `CampaignContext(root != null)` replaces the
   step-7 `TryGetSelected` proxy for "is the real campaign flow available".
4. **§15 safeguards are deliberate:** `RewardEarnedEvent.XpAmount` is hardcoded 0, `IPlayerProfile`/
   `IProfileStorage`/`ITimeProvider` exist as seams only, `CreatureTeaserView` is a logic-free anchor
   (its unused `_root` field and `TempleView._creatureTeaser` are intentional, not dead code).
5. **`UpgradeLevelEntry.EffectValue` semantics are split by category** — percent for HandPower/
   MiraclePower multipliers, absolute for `FaithCapacityBonus`. Documented on
   `IEffectiveStatsService`; that interface is the only reader.

## Verified-correct facts worth reusing (don't re-derive)

- `ContainerBuilderUnityExtensions.cs:138` — `RegisterComponent` adds
  `builder.RegisterBuildCallback(container => container.Resolve<TInterface>(key))`. So a
  `RegisterComponent`'d MonoBehaviour's `[Inject] Construct` parameters must ALL be registered, and a
  miss throws at `LifetimeScope.Build()`, before any `Start()`. Conditional `RegisterComponent` by
  `!= null` does not protect against a missing *dependency* of that component.
- `GameFlowService` iterates `IEnumerable<IGameStateBinding>` **inside its constructor**, so the
  whole state table (and every state's ctor deps) resolves eagerly the moment `GameFlowService` is
  resolved by the `EntryPointDispatcher` build callback.
- `StateMachine.TryGetState` on a missing key logs `Debug.LogError` and returns null — it does NOT
  throw. Omitting a state binding in a scene that never transitions to it is therefore safe, which
  makes "gate the binding" a legitimate minimal fix.
- `EventBus` has no replay/sticky channel (`EventBus.Publish` no-ops when no channel exists yet), and
  `EntryPointDispatcher` calls `IStartable.Start()` in registration order. Publish-in-`Start()` only
  reaches subscribers registered earlier.
- `LevelHudPresenter.Start()` reads `_alignmentService.Score` directly (not only the event), so
  seeding `AlignmentService._score` from the profile in its ctor without publishing
  `AlignmentChangedEvent` is correct — no missing initial value, no double publication.
- `BuildSettingsEditor.EnsureCampaignScenesInBuildSettings` skips canonical paths that don't exist on
  disk, and `TempleSceneSetupEditor` calls it AFTER `SaveScene` — so Temple lands in Build Settings on
  the same run. A 3-entry `EditorBuildSettings.asset` before the scene is built is expected, not a bug.

Delivery conventions: [[review-format-and-scope]]. Defect classes: [[recurring-defects]].
