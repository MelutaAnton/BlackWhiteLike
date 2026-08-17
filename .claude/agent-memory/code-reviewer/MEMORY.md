# Code Reviewer Memory

- [VContainer review false positives](feedback_vcontainer_false_positives.md) — `RegisterEntryPoint<T>().As<I>()` is redundant-but-correct, never a blocker; hunt the opposite (plain `Register` on a service that needs ticking).
