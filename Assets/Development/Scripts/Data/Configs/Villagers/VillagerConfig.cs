using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Villagers
{
    /// <summary>
    /// Параметры жителя (game-design §5, §12). Дефолты — черновые, крутятся playtest'ом
    /// (как и HandConfig/IsometricCameraConfig на шаге 2).
    /// </summary>
    [CreateAssetMenu(fileName = "VillagerConfig", menuName = "BlackWhiteLike/Villager Config")]
    public sealed class VillagerConfig : ScriptableObject
    {
        [Header("Блуждание (Idle) — случайные точки в радиусе от места спавна")]
        [SerializeField] private float _moveSpeed = 2f;
        [SerializeField] private float _wanderRadius = 5f;
        [Tooltip("Мин/макс пауза (сек) между точками блуждания.")]
        [SerializeField] private float _wanderPauseMin = 1f;
        [SerializeField] private float _wanderPauseMax = 3f;

        [Tooltip(
            "П3 (второй playtest шага 3): без NavMesh цель блуждания может оказаться недостижимой " +
            "(перекрыта другим жителем) — если не дойти за это время, выбирается новая цель. " +
            "Дополнительно есть более быстрый детект \"уперся\" (см. VillagerController) — этот " +
            "таймаут страхует более медленные/частичные блокировки.")]
        [SerializeField] private float _wanderTargetTimeout = 4f;

        [Tooltip(
            "Д1 (ревью плейтеста шага 3): скорость доворота к направлению движения, град/сек. " +
            "Поворот интерполируется (Quaternion.RotateTowards), а не мгновенный MoveRotation — " +
            "иначе жители дёргано телепортируются лицом к новой цели блуждания.")]
        [SerializeField] private float _turnSpeedDegreesPerSecond = 540f;

        [Header("Паника (Panic) — бегство от источника опасности, без пауз")]
        [SerializeField] private float _panicRadius = 6f;
        [SerializeField] private float _panicDuration = 4f;

        [Tooltip(
            "П2 (второй playtest шага 3): скорость в панике — заведомо выше обычной MoveSpeed, иначе " +
            "паника визуально неотличима от прогулки. Это же значение — порог Run в блендтри Movement: " +
            "Setup Villager Prefab читает MoveSpeed/PanicMoveSpeed при сборке контроллера, поэтому после " +
            "изменения поля достаточно перепрогнать пункт меню — ручной синхронизации с аниматором нет.")]
        [SerializeField] private float _panicMoveSpeed = 5f;

        [Header("Молитва (Worship)")]
        [SerializeField] private float _worshipDuration = 3f;

        [Header("Оглушение (Stunned)")]
        [SerializeField] private float _stunDuration = 2f;

        [Tooltip(
            "Значение derived — штампуется editor-скриптом Setup Villager Prefab как длина клипа " +
            "Mixamo \"Stand Up\" делённая на StandUpPlaybackSpeed. Руками не крутить: следующий " +
            "прогон пункта меню перезапишет вручную выставленное значение.")]
        [SerializeField] private float _standUpDuration = 1.8f;

        [Tooltip(
            "Множитель скорости состояния StandUp в аниматоре. Применяется editor-скриптом Setup " +
            "Villager Prefab при сборке контроллера; он же штампует StandUpDuration = длина клипа / " +
            "эта скорость — после изменения перепрогнать пункт меню.")]
        [SerializeField] private float _standUpPlaybackSpeed = 2f;

        [Header("Бросок (Tossed) — без полного ragdoll'а, Scalability Hook (game-design §14)")]
        [Tooltip("Диапазон случайной угловой скорости тумбла, применяется при отпускании броском.")]
        [SerializeField] private float _tossAngularVelocityMin = 2f;
        [SerializeField] private float _tossAngularVelocityMax = 6f;

        [Tooltip("Порог относительной скорости приземления, начиная с которого приземление жёсткое (Stunned вместо возврата в Idle).")]
        [SerializeField] private float _hardLandingSpeedThreshold = 4f;

        [Tooltip(
            "Н3 (ревью шага 3): страховочный таймаут Tossed → Idle. Если Landed/HardLanded так и не " +
            "пришёл (улетел за коллайдеры, застрял в геометрии), житель не должен зависать навсегда " +
            "нехватаемым (CanBeGrabbed == false в Tossed).")]
        [SerializeField] private float _tossedTimeout = 6f;

        [Header("Физика")]
        [Tooltip("Масса Rigidbody — сверяется с HandConfig.MaxGrabMass при захвате (см. IHandInteractionService.CanGrab).")]
        [SerializeField] private float _mass = 10f;

        [Tooltip(
            "Третий playtest шага 3 (критический дефект): страховка от падения в бездну — если " +
            "житель оказался ниже этой высоты по Y (ушёл за край земли до/несмотря на кламп области " +
            "блуждания, или ещё каким-то путём), он телепортируется на ближайшую точку спавна со " +
            "сбросом скорости и состояния в Idle, с Debug.LogWarning (не маскируем дефект молча). " +
            "Тот же класс проблемы, что залипание в Tossed и вечное ожидание недостижимой цели — " +
            "состояние без выхода без внешнего события.")]
        [SerializeField] private float _fallKillHeight = -5f;

        [Header("Префаб")]
        [Tooltip("Ссылка на префаб жителя — единственный источник (используется VillagerFactory через IPoolManager, не через ещё одно поле на GameLifetimeScope).")]
        [SerializeField] private GameObject _prefab;

        public float MoveSpeed => _moveSpeed;
        public float WanderRadius => _wanderRadius;
        public float WanderPauseMin => _wanderPauseMin;
        public float WanderPauseMax => _wanderPauseMax;
        public float WanderTargetTimeout => _wanderTargetTimeout;
        public float TurnSpeedDegreesPerSecond => _turnSpeedDegreesPerSecond;
        public float PanicRadius => _panicRadius;
        public float PanicDuration => _panicDuration;
        public float PanicMoveSpeed => _panicMoveSpeed;
        public float WorshipDuration => _worshipDuration;
        public float StunDuration => _stunDuration;
        public float StandUpDuration => _standUpDuration;
        public float StandUpPlaybackSpeed => _standUpPlaybackSpeed;
        public float TossAngularVelocityMin => _tossAngularVelocityMin;
        public float TossAngularVelocityMax => _tossAngularVelocityMax;
        public float HardLandingSpeedThreshold => _hardLandingSpeedThreshold;
        public float TossedTimeout => _tossedTimeout;
        public float Mass => _mass;
        public float FallKillHeight => _fallKillHeight;
        public GameObject Prefab => _prefab;

        // НБ2-стиль (см. IsometricCameraConfig): без OnValidate Min/Max могли разъехаться в
        // Inspector и тихо сломать Random.Range (min > max — исключение ArgumentException).
        private void OnValidate()
        {
            _wanderPauseMax = Mathf.Max(_wanderPauseMin, _wanderPauseMax);
            _tossAngularVelocityMax = Mathf.Max(_tossAngularVelocityMin, _tossAngularVelocityMax);
        }
    }
}
