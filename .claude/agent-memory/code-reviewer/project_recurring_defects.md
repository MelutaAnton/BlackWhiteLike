---
name: recurring-defects
description: Defect classes that keep recurring in this project's editor-script + runtime hand-off — check these first on any step review
metadata:
  type: project
---

Two defect classes have now appeared across steps and are worth checking first on every review:

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

**Why:** `code-writer` and `editor-script-creator` run in parallel (CLAUDE.md workflow), so the contract between prefab/scene state and runtime state has no single owner.

**How to apply:** for any new MonoBehaviour in a diff, grep every `[SerializeField]` name against the editor scripts that instantiate it, and grep every physics property the runtime *reads* (`isKinematic`, `useGravity`, `constraints`, `collisionDetectionMode`) against what the editor script *writes*. Report mismatches as blockers — both defect classes are invisible until playtest.
