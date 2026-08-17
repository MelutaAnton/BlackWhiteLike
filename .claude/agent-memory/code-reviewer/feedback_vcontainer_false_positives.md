---
name: vcontainer-review-false-positives
description: VContainer registration patterns that look like bugs during review but are correct — checked against the package source in Library/PackageCache
metadata:
  type: feedback
---

`builder.RegisterEntryPoint<T>(lifetime).As<ISomething>()` is NOT a bug — do not flag it as a lost/duplicate registration.

**Why:** verified in `Library/PackageCache/jp.hadashikick.vcontainer@*/Runtime/Unity/ContainerBuilderUnityExtensions.cs` — `RegisterEntryPoint<T>` is literally `builder.Register<T>(lifetime).AsImplementedInterfaces()`. It already registers *every* implemented interface (both `ITickable`/`IStartable` and the project's own service interface), and `RegistrationBuilder.AddInterfaceType` (Runtime/RegistrationBuilder.cs) de-duplicates, so a following `.As<T>()` is a silent no-op rather than a double registration. Singleton lifetime means the entry-point instance and the interface resolve to the same object.

**How to apply:** when reviewing `GameLifetimeScope`, treat this as a readability nit ("`.As<>` is redundant here"), never a blocker. The real registration bug to hunt for is the opposite case: a service that needs per-frame work registered with plain `builder.Register<T>()` and therefore never ticked, because `Register` does not wire `ITickable`.
