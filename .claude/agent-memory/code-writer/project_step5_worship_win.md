---
name: project_step5_worship_win
description: Шаг 5 (Worship + win) реализован — WorshipService/AlignmentService/LevelFlowService, FSM-биндинги через IGameStateBinding, InputRouter, HUD/оверлеи уровня; что уже есть и не нужно дублировать
type: project
---

Шаг 5 «Worship + win» (cli-workflow.md) реализован целиком (2026-08-29), в паре с editor-скриптами
(тот же контракт имён передан обоим агентам одним промптом оркестратора). Не путать с шагом 6:
Heal/Lightning и AlignmentVisualController ещё НЕ реализованы.

**Что уже есть (не дублировать)**:
- `Models/WorshipSource.cs` (Awe/Fear), `Models/TerrorAct.cs` (FireballOnStructure/LightningStrike/
  VillagerHardLanding/ObjectHitStructure — Lightning/ObjectHit данные готовы, источники — шаг 6),
  `Models/InputContext.cs` (Gameplay/UI).
- `Core/Events/Events.Worship.cs` — `WorshipChangedEvent`, `AlignmentChangedEvent`,
  `LevelCompletedEvent`, `LevelStalledEvent`.
- `NeedTriggeredEvent`/`NeedResolvedEvent` (`Events.Village.cs`) несут 4-й параметр
  `MiracleType CauseMiracle` — валиден только при `ByPlayer == true`, для таймлайна/прочих путей —
  `default`. `NeedExpiredEvent` НЕ трогали — без CauseMiracle.
  `ILevelEventService.ResolveNeed`/`TriggerNeed` расширены тем же параметром (5 и 6 аргументов
  соответственно) — при новом call-site не забыть его передать.
- `Data/Configs/Worship/WorshipConfig.cs` + `NeedAweEntry.cs`/`TerrorFearEntry.cs` — `GetAwe(NeedType)`/
  `GetFear(TerrorAct)`, отсутствующая запись → 0 + LogWarning один раз на ключ. Поля:
  `_aweByNeed`, `_fearByAct`, `_worshipDecayPerSecond` (0 по умолчанию), `_handTerrorAlignmentShift` (-3).
- `MiracleConfig._worshipYield`/`WorshipYield` УДАЛЕНЫ — Worship больше не даётся за факт применения
  чуда, только через `WorshipService` по Awe/Fear (`WorshipConfig`).
- `Services/Worship/IWorshipService.cs`+`WorshipService.cs` (Current/Target/AweTotal/FearTotal/
  IsTargetReached/Tick, ITickable+IDisposable) — единая шкала 0..Target, клампится по Target,
  Awe/FearTotal некламплённые. `Services/Worship/TerrorActRules.cs` (static, Fireball→
  FireballOnStructure, Lightning→LightningStrike). `Services/Worship/IAlignmentService.cs`+
  `AlignmentService.cs` (Score −100..+100, сдвиг из `MiracleCatalogConfig[CauseMiracle].AlignmentShift`
  + `WorshipConfig.HandTerrorAlignmentShift` на hard landing).
- `Services/Level/ILevelFlowService.cs`+`LevelFlowService.cs` (StartLevel/Elapsed/IsRunning/IsStalled/
  Tick, ITickable+IDisposable) — win подпиской на `WorshipChangedEvent`, stall — 4 условия
  (Faith≤0.01 ∧ !HasActiveNeeds ∧ !HasPendingNeeds ∧ Worship<Target), однократный до сброса
  (Faith>эпсилон).
- `ILevelEventService`+`LevelEventService` — новые `HasActiveNeeds`/`HasPendingNeeds` (bool, линейный
  проход по внутренним записям).
- `IFaithService`+`FaithService` — новый `RefillToCapacity()` (SetCurrent(Capacity), публикует
  FaithChangedEvent только при реальном изменении).
- **FSM переработана**: `GameFlowService` больше НЕ принимает `MainMenuState` напрямую — ctor
  `(IEventBus, IEnumerable<IGameStateBinding> bindings)`. Новый слой `Services/Flow/IGameStateBinding.cs`
  +`GameStateBinding.cs` (Key+State), `Services/Flow/States/LevelPlayingState.cs` (Enter → 
  `ILevelFlowService.StartLevel()`), `LevelCompleteState.cs` (только логи), `PausedState.cs`
  (`ITimeScaleProvider` — единственное место с `Time.timeScale`, запоминает/восстанавливает,
  страховка 0→1). `IGameFlowService.IsPaused` — новый member (`Current == Paused`).
  `Services/Flow/BootstrapSettings.cs` (GameState InitialState) — `GameBootstrap` берёт стартовый
  стейт оттуда, а не хардкодит MainMenu; smoke-тест push-pop шага 1 удалён из GameBootstrap.Start().
- **InputRouter**: `Core/Input/IInputRouter.cs`+`InputRouter.cs` (Context/IsGameplay, пересчёт на
  `GameStateChangedEvent`, Gameplay ⇔ `LevelPlaying`). Потребители обновлены: `GodHandController`
  (не хватать вне Gameplay + мягкий релиз тем же путём, что смена режима на Miracle),
  `IsometricPanZoomBehaviour` (гасит оба pan-пути вне Gameplay), `MiracleTargetingController`
  (CancelCast + превью off вне Gameplay). Не путать с `IInputProvider.IsPointerOverUi`.
- `Views/UI/Hud/WorshipPopupView.cs`+`IWorshipPopupHost.cs`, `Factories/Ui/IWorshipPopupFactory.cs`+
  `WorshipPopupFactory.cs` (Prewarm 6 в ctor, попап якорится к `IWorshipPopupHost.PopupRoot` —
  мировых координат в Worship-событиях нет).
- `Views/UI/Hud/ILevelHudView.cs`+`LevelHudView.cs` (реализует и `IWorshipPopupHost`)+
  `LevelHudPresenter.cs`+`WorshipPopupPresenter.cs`.
- `Views/UI/Overlay/` — `ILevelCompleteView`+`LevelCompleteView`+`LevelCompletePresenter` (→
  `GameState.TempleMeta`, заглушка NullState до шага 8), `ILevelStalledView`+`LevelStalledView`+
  `LevelStalledPresenter` (`// ADS_STUB` в `RefillClicked`), `IPauseOverlayView`+`PauseOverlayView`+
  `PauseOverlayPresenter`.
- `GameLifetimeScope.RegisterLevelFlow` (после `RegisterMiracleSystems`) — FSM-биндинги
  (MainMenu/LevelPlaying/LevelComplete/Paused реальные, LevelSelect/TempleMeta/WelcomeBack —
  `NullState`), `UnityTimeScaleProvider`, `InputRouter`, `WorshipService`/`AlignmentService`/
  `LevelFlowService` (tickable — после FaithService), `RegisterLevelHud`/`RegisterLevelOverlays`
  (условная регистрация по прошитости view, тот же паттерн, что MiraclePanelView/FaithBarView).
  `LevelHudView` регистрируется ДВАЖДY разными generic `RegisterComponent<TInterface>` (под
  `ILevelHudView` и под `IWorshipPopupHost`) — безопасно, т.к. у `LevelHudView` нет `[Inject]
  Construct` (чистый View), а VContainer `RegisterComponent<T>` создаёт отдельный
  `ComponentRegistrationBuilder` на каждый вызов.

**Why**: избежать дублирования при шаге 6 (Heal/Lightning/AlignmentVisualController) — читать этот
файл и существующий код перед новым чудом/UI-экраном/FSM-состоянием.

**How to apply**: новый TerrorAct-источник (Lightning-чудо, бросок объекта в дом, шаг 6) —
публикация `NeedTriggeredEvent`/будущего события с валидным `CauseMiracle`, `WorshipService`/
`AlignmentService` сами разберутся через `TerrorActRules`/каталог — новых веток в них не нужно.
Новое состояние FSM — новый `IState` + одна строка `builder.Register<IGameStateBinding>(...)` в
`RegisterLevelFlow`, конструктор `GameFlowService` не трогается.

Связано: [[project_step4_miracles_faith]] (шаг 4 — Faith/чудеса, база для этого шага).
