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
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Data.Configs.Throwable;
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
using BlackWhiteLike.Services.Idle;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Meta;
using BlackWhiteLike.Services.Miracles;
using BlackWhiteLike.Services.Village;
using BlackWhiteLike.Services.Worship;
using BlackWhiteLike.Views.UI.Hud;
using BlackWhiteLike.Views.UI.Overlay;
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

        protected override void Configure(IContainerBuilder builder)
        {
            // Core
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventBus>();

            // Services
            builder.Register<SystemTimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
            builder.Register<PlayerPrefsProfileStorage>(Lifetime.Singleton).As<IProfileStorage>();

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

            // Шаг 2 — Камера + рука.
            RegisterHandAndCamera(builder);

            // Шаг 3 — Жители + события.
            RegisterVillageSystems(builder);

            // Шаг 4 — Чудеса + Faith.
            RegisterMiracleSystems(builder);

            // Шаг 5 — Worship + win.
            RegisterLevelFlow(builder);

            // Шаг 6 — Alignment-визуал (Heal/Lightning — данные, новых сервисов не требуют).
            RegisterAlignmentVisual(builder);

            // Точка входа
            builder.RegisterEntryPoint<GameBootstrap>();
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
        private void RegisterVillageSystems(IContainerBuilder builder)
        {
            var villagerConfig = _villagerConfig;
            if (villagerConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _villagerConfig не прошит (ожидаемо для сцен без деревни) — используется fallback VillagerConfig с дефолтами.");
                villagerConfig = ScriptableObject.CreateInstance<VillagerConfig>();
            }

            builder.RegisterInstance(villagerConfig);

            var levelConfig = _levelConfig;
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
        /// FSM-биндинги, Worship/Alignment/LevelFlow, InputRouter и HUD/оверлеи уровня (game-design
        /// §9, §10, §13, шаг 5). Состояния регистрируются как именованные биндинги
        /// (<see cref="IGameStateBinding"/>), собираемые <c>GameFlowService</c> в конструкторе
        /// (см. remarks на нём) — не в его конструкторе напрямую. LevelSelect/TempleMeta/WelcomeBack
        /// — заглушки <see cref="NullState"/> (экраны шагов 7-8), чтобы ChangeState/PushState на них
        /// не роняли <c>StateMachine.TryGetState</c> в LogError.
        /// </summary>
        private void RegisterLevelFlow(IContainerBuilder builder)
        {
            builder.RegisterInstance(new BootstrapSettings(_initialState));

            var worshipConfig = _worshipConfig;
            if (worshipConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _worshipConfig не прошит (ожидаемо для сцен без Worship) — используется fallback WorshipConfig с дефолтами (пустые таблицы Awe/Fear).");
                worshipConfig = ScriptableObject.CreateInstance<WorshipConfig>();
            }

            builder.RegisterInstance(worshipConfig);

            builder.Register<UnityTimeScaleProvider>(Lifetime.Singleton).As<ITimeScaleProvider, IDisposable>();

            // Состояния FSM — обычные Singleton; биндинг на GameState — отдельной записью
            // IGameStateBinding ниже. Порядок регистрации между Register<TState> и
            // Register<IGameStateBinding> значения не имеет — VContainer резолвит граф зависимостей
            // после Build(), а не по мере вызовов Register.
            builder.Register<MainMenuState>(Lifetime.Singleton);
            builder.Register<LevelPlayingState>(Lifetime.Singleton);
            builder.Register<LevelCompleteState>(Lifetime.Singleton);
            builder.Register<PausedState>(Lifetime.Singleton);

            // Первый Play Mode шага 5: несколько Register<IGameStateBinding>(r => ...) VContainer
            // НЕ собирает в коллекцию — у Func-регистраций один и тот же ImplementationType, и
            // CollectionInstanceProvider бросает «Conflict implementation type» на Build(). Поэтому
            // коллекция регистрируется ОДНОЙ записью точного типа IEnumerable<IGameStateBinding>:
            // Registry.TryGet проверяет точное совпадение типа раньше fallback'ов на коллекции
            // (Registry.cs, TryGet), и GameFlowService получает готовый список.
            // Заглушки LevelSelect/TempleMeta/WelcomeBack (NullState) — экраны шагов 7-8 ещё не
            // существуют, но ключ должен быть зарегистрирован, чтобы ChangeState не падал.
            builder.Register<IEnumerable<IGameStateBinding>>(
                r => new IGameStateBinding[]
                {
                    new GameStateBinding(GameState.MainMenu, r.Resolve<MainMenuState>()),
                    new GameStateBinding(GameState.LevelPlaying, r.Resolve<LevelPlayingState>()),
                    new GameStateBinding(GameState.LevelComplete, r.Resolve<LevelCompleteState>()),
                    new GameStateBinding(GameState.Paused, r.Resolve<PausedState>()),
                    new GameStateBinding(GameState.LevelSelect, new NullState()),
                    new GameStateBinding(GameState.TempleMeta, new NullState()),
                    new GameStateBinding(GameState.WelcomeBack, new NullState()),
                },
                Lifetime.Singleton);

            builder.Register<InputRouter>(Lifetime.Singleton).As<IInputRouter>();

            // WorshipService/LevelFlowService — оба ITickable ПОСЛЕ FaithService (RegisterMiracleSystems
            // отработал выше): реген Faith в этом кадре должен применяться раньше decay/win-check
            // Worship и stall-check LevelFlow, тот же приём, что порядок VillagerStateService → FaithService.
            builder.Register<WorshipService>(Lifetime.Singleton).As<IWorshipService, ITickable, IDisposable>();
            builder.Register<AlignmentService>(Lifetime.Singleton).As<IAlignmentService, IDisposable>();
            builder.Register<LevelFlowService>(Lifetime.Singleton).As<ILevelFlowService, ITickable, IDisposable>();

            RegisterLevelHud(builder);
            RegisterLevelOverlays(builder);
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
    }
}
