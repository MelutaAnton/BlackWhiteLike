---
name: step3-accepted-deviations
description: Two design decisions the owner pre-accepted for step 3 (Жители + события) — assess risk only, never re-litigate as violations
metadata:
  type: project
---

Owner pre-accepted (2026-08-18, step 3 review request) two departures from `docs/game-design.md`:

1. `LevelConfig.CreatureSpawnPosition` is a `Vector3`, not the `Transform`/nullable `creatureSpawnPoint` of §15. Reason: a ScriptableObject must not hold a scene reference.
2. `VillagerController.Construct()` is self-sufficient — it registers the villager in `IVillagerStateService` and subscribes to `VillagerStateChangedEvent` itself, so statically placed villagers (via `autoInjectGameObjects`) and pooled ones (via `VillagerFactory`) share one path. `Launch()` is reduced to pool-reuse reset (zero velocities, clear wander target).

**Why:** both follow from harder project rules — SO stays scene-independent, and the FSM must work for villagers that never pass through the factory.

**How to apply:** report only *drift risk*, not violation. For (1) the live risk is that `Vector3` cannot express "not set" — §15 called the field nullable, so a future creature would silently spawn at world origin. For (2) the live risks are the mixed-path hazards: `Despawn` on a statically placed villager unregisters it while `PoolManager.Release` refuses it, and `Construct` is the *only* subscribe site, so a `SetActive(false)/(true)` cycle on a static villager drops the subscription permanently. Delivery conventions: [[review-format-and-scope]]. Step 2 settled items: [[step2-accepted-deviations]].
