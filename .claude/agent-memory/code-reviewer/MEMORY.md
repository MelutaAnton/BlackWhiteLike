# Code Reviewer Memory

- [VContainer review false positives](feedback_vcontainer_false_positives.md) — `RegisterEntryPoint<T>().As<I>()` is redundant-but-correct, never a blocker; hunt the opposite (plain `Register` on a service that needs ticking).
- [Review format and scope](feedback_review_format.md) — triage блокер/важно/на будущее with file:line, never self-fix, trace the failing input before calling a rule satisfied.
- [Step 2 accepted deviations](project_step2_accepted_deviations.md) — hand events published by the controller and `Construct(IEventBus, HandConfig)` are settled; report drift risk only.
