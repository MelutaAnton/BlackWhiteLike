# Code Reviewer Memory

- [VContainer review false positives](feedback_vcontainer_false_positives.md) — `RegisterEntryPoint<T>().As<I>()` is redundant-but-correct, never a blocker; hunt the opposite (plain `Register` on a service that needs ticking).
- [Review format and scope](feedback_review_format.md) — triage блокер/важно/на будущее with file:line, never self-fix, trace the failing input before calling a rule satisfied.
- [Step 2 accepted deviations](project_step2_accepted_deviations.md) — hand events published by the controller and `Construct(IEventBus, HandConfig)` are settled; report drift risk only.
- [Step 3 accepted deviations](project_step3_accepted_deviations.md) — `Vector3` creature spawn and self-sufficient `Construct()` are settled; report only the nullable-loss and mixed-path drift.
- [Step 4 accepted deviations](project_step4_accepted_deviations.md) — сессия каста без Command, `Blasted`→`Tossed`, `ByPlayer`, `IsPointerOverUi` — решено, только drift risk.
- [Step 5 accepted deviations](project_step5_accepted_deviations.md) — WorshipConfig-only значения, CauseMiracle, пауза, IGameStateBinding, InputRouter — решено, только drift risk.
- [Step 6 accepted deviations](project_step6_accepted_deviations.md) — Lightning→Stunned, Disease-despawn, alignment-визуал — решено, только drift risk.
- [Step 7 accepted deviations](project_step7_accepted_deviations.md) — root `static Instance`, one baked village, per-scene EventBus + LevelCompletionRecorder; plus verified IStartable-order and Worship-math facts.
- [Step 9 accepted deviations](project_step9_accepted_deviations.md) — PinchGestureRules static, unconditional NullHapticsService, DI-less SafeAreaFitter, 88-unit hit zone — решено, только drift risk.
- [Step 8 accepted deviations](project_step8_accepted_deviations.md) — Meta + idle, Temple, экономика/апгрейды/offline income — решено, только drift risk.
- [Step 10 accepted deviations](project_step10_accepted_deviations.md) — Tick без гейта `_levelFinished`, LevelStalled/HasPendingNeeds вне скоупа, семантика NeedTriggerMode и `EntryState.Previous` по ссылке.
- [Step 10 wave 2 open questions](project_step10_wave2_open_questions.md) — runtime `FindObjectsByType` ждёт решения владельца; проверенные факты про Pop без Enter, timeScale-страховки, PopupRoot.
- [Recurring defect classes](project_recurring_defects.md) — editor script vs runtime physics disagreement, and new `[SerializeField]` left unwired by the editor script that places the component.
- [Static *Rules-классы — не нарушение DI](false-positive-static-rules-tables.md) — почему `VillagerMovementRules`/`VillagerPoseRules` без интерфейса это норма
- [Disease-на-жителе: проверенные факты](project_disease_on_villager_facts.md) — VContainer игнорирует optional-параметры ctor; CFXR4 Flies Cloud без CFXR_Effect; agony.anim уже loop; AnyState > StandUp-триггер.
