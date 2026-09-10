using System.Collections.Generic;
using System;
using BlackWhiteLike.Controllers.Camera;
using BlackWhiteLike.Controllers.Hand;
using BlackWhiteLike.Controllers.Miracles;
using BlackWhiteLike.Controllers.Temple;
using BlackWhiteLike.Controllers.Vfx;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.Input;
using BlackWhiteLike.Core.Interaction;
using BlackWhiteLike.Core.Pool;
using BlackWhiteLike.Core.StateMachine;
using BlackWhiteLike.Data.Configs.Alignment;
using BlackWhiteLike.Data.Configs.Camera;
using BlackWhiteLike.Data.Configs.Faith;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Data.Configs.Idle;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Data.Configs.Rewards;
using BlackWhiteLike.Data.Configs.Throwable;
using BlackWhiteLike.Data.Configs.Upgrades;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Data.Configs.Vfx;
using BlackWhiteLike.Data.Configs.Worship;
using BlackWhiteLike.Factories.Ui;
using BlackWhiteLike.Factories.Village;
using BlackWhiteLike.Factories.Vfx;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Flow.States;
using BlackWhiteLike.Services.Hand;
using BlackWhiteLike.Services.Haptics;
using BlackWhiteLike.Services.Idle;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Meta;
using BlackWhiteLike.Services.Miracles;
using BlackWhiteLike.Services.Village;
using BlackWhiteLike.Services.Worship;
using BlackWhiteLike.Views.UI.Hud;
using BlackWhiteLike.Views.UI.LevelSelect;
using BlackWhiteLike.Views.UI.MainMenu;
using BlackWhiteLike.Views.UI.Overlay;
using BlackWhiteLike.Views.UI.Temple;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BlackWhiteLike.Installers
{
    /// <summary>
    /// Composition Root (game-design §19). Шаг 2 добавил первые SerializeField — конфиги
    /// руки/камеры и сцено-объекты руки/камеры. Поля намеренно nullable: сцены без руки/камеры
    /// (MainMenu и т.п.) не обязаны их прошивать — см. фоллбэки в <see cref="Configure"/>.
    /// </summary>
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [Header("Шаг 2 — Камера + рука")]
        [SerializeField] private HandConfig _handConfig;
        [SerializeField] private IsometricCameraConfig _isometricCameraConfig;
        [SerializeField] private GodHandController _godHandController;
        [SerializeField] private CameraRig _cameraRig;

        [Header("Шаг 3 — Жители + события")]
        [SerializeField] private VillagerConfig _villagerConfig;
        [SerializeField] private LevelConfig _levelConfig;
        [SerializeField] private ThrowableConfig _throwableConfig;

        [Header("Шаг 4 — Чудеса + Faith")]
        [SerializeField] private MiracleCatalogConfig _miracleCatalogConfig;
        [SerializeField] private FaithConfig _faithConfig;
        [SerializeField] private VfxConfig _vfxConfig;
        [SerializeField] private MiracleTargetingController _miracleTargetingController;
        [SerializeField] private MiracleImpactController _miracleImpactController;
        [SerializeField] private MiracleVfxController _miracleVfxController;
        [SerializeField] private MiraclePanelView _miraclePanelView;
        [SerializeField] private FaithBarView _faithBarView;

        [Header("Шаг 5 — Worship + win")]
        [SerializeField] private WorshipConfig _worshipConfig;
        [SerializeField] private GameState _initialState = GameState.MainMenu;
        [SerializeField] private LevelHudView _levelHudView;
        [SerializeField] private LevelCompleteView _levelCompleteView;
        [SerializeField] private LevelStalledView _levelStalledView;
        [SerializeField] private PauseOverlayView _pauseOverlayView;

        [Header("Шаг 6 — Heal/Lightning + Alignment-визуал")]
        [SerializeField] private AlignmentVisualConfig _alignmentVisualConfig;
        [SerializeField] private AlignmentVisualController _alignmentVisualController;

        [Header("Шаг 7 — Кампания (LevelSelect, прогресс)")]
        [SerializeField] private MainMenuView _mainMenuView;
        [SerializeField] private LevelSelectView _levelSelectView;

        [Header("Шаг 8 — Meta + idle")]
        [SerializeField] private TempleView _templeView;
        [SerializeField] private WelcomeBackView _welcomeBackView;

        protected override void Configure(IContainerBuilder builder)
        {
            // Шаг 7: root-scope (Bootstrap, DontDestroyOnLoad) — может отсутствовать (тестовые
            // сцены/сцена запущена напрямую в редакторе). Вычисляется один раз здесь и передаётся
            // в подметоды, которым он нужен (RegisterVillageSystems — выбор LevelConfig,
            // RegisterCampaignFlow — root-сервисы кампании). См. remarks RootLifetimeScope —
            // почему это static Instance, а не канонический parentReference VContainer.
            var root = RootLifetimeScope.Instance;

            // Шаг 8: явный флаг "реальный кампания-флоу доступен" — закрывает долг шага 7
            // (LevelCompletePresenter раньше выводил это косвенно, см. remarks CampaignContext).
            builder.RegisterInstance(new CampaignContext(root != null));

            // Core
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventBus>();

            // Services
            builder.Register<SystemTimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
            builder.Register<PlayerPrefsProfileStorage>(Lifetime.Singleton).As<IProfileStorage>();

            // Шаг 9 (§19.9): тактильный отклик — во ВСЕХ сценах, безусловно (не под гейтом
            // _initialState — LightTap/Success нужны и мета-сценам, например будущим кнопкам
            // Temple). NullHapticsService — no-op контрактная точка, реализация с вибрацией
            // добавляется позже без изменения точек вызова.
            builder.Register<NullHapticsService>(Lifetime.Singleton).As<IHapticsService>();

            // Input — ITickable, тикается VContainer'ом каждый кадр без MonoBehaviour.
            // RegisterEntryPoint делает AsImplementedInterfaces() сам по себе — доп. .As<>() после него
            // был бы no-op, поэтому все контракты перечислены явно. IStartable/IDisposable — жизненный
            // цикл EnhancedTouchSupport (переход на новый Input System, Android): Enable() в Start(),
            // Disable() в Dispose(); диспетчер IStartable уже зарегистрирован через RegisterEntryPoint
            // ниже (GameFlowService/GameBootstrap), контейнер сам вызывает Dispose() у любого синглтона
            // IDisposable при закрытии scope (не через ExistingInstanceProvider — т.е. не для
            // RegisterInstance), см. VContainer Container.cs.
            builder.Register<PointerInputProvider>(Lifetime.Singleton)
                .As<IInputProvider, ITickable, IStartable, IDisposable>();

            // GameFlowService — тоже ITickable (см. IGameFlowService.Tick). ВАЖНО: порядок
            // регистрации ITickable в VContainer = порядок тика. Ввод обязан тикаться раньше своих
            // потребителей (FSM, шаг 2+ — камера/хват), поэтому регистрация — строго после
            // PointerInputProvider. При добавлении новых tickable'ов порядок не нарушать.
            // RegisterEntryPoint даёт и IGameFlowService, и ITickable через AsImplementedInterfaces().
            // Зависимость на IEnumerable<IGameStateBinding> резолвится после Build() — регистрация
            // самих биндингов ниже, в RegisterLevelFlow, порядок вызовов Register между ними значения
            // не имеет (см. remarks GameFlowService).
            builder.RegisterEntryPoint<GameFlowService>(Lifetime.Singleton);

            // Точка входа GameBootstrap — шаг 7 (пересмотр решения шага 1): регистрируется здесь,
            // СРАЗУ после GameFlowService, а не последней записью Configure. Порядок IStartable.Start()
            // = порядок RegisterEntryPoint (тот же принцип, что у ITickable выше) — MainMenuPresenter/
            // LevelSelectPresenter (RegisterCampaignFlow, шаг 7) читают IGameFlowService.Current уже в
            // своём Start(), и GameBootstrap.Start() обязан выставить стартовое состояние ДО них,
            // иначе Current отражал бы ещё не начатую FSM. До шага 7 порядок не имел значения — ни
            // один presenter не читал Current в Start(), только подписывался на события.
            builder.RegisterEntryPoint<GameBootstrap>();

            // Шаг 8 (гейт): деревня/жители/нужды/чудеса/Faith/VFX/рука/камера — ТОЛЬКО в игровой
            // сцене (_initialState == LevelPlaying). Мета-сцены (MainMenu/Temple) их не используют —
            // без гейта они падали бы на резолве отсутствующих зависимостей или спавнили жителей в
            // меню (playtest-дефект шага 7, см. remarks RegisterVillageSystems). Проверено по факту:
            // _TestVillage/_TestHand/Village_Base сериализованы с _initialState = LevelPlaying(2),
            // поэтому гейт не отбирает у тестовых сцен ничего из уже работавшего.
            if (_initialState == GameState.LevelPlaying)
            {
                // Шаг 2 — Камера + рука.
                RegisterHandAndCamera(builder);

                // Шаг 3 — Жители + события.
                RegisterVillageSystems(builder, root);

                // Шаг 4 — Чудеса + Faith.
                RegisterMiracleSystems(builder);
            }
            else
            {
                Debug.Log("[GameLifetimeScope] _initialState != LevelPlaying — рука/камера/деревня/жители/нужды/чудеса/Faith/VFX не регистрируются в этой сцене (гейт шага 8, мета-сцена).");
            }

            // Шаг 5 — Worship + win (FSM-биндинги/InputRouter — всегда; Worship/Alignment/LevelFlow/
            // HUD уровня — гейт внутри метода, см. remarks RegisterLevelFlow).
            RegisterLevelFlow(builder);

            // Шаг 6 — Alignment-визуал (Heal/Lightning — данные, новых сервисов не требуют). Не
            // гейтится по _initialState — независимый условный паттерн (по прошитости полей), см.
            // remarks RegisterAlignmentVisual.
            RegisterAlignmentVisual(builder);

            // Шаг 7 — Кампания: LevelSelect, прогресс, root↔scene.
            RegisterCampaignFlow(builder, root);

            // Шаг 8 — Экономика/мета-статы (все сцены) + офлайн-доход/Temple (мета-сцены).
            RegisterMetaSystems(builder, root);
        }

        /// <summary>
        /// Конфиги и сцено-объекты руки/камеры (game-design §19, шаг 2). Конфиги — если поле не
        /// прошито, регистрация null дала бы NRE у любого резолвера; вместо этого — fallback-инстанс
        /// с дефолтами, чтобы контейнер собрался и сцена не падала молча (НБ10, ревью шага 2:
        /// LogWarning, а не LogError, — сцены без руки/камеры, например MainMenu, штатно не
        /// прошивают эти поля, это не авария). Контроллеры — MonoBehaviour, регистрируются условно
        /// по той же причине.
        /// </summary>
        private void RegisterHandAndCamera(IContainerBuilder builder)
        {
            var handConfig = _handConfig;
            if (handConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _handConfig не прошит (ожидаемо для сцен без руки) — используется fallback HandConfig с дефолтами.");
                handConfig = ScriptableObject.CreateInstance<HandConfig>();
            }

            builder.RegisterInstance(handConfig);

            var isometricCameraConfig = _isometricCameraConfig;
            if (isometricCameraConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _isometricCameraConfig не прошит (ожидаемо для сцен без изометрической камеры) — используется fallback IsometricCameraConfig с дефолтами.");
                isometricCameraConfig = ScriptableObject.CreateInstance<IsometricCameraConfig>();
            }

            builder.RegisterInstance(isometricCameraConfig);

            // Б1+Б3 (ревью шага 2): единственный источник ответа "что под pointer'ом" — рука и камера
            // обе резолвят его отсюда, вместо независимых (и расходящихся) raycast'ов.
            builder.Register<PointerWorldProjector>(Lifetime.Singleton).As<IPointerWorldProjector>();

            builder.Register<HandInteractionService>(Lifetime.Singleton).As<IHandInteractionService>();

            if (_godHandController != null)
            {
                builder.RegisterComponent(_godHandController);
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _godHandController не прошит — рука не будет работать в этой сцене.");
            }

            if (_cameraRig != null)
            {
                builder.RegisterComponent(_cameraRig);

                // PanZoomBehaviour берётся с самого CameraRig (его сериализует CameraRig, не Scope) —
                // единственная точка ссылки на сцено-объект, без дублирования SerializeField'а здесь.
                if (_cameraRig.PanZoomBehaviour != null)
                {
                    builder.RegisterComponent(_cameraRig.PanZoomBehaviour);
                }
                else
                {
                    Debug.LogWarning("[GameLifetimeScope] CameraRig.PanZoomBehaviour не прошит — pan/zoom не будет работать.");
                }
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _cameraRig не прошит — камера не будет работать в этой сцене.");
            }
        }

        /// <summary>
        /// Конфиги и сервисы жителей/событий уровня (game-design §5, §6, шаг 3). Конфиги — тот же
        /// fallback-паттерн, что у HandConfig/IsometricCameraConfig (см. <see cref="RegisterHandAndCamera"/>):
        /// LogWarning + CreateInstance вместо RegisterInstance(null), сцены без деревни (MainMenu и
        /// т.п.) не обязаны их прошивать. VillagerStateService/LevelEventService регистрируются как
        /// ITickable явным списком интерфейсов (тот же паттерн, что PointerInputProvider), а не через
        /// RegisterEntryPoint — порядок их тика не завязан на порядок Input/GameFlowService.
        /// </summary>
        /// <param name="root">
        /// Шаг 7: root-scope, если сцена запущена через Bootstrap (иначе null). Используется только
        /// для выбора активного <see cref="LevelConfig"/> — см. комментарий ниже.
        /// </param>
        private void RegisterVillageSystems(IContainerBuilder builder, RootLifetimeScope root)
        {
            var villagerConfig = _villagerConfig;
            if (villagerConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _villagerConfig не прошит (ожидаемо для сцен без деревни) — используется fallback VillagerConfig с дефолтами.");
                villagerConfig = ScriptableObject.CreateInstance<VillagerConfig>();
            }

            builder.RegisterInstance(villagerConfig);

            var levelConfig = _levelConfig;

            // Шаг 7 (кампания): если уровень был выбран через экран LevelSelect (root доступен и
            // игрок реально кликнул карточку) — используем его; серилизованный _levelConfig
            // остаётся фоллбэком для тестовых сцен без root (см. remarks RootLifetimeScope).
            // Гейт по _initialState (playtest-дефект шага 7): root-selection переживает возврат в
            // меню, и БЕЗ гейта мета-сцена MainMenu после первого пройденного уровня получала
            // боевой LevelConfig с настоящим Layout — VillagerSpawnService спавнил жителей прямо
            // в меню (8 × LogError от фабрики: _villagerConfig там не прошит). Selection — источник
            // конфига только для игровой сцены (_initialState == LevelPlaying).
            if (_initialState == GameState.LevelPlaying
                && root != null && root.LevelSelection.TryGetSelected(out var selectedLevel))
            {
                levelConfig = selectedLevel;
            }

            if (levelConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _levelConfig не прошит (ожидаемо для сцен без уровня) — используется fallback LevelConfig с дефолтами (пустой таймлайн).");
                levelConfig = ScriptableObject.CreateInstance<LevelConfig>();
            }

            builder.RegisterInstance(levelConfig);

            // Третий playtest шага 3 (критический дефект — жители падали за край земли):
            // VillageLayoutConfig регистрируется ОТДЕЛЬНО от LevelConfig — VillagerController нужен
            // прямой доступ к VillagerBoundsHalfExtent/VillagerSpawnPositions (кламп цели блуждания,
            // страховка от падения), а не через LevelConfig.Layout на каждое обращение. Тот же
            // fallback-паттерн: без раскладки — дефолтные границы/пустой список спавнов, не NRE.
            var villageLayoutConfig = levelConfig.Layout;
            if (villageLayoutConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] LevelConfig.Layout не прошит — используется fallback VillageLayoutConfig с дефолтами (VillagerBoundsHalfExtent по умолчанию).");
                villageLayoutConfig = ScriptableObject.CreateInstance<VillageLayoutConfig>();
            }

            builder.RegisterInstance(villageLayoutConfig);

            // ThrowableConfig ниже НЕ используется ThrowableObjectController напрямую (он читает
            // собственную прямую ссылку на префабе, см. ThrowableConfig.cs) — регистрация здесь для
            // будущих потребителей через DI (например, WorshipConfig-расчёты Fear от разрушения декора).
            var throwableConfig = _throwableConfig;
            if (throwableConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _throwableConfig не прошит — используется fallback ThrowableConfig с дефолтами.");
                throwableConfig = ScriptableObject.CreateInstance<ThrowableConfig>();
            }

            builder.RegisterInstance(throwableConfig);

            // В2 (ревью шага 3): PoolManager теперь IDisposable — контейнер сам вызывает Dispose()
            // при закрытии scope (тот же механизм, что у PointerInputProvider, см. комментарий выше),
            // уничтожая корневой GameObject пула вместе со scope, а не полагаясь на DontDestroyOnLoad.
            builder.Register<PoolManager>(Lifetime.Singleton).As<IPoolManager, IDisposable>();

            builder.Register<VillagerStateService>(Lifetime.Singleton).As<IVillagerStateService, ITickable>();
            builder.Register<LevelEventService>(Lifetime.Singleton).As<ILevelEventService, ITickable>();

            builder.Register<VillagerFactory>(Lifetime.Singleton).As<IVillagerFactory>();

            // Б3 (ревью шага 3): без этого сервиса IVillagerFactory не имел ни одного вызывающего.
            // IStartable — обоснование см. VillagerSpawnService (remarks).
            builder.Register<VillagerSpawnService>(Lifetime.Singleton).As<IVillagerSpawnService, IStartable>();
        }

        /// <summary>
        /// Конфиги, сервисы, контроллеры и UI чудес/Faith (game-design §4, §12, шаг 4). Тот же
        /// fallback-паттерн для конфигов, что у остальных SO проекта (см. remarks выше);
        /// MiracleService/HandModeService не ITickable — их порядок регистрации друг относительно
        /// друга и Input значения не имеет. FaithService — ITickable, регистрируется явным списком
        /// интерфейсов (тот же приём, что VillagerStateService/LevelEventService): реген должен
        /// тикаться после того, как VillagerStateService в этом же кадре уже применил все переходы
        /// FSM (иначе счёт молящихся на этом Tick мог бы быть на кадр устаревшим), поэтому
        /// регистрация — строго после RegisterVillageSystems.
        /// </summary>
        private void RegisterMiracleSystems(IContainerBuilder builder)
        {
            var miracleCatalogConfig = _miracleCatalogConfig;
            if (miracleCatalogConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _miracleCatalogConfig не прошит (ожидаемо для сцен без чудес) — используется fallback MiracleCatalogConfig с пустым каталогом (все чудеса недоступны).");
                miracleCatalogConfig = ScriptableObject.CreateInstance<MiracleCatalogConfig>();
            }

            builder.RegisterInstance(miracleCatalogConfig);

            var faithConfig = _faithConfig;
            if (faithConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _faithConfig не прошит (ожидаемо для сцен без Faith) — используется fallback FaithConfig с дефолтами.");
                faithConfig = ScriptableObject.CreateInstance<FaithConfig>();
            }

            builder.RegisterInstance(faithConfig);

            var vfxConfig = _vfxConfig;
            if (vfxConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _vfxConfig не прошит (ожидаемо для сцен без VFX) — используется fallback VfxConfig с пустым каталогом (эффекты не проигрываются, только LogWarning).");
                vfxConfig = ScriptableObject.CreateInstance<VfxConfig>();
            }

            builder.RegisterInstance(vfxConfig);

            // HandModeService нужен руке и камере всегда (даже без выбранного чуда — Mode стартует
            // Grab), регистрируется безусловно.
            builder.Register<HandModeService>(Lifetime.Singleton).As<IHandModeService>();

            builder.Register<FaithService>(Lifetime.Singleton).As<IFaithService, ITickable>();
            builder.Register<MiracleService>(Lifetime.Singleton).As<IMiracleService>();
            builder.Register<VfxFactory>(Lifetime.Singleton).As<IVfxFactory>();

            if (_miracleTargetingController != null)
            {
                builder.RegisterComponent(_miracleTargetingController);
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _miracleTargetingController не прошит — превью и ввод каста чуда не будут работать в этой сцене.");
            }

            if (_miracleImpactController != null)
            {
                builder.RegisterComponent(_miracleImpactController);
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _miracleImpactController не прошит — Fireball не будет разбрасывать жителей в этой сцене.");
            }

            if (_miracleVfxController != null)
            {
                builder.RegisterComponent(_miracleVfxController);
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _miracleVfxController не прошит — VFX каста чуда не будет проигрываться в этой сцене.");
            }

            if (_miraclePanelView != null)
            {
                builder.RegisterComponent<IMiraclePanelView>(_miraclePanelView);
                builder.RegisterEntryPoint<MiraclePanelPresenter>();
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _miraclePanelView не прошит — панель чудес не будет работать в этой сцене (презентер не регистрируется, чтобы не упасть на резолве отсутствующего view).");
            }

            if (_faithBarView != null)
            {
                builder.RegisterComponent<IFaithBarView>(_faithBarView);
                builder.RegisterEntryPoint<FaithBarPresenter>();
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _faithBarView не прошит — бар Faith не будет работать в этой сцене.");
            }
        }

        /// <summary>
        /// FSM-биндинги (ВСЕГДА, все сцены), InputRouter (всегда) и, под гейтом шага 8 — Worship/
        /// Alignment/LevelFlow/HUD/оверлеи уровня (game-design §9, §10, §13, шаг 5). Состояния
        /// регистрируются как именованные биндинги (<see cref="IGameStateBinding"/>), собираемые
        /// <c>GameFlowService</c> в конструкторе (см. remarks на нём) — не в его конструкторе
        /// напрямую. TempleMeta/WelcomeBack — полноценные состояния с шага 8 (были заглушками
        /// <see cref="NullState"/> на шаге 5).
        /// </summary>
        private void RegisterLevelFlow(IContainerBuilder builder)
        {
            builder.RegisterInstance(new BootstrapSettings(_initialState));

            builder.Register<UnityTimeScaleProvider>(Lifetime.Singleton).As<ITimeScaleProvider, IDisposable>();

            // Состояния FSM — обычные Singleton; биндинг на GameState — отдельной записью
            // IGameStateBinding ниже. Порядок регистрации между Register<TState> и
            // Register<IGameStateBinding> значения не имеет — VContainer резолвит граф зависимостей
            // после Build(), а не по мере вызовов Register. ВСЕГДА регистрируется целиком (не под
            // гейтом шага 8) — каждая сцена (включая мета-сцены) должна иметь полную таблицу
            // состояний, иначе ChangeState/PushState на непрошитый ключ роняет StateMachine.TryGetState.
            builder.Register<MainMenuState>(Lifetime.Singleton);
            builder.Register<LevelSelectState>(Lifetime.Singleton);
            builder.Register<LevelCompleteState>(Lifetime.Singleton);
            builder.Register<PausedState>(Lifetime.Singleton);
            builder.Register<TempleMetaState>(Lifetime.Singleton);
            builder.Register<WelcomeBackState>(Lifetime.Singleton);

            // БЛОКЕР 1 (ревью шага 8, регрессия MainMenu/Temple): LevelPlayingState зависит от
            // ILevelFlowService, который регистрируется НИЖЕ в этом же методе только под гейтом
            // _initialState == LevelPlaying. Раньше LevelPlayingState регистрировался и попадал в
            // биндинги БЕЗУСЛОВНО, а GameFlowService резолвит IEnumerable<IGameStateBinding> эагерно
            // в конструкторе (RegisterEntryPoint строит EntryPointDispatcher сразу на Build()) — в
            // мета-сценах контейнер падал на Build() с "No such registration ILevelFlowService".
            // Регистрация состояния — под тем же гейтом, что и его единственная зависимость.
            if (_initialState == GameState.LevelPlaying)
            {
                builder.Register<LevelPlayingState>(Lifetime.Singleton);
            }

            // Первый Play Mode шага 5: несколько Register<IGameStateBinding>(r => ...) VContainer
            // НЕ собирает в коллекцию — у Func-регистраций один и тот же ImplementationType, и
            // CollectionInstanceProvider бросает «Conflict implementation type» на Build(). Поэтому
            // коллекция регистрируется ОДНОЙ записью точного типа IEnumerable<IGameStateBinding>:
            // Registry.TryGet проверяет точное совпадение типа раньше fallback'ов на коллекции
            // (Registry.cs, TryGet), и GameFlowService получает готовый список.
            // Шаг 8: TempleMeta/WelcomeBack — полноценные состояния (TempleMetaState/WelcomeBackState),
            // заменили NullState-заглушки шага 5.
            builder.Register<IEnumerable<IGameStateBinding>>(
                r =>
                {
                    var bindings = new List<IGameStateBinding>
                    {
                        new GameStateBinding(GameState.MainMenu, r.Resolve<MainMenuState>()),
                        new GameStateBinding(GameState.LevelSelect, r.Resolve<LevelSelectState>()),
                        new GameStateBinding(GameState.LevelComplete, r.Resolve<LevelCompleteState>()),
                        new GameStateBinding(GameState.Paused, r.Resolve<PausedState>()),
                        new GameStateBinding(GameState.TempleMeta, r.Resolve<TempleMetaState>()),
                        new GameStateBinding(GameState.WelcomeBack, r.Resolve<WelcomeBackState>()),
                    };

                    // БЛОКЕР 1: тот же гейт, что регистрация LevelPlayingState выше — без него
                    // резолвер упал бы на r.Resolve<LevelPlayingState>() в мета-сцене (тип не
                    // зарегистрирован). Без записи для GameState.LevelPlaying в мета-сцене
                    // StateMachine.TryGetState на этот ключ пишет LogError и возвращает null (не
                    // бросает) — переход в LevelPlaying там штатно никто не запрашивает, это не
                    // регрессия, а корректное отсутствие игровой FSM-ветки вне игровой сцены.
                    if (_initialState == GameState.LevelPlaying)
                    {
                        bindings.Add(new GameStateBinding(GameState.LevelPlaying, r.Resolve<LevelPlayingState>()));
                    }

                    return bindings;
                },
                Lifetime.Singleton);

            builder.Register<InputRouter>(Lifetime.Singleton).As<IInputRouter>();

            // Шаг 8 (гейт): Worship/Alignment/LevelFlow/HUD уровня — только в игровой сцене. Мета-сцены
            // (MainMenu/Temple) не ведут счёт Worship/Alignment уровня — читают персистентный
            // IMetaProgressService.AlignmentScore напрямую, если понадобится (сейчас не понадобилось).
            if (_initialState == GameState.LevelPlaying)
            {
                var worshipConfig = _worshipConfig;
                if (worshipConfig == null)
                {
                    Debug.LogWarning("[GameLifetimeScope] _worshipConfig не прошит (ожидаемо для сцен без Worship) — используется fallback WorshipConfig с дефолтами (пустые таблицы Awe/Fear).");
                    worshipConfig = ScriptableObject.CreateInstance<WorshipConfig>();
                }

                builder.RegisterInstance(worshipConfig);

                // WorshipService/LevelFlowService — оба ITickable ПОСЛЕ FaithService (RegisterMiracleSystems
                // отработал выше): реген Faith в этом кадре должен применяться раньше decay/win-check
                // Worship и stall-check LevelFlow, тот же приём, что порядок VillagerStateService → FaithService.
                builder.Register<WorshipService>(Lifetime.Singleton).As<IWorshipService, ITickable, IDisposable>();
                builder.Register<AlignmentService>(Lifetime.Singleton).As<IAlignmentService, IDisposable>();
                builder.Register<LevelFlowService>(Lifetime.Singleton).As<ILevelFlowService, ITickable, IDisposable>();

                RegisterLevelHud(builder);
                RegisterLevelOverlays(builder);
            }
        }

        /// <summary>
        /// HUD уровня — Worship-бар, Alignment, кнопка паузы, попапы (game-design §10, шаг 5). Тот
        /// же условный паттерн, что MiraclePanelView/FaithBarView шага 4: без прошитого view
        /// презентер не регистрируется, чтобы не упасть на резолве отсутствующего view.
        /// </summary>
        private void RegisterLevelHud(IContainerBuilder builder)
        {
            if (_levelHudView != null)
            {
                // Один инстанс, две роли (ILevelHudView + IWorshipPopupHost) — одна регистрация с
                // двумя .As<>(), а не два RegisterComponent на тот же _levelHudView (аудит п.7):
                // раньше форс-инъекция срабатывала дважды на одном компоненте без вреда, но
                // заводила два отдельных Registration на один и тот же объект — не по одному ключу.
                builder.RegisterComponent(_levelHudView).As<ILevelHudView, IWorshipPopupHost>();
                builder.RegisterEntryPoint<LevelHudPresenter>();

                builder.Register<WorshipPopupFactory>(Lifetime.Singleton).As<IWorshipPopupFactory>();
                builder.RegisterEntryPoint<WorshipPopupPresenter>();
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _levelHudView не прошит — HUD уровня (Worship/Alignment/пауза/попапы) не будет работать в этой сцене.");
            }
        }

        /// <summary>Оверлеи Level Complete / Level Stalled / Pause (game-design §9, §10, шаг 5) — тот же условный паттерн.</summary>
        private void RegisterLevelOverlays(IContainerBuilder builder)
        {
            if (_levelCompleteView != null)
            {
                builder.RegisterComponent<ILevelCompleteView>(_levelCompleteView);
                builder.RegisterEntryPoint<LevelCompletePresenter>();
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _levelCompleteView не прошит — экран итогов уровня не будет работать в этой сцене.");
            }

            if (_levelStalledView != null)
            {
                builder.RegisterComponent<ILevelStalledView>(_levelStalledView);
                builder.RegisterEntryPoint<LevelStalledPresenter>();
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _levelStalledView не прошит — soft-fail оверлей не будет работать в этой сцене.");
            }

            if (_pauseOverlayView != null)
            {
                builder.RegisterComponent<IPauseOverlayView>(_pauseOverlayView);
                builder.RegisterEntryPoint<PauseOverlayPresenter>();
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _pauseOverlayView не прошит — оверлей паузы не будет работать в этой сцене.");
            }
        }

        /// <summary>
        /// Alignment-визуал руки/храма (game-design §7, §11.1, шаг 6). Условная регистрация по
        /// прошитости ОБОИХ полей (тот же паттерн, что MiraclePanelView/FaithBarView) — контроллеру
        /// нужен конфиг, регистрировать один без другого смысла не имеет.
        /// </summary>
        private void RegisterAlignmentVisual(IContainerBuilder builder)
        {
            if (_alignmentVisualConfig != null && _alignmentVisualController != null)
            {
                builder.RegisterInstance(_alignmentVisualConfig);
                builder.RegisterComponent(_alignmentVisualController);
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _alignmentVisualConfig и/или _alignmentVisualController не прошиты — alignment-визуал руки/храма не будет работать в этой сцене.");
            }
        }

        /// <summary>
        /// LevelSelect, прогресс кампании, root↔scene (game-design §7, шаг 7).
        /// </summary>
        /// <remarks>
        /// Root-сервисы (<see cref="ILevelSelectionService"/>/<see cref="ILevelProgressService"/>/
        /// <see cref="ISceneLoaderService"/>/<see cref="LevelCatalogConfig"/>) резолвятся из
        /// <see cref="RootLifetimeScope.Instance"/>, если сцена запущена через Bootstrap. Иначе —
        /// тот же fallback-паттерн, что у остальных конфигов проекта (LogWarning + локальный
        /// singleton уровня сцены), чтобы тестовые сцены без Bootstrap
        /// (_TestVillage/_TestHand/Village_Base напрямую) продолжали работать как раньше — их
        /// прогресс/выбор просто не переживают выгрузку сцены.
        /// <para>
        /// <see cref="LevelCompletionRecorder"/> (мост LevelCompletedEvent → прогресс)
        /// регистрируется ТОЛЬКО когда root реально доступен — фоллбэк-прогресс живёт и умирает
        /// вместе со сценой, отмечать в нём уровень пройденным бессмысленно (он не пережил бы даже
        /// собственный Continue).
        /// </para>
        /// </remarks>
        private void RegisterCampaignFlow(IContainerBuilder builder, RootLifetimeScope root)
        {
            LevelCatalogConfig levelCatalog;

            if (root != null)
            {
                levelCatalog = root.LevelCatalog;
                builder.RegisterInstance(root.LevelSelection);

                // Шаг 8: root.LevelProgress и root.MetaProgress резолвят ОДИН И ТОТ ЖЕ инстанс
                // MetaProgressService (см. remarks на нём) — регистрируем его в сценовом контейнере
                // ОДНОЙ записью под обоими интерфейсами (тот же приём, что LevelHudView/IWorshipPopupHost),
                // а не двумя RegisterInstance с двумя отдельными Registration на один объект.
                builder.RegisterInstance(root.MetaProgress).As<IMetaProgressService, ILevelProgressService>();

                builder.RegisterInstance(root.SceneLoader);

                // Мост LevelCompletedEvent -> прогресс нужен только игровой сцене (симметрично
                // гейту root-selection выше): в мета-сцене LevelCompletedEvent не публикуется,
                // держать там подписчика незачем.
                if (_initialState == GameState.LevelPlaying)
                {
                    builder.RegisterEntryPoint<LevelCompletionRecorder>();
                }
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] RootLifetimeScope не найден (сцена запущена напрямую, минуя Bootstrap) — LevelSelect/прогресс кампании работают в пределах одной этой сцены и не переживут её выгрузку.");
                levelCatalog = null;
                builder.Register<LevelSelectionService>(Lifetime.Singleton).As<ILevelSelectionService>();
                builder.Register<SceneLoaderService>(Lifetime.Singleton).As<ISceneLoaderService>();

                // Шаг 8: LevelProgressService слился в MetaProgressService (см. remarks на нём) —
                // тот же fallback-паттерн, что у остальных root-сервисов этой ветки.
                builder.Register<PlayerProfile>(Lifetime.Singleton).As<IPlayerProfile>();
                builder.Register<MetaProgressService>(Lifetime.Singleton).As<IMetaProgressService, ILevelProgressService>();
            }

            // root.LevelCatalog резолвится из контейнера root-scope и никогда не бывает null (сам
            // RootLifetimeScope уже подставил свой fallback с LogWarning, если его _levelCatalog не
            // прошит, см. remarks там) — эта проверка нужна только для ветки "root == null" выше.
            if (levelCatalog == null)
            {
                levelCatalog = ScriptableObject.CreateInstance<LevelCatalogConfig>();
            }

            builder.RegisterInstance(levelCatalog);

            if (_mainMenuView != null)
            {
                builder.RegisterComponent<IMainMenuView>(_mainMenuView);
                builder.RegisterEntryPoint<MainMenuPresenter>();
            }
            else
            {
                Debug.Log("[GameLifetimeScope] _mainMenuView не прошит (ожидаемо для сцен без главного меню, например Village_Base) — главное меню не будет работать в этой сцене.");
            }

            if (_levelSelectView != null)
            {
                builder.RegisterComponent<ILevelSelectView>(_levelSelectView);
                builder.RegisterEntryPoint<LevelSelectPresenter>();
            }
            else
            {
                Debug.Log("[GameLifetimeScope] _levelSelectView не прошит (ожидаемо для сцен без экрана выбора уровня, например Village_Base) — экран выбора уровня не будет работать в этой сцене.");
            }
        }

        /// <summary>
        /// Экономика/мета-статы и офлайн-доход/Temple (game-design §8, §15, шаг 8).
        /// </summary>
        /// <remarks>
        /// <see cref="EconomyService"/>/<see cref="EffectiveStatsService"/> регистрируются БЕЗ гейта
        /// на <see cref="_initialState"/> (в отличие от RegisterHandAndCamera/RegisterVillageSystems/
        /// RegisterMiracleSystems/внутреннего гейта RegisterLevelFlow выше) — они нужны и в
        /// LevelPlaying (награда за уровень, бонусы Faith/руки/чудес через
        /// <see cref="IEffectiveStatsService"/>), и в Temple (покупка апгрейдов). Каталоги
        /// (Upgrade/Reward/Idle) — тот же root-или-fallback паттерн, что <see cref="RegisterCampaignFlow"/>.
        /// </remarks>
        private void RegisterMetaSystems(IContainerBuilder builder, RootLifetimeScope root)
        {
            UpgradeCatalogConfig upgradeCatalog;
            RewardConfig rewardConfig;
            IdleIncomeConfig idleIncomeConfig;

            if (root != null)
            {
                upgradeCatalog = root.UpgradeCatalog;
                rewardConfig = root.RewardConfig;
                idleIncomeConfig = root.IdleIncomeConfig;
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] RootLifetimeScope не найден — каталог апгрейдов/наград/idle-конфиг локальные fallback'и (не переживут выгрузку сцены).");
                upgradeCatalog = ScriptableObject.CreateInstance<UpgradeCatalogConfig>();
                rewardConfig = ScriptableObject.CreateInstance<RewardConfig>();
                idleIncomeConfig = ScriptableObject.CreateInstance<IdleIncomeConfig>();
            }

            builder.RegisterInstance(upgradeCatalog);
            builder.RegisterInstance(rewardConfig);
            builder.RegisterInstance(idleIncomeConfig);

            builder.Register<EconomyService>(Lifetime.Singleton).As<IEconomyService>();
            builder.Register<EffectiveStatsService>(Lifetime.Singleton).As<IEffectiveStatsService>();

            // БЛОКЕР 4 (ревью шага 8, Temple standalone падал): гейт IdleIncomeService — ТОЛЬКО
            // _initialState != LevelPlaying, БЕЗ проверки root. Без root в этой ветке уже
            // зарегистрировано всё необходимое (ITimeProvider/IMetaProgressService/IEconomyService/
            // IdleIncomeConfig — все выше и без гейта на root), а TemplePresenter требует
            // IIdleIncomeService безусловно (см. ниже) — раньше Temple.unity, запущенная напрямую
            // (минуя Bootstrap), падала на резолве. Цена: standalone-Temple штампует LastSeenUtc в
            // сцено-локальный fallback-профиль (RegisterCampaignFlow, ветка root == null) — это
            // безвредно, профиль всё равно не переживает выгрузку сцены.
            if (_initialState != GameState.LevelPlaying)
            {
                builder.Register<IdleIncomeService>(Lifetime.Singleton).As<IIdleIncomeService, IStartable>();

                // Решение оркестратора (двойной idle-UI, ревью шага 8): в Temple офлайн-доход
                // обслуживает ТОЛЬКО кнопка _collectIdleButton (TempleView/§6) — WelcomeBackPresenter
                // регистрируется исключительно в мета-сцене БЕЗ Temple-view (одна точка UX на сцену).
                // Оверлей "с возвращением" в TempleCanvas остаётся заделом на будущее.
                if (_welcomeBackView != null && _templeView == null)
                {
                    builder.RegisterComponent<IWelcomeBackView>(_welcomeBackView);
                    builder.RegisterEntryPoint<WelcomeBackPresenter>();
                }
                else if (_welcomeBackView != null)
                {
                    Debug.Log("[GameLifetimeScope] _welcomeBackView прошит вместе с _templeView — оверлей 'с возвращением' не регистрируется в этой сцене (idle-доход в Temple обслуживает кнопка TempleView, см. решение оркестратора шага 8).");
                }
                else
                {
                    Debug.LogWarning("[GameLifetimeScope] _welcomeBackView не прошит — оверлей 'с возвращением' не будет работать в этой сцене.");
                }
            }

            if (_templeView != null)
            {
                builder.RegisterComponent<ITempleView>(_templeView);
                builder.RegisterEntryPoint<TemplePresenter>();
            }
            else
            {
                Debug.Log("[GameLifetimeScope] _templeView не прошит (ожидаемо для сцен без Temple) — экран Temple не будет работать в этой сцене.");
            }
        }
    }
}
