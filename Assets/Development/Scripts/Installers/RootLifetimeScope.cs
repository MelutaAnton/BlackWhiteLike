using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Meta;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BlackWhiteLike.Installers
{
    /// <summary>
    /// Root Composition Root кампании (game-design §6/§9, шаг 7) — живёт на объекте
    /// <c>[RootScope]</c> в сцене Bootstrap, переживает загрузки сцен (DontDestroyOnLoad) и держит
    /// ТОЛЬКО межсценное: выбор уровня (<see cref="ILevelSelectionService"/>), прогресс кампании
    /// (<see cref="ILevelProgressService"/>), загрузчик сцен (<see cref="ISceneLoaderService"/>)
    /// и каталог уровней. Игровые системы остаются в пер-сценном <see cref="GameLifetimeScope"/>;
    /// EventBus тоже пер-сценный — межсценной шины нет осознанно.
    /// </summary>
    /// <remarks>
    /// Почему контролируемый <see cref="Instance"/>, а не канонический parentReference VContainer:
    /// по исходнику <c>LifetimeScope.Awake</c> (VContainer 1.19, GetRuntimeParent) scope с
    /// parentReference на отсутствующий в сцене тип ловит VContainerParentTypeReferenceNotFound и
    /// навсегда паркуется во внутреннем waiting list — тестовые сцены (_TestVillage/_TestHand),
    /// запущенные напрямую без Bootstrap, вообще не собрали бы контейнер. Требование проекта —
    /// standalone-запуск любой сцены, поэтому связь ручная: GameLifetimeScope читает Instance в
    /// Configure() и при null регистрирует локальные фоллбэки. Static-стейт контролируем
    /// (чек-лист CLAUDE.md п.10): гвард от дублей в Awake, сброс в OnDestroy.
    /// </remarks>
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [Header("Шаг 7 — Кампания")]
        [Tooltip("Каталог уровней кампании (порядок записей = линейный анлок).")]
        [SerializeField] private LevelCatalogConfig _levelCatalog;

        /// <summary>Единственный живой root-scope; null — игра запущена минуя Bootstrap (тестовые сцены).</summary>
        public static RootLifetimeScope Instance { get; private set; }

        /// <summary>Сервис выбора уровня — резолвится из собственного контейнера root'а.</summary>
        public ILevelSelectionService LevelSelection => Container.Resolve<ILevelSelectionService>();

        /// <summary>Прогресс кампании (пройденные уровни, линейный анлок).</summary>
        public ILevelProgressService LevelProgress => Container.Resolve<ILevelProgressService>();

        /// <summary>Загрузчик сцен (имена — только SceneNames).</summary>
        public ISceneLoaderService SceneLoader => Container.Resolve<ISceneLoaderService>();

        /// <summary>Каталог уровней; никогда не null — при непрошитом поле подставлен fallback (см. Configure).</summary>
        public LevelCatalogConfig LevelCatalog => Container.Resolve<LevelCatalogConfig>();

        protected override void Awake()
        {
            // Гвард от дублей (чек-лист CLAUDE.md п.10): повторная загрузка Bootstrap при живом
            // root'е (возврат в меню через Bootstrap, ручной рестарт) не должна плодить второй
            // DontDestroyOnLoad-scope и двойные регистрации.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[RootLifetimeScope] Обнаружен дубль root-scope (повторная загрузка Bootstrap) — новый экземпляр уничтожен, действующий сохранён.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            base.Awake(); // LifetimeScope.Awake строит контейнер — после установки Instance
        }

        protected override void OnDestroy()
        {
            // Сброс контролируемого static (страховка п.10) — только если умирает действующий
            // экземпляр, а не отбитый гвардом дубль.
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnDestroy();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            var levelCatalog = _levelCatalog;
            if (levelCatalog == null)
            {
                // Непрошитое поле — сцена Bootstrap собрана до прогона editor-скрипта. Fallback
                // (пустой каталог) вместо NRE: кампания покажет ноль карточек, но не упадёт.
                Debug.LogWarning("[RootLifetimeScope] _levelCatalog не прошит — создан пустой fallback-каталог. Прогоните BlackWhiteLike/Setup/Build Bootstrap Scene.");
                levelCatalog = ScriptableObject.CreateInstance<LevelCatalogConfig>();
            }

            builder.RegisterInstance(levelCatalog);

            // PlayerPrefs-хранилище root'а и сценовых scope'ов — один и тот же физический
            // PlayerPrefs, отдельные инстансы обёртки безвредны.
            builder.Register<PlayerPrefsProfileStorage>(Lifetime.Singleton).As<IProfileStorage>();

            builder.Register<LevelSelectionService>(Lifetime.Singleton).As<ILevelSelectionService>();
            builder.Register<LevelProgressService>(Lifetime.Singleton).As<ILevelProgressService>();
            builder.Register<SceneLoaderService>(Lifetime.Singleton).As<ISceneLoaderService>();

            // Из Bootstrap-сцены сразу уходим в MainMenu (Bootstrap — пустая служебная сцена).
            builder.RegisterEntryPoint<BootstrapFlowEntryPoint>();
        }
    }
}
