---
name: recurring-defects
description: Defect classes that keep recurring in this project's editor-script + runtime hand-off — check these first on any step review
metadata:
  type: project
---

Defect classes that have appeared across steps and are worth checking first on every review:

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
   under `Assets/ThirdParty` is a blocker. (A connected instance of OUR OWN `Development/Prefabs/...`
   asset is correct and must NOT be flagged — step 8's `TempleSceneSetupEditor` does this deliberately.)

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
   by a *different* menu item. (Step 8 got this right — three new descriptors, three migrations.)

7. **A central class delivered as an empty stub while everything else is written against its full
   API.** Step 7: `Installers/RootLifetimeScope.cs` shipped as a bare `LifetimeScope` with an empty
   `Configure` and no members at all, while `GameLifetimeScope.Configure` calls
   `RootLifetimeScope.Instance` / `.LevelCatalog` / `.LevelSelection` / `.LevelProgress` /
   `.SceneLoader`, `BootstrapSceneSetupEditor` wires `_levelCatalog` on it, and
   `BootstrapFlowEntryPoint` exists with no registration anywhere — i.e. the project does not
   compile. The surrounding xml-doc even *describes* the missing behaviour ("см. remarks
   RootLifetimeScope"), which makes the stub easy to read past. **Check:** for every new type the
   diff introduces, grep its members against every call site before reviewing anything else; a
   `<remarks>` cross-reference to a file is not evidence the file implements it.

8. **A new conditional gate around registrations orphans the dependencies of things left OUTSIDE the
   gate.** Step 8 (three blockers from ONE gate): `GameLifetimeScope.Configure` grew
   `if (_initialState == GameState.LevelPlaying)` around village/miracle/worship registrations. But
   (a) `LevelPlayingState` stayed registered unconditionally in the always-built
   `IEnumerable<IGameStateBinding>` table while its ctor dep `ILevelFlowService` moved inside the
   gate — `GameFlowService` iterates the bindings **in its constructor**, so the collection resolves
   eagerly and every meta scene dies at `Build()` (MainMenu.unity was a step-7 regression, not just
   the new scene); (b) `RegisterAlignmentVisual` stayed ungated while `AlignmentVisualController.
   Construct` needs the now-gated `IAlignmentService`, and `RegisterComponent` **forces injection via
   a build callback** (`ContainerBuilderUnityExtensions.cs:138`), so it also throws at `Build()`;
   (c) a service gated on `root != null` (`IdleIncomeService`) was consumed by a presenter gated only
   on `_view != null` (`TemplePresenter`).
   **Check on every new gate:** list what the gate now EXCLUDES, then grep every excluded contract
   for consumers that are still registered unconditionally — including indirect ones reached through
   an eagerly-resolved `IEnumerable<T>` and through `RegisterComponent`'d MonoBehaviour `Construct`
   signatures. Two conditions on the same object graph (`_initialState` and `root != null`) must be
   checked as a matrix, not one at a time.

9. **`IStartable` that PUBLISHES an event registered before the `IStartable`s that SUBSCRIBE to it.**
   Step 8: `IdleIncomeService` (registered line 718) computes offline income and publishes
   `IdleIncomeReadyEvent` in `Start()`; `WelcomeBackPresenter` (723) and `TemplePresenter` (734)
   subscribe in *their* `Start()`. `EntryPointDispatcher` resolves `IReadOnlyList<IStartable>` and
   calls `Start()` in registration order, and `EventBus` has no replay/sticky channel — so the event
   is published into an empty channel and the whole feature is silently dead. This is the concrete
   realisation of the step-7 warning already in [[step7-accepted-deviations]] ("the moment any
   `IState.Enter()` starts publishing events, every startable registered after will miss them").
   **Check:** for every `IStartable` in a diff, does its `Start()` call `_eventBus.Publish`? If yes,
   every subscriber must either be registered EARLIER or read the state through a pull API
   (`bool HasPending { get; }`) instead of the event.

10. **An editor script measures a `RectTransform` by `sizeDelta` instead of `rect.size`.** Step 9:
    `TouchHitZoneEditor.EnsureMinHitZone` grows `sizeDelta` to a minimum and logs "72 -> 88" as if that
    were the rendered size. For a *stretched* rect (`anchorMin != anchorMax` on that axis) `sizeDelta` is
    an **offset from the parent's rect**, not a size: `TempleCanvas/TemplePanel/NextLevelButton` has
    `anchorMin (0.98, 0.04)` / `anchorMax (1, 0)` (the y pair is even inverted), so at 1920×1080 its real
    height is `72 + (0 - 0.04)*1080 = 28.8`, and setting `sizeDelta.y = 88` yields 44.8 — the audit reports
    success while the button stays under half the target. **Check:** any diff that reads or writes
    `sizeDelta` as a size — it is only equal to the size when `anchorMin == anchorMax` on that axis.
    Correct form: measure `rt.rect.size`, grow via `rt.sizeDelta += (min - rt.rect.size)` per axis.
    Layout groups are the *second* question here, not the first (step 9's `UpgradesColumn`
    `VerticalLayoutGroup` was harmless — all four `m_ChildControl*`/`m_ChildForceExpand*` are 0).

11. **A structural change to a canvas prefab silently breaks the sibling `Build ...` script that owns it.**
    Step 9: `SafeAreaSetupEditor` inserts a `SafeAreaRoot` node and reparents every direct child of the
    canvas root under it, but `LevelHudSetupEditor` / `MainMenuCanvasSetupEditor` /
    `TempleCanvasSetupEditor` each have a private `FindOrCreateUIChild(parent, name)` that does
    `parent.Find(name)` — root-only. After the insert, a rerun of any `Build ...` item does not just orphan
    nodes: it creates an EMPTY duplicate at the root and then `SetRef`s the view's `[SerializeField]`s to
    that duplicate (`LevelHudSetupEditor.cs:280-286` wires `_worshipFill`, `_pauseButton`, `_popupRoot`, …),
    i.e. the HUD goes blank with no error. **Check:** whenever a diff re-parents or wraps nodes inside a
    prefab another editor script builds, grep that script's node-lookup helper — a root-relative `Find`
    must learn the new intermediate node (step 9's own `TouchHitZoneEditor.ResolveRelative` shows the
    correct two-probe form: direct path, then `"SafeAreaRoot/" + path`). A warning emitted by the *inserting*
    script on its NEXT run is not a guard — it fires one run after the breakage.

12. **A safe-area wrapper swallows the full-screen backdrops it was supposed to leave alone.** Step 9:
    reparenting *all* canvas-root children under `SafeAreaRoot` also moves opaque full-stretch panels —
    `MainMenuPanel`, `LevelSelectPanel`, `TemplePanel` each carry an `Image` at `a: 1` (`0.08/0.08/0.12`) —
    so on a notched/cutout device the menu background stops painting the inset strips. **Check:** list the
    canvas root's direct children with their anchors + `Image.m_Color.a` before accepting a blanket
    reparent; anything full-stretch and opaque (backgrounds, `Dim` blockers) must stay outside the fitted
    node, only content goes inside.

**Why:** `code-writer` and `editor-script-creator` run in parallel (CLAUDE.md workflow), so the contract between prefab/scene state and runtime state has no single owner.

**How to apply:** for any new MonoBehaviour in a diff, grep every `[SerializeField]` name against the editor scripts that instantiate it, and grep every physics property the runtime *reads* (`isKinematic`, `useGravity`, `constraints`, `collisionDetectionMode`) against what the editor script *writes*. Report mismatches as blockers — both defect classes are invisible until playtest. For material-swapping controllers, also compare renderer **count and material heterogeneity** in the source prefab YAML against what the config's per-stage array can express: step 6's Synty church has 2 MeshRenderers with 2 *different* materials, but `AlignmentStage._templeMaterials` is a slot array, so a 1-element stage silently repaints both.
