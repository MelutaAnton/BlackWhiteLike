# Code Reviewer Memory

- [VContainer review false positives](feedback_vcontainer_false_positives.md) — `RegisterEntryPoint<T>().As<I>()` is redundant-but-correct, never a blocker; hunt the opposite (plain `Register` on a service that needs ticking).
- [Review format and scope](feedback_review_format.md) — triage блокер/важно/на будущее with file:line, never self-fix, trace the failing input before calling a rule satisfied.
- [Step 2 accepted deviations](project_step2_accepted_deviations.md) — hand events published by the controller and `Construct(IEventBus, HandConfig)` are settled; report drift risk only.
- [Step 3 accepted deviations](project_step3_accepted_deviations.md) — `Vector3` creature spawn and self-sufficient `Construct()` are settled; report only the nullable-loss and mixed-path drift.
- [Recurring defect classes](project_recurring_defects.md) — editor script vs runtime physics disagreement, and new `[SerializeField]` left unwired by the editor script that places the component.
- [Static *Rules-классы — не нарушение DI](false-positive-static-rules-tables.md) — почему `VillagerMovementRules`/`VillagerPoseRules` без интерфейса это норма
