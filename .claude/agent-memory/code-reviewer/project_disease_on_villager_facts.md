---
name: disease-on-villager-verified-facts
description: Verified facts from the "Disease переезжает на жителя" review (2026-09-12) — VContainer optional-ctor-param trap, CFXR4 Flies Cloud has no CFXR_Effect, agony.anim already loops, AnyState priority vs StandUp trigger
metadata:
  type: project
---

Facts verified by reading source/YAML during the Disease-on-villager review (2026-09-12). Reuse, don't re-derive.

- **VContainer ignores C# optional constructor parameters.** `ReflectionInjector.CreateInstance`
  (`Runtime/Internal/ReflectionInjector.cs:44`) calls `ResolveOrParameter` for EVERY parameter;
  with no `.WithParameter(...)` on the registration, `parameters` is null and it goes straight to
  `resolver.Resolve(type)` → `Container.cs:93` `VContainerException: No such registration of type`.
  `TypeAnalyzer` (`TypeAnalyzer.cs:227-246`) picks the max-parameter ctor when none is `[Inject]`-marked,
  so an "optional test seam" like `(IEventBus, IVillagerStateService, System.Random random = null)`
  is always selected and always fails. Fixes: `.WithParameter(new System.Random())`, or a 2-arg
  `[Inject]` ctor delegating to the 3-arg one.
  **How to apply:** flag any DI-registered pure-C# service whose ctor has a defaulted parameter of a
  type nobody registers — it dies at `Build()`, not lazily, because `RegisterComponent`'d consumers
  force injection in a build callback.

- **`Assets/ThirdParty/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR4 Flies Cloud.prefab`
  contains exactly one GameObject with a ParticleSystem + renderer — no `CFXR_Effect`, no AudioSource,
  no children.** `looping: 1`, `playOnAwake: 1`, `stopAction: 0`. So `FixCfxrClearBehaviorToNone` on it
  is a permanent no-op that logs a Warning every run (breaks "second run is silent").
  Not every CFXR prefab carries `CFXR_Effect` — check the YAML before asserting the component exists.

- **`Assets/Content/Animations/Payne/agony.anim`**: humanoid muscle clip (RootT/RootQ + muscle float
  curves), `m_StopTime: 3.7`, and **`m_LoopTime: 1` already in the source** (`m_LoopBlend: 0`).
  Any comment claiming the source is "не зациклен / m_LoopTime=0" is wrong.

- **Animator priority:** AnyState transitions are evaluated before the current state's own transitions.
  `Villager.controller` enters `StandUp` only via `StunnedLying -> StandUp` (a trigger transition,
  NOT AnyState) — so a new `AnyState -> X` whose condition becomes true on the same frame the
  `StandUp` trigger is set will pre-empt the get-up and leave the trigger armed (Unity triggers
  persist until a transition consumes them).

- **`IVillagerSpawnService.DespawnAll` has zero callers** in the whole codebase (grep, 2026-09-12) —
  any remark that a limitation is "reachable only through DespawnAll at level end" is describing an
  unreachable path; the real reachable path is a manual `SetActive(false)` / pool release.

- `VillagerId` is `readonly struct : IEquatable<VillagerId>` with `GetHashCode() => Value`, so
  `Dictionary<VillagerId, …>` lookups in hot handlers do not box. Don't flag them.
