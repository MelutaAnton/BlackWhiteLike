---
name: project_step8_meta_idle
description: Шаг 8 (Meta + idle) реализован — MetaProgressService/EconomyService/EffectiveStatsService/IdleIncomeService, Temple/WelcomeBack UI, гейт gameplay-регистраций по _initialState; что уже есть и не нужно дублировать
type: project
---

Шаг 8 «Meta + idle» (cli-workflow.md) реализован 2026-09-02, в паре с editor-скриптами (контракт
имён передан обоим агентам одним промптом оркестратора). LevelProgressService.cs (шаг 7) УДАЛЁН —
слился в MetaProgressService.

**Что уже есть (не дублировать)**:
- `Models/ProfileKeys.cs` — добавлены `Coins`, `AlignmentScore`, `LastSeenUtc`, `UpgradeLevelPrefix`.
  `Models/SceneNames.cs` — добавлен `Temple`.
- `Data/Configs/Upgrades/UpgradeCatalogConfig.cs` — `UpgradeLevelEntry`/`UpgradeCategoryEntry`/
  `UpgradeCatalogConfig` в ОДНОМ файле (контракт оркестратора, отступление от «1 класс = 1 файл»
  для нескольких `[Serializable]` в одном SO-конфиге). `TryGetLevelEntry`/`MaxLevel`.
- `Data/Configs/Rewards/RewardConfig.cs` (`FirstClearMultiplier`, default 2), `Data/Configs/Idle/
  IdleIncomeConfig.cs` (`CoinsPerHour`=60, `CapHours`=8, `MinOfflineMinutes`=5).
- `Services/Meta/IPlayerProfile.cs` — РАСШИРЕН мутаторами (`AddCoins`/`SpendCoins`/
  `MarkLevelCompleted`/`SetUpgradeLevel`/`SetAlignmentScore`/`SetLastSeenUtc`) — раньше был
  read-only стаб. `Services/Meta/PlayerProfile.cs` — единственная реализация, читает/пишет
  `IProfileStorage`+`ProfileKeys` точечно (каждый мутатор — свой ключ), `LastSeenUtc` хранится как
  ISO "O"-строка, `default(DateTime)` = профиль ни разу не штамповался.
- `Services/Meta/IMetaProgressService.cs`+`MetaProgressService.cs` — root-сервис, реализует ОБА
  `IMetaProgressService` И `ILevelProgressService` на одном инстансе (MarkCompleted/IsCompleted/
  IsUnlocked перенесены из удалённого `LevelProgressService`, та же `LevelUnlockRules`-логика).
  Бизнес-правила (TrySpendCoins — списание с проверкой баланса, GrantXxx) здесь, не в `PlayerProfile`.
- `Services/Meta/IEconomyService.cs`+`EconomyService.cs` — сценовый: `GrantLevelReward`
  (first-clear × `RewardConfig.FirstClearMultiplier`), `TryPurchaseUpgrade` (следующий уровень
  каталога, TrySpendCoins → IncrementUpgradeLevel), `GrantIdleIncome`. Публикует
  `Core/Events/Events.Meta.cs`: `RewardEarnedEvent` (XpAmount ВСЕГДА 0 — предохранитель §15),
  `CurrencyChangedEvent`, `UpgradePurchasedEvent`, `IdleIncomeReadyEvent`, `IdleIncomeCollectedEvent`.
  `Models/RewardSource.cs` (`LevelComplete`/`IdleIncome`) уже существовал с шага 7 — переиспользован.
- `Services/Meta/IEffectiveStatsService.cs`+`EffectiveStatsService.cs` — единственная точка чтения
  бонусов апгрейдов: `HandForceMultiplier`/`HandMassMultiplier` (HandPower), `FaithCapacityBonus`
  (абсолют)/`FaithRegenMultiplier` (процент, FaithCapacity), `MiraclePowerMultiplier`/
  `MiracleRadiusMultiplier` (MiraclePower). `EffectValue` каталога — процент для множителей (10 =
  +10% → ×1.10), абсолют для `FaithCapacityBonus`. Уровень 0 / null-каталог/metaProgress —
  нейтральные значения (1/1/0/1/1/1), без исключений.
- `Services/Idle/IIdleIncomeService.cs`+`IdleIncomeService.cs` — считает офлайн-доход ОДИН раз в
  `Start()` (IStartable), не тикается; штампует `LastSeenUtc` ДО публикации `IdleIncomeReadyEvent`
  (повторный вход в ту же мета-сцену той же сессии не задваивает); `Collect()` — однократно,
  `_collected` флаг. Первый запуск (`LastSeenUtc == default`) — только штамп.
- **Потребители IEffectiveStatsService** (все трое теперь принимают его последним/новым ctor-
  параметром — фейки во ВСЕХ тестах): `FaithService.Capacity = BaseCapacity + stats.FaithCapacityBonus`,
  реген × `stats.FaithRegenMultiplier`; `HandInteractionService` — `MaxGrabMass`/`MaxThrowForce`
  × соответствующих множителей; `MiracleService` — держит `_effectiveBaseRadius = BaseRadius ×
  MiracleRadiusMultiplier`, зафиксированный на `BeginCast` (апгрейд не может смениться посреди
  сессии — покупка только в Temple), power × `MiraclePowerMultiplier`.
- **CampaignContext** (`Services/Flow/CampaignContext.cs`) — явный `bool HasRoot`, тот же паттерн,
  что `BootstrapSettings`. Закрывает долг «TryGetSelected-прокси» шага 7:
  `LevelCompletePresenter.OnContinueClicked` теперь `_selection.ClearSelection()` (НОВЫЙ метод
  `ILevelSelectionService`) + `campaignContext.HasRoot ? sceneLoader.Load(Temple) :
  gameFlow.ChangeState(TempleMeta)` — без обращения к `TryGetSelected` как сигналу.
- **FSM**: `Services/Flow/States/TempleMetaState.cs`/`WelcomeBackState.cs` — тонкие IState (лог),
  заменили `NullState`-заглушки шага 5 в биндингах `GameLifetimeScope`. WelcomeBack — PUSH-состояние
  (`gameFlow.PushState(WelcomeBack)`/`PopState()`, тот же паттерн что `PausedState`).
- **Views**: `Views/UI/Temple/{ITempleView,TempleView,UpgradeRowView,CreatureTeaserView,
  TemplePresenter}.cs` — TempleView агрегирует `UpgradeRowView[]` (индекс строки = позиция в
  фиксированном `UpgradeCategory[] {HandPower, FaithCapacity, MiraclePower}` внутри Presenter'а,
  НЕ хардкод в View). `CreatureTeaserView` — ноль логики (только `_root`, предохранитель §15).
  `Views/UI/Overlay/{IWelcomeBackView,WelcomeBackView,WelcomeBackPresenter}.cs`. `MainMenuView`/
  `IMainMenuView`/`MainMenuPresenter` — добавлена кнопка/событие `TempleClicked` →
  `sceneLoader.Load(SceneNames.Temple)` (презентер теперь требует `ISceneLoaderService`).
- **Гейт шага 8** (`GameLifetimeScope.Configure`): `RegisterHandAndCamera`/`RegisterVillageSystems`/
  `RegisterMiracleSystems` — ТОЛЬКО при `_initialState == GameState.LevelPlaying` (вызов методов
  обёрнут в `if`, не сами методы). Внутри `RegisterLevelFlow` — FSM-биндинги/`InputRouter` ВСЕГДА
  (нужны каждой сцене), а `WorshipConfig`/`WorshipService`/`AlignmentService`/`LevelFlowService`/
  `RegisterLevelHud`/`RegisterLevelOverlays` — тоже под тем же гейтом `_initialState ==
  LevelPlaying` (вложенный if внутри метода). `RegisterAlignmentVisual` — НЕ гейтится (независимый
  паттерн по прошитости полей). `EconomyService`/`EffectiveStatsService` — ВСЕГДА (все сцены,
  нужны и LevelPlaying, и Temple). `IdleIncomeService`+`WelcomeBackPresenter` — только
  `root != null && _initialState != LevelPlaying`. `TemplePresenter` — только по `_templeView != null`
  (без проверки root — известное ограничение: Temple.unity без Bootstrap не соберётся, т.к.
  TemplePresenter требует IIdleIncomeService, который не регистрируется без root; сохранено по
  контракту оркестратора «при наличии root» для IdleIncomeService).
  Проверено по факту (`grep _initialState Scenes/*.unity`): `_TestVillage`/`_TestHand`/
  `Village_Base` ВСЕ = `LevelPlaying(2)` — гейт не отбирает у тестовых сцен ничего рабочего.
- `RootLifetimeScope` — новые SerializeField `_upgradeCatalog`/`_rewardConfig`/`_idleIncomeConfig`
  (тот же fallback-паттерн CreateInstance+LogWarning, что `_levelCatalog`), регистрирует
  `PlayerProfile`+`MetaProgressService` (root, `.As<IMetaProgressService, ILevelProgressService>()`),
  публичные свойства `MetaProgress`/`UpgradeCatalog`/`RewardConfig`/`IdleIncomeConfig`.
  **ВАЖНО**: `GameLifetimeScope.RegisterCampaignFlow` при `root != null` регистрирует
  `root.MetaProgress` В СЦЕНОВЫЙ контейнер ОДНОЙ записью `.As<IMetaProgressService,
  ILevelProgressService>()` (не двумя раздельными RegisterInstance) — если добавляешь новый
  root-сервис с несколькими интерфейсами, повторяй этот приём, иначе часть потребителей в сцене не
  найдёт нужный интерфейс.

**Why**: избежать дублирования при будущих шагах (новая категория апгрейда, новый мета-экран,
дальнейшая балансировка idle) — читать этот файл и существующий код перед новым мета-фичем.

**How to apply**: новая категория апгрейда — запись в `UpgradeCategory` enum + строка в
`UpgradeCatalogConfig` ассете + (если нужен новый эффект) новое свойство в
`IEffectiveStatsService`/`EffectiveStatsService`, НЕ новый сервис. Новый источник coins — метод в
`IEconomyService`, не прямой вызов `IMetaProgressService.AddCoins` из UI-слоя. Новое мета-состояние
FSM — новый `IState` + одна строка в списке биндингов `RegisterLevelFlow`, тот же приём, что
`TempleMetaState`/`WelcomeBackState`.

Связано: [[project_step5_worship_win]] (FSM push-pop, паттерн `PausedState` → `WelcomeBackState`),
[[project_step4_miracles_faith]] (Faith/чудеса — база для эффективных статов).

**Правки по ревью (2026-09-02, тот же коммит, до пуша)** — 4 блокера ломали `Build()` в мета-сценах:
- Гейт `RegisterLevelFlow` разорвал FSM-таблицу: `LevelPlayingState` регистрировался и попадал в
  `IEnumerable<IGameStateBinding>` БЕЗУСЛОВНО, а его единственная зависимость `ILevelFlowService` —
  под гейтом `_initialState == LevelPlaying`. Фикс: и `Register<LevelPlayingState>`, и запись
  `GameStateBinding(LevelPlaying, ...)` внутри Func-фабрики коллекции — под тем же гейтом. Общее
  правило на будущее: любое новое состояние FSM, чья зависимость гейтится по сцене, регистрируется
  и попадает в биндинги ПОД ТЕМ ЖЕ гейтом, не отдельно.
- `AlignmentVisualController` (Controllers/Temple) зависел от `IAlignmentService` (гейтится
  LevelPlaying) — в Temple падал. Заменено на `IMetaProgressService` (зарегистрирован всегда,
  включая fallback-ветку без root); `AlignmentService` в игровой сцене сидируется из того же
  `metaProgress.AlignmentScore`, стартовые значения совпадают по построению — если меняешь seed
  `AlignmentService`, сверяй с этим контроллером.
- `IIdleIncomeService` получил pull-свойства `HasPendingReward`/`PendingCoins`/`OfflineHours` —
  порядок `IStartable.Start()` между сервисом и презентерами (`TemplePresenter`/`WelcomeBackPresenter`)
  не гарантирован, событие `IdleIncomeReadyEvent` могло публиковаться до подписки. Презентеры теперь
  читают состояние в СВОЁМ `Start()`, подписка на событие осталась как дополнение. Общее правило:
  любой сервис, чей `IStartable.Start()` кому-то что-то сообщает о готовности через событие, должен
  ТАКЖЕ выставлять pull-свойства — не полагаться на порядок регистрации `RegisterEntryPoint`.
- Гейт `IdleIncomeService` был `root != null && _initialState != LevelPlaying`, а `TemplePresenter`
  требовал его безусловно (по `_templeView != null`, без проверки root) — standalone Temple без
  Bootstrap падал. Фикс: гейт `IdleIncomeService` — только `_initialState != LevelPlaying`, без
  проверки root (без root в этой ветке уже зарегистрировано всё нужное — fallback root-сервисы).

**Семантика штампа `LastSeenUtc` пересмотрена** (было: штамп "на входе" в `Start()`, ДО сбора —
несобранная награда сгорала навсегда при выходе без клика). Теперь: штамп ТОЛЬКО в `Collect()`
(значением, посчитанным в `Start()`, не временем клика) и на первом запуске профиля. Повторный вход
без сбора пересчитывает от того же `LastSeenUtc` — тот же результат, не задваивается (клампится
`CapHours`). Добавлена защита от перевода часов назад (`now < lastSeen` → LogWarning, без начисления
и без штампа). Если добавляешь новый источник "накопленной, но не собранной" награды — используй тот
же паттерн (посчитать в Start/Update, штамповать только в момент фактического Collect).

**Двойной idle-UI — решение оркестратора**: в Temple офлайн-доход обслуживает ТОЛЬКО кнопка
`_collectIdleButton` на `TempleView`; `WelcomeBackPresenter` регистрируется ТОЛЬКО когда
`_welcomeBackView != null && _templeView == null` (мета-сцена без Temple-view) — одна точка UX на
сцену, оверлей в TempleCanvas остаётся заделом. Если сцена получит оба view одновременно —
`WelcomeBackPresenter` не регистрируется (Debug.Log, не warning — ожидаемо).

**`IPlayerProfile.CompletedLevels` теперь `ISet<LevelId>`** (было `IReadOnlyCollection<LevelId>`) —
`MetaProgressService.IsUnlocked` раньше аллоцировал `new HashSet<LevelId>(...)` на каждый вызов
(LevelSelect дёргает на каждую карточку). `PlayerProfile._completedLevels` и так `HashSet<LevelId>` —
просто убрали лишний слой интерфейса. Единственная реализация `IPlayerProfile` — `PlayerProfile`,
фейков в тестах нет, смена типа безопасна.

`ConfigAssetCreatorEditor.MigrateRewardConfig()` УДАЛЁН — ветка была недостижима: `RewardConfig`
имеет C#-инициализатор `2f`, который Unity подставляет для отсутствующего в YAML поля ДО срабатывания
`OnValidate` (сам `OnValidate` дополнительно клампит к `>= 1`) — значение на диске к моменту миграции
никогда не бывает `0`. При появлении новых полей в существующих SO предпочитать этот вывод: если у
поля есть ненулевой C#-инициализатор, проверка `== 0` в миграции недостижима с самого начала.
