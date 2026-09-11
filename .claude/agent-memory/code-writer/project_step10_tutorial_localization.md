---
name: project_step10_tutorial_localization
description: Шаг 10 (Polish) — онбординг-туториал уровня 1, минимальный слой локализации (LocalizedString/ILocalizationService), NeedSiteRegistry; что уже есть и не нужно дублировать
type: project
---

Шаг 10 «Polish» (туториал) реализован 2026-09-11, в паре с editor-script-creator (контракт имён
передан обоим агентам одним промптом оркестратора, сверено после факта — расхождений не было).

**Что уже есть (не дублировать)**:
- `Models/GameLocale.cs` (Ru/En), `Models/LocalizedString.cs` ([Serializable] struct, `_ru`/`_en`,
  `Resolve(GameLocale)` — En с пустой `_en` фоллбэчит на `_ru`).
- `Services/Localization/ILocalizationService.cs`+`SystemLanguageLocalizationService.cs` — локаль
  вычисляется ОДИН РАЗ в конструкторе из `Application.systemLanguage` (тот же приём, что
  `SystemTimeProvider` для `DateTime.UtcNow` — единственное место, трогающее эту Unity-статику).
  Регистрируется в `GameLifetimeScope` БЕЗУСЛОВНО (все сцены, как `NullHapticsService`).
- `Services/Village/INeedSiteRegistry.cs`+`NeedSiteRegistry.cs` — pure C#, Dictionary<int,Transform>,
  `TryGetSitePosition` проверяет fake-null явным `!= null`. `NeedSiteController.Construct` делает
  `Register(_siteIndex, transform)` для самого первого включения (до первого OnEnable);
  **ревью-фикс (см. ниже, ВАЖНО 3)**: `OnEnable` теперь ТОЖЕ вызывает `Register` рядом с
  `Subscribe()` — симметрично `Unregister` в `OnDisable`, повторный `SetActive(false→true)` больше
  не теряет сайт в реестре навсегда (Register идемпотентен, перезаписывает ту же пару). Регистрация
  `NeedSiteRegistry` в `GameLifetimeScope` — безусловно внутри гейта LevelPlaying (в
  `RegisterVillageSystems`, не под отдельным гейтом туториала — NeedSiteController требует его
  независимо от того, прошит ли туториал в сцене).
- `Models/TutorialTriggerType.cs` (None/VillagerTossedByHand/NeedTriggered/MiracleSelected/
  NeedResolvedByPlayer/WorshipReached), `Models/TutorialPointerTarget.cs` (None/ActiveNeedSite/
  MiracleButton/WorshipBar).
- `Data/Configs/Tutorial/TutorialConfig.cs` — три класса в одном файле (контракт оркестратора,
  прецедент `LevelCatalogConfig`/`WorshipConfig`): `TutorialTrigger` (struct: `_type`/`_needType`/
  `_miracleType`/`_worshipThreshold` — фильтры валидны только для своего `_type`), `TutorialStep`
  (class: `_activationTrigger`/`_completionTrigger`/`_targetCount` (клампится `Mathf.Max(1,...)` в
  геттере)/`_text` (LocalizedString)/`_pointerTarget`/`_highlightMiracleButton`/`_highlightedMiracle`),
  `TutorialConfig` (SO: `_targetLevel`/`_steps`). `CreateAssetMenu` → `BlackWhiteLike/Tutorial/Tutorial
  Config`. Ассет — `TutorialConfig_Level01.asset` (editor-скрипт `TutorialConfigSetupEditor`,
  4 шага: бросок жителя → NeedTriggered(Fire) активирует "выбери Rain" → MiracleSelected(Rain) →
  NeedResolvedByPlayer(Fire) → WorshipReached(30)).
- `Services/Tutorial/ITutorialService.cs`+`TutorialService.cs` — pure C#, pull-модель для UI (урок
  шага 8: никаких Publish из Start, `StepChanged` — событие БЕЗ данных, презентер перечитывает
  свойства). Активность (`IsActive`) вычисляется РОВНО ОДИН РАЗ в конструкторе:
  `levelConfig.Id == config.TargetLevel && !profile.TutorialCompleted && steps.Length > 0` — не
  переоценивается в рантайме. Линейная последовательность: `ActivationTrigger.Type == None` — шаг
  виден сразу; иначе скрыт (`HasVisibleStep == false`) до совпадения `ActivationTrigger`. Каждое
  совпадение `CompletionTrigger` инкрементирует `CurrentProgress` до `TargetCount`, по достижении —
  переход к следующему шагу; после последнего — `IPlayerProfile.MarkTutorialCompleted()`.
  Единая точка сверки — приватный `Matches(TutorialTrigger, incoming, needType, miracleType,
  worshipValue)`: `Type == None` никогда не матчит (сентинел "сразу", не событие).
  **Триггер VillagerTossedByHand** — `VillagerStateChangedEvent.To == Tossed && Cause == Grabbed`
  (НЕ Blasted — тот же фильтр, что `WorshipService` использует для `VillagerHardLanding`; мягкая
  постановка/soft-release вообще не порождает переход в Tossed, отдельно отсекать не нужно).
  **TryGetWorldTargetPosition** — сервис следит за индексом ПОСЛЕДНЕЙ сработавшей и ещё не
  разрешённой/просроченной нужды (любого `NeedType`, не хардкодит Fire, хотя в туториале уровня 1
  фактически всегда Fire) через `NeedTriggeredEvent`/`NeedResolvedEvent`/`NeedExpiredEvent`,
  резолвит позицию через `INeedSiteRegistry`.
- `Views/UI/Hud/TutorialView.cs` — MonoBehaviour, **без интерфейса** (контракт оркестратора:
  презентер зависит от конкретного класса, не от `ITutorialView`) — прецедент `WorshipPopupFactory`
  зависящей от конкретного `WorshipPopupView`. SerializeField: `_objectivePanel`/`_objectiveText`/
  `_objectiveProgressText`/`_pointer`/`_rainButtonTarget`/`_healButtonTarget`/`_fireballButtonTarget`/
  `_lightningButtonTarget`/`_worshipBarTarget` (публичное свойство `WorshipBarTarget` — презентеру
  нужен доступ для `ShowPointerOverRect`, единственный публичный аксессор к приватному полю сверх
  контрактных методов). `SetMiracleHighlight` — DOTween `DOScale` yoyo-loop с `DOKill()` перед каждым
  вызовом (в т.ч. `on=false`) — не плодит твины на повторных вызовах. `ShowPointerAtScreenPoint`
  через `RectTransformUtility.ScreenPointToLocalPointInRectangle` (кэширует `Canvas`/`_pointerParent`
  в Awake). `ShowPointerOverRect` — прямое совмещение `RectTransform.position` (тот же канвас).
- `Views/UI/Hud/TutorialPresenter.cs` — pure C# `IStartable, ITickable, IDisposable`. `Tick()` нужен
  (не только `StepChanged`), т.к. screen-space позиция указателя на мировую цель меняется с
  паном/зумом камеры каждый кадр без изменения самого шага. Камера — `UnityEngine.Camera.main`
  **полностью квалифицированный** (без `using UnityEngine;` для типа Camera — страховка от
  коллизии с суб-неймспейсами `Controllers.Camera`, чек-лист CLAUDE.md п.3), кэшируется в поле,
  переполучается только если стал null (тот же принцип, что `PointerWorldProjector`). За спиной
  камеры (`WorldToScreenPoint().z < 0`) — указатель скрывается.
- **Дыра в DI, найденная и закрытая по ходу**: `GameLifetimeScope` при `root != null` (реальный
  Bootstrap-флоу) НИКОГДА не регистрировал голый `IPlayerProfile` в сценовом контейнере — только
  производные `IMetaProgressService`/`ILevelProgressService` через `root.MetaProgress`. `PlayerProfile`
  как таковой был недоступен по интерфейсу в игровой сцене, только в fallback-ветке `root == null`.
  `TutorialService` — первый потребитель, которому нужен именно `IPlayerProfile` (флаг
  `TutorialCompleted`). Фикс — **общий паттерн для будущих подобных случаев**: добавлено свойство
  `RootLifetimeScope.PlayerProfile => Container.Resolve<IPlayerProfile>()` (тот же приём, что
  `MetaProgress`/`LevelSelection`/`SceneLoader`) + `builder.RegisterInstance(root.PlayerProfile)` в
  `GameLifetimeScope.RegisterCampaignFlow` (ветка `root != null`, рядом с `root.MetaProgress`).
  Если новый сервис нуждается в root-зарегистрированном интерфейсе, которого нет среди публичных
  свойств `RootLifetimeScope` — добавлять свойство туда, а не создавать параллельный путь.
- `GameLifetimeScope` — новые SerializeField `_tutorialConfig`/`_tutorialView` (Header "Шаг 10 —
  Polish"), метод `RegisterTutorial` (условно, `_tutorialConfig != null && _tutorialView != null`,
  `TutorialView` — `RegisterComponent`, не `RegisterInstance`), вызывается ВНУТРИ гейта
  `_initialState == LevelPlaying` (после `RegisterMiracleSystems`) — туториал осмысленен только в
  игровой сцене. `ILocalizationService` — вне гейта (см. выше).
- `Models/ProfileKeys.TutorialCompleted`, `IPlayerProfile.TutorialCompleted`/`MarkTutorialCompleted()`,
  `PlayerProfile` — int 0/1 в storage, идемпотентный мутатор (уже true — повторно на диск не пишет,
  тот же приём, что `MarkLevelCompleted`). Единственная реализация `IPlayerProfile` — фейков в тестах
  нет (кроме `FakeProfileStorage` + реальный `PlayerProfile`, см. прецедент `MetaProgressServiceTests`).

**Why**: избежать дублирования при полной волне локализации (следующий шаг — таблица строк) и при
новых онбординг-шагах/уровнях с туториалом — читать этот файл и `TutorialConfigSetupEditor.cs`
(editor-скрипт, дословный контракт полей) перед новой правкой.

**How to apply**: второй уровень с туториалом — новый `TutorialConfig` ассет (`TargetLevel` = другой
`LevelId`) + либо расширение `TestVillageSceneSetupEditor.WireScope` на выбор конфига по сцене (см.
`[[wire-scope-level-scenes-only]]` в памяти editor-script-creator), либо параметризация текущего.
Новый тип триггера — значение в `TutorialTriggerType` + ветка в `TutorialService.Matches` +
соответствующий `Process*`-обработчик события. Полная локализация (таблица строк вне туториала) —
новый потребитель `ILocalizationService.CurrentLocale`, сам интерфейс не меняется.

Связано: [[project_step8_meta_idle]] (pull-модель UI, `RegisterInstance` vs `RegisterComponent`),
[[project_step5_worship_win]] (презентеры HUD — тот же паттерн `IStartable`+подписка на события).

**Ревью-фикс той же волны (2026-09-11, тот же день)** — 2 блокера + 4 важных + 3 незначительных,
все закрыты, 397/397 EditMode тестов зелёные (393 было + 4 новых регресса):
- `TutorialService`: одиночные поля `_hasActiveNeedSite`/`_activeNeedSiteIndex` заменены на
  `Dictionary<NeedType, int> _activeNeedsByType` (siteIndex последнего `NeedTriggeredEvent` КАЖДОГО
  типа, снимается `NeedResolvedEvent`/`NeedExpiredEvent` с тем же siteIndex). Решает: (1) событие
  активации, пришедшее раньше, чем шаг стал текущим (таймлайн публикует `NeedTriggeredEvent` ОДИН
  РАЗ) — новый `ActivateCurrentStepIfEligible()` перепроверяет латч в конструкторе и в
  `AdvanceStep()`; (2) `TryGetWorldTargetPosition` резолвит siteIndex по
  `CurrentStep.CompletionTrigger.NeedType` (валидно только если тип триггера
  NeedTriggered/NeedResolvedByPlayer), а не "последнюю сработавшую нужду любого типа" — при
  нескольких одновременно активных нуждах разных типов старая версия указывала не туда.
  `OnNeedExpired` дополнительно форсирует `AdvanceStep()`, если текущий шаг ждёт
  `NeedResolvedByPlayer` именно истёкшего типа и активных нужд этого типа больше нет — иначе
  туториал зависает навсегда (цель объективно недостижима). Новая подписка `LevelCompletedEvent` →
  `MarkTutorialCompleted()` безусловно + гасит текущий шаг (`_stepActivated=false`) — уровень
  пройден раньше, чем игрок долистал все шаги, обучать больше нечему. Подписки на шину не
  создаются вовсе, если `_isActive == false` после конструктора (`if (!_isActive) return;` сразу
  после вычисления).
- `NeedSiteController.OnEnable`: теперь вызывает `_needSiteRegistry.Register(_siteIndex, transform)`
  рядом с `Subscribe()` (была асимметрия — Construct регистрировал один раз, повторный
  `SetActive(false→true)` терял точку в реестре навсегда). **Прежняя заметка про "асимметрию,
  осознанное решение" в этом же файле выше — устарела, реестр теперь симметричен для toggle.**
- `TutorialPresenter.Refresh`: ранний `return` при `!IsActive` (IsActive не меняется всю сессию, не
  гонять Hide() каждый Tick); строка прогресса кэшируется в `_cachedProgress`/`_cachedTargetCount`/
  `_cachedProgressText`, `string.Format` только при реальном изменении. `Dispose()` дополнительно
  зовёт `SetHighlight(null)` — снимает пульс подсветки кнопки при разрушении презентера.
- `TutorialView`: новый `OnDisable()` — `DOKill()` + `localScale = Vector3.one` на все четыре
  кнопочные RectTransform безусловно (явные `== null` гварды), симметрично тому, что пульс
  запускается на чужом Transform, а не на этом GameObject.
- `NeedSiteRegistry.Register`: `Debug.LogWarning`, если siteIndex перезаписывается ДРУГИМ
  Transform (вероятный признак дублирующегося siteIndex в LevelConfig/сцене); тот же Transform
  (повторный Register из OnEnable) — молчаливый no-op.
- `TutorialConfig.Steps`: тип `IReadOnlyList<TutorialStep>` вместо `TutorialStep[]` наружу
  (единственный call-site — `TutorialService` конструктор; editor-скрипт `TutorialConfigSetupEditor`
  использует `SerializedProperty`, не задет).
- Новые regression-тесты в `TutorialServiceTests`: `ActivationEvent_ArrivesBeforeStepIsCurrent_*`,
  `CompletionTarget_NeedExpiresBeforePlayerResolves_*`, `LevelCompletedEvent_WhileActive_*`,
  `TryGetWorldTargetPosition_ResolvesByCurrentStepNeedType_*` (последний создаёт реальные
  `GameObject`/`Transform` через локальный `CreateTransform`+`[TearDown]`, тот же приём, что
  `NeedSiteRegistryTests`).
