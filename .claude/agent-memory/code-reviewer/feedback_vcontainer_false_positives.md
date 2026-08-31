---
name: vcontainer-review-false-positives
description: VContainer registration patterns that look like bugs during review but are correct — checked against the package source in Library/PackageCache
metadata:
  type: feedback
---

`builder.RegisterEntryPoint<T>(lifetime).As<ISomething>()` is NOT a bug — do not flag it as a lost/duplicate registration.

**Why:** verified in `Library/PackageCache/jp.hadashikick.vcontainer@*/Runtime/Unity/ContainerBuilderUnityExtensions.cs` — `RegisterEntryPoint<T>` is literally `builder.Register<T>(lifetime).AsImplementedInterfaces()`. It already registers *every* implemented interface (both `ITickable`/`IStartable` and the project's own service interface), and `RegistrationBuilder.AddInterfaceType` (Runtime/RegistrationBuilder.cs) de-duplicates, so a following `.As<T>()` is a silent no-op rather than a double registration. Singleton lifetime means the entry-point instance and the interface resolve to the same object.

**How to apply:** when reviewing `GameLifetimeScope`, treat this as a readability nit ("`.As<>` is redundant here"), never a blocker. The real registration bug to hunt for is the opposite case: a service that needs per-frame work registered with plain `builder.Register<T>()` and therefore never ticked, because `Register` does not wire `ITickable`.

## Tick ordering vs MonoBehaviour.Update (verified, step 4)

`PlayerLoopHelper.Initialize` (`Runtime/Unity/PlayerLoopHelper.cs`) inserts `VContainerUpdate` **before** `Update.ScriptRunBehaviourUpdate`. So every `ITickable.Tick()` runs *before* all `MonoBehaviour.Update()` in the same frame — including `EventSystem.Update()`.

**Why it matters at review time:** anything a tickable reads that is produced by a MonoBehaviour later in the frame is one frame stale. Concretely: `EventSystem.IsPointerOverGameObject()` (the Input System module documents this) returns last frame's raycast state when polled from a tickable. Mouse hover hides it; touch has no hover, so the press frame reads a stale `false`.

**How to apply:** when a tickable input/state provider caches something UI- or physics-derived, check who writes it and in which player-loop phase. Cross-phase reads belong in a lazily-evaluated property, not a value cached in `Tick()`.

## Real registration bugs, verified in package source (step 5)

Two failure modes that DO belong in a review, both traced in
`Library/PackageCache/jp.hadashikick.vcontainer@*/Runtime/`:

1. **N× `builder.Register<IFoo>(r => ..., Lifetime.Singleton)` for the same contract throws at
   `Build()`** — `Internal/InstanceProviders/CollectionInstanceProvider.Add` rejects a second
   Singleton registration whose `ImplementationType` matches one already in the collection, with
   `VContainerException: Conflict implementation type`. A delegate registration's
   `ImplementationType` is the *contract* itself, so every such lambda collides. `RegisterInstance`
   has the same problem when the instances share a concrete type. Multi-registration works with
   `Lifetime.Transient` (the guard only checks Singleton), distinct concrete types, or distinct keys.

2. **`IEnumerable<T>` collections are resolved EAGERLY, so cycles hide behind lambdas.**
   `CollectionInstanceProvider.SpawnInstance` resolves every element immediately, and delegate
   registrations are opaque to `TypeAnalyzer.CheckCircularDependency` — so a cycle routed through
   `IEnumerable<T>` + a factory lambda passes build validation and then dies at instantiation:
   `Container.cs` stores singletons in `ConcurrentDictionary<Registration, Lazy<object>>`, and
   re-entering the same `Lazy.Value` on one thread throws `InvalidOperationException`.

**How to apply:** whenever a diff introduces "collection of bindings/handlers/strategies", check
(a) the lifetime of the multi-registration and (b) whether any element's dependency chain leads
back to the type that consumes the collection.

## IStartable runs AFTER every build callback (verified, step 5)

`EntryPointDispatcher.Dispatch()` is itself a build callback, but it only *schedules* startables via
`PlayerLoopHelper.Dispatch(PlayerLoopTiming.Startup, ...)`. `RegisterComponent`'s forced
`container.Resolve` build callbacks therefore run BEFORE any `IStartable.Start()`.

**Why it matters at review time:** a service that snapshots state in its constructor sees the
*pre-bootstrap* world if any `RegisterComponent`'d MonoBehaviour depends on it — even though
`GameBootstrap` is registered last. Check every ctor that reads `IGameFlowService.Current`,
`.Current`-style properties, or scene state instead of subscribing.
