---
name: recurring-defects
description: Defect classes that keep recurring in this project's editor-script + runtime hand-off — check these first on any step review
metadata:
  type: project
---

Six defect classes have now appeared across steps and are worth checking first on every review:

1. **Editor script and runtime disagree about physics state.** The editor script bakes a component setup the runtime never completes. Step 3 example: `VillagerPrefabSetupEditor` writes `isKinematic = true; useGravity = false` on `Villager.prefab` with the comment "рантайм переключит при Tossed", but `VillagerController.ApplyPhysicsForState` only touches `constraints` — nothing ever clears `isKinematic`, so every `linearVelocity` write is a no-op and no `OnCollisionEnter` fires against static ground.
2. **`[SerializeField]` added to a controller but never wired by the editor script that places it.** Step 3 example: `TestVillageSceneSetupEditor.EnsureNeedSite` adds `NeedSiteController` but leaves `_siteIndex`, `_needType` and the three visual refs at their defaults, so every site is `(Fire, 0)` and `SetVisualState` is a silent no-op. `.claude/rules/editor-files.md` names this class ("проверять результат численно"), which makes it a regression, not a new discovery.

3. **ThirdParty prefab instances in scenes or nested inside our prefabs.** Step 4 (found by the owner
   on playtest, missed by review): scene houses/field were instances of Synty prefabs with our
   `NeedSiteController` on top; `Villager.prefab`, `GodHand.prefab` and `Vfx_FireballExplosion.prefab`
   held nested instances of `SM_Chr_Peasant_Male_01`, `MaleHand.FBX` and a CFXR prefab. Owner rule
   (CLAUDE.md «Из ThirdParty в сцену... только визуал»): only asset-level references (mesh, material,
   clip, avatar) may point into `Assets/ThirdParty/`; prefabs/models are instantiated through
   `EditorPrefabUtility.InstantiateUnpacked` and saved as our own prefab. **Check:** every
   `PrefabUtility.InstantiatePrefab` call in a diff — if the argument can be a ThirdParty asset and it is
   not immediately unpacked, it is a blocker; in YAML, `m_SourcePrefab: {... guid: X}` whose GUID resolves
   under `Assets/ThirdParty` is a blocker.

4. **DI graph defects that only surface at `Build()`.** Step 5: (a) seven `Register<IGameStateBinding>(r => ..., Singleton)` — VContainer's `CollectionInstanceProvider` rejects same-`ImplementationType` singletons ("Conflict implementation type"); (b) a cycle hidden behind the Func: `GameFlowService(IEnumerable<IGameStateBinding>)` → `LevelPlayingState(ILevelFlowService)` → `LevelFlowService(IGameFlowService)` — `TypeAnalyzer` cannot see through lambdas, so it dies at runtime as `Lazy<T>` re-entrancy. Also (c) `InputRouter` snapshotting `gameFlow.Current` in its ctor: `RegisterComponent` build-callbacks resolve it BEFORE `GameBootstrap.Start()`, and `Start()` publishes no event → latched to UI forever. **Check:** every `Register<T>(Func)` in `GameLifetimeScope` — are there ≥2 for the same interface? Walk ctor deps of every type resolved inside a Func back to the Func's owner. Any service that caches FSM state in its ctor is wrong — read `gameFlow.Current` lazily.

5. **A "generic ThirdParty wrapper" editor helper drops the per-pack neutralisation the hand-written
   one did.** Step 6: `MiracleVfxPrefabSetupEditor.SetupWrappedThirdPartyVfxPrefab` (new, generic) omits
   `main.loop = false`, which the two hand-written builders (`SetupFireballExplosionPrefab`,
   `SetupRainCastPrefab`) do explicitly. Both Hovl sources are 100 % looping (`Buff heal` 8/8,
   `Lightning strike` 12/12 `looping: 1`), and `VfxInstanceController` returns to the pool on
   `!IsAlive(withChildren: true)` — a looping system never satisfies it, so every cast rides the
   `_maxLifetime` fallback and logs a warning. **Check for any pooled ThirdParty visual:** grep the
   source `.prefab` YAML for `looping: 1`, `playOnAwake: 1`, `stopAction:` (non-zero = self-destruct),
   `^AudioSource:` (`m_PlayOnAwake`/`Loop`), and `m_Script:` GUIDs resolved against `.meta` files —
   YAML answers all of these without opening Unity. Disabling a helper `Behaviour` IS sufficient for
   its own callbacks (Unity skips `OnParticleCollision`/`Start` on `enabled == false`), but it says
   nothing about ParticleSystem/AudioSource settings baked into the asset.

6. **An SO whose initializer runs only at asset creation, shipped without a companion migration.**
   Step 6: `ConfigAssetCreatorEditor` grew `MigrateMiracleCatalogEntries` and
   `MigrateWorshipTerrorFearEntries` for exactly this reason, but the new `AlignmentVisualConfig`
   descriptor got none — and `CreateAll` is itself a menu item (`Configs/Create Empty SO Set`), so
   running it before the material-producing script bakes an asset full of nulls that nothing ever
   repairs. **Check:** every new `ConfigAssetDescriptor` whose initializer depends on assets produced
   by a *different* menu item.

**Why:** `code-writer` and `editor-script-creator` run in parallel (CLAUDE.md workflow), so the contract between prefab/scene state and runtime state has no single owner.

**How to apply:** for any new MonoBehaviour in a diff, grep every `[SerializeField]` name against the editor scripts that instantiate it, and grep every physics property the runtime *reads* (`isKinematic`, `useGravity`, `constraints`, `collisionDetectionMode`) against what the editor script *writes*. Report mismatches as blockers — both defect classes are invisible until playtest. For material-swapping controllers, also compare renderer **count and material heterogeneity** in the source prefab YAML against what the config's per-stage array can express: step 6's Synty church has 2 MeshRenderers with 2 *different* materials, but `AlignmentStage._templeMaterials` is a slot array, so a 1-element stage silently repaints both.
