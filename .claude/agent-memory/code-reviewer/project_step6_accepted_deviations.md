---
name: step6-accepted-deviations
description: Owner-preapproved design decisions for step 6 (Heal + Lightning, Disease/Hunger, alignment visual) — assess risk only, never re-litigate as violations
metadata:
  type: project
---

Owner pre-accepted (2026-09-01, step 6 review request):

1. Lightning on a villager = instant `Stunned` via new `VillagerStimulus.LightningStruck`; Fear via
   new `TerrorAct.VillagerStruckByLightning = 8`. FSM: from `Tossed` ignored, from `Stunned` self-loop
   without an event (Fear cannot be farmed).
2. Lightning igniting a building is **data only** — `Miracle_Lightning.CreatesNeed = Fire`; Fear rides
   the existing `TerrorAct.LightningStrike = 20` through `NeedTriggered` → `TerrorActRules`. No effect
   on other objects.
3. Expired `Disease` despawns villagers inside `PanicRadius` through `IVillagerFactory.Despawn`
   (skipping `Tossed`); `NeedSiteController` gained that dependency.
4. `AlignmentVisualConfig` (+ `AlignmentUiPalette`, deliberately read by nobody yet),
   `AlignmentVisualController` in `Controllers.Temple`; materials are `sharedMaterial` refs to material
   **assets**; `_decalPrefabsToSpawn` / `_vfxToActivate` are unread placeholders, Saint/Demon stages
   intentionally empty.
5. Command pattern still excluded; cast session stays in the service (carried from step 4/5).

**Why:** they follow the harder project rules (services pure C#, SO holds data, one source per
identifier) and the step's scope in `docs/cli-workflow.md`.

**How to apply:** treat as settled; report only drift risk. Live risks worth watching later:
(1)+(2) a Lightning strike that hits no `NeedSite` produces Fear but **zero** `AlignmentShift` —
shift is only applied by outcome via `CauseMiracle` on `NeedTriggered`/`NeedResolved`
(see [[step5-accepted-deviations]] item 2), same as a Fireball blasting only villagers.
(4) the empty Saint/Demon stages own the outer score bands ([75, 100] and [-100, -75)), so a large
single shift or a restored score would leave hand/temple on the previously applied stage —
harmless while shifts are ±10 and always pass through Kind/Cruel.
Delivery conventions: [[review-format-and-scope]]. Defect classes: [[recurring-defects]].
