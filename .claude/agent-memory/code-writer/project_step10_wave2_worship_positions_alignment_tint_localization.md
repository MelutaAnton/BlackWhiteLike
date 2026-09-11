---
name: project_step10_wave2_worship_positions_alignment_tint_localization
description: Шаг 10 «Polish», волна 2 — мировые попапы Worship, UI-тинт по alignment, заморозка мира на LevelComplete, названия уровней, полная локализация статичных канвасов; что уже есть и не нужно дублировать
type: project
---

Реализовано 2026-09-12, впятером-параллельно с editor-script-creator (контракт имён — в едином
промпте оркестратора обоим агентам, сверено по факту через git diff чужих Editor-файлов, а не
заранее написанным контрактом в памяти — расхождений не найдено). 467/467 EditMode тестов зелёные
(446 было + 21 новый: WorshipService-позиции 7, LevelCompleteState 4, AlignmentVisualConfig.GetStageIndex 5,
SystemLanguageLocalizationService 5).

**1. Мировая позиция Worship-попапов (было: все всплывали в одной HUD-точке)**:
- `Core/Events/Events.Village.cs` — `VillagerStateChangedEvent` получил `Vector3 WorldPosition` +
  `bool HasWorldPosition` ОПЦИОНАЛЬНЫМИ параметрами конструктора (`= default`, `= false`) — все ~15
  прежних 4-арг вызовов в тестах/коде остались рабочими без правок. Единственный публикующий сайт —
  `VillagerStateService.ApplyTransition`.
- `Services/Village/IVillagerStateService.ReportStimulus` — новый ОПЦИОНАЛЬНЫЙ параметр
  `Vector3? worldPosition = null` (3-й позиционный) — прокидывается в событие. Издатели, знающие
  `transform.position`, передают его явно: `VillagerController` (OnGrabbed/ApplyBlast/OnCollisionEnter),
  `MiracleImpactController` (LightningStruck-ветка, `villager.transform.position`). Переходы по
  таймеру (`VillagerStateService.Tick`) и `NeedSiteController`'овские вызовы (FireNearby/
  NeedResolvedNearby) позицию НЕ передают — не нужна для Fear/Awe-якорения.
  **Ловушка при расширении интерфейса с опциональным параметром**: implicit-реализация интерфейса
  требует ТУ ЖЕ арность у реализующего метода (default-значения можно не повторять, но параметр
  обязан присутствовать) — единственный `Fake` (`FaithServiceTests.FakeVillagerStateService`)
  обновлён третьим параметром.
- `Core/Events/Events.Worship.cs` — `WorshipChangedEvent` аналогично получил `WorldPosition`/
  `HasWorldPosition` опциональными параметрами (единственный publish-сайт — `WorshipService.SetCurrent`,
  9 старых тестовых конструкций в других файлах не тронуты).
- `Services/Worship/WorshipService.cs` — новая зависимость `INeedSiteRegistry` (уже существовал с
  шага 10 «туториал», `Services/Village/`, уже используется `TutorialPresenter`). Позиция для Awe
  (`NeedResolvedEvent`)/Fear-от-нужды (`NeedTriggeredEvent`) — `_needSiteRegistry.TryGetSitePosition(evt.SiteIndex)`;
  для Fear-от-жителя (HardLanding/Blasted/StruckByLightning) — `evt.WorldPosition`/`HasWorldPosition`
  самого `VillagerStateChangedEvent`. Decay (`Tick`) переиспользует позицию ПОСЛЕДНЕГО реального
  прироста (`_lastWorldPosition`/`_lastHasWorldPosition`, тот же паттерн, что `_lastSource`).
  **Регистрация**: `builder.Register<WorshipService>(...)` уже был под гейтом `_initialState ==
  LevelPlaying` (RegisterLevelFlow) — `NeedSiteRegistry` регистрируется под ТЕМ ЖЕ гейтом
  (RegisterVillageSystems), порядок Register-вызовов между методами значения не имеет.
- `Views/UI/Hud/WorshipPopupPresenter.cs` — новая зависимость `IWorshipPopupHost` (уже
  зарегистрирован для `LevelHudView` под теми же двумя интерфейсами). World→screen→canvas —
  ТОЧНАЯ копия приёма `TutorialPresenter.UpdateWorldPointer`/`TutorialView.ShowPointerAtScreenPoint`:
  кэш `UnityEngine.Camera.main` (полностью квалифицированный), `WorldToViewportPoint` →
  фоллбэк на HUD-точку (`return null`) если `viewportPoint.z <= 0` ИЛИ x/y вне 0..1 → `ViewportToScreenPoint`
  → `RectTransformUtility.ScreenPointToLocalPointInRectangle` с учётом `Canvas.renderMode`
  (Overlay → `worldCamera = null`). В отличие от туториального off-screen-указателя (прижимается к
  краю) — попап вне кадра просто уходит на дефолтную HUD-точку, не клампится.
- `Views/UI/Hud/{WorshipPopupView,WorshipPopupFactory,IWorshipPopupFactory}.cs`:
  `IWorshipPopupFactory.Show` — новый опциональный `Vector2? anchoredPosition = null`.
  `WorshipPopupFactory` теперь хранит `_defaultAnchoredPosition` (снят с `PopupPrefab` ОДИН раз в
  конструкторе) и на каждый `Show` выставляет `rectTransform.anchoredPosition = anchoredPosition ??
  _defaultAnchoredPosition` ДО `view.Launch(...)`.
  **Ловушка, которую пришлось чинить**: `WorshipPopupView.Launch` раньше кэшировал стартовую позицию
  ОДИН раз на весь пул (`_hasStartPosition`) — для фиксированной HUD-точки это работало, но с мировой
  проекцией разные показы одного и того же пулового инстанса требуют РАЗНУЮ стартовую позицию. Флаг
  убран — `_startAnchoredPosition` перечитывается из `_rectTransform.anchoredPosition` на КАЖДЫЙ
  `Launch` (фабрика гарантированно выставляет позицию ДО вызова Launch).

**2. UI-тинт по alignment (отложка шага 6)**:
- `Data/Configs/Alignment/AlignmentVisualConfig.cs` — новый `public int GetStageIndex(float score)`
  РЯДОМ с `GetStageFor` (та же `EnsureSorted`/`_sortedCache`, `GetStageFor` теперь делегирует в
  `GetStageIndex` — таблица переходов НЕ задвоена). Возвращает позицию стейджа (0..N-1) в
  отсортированном по убыванию `ScoreThreshold` массиве.
- `AlignmentVisual.asset` (реальные данные) — РОВНО 5 стейджей с порогами 75/25/-25/-75/-100 —
  позиции 0..4 соответствуют 1:1 пяти тонам `AlignmentUiPalette` (Saint/Kind/Neutral/Cruel/Demon).
  Presenter клампит `default: return palette.DemonTint` для index >= 4 (на случай другого числа
  стейджей в другом ассете).
- Новый `Views/UI/Hud/AlignmentUiTintPresenter.cs` (IStartable+IDisposable, pure C#) — стартовый pull
  `IAlignmentService.Score` в `Start()` (тот же приём, что `AlignmentVisualController.Construct` —
  событие для старта не публикуется) + подписка `AlignmentChangedEvent`. Индекс через
  `AlignmentVisualConfig.GetStageIndex`, маппинг индекс→тон — приватный `switch` внутри презентера
  (не отдельный класс — тривиальный, единственный потребитель).
- `ILevelHudView`/`LevelHudView` — новый `void SetAlignmentTint(Color color)`, тинтит `_worshipFill`
  (существовал) + НОВЫЕ SerializeField `_evilLabel`/`_goodLabel` (TextMeshProUGUI, по имени
  GameObject'ов "EvilLabel"/"GoodLabel" в `LevelHudSetupEditor`) — все три с null-гардами.
  **Fake в тестах** (`LevelHudPresenterTests.FakeLevelHudView`) — добавлен `SetAlignmentTint`.
- Регистрация — ВНУТРИ `RegisterLevelHud` (не отдельный метод), гейт — И `_levelHudView != null`
  (обёртка метода), И `_alignmentVisualConfig != null && _alignmentVisualController != null` (тот же
  парный гейт, что `RegisterAlignmentVisual` — иначе `AlignmentVisualConfig` может быть не
  зарегистрирован в контейнере, т.к. `RegisterAlignmentVisual` кладёт `RegisterInstance` ТОЛЬКО при
  обоих полях).

**3. LevelComplete замораживает мир**:
- `Services/Flow/States/LevelCompleteState.cs` — добавлен `ITimeScaleProvider` в конструктор,
  `Enter`/`Exit` СИМВОЛ-В-СИМВОЛ копируют `PausedState` (включая страховку "было 0 → восстановить 1").
  `UnityTimeScaleProvider` уже был зарегистрирован БЕЗУСЛОВНО в `RegisterLevelFlow`, `LevelCompleteState`
  тоже (в общем списке `IState`) — DI не потребовал правок GameLifetimeScope.
- `LevelCompleteStateTests.cs` — дословная копия структуры `PausedStateTests.cs` (4 теста).

**4. Названия уровней**:
- `Data/Configs/Levels/LevelCatalogConfig.cs` — `LevelCatalogEntry` получил
  `[SerializeField] private LocalizedString _displayName;` + `public LocalizedString DisplayName`.
- `LevelSelectPresenter` — новая зависимость `ILocalizationService`, `BindCards` теперь зовёт
  `ResolveDisplayName(entry)`: `entry.DisplayName.Resolve(_localization.CurrentLocale)`, пустая
  строка → фоллбэк `entry.Level.Id.ToString()`. Единственный другой кандидат (grep по проекту) —
  заголовки Level Complete/Temple — НЕ показывают LevelId вообще, править было нечего.
- Editor-агент синхронно застампил `ConfigAssetCreatorEditor.StampLevelCatalogDisplayNames()` (пять
  русских/английских названий для Level01..05) — вызывается БЕЗУСЛОВНО из `CreateAll()`, не только
  при первом создании ассета (правки названий переприменяются на каждом прогоне меню).

**5. Локализация статичных канвасов (база ru + EN по системной локали)** — контракт полностью
совпал с параллельным editor-агентом БЕЗ предварительной сверки (оба читали один и тот же детальный
промпт оркестратора):
- `Models/LocalizationKeys.cs` — 22 `public const string` (snake_case значения), сгруппированы по
  канвасу (MainMenu/LevelSelect/WelcomeBack, Temple, LevelHud) + `LevelStalledQuestion` (рантайм-
  текст презентера, НЕ статичный канвас-лейбл — `LocalizedTextView` на него не вешается).
  **Известный пробел**: `LocalizationConfigSetupEditor.Entries` (единственный источник САМИХ
  текстов) сознательно НЕ включает `LevelStalledQuestion` (его докстринг явно перечисляет
  "рантайм-тексты презентеров... сюда тоже не входят") — ключ определён и используется в рантайме
  (`GetText` вернёт сам ключ + `LogWarning`, пока запись не добавлена), но пары ru/en для него нужно
  дописать в `LocalizationConfigSetupEditor.Entries` отдельным шагом (не editor-скрипт этой волны).
- `Data/Configs/Localization/LocalizationConfig.cs` — `LocalizationEntry` (`_key`/`_text:
  LocalizedString`, тот же прецедент, что `LevelCatalogEntry`) + SO с `_entries[]`, ленивый
  `Dictionary` (`TryGetLookup`/`OnValidate`-инвалидация — калька с `AlignmentVisualConfig._sortedCache`).
- `ILocalizationService`/`SystemLanguageLocalizationService` — новый метод `string GetText(string key)`,
  ctor принимает `LocalizationConfig`. Отсутствующий ключ → сам ключ + `Debug.LogWarning` ОДИН раз
  (`HashSet<string> _warnedKeys`). `CurrentLocale`-контракт не менялся.
- `Views/UI/Common/LocalizedTextView.cs` — тупой MonoBehaviour (`_key` + `Apply(string)` через
  `GetComponent<TMP_Text>()`, null-гард), БЕЗ DI.
- `Views/UI/Common/LocalizedTextApplyEntryPoint.cs` — pure C# `IStartable`, `Object.FindObjectsByType<LocalizedTextView>(Include,
  None)` → `Apply(GetText(Key))` на каждый (прецедент Unity-статики в pure C# — `SystemTimeProvider`/
  `TutorialService`). Регистрация в `GameLifetimeScope` — БЕЗУСЛОВНАЯ, рядом с `ILocalizationService`
  (все сцены, до Input/GameFlowService).
- `GameLifetimeScope` — `_localizationConfig` (Header "Шаг 10 — Polish (волна 2, локализация)"),
  фоллбэк `CreateInstance<LocalizationConfig>() + LogWarning` (мета-сцены без прошивки не падают,
  `GetText` просто возвращает сами ключи).
- `LevelStalledPresenter` — локальный `LocalizedString QuestionText` (TODO с шага 10 «туториал»)
  УДАЛЁН, заменён на `_localizationService.GetText(LocalizationKeys.LevelStalledQuestion)`. Fake в
  `LevelStalledPresenterTests` (`FakeLocalizationService`) — добавлен `GetText` с тем же текстом
  ru/en, что раньше был захардкожен (тесты `_RuLocale_`/`_EnLocale_` не переписывались).

**Editor-контракт (сделано параллельным агентом, НЕ этой сессией, для справки)**: `LocalizationConfigSetupEditor.cs`
(новый, `BlackWhiteLike/Configs/Create Localization Config`) — единственный источник пар
ключ→{ru,en}; `MainMenuCanvasSetupEditor`/`TempleCanvasSetupEditor`/`LevelHudSetupEditor` вешают
`LocalizedTextView` через общий приватный `SetLocalizationKey(TextMeshProUGUI, string)`;
`ConfigAssetCreatorEditor.StampLevelCatalogDisplayNames()`; `_localizationConfig` прошит на
`GameLifetimeScope` во ВСЕХ Setup-сценах (`TestHandSceneSetupEditor`/`TestVillageSceneSetupEditor`/
`MainMenuSceneSetupEditor`/`TempleSceneSetupEditor`).

**Why**: избежать дублирования при следующей локализуемой фиче (новый UI-текст — просто новый
ключ+запись в `LocalizationConfigSetupEditor.Entries`, НЕ новый сервис/паттерн) и при новом
источнике мировой позиции для попапов/эффектов (переиспользовать `INeedSiteRegistry`-подобный
реестр или опциональный `Vector3?`-параметр события, не городить параллельный канал).

**How to apply**: новый статичный UI-текст — ключ в `LocalizationKeys` + запись в
`LocalizationConfigSetupEditor.Entries` (editor) + `LocalizedTextView` на лейбле (editor) — рантайм
не меняется. Новый рантайм-текст презентера (программный, не статичный лейбл) — ключ в
`LocalizationKeys` + `ILocalizationService.GetText(key)`, ключ добавляется в ТУ ЖЕ таблицу
`LocalizationConfigSetupEditor.Entries` отдельно (см. известный пробел `LevelStalledQuestion` выше —
доделать при следующей правке этого файла). Новый источник Fear/Awe с мировой позицией — либо через
`INeedSiteRegistry` (если это NeedSite), либо расширить `VillagerStateChangedEvent`-путь (издатель
передаёт `transform.position` в `ReportStimulus`).

Связано: [[project_step10_tutorial_localization]] (база `LocalizedString`/`ILocalizationService`/
`INeedSiteRegistry`, world→screen приём `TutorialPresenter`), [[project_step5_worship_win]]
(`WorshipService`/`AlignmentService`/`LevelHudView`/`WorshipPopupFactory` — базовая архитектура),
[[project_step6_heal_lightning_alignment]] (`AlignmentVisualConfig`/`AlignmentVisualController`,
`UiPalette` заведена, но не читалась — читается теперь).
