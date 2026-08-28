using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.Interaction;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Village
{
    /// <summary>
    /// Житель (game-design §5, §12) — Unity API only: NavMesh-free блуждание по случайным точкам,
    /// анимации (<see cref="AnimatorHashes"/>), Rigidbody-tumble в Tossed. Правила FSM живут в
    /// <see cref="IVillagerStateService"/> — контроллер только репортит стимулы и реагирует на
    /// <c>VillagerStateChangedEvent</c> визуалом/движением. Реализует <see cref="IGrabbable"/> —
    /// вторая (после <c>ThrowableObjectController</c>) реализация в проекте.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VillagerController : MonoBehaviour, IGrabbable
    {
        // "Достаточно близко к цели блуждания" — эпсилон реализации (как MinSampleCapacity в
        // HandInteractionService), не геймдизайн-параметр, поэтому не в VillagerConfig.
        private const float WanderArrivalThreshold = 0.3f;

        // Д1 (ревью плейтеста шага 3): ниже этой горизонтальной скорости направление движения
        // вырождается (шум/деление на почти-ноль) — поворот в этом случае не делаем вовсе, иначе
        // житель дёргается на месте вместо того, чтобы стоять неподвижно.
        private const float MinSpeedForRotation = 0.05f;

        // Пятый playtest шага 3: ниже этого отклонения transform.up от мирового верха корпус
        // считается уже вертикальным — не гоняем MoveRotation каждый тик ради миллиградусов.
        private const float UprightAngleEpsilonDegrees = 1f;

        // П3 (второй playtest шага 3): эпсилоны детекта "уперся" — не геймдизайн-параметры
        // (WanderTargetTimeout, вот он геймдизайн-параметр, — в VillagerConfig), а порог реализации:
        // "почти не двигается" и "как долго это терпим, прежде чем считать упёршимся раньше общего таймаута".
        private const float StuckSpeedThreshold = 0.1f;
        private const float StuckDetectionSeconds = 0.5f;

        [Tooltip("Animator жителя — параметры см. AnimatorHashes (MoveSpeed/IsWorshipping/IsStunned/IsLying/Scared/StandUp).")]
        [SerializeField] private Animator _animator;

        private IVillagerStateService _stateService;
        private IEventBus _eventBus;
        private VillagerConfig _config;
        private VillageLayoutConfig _layout;
        private bool _initialized;

        private Rigidbody _body;
        private VillagerId _villagerId;
        private GrabbableId _grabbableId;

        private IDisposable _stateSubscription;
        private VillagerState _currentState = VillagerState.Idle;

        private Vector3 _wanderOrigin;
        private Vector3 _wanderTarget;
        private bool _hasWanderTarget;
        private float _wanderTimer;

        // П3 (второй playtest шага 3): без NavMesh цель может оказаться недостижимой (перекрыта
        // другим жителем) — без страховки житель упирается и ждёт вечно (владелец прислал скриншот
        // залипших вплотную жителей). _wanderTargetElapsed — общий таймаут на попытку добраться до
        // текущей цели; _stuckTimer — более быстрый детект "почти не двигается".
        private float _wanderTargetElapsed;
        private float _stuckTimer;

        // П2 (второй playtest шага 3): позиция источника опасности для бегства в Panic — репортится
        // NeedSiteController'ом перед ReportStimulus. Без источника (стимул пришёл из другого места)
        // — фолбэк на прежнее блуждание-мечется в PanicRadius, см. FixedUpdate.
        private Vector3 _panicSourcePosition;
        private bool _hasPanicSource;

        // П1 (второй playtest шага 3): пока играет клип "Stand Up", жителя не двигаем и не
        // поворачиваем — иначе Idle-блуждание стартует поверх анимации вставания.
        private bool _isStandingUp;
        private float _standUpTimer;

        // В1-стиль (см. ThrowableObjectController): пока житель в руке, приземление не считаем —
        // трогание земли/соседа во время переноса не должно ложно триггерить ObjectLandedEvent.
        private bool _isGrabbed;
        private bool _gravityBeforeGrab;

        public GrabbableId Id => _grabbableId;
        public GrabbableKind Kind => GrabbableKind.Villager;
        public Rigidbody Body => _body;

        // Вычитка диффа целиком: "id жителя = InstanceId" раньше пересчитывался ещё и в
        // NeedSiteController — единственный источник теперь здесь (CLAUDE.md, запрет дублирования идентификаторов).
        public VillagerId VillagerId => _villagerId;

        // Уже летящего/несомого жителя повторно не схватить — тот же смысл, что "Tossed" в FSM.
        public bool CanBeGrabbed => _initialized && _currentState != VillagerState.Tossed;

        [Inject]
        public void Construct(IVillagerStateService stateService, IEventBus eventBus, VillagerConfig config, VillageLayoutConfig layout)
        {
            _stateService = stateService;
            _eventBus = eventBus;
            _config = config;
            _layout = layout;

            _body = GetComponent<Rigidbody>();
            _body.mass = _config.Mass;

            // Б1 (ревью шага 3): житель ВСЕГДА динамический — физику блуждания/броска ведёт рантайм,
            // не префаб (префаб теперь собирается editor-агентом с isKinematic=false, useGravity=true;
            // здесь дублируем явно, а не полагаемся молча на настройку префаба). useGravity НЕ
            // трогаем — выключается только на время захвата рукой, см. OnGrabbed/OnReleased.
            _body.isKinematic = false;

            _grabbableId = GrabbableId.FromInstanceId(gameObject.GetInstanceID());
            _villagerId = VillagerId.FromInstanceId(gameObject.GetInstanceID());

            Subscribe();

            _initialized = true;
        }

        private void OnEnable()
        {
            // В4 (ревью шага 3): пуловый цикл покрыт (Spawn → InjectGameObject → Construct →
            // Subscribe), но любой SetActive(false→true) МИМО фабрики (например, житель выключен и
            // снова включён вручную) раньше навсегда терял подписку — OnDisable отписывал
            // безусловно, а переподписки не было нигде. Subscribe() идемпотентен (сам себя
            // отписывает перед новой подпиской), поэтому дублирования с Construct не будет — на
            // самом первом спавне здесь просто рано (_initialized ещё false, CLAUDE.md: OnEnable до
            // Construct), а на повторных enable этот путь и есть единственный источник подписки.
            if (!_initialized) return;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (_isGrabbed && _body != null)
            {
                // Симметрично В12 GodHandController: объект не должен остаться навсегда без
                // гравитации, если сцену выгрузили/руку выключили прямо во время переноса.
                _body.useGravity = _gravityBeforeGrab;
                _isGrabbed = false;
            }
        }

        private void Subscribe()
        {
            Unsubscribe(); // защита от дублирования, если Construct и OnEnable оба успели вызваться
            _stateSubscription = _eventBus.Subscribe<VillagerStateChangedEvent>(OnVillagerStateChanged);
        }

        private void Unsubscribe()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
        }

        /// <summary>
        /// Явный сброс перед (пере)использованием — вызывается <c>VillagerFactory.Spawn</c> сразу
        /// после <c>InjectGameObject</c>, для каждого спавна (включая самый первый), а не только для
        /// пулового переиспользования. Переименован из Launch() (ревью шага 3): это не "запуск
        /// поведения" вообще, а идемпотентный сброс FSM-записи/физики/анимации перед новой жизнью —
        /// с шага 3 (Б3 ревью) VillagerFactory единственный путь появления жителя в сцене, поэтому
        /// именно здесь, а не в Construct, — регистрация в IVillagerStateService.
        /// </summary>
        public void ResetForReuse()
        {
            _stateService.Register(_villagerId);

            _wanderOrigin = transform.position;
            _hasWanderTarget = false;
            _wanderTimer = 0f;
            _wanderTargetElapsed = 0f;
            _stuckTimer = 0f;
            _hasPanicSource = false;
            _isStandingUp = false;
            _standUpTimer = 0f;

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;

            _currentState = VillagerState.Idle;
            ApplyPhysicsForState(VillagerState.Idle);
            // from=Idle (не Stunned) — это хард-сброс перед новой жизнью, а не настоящий переход
            // Stunned->Idle, StandUp здесь стрелять не должен.
            ApplyStateVisuals(VillagerState.Idle, VillagerState.Idle);
        }

        /// <summary>
        /// П2 (второй playtest шага 3): позиция источника опасности — репортит <c>NeedSiteController</c>
        /// перед <c>IVillagerStateService.ReportStimulus</c>, чтобы бегство в Panic шло прочь от
        /// конкретной точки, а не в случайную сторону. Физический источник направления — законная
        /// работа контроллера; решение о самом переходе в Panic остаётся в сервисе (эта запись
        /// направления с ним никак не связана и переход не вызывает).
        /// </summary>
        public void SetPanicSource(Vector3 sourcePosition)
        {
            _panicSourcePosition = sourcePosition;
            _hasPanicSource = true;
        }

        public void OnGrabbed()
        {
            _isGrabbed = true;
            _gravityBeforeGrab = _body.useGravity;
            _body.useGravity = false;
            _body.angularVelocity = Vector3.zero;

            // ReportStimulus публикует VillagerStateChangedEvent синхронно (Idle/Panic/Worship →
            // Tossed) — OnVillagerStateChanged тут же вызовет ApplyPhysicsForState(Tossed), который
            // и выставит constraints/collisionDetectionMode для полёта. Дублировать здесь не нужно.
            _stateService.ReportStimulus(_villagerId, VillagerStimulus.Grabbed);
        }

        public void OnReleased(Vector3 throwVelocity)
        {
            _isGrabbed = false;
            _body.useGravity = _gravityBeforeGrab;
            _body.linearVelocity = throwVelocity;

            ApplyTossTumble();
        }

        /// <summary>
        /// Шаг 4: разброс жителя взрывом чуда Fireball (game-design §4 — "разбрасывает жителей
        /// взрывом"). Вызывается <c>MiracleImpactController</c> по <c>MiracleCastEvent</c>, никогда
        /// напрямую из чуда — единственная связь чуда со сценой это событие (см. CLAUDE.md §4).
        /// В руке взрыв не вмешивается — симметрично тому, как OnGrabbed/OnReleased уже владеют
        /// физикой захвата целиком.
        /// </summary>
        public void ApplyBlast(Vector3 origin, float power)
        {
            if (_isGrabbed) return;

            // ReportStimulus публикует VillagerStateChangedEvent синхронно (тот же контракт, что в
            // OnGrabbed) — ApplyPhysicsForState(Tossed) снимает constraints/выставляет
            // CollisionDetectionMode ДО того, как ниже будет выставлена скорость разлёта.
            _stateService.ReportStimulus(_villagerId, VillagerStimulus.Blasted);

            _body.linearVelocity = VillagerMovementRules.ComputeBlastVelocity(origin, _body.position, power, _config.BlastUpwardBias);
            ApplyTossTumble();
        }

        /// <summary>
        /// Случайный тумбл (game-design §4/§5) — общий кусок для броска рукой (OnReleased) и взрыва
        /// чуда (ApplyBlast), вынесен, чтобы не дублировать три Random.Range.
        /// </summary>
        private void ApplyTossTumble()
        {
            _body.angularVelocity = new Vector3(
                UnityEngine.Random.Range(_config.TossAngularVelocityMin, _config.TossAngularVelocityMax),
                UnityEngine.Random.Range(_config.TossAngularVelocityMin, _config.TossAngularVelocityMax),
                UnityEngine.Random.Range(_config.TossAngularVelocityMin, _config.TossAngularVelocityMax));
        }

        private void FixedUpdate()
        {
            if (!_initialized) return;

            // Критический дефект третьего playtest'а шага 3: житель падал за край земли в свободном
            // падении навсегда (не в Tossed, FSM считала его нормальным, пул не забирал — тот же
            // класс "состояние без выхода без внешнего события", что и залипание в Tossed/вечное
            // ожидание недостижимой цели). Страховка независима от кламп-фикса выше по стеку —
            // работает даже если причина падения окажется какой-то другой. Не проверяем, пока в
            // руке — рука не носит ниже FallKillHeight, а рвать grab-контракт с GodHandController
            // здесь не нужно.
            if (!_isGrabbed && transform.position.y < _config.FallKillHeight)
            {
                RecoverFromFall();
                return;
            }

            if (_isStandingUp)
            {
                // П1 (второй playtest шага 3): пока играет клип "Stand Up" — ни движения, ни
                // доворота к цели (StopHorizontalMovement, UpdateWander не вызываются вовсе),
                // иначе Idle-блуждание стартует немедленно поверх анимации вставания и персонаж
                // визуально "уезжает". _currentState уже Idle к этому моменту (см.
                // OnVillagerStateChanged) — обычная Idle-ветка ниже просто не выполняется, пока флаг
                // не спал. Выпрямление корпуса (ApplyGroundedRotation, ниже по стеку) при этом
                // ПРОДОЛЖАЕТ работать — лок блокирует ходьбу и доворот к цели, но не выпрямление
                // (пятый playtest шага 3, требование явное).
                _standUpTimer -= Time.fixedDeltaTime;
                StopHorizontalMovement();
                if (_standUpTimer <= 0f)
                {
                    _isStandingUp = false;
                }
            }
            else
            {
                switch (_currentState)
                {
                    case VillagerState.Idle:
                        UpdateWander(_config.WanderRadius, _wanderOrigin, _config.MoveSpeed, _config.WanderPauseMin, _config.WanderPauseMax);
                        break;

                    case VillagerState.Panic:
                        // П2 (второй playtest шага 3): бегство прочь от источника опасности —
                        // PanicMoveSpeed (заведомо выше порога Walk→Run в аниматоре), не MoveSpeed.
                        // Без известного источника (стимул пришёл не от NeedSiteController) —
                        // фолбэк на прежнее "мечется в радиусе", лучше, чем стоять на месте.
                        if (_hasPanicSource)
                        {
                            UpdateFlee(_panicSourcePosition, _config.PanicMoveSpeed);
                        }
                        else
                        {
                            UpdateWander(_config.PanicRadius, transform.position, _config.PanicMoveSpeed, 0f, 0f);
                        }
                        break;

                    case VillagerState.Worship:
                    case VillagerState.Stunned:
                        StopHorizontalMovement();
                        break;

                    case VillagerState.Tossed:
                        // В полёте/в руке контроллер не вмешивается — скорость задаёт GodHandController
                        // (velocity-follow) или гравитация+бросок после отпускания.
                        break;
                }
            }

            // Правка после живого прогона: жёсткий кламп позиции/скорости — КАЖДЫЙ физтик, включая
            // окно "Stand Up" (StopHorizontalMovement не двигает жителя дальше, но не гарантирует,
            // что он приземлился/оглушился уже В границах — HardLanded мог случиться и за краем).
            // ПОСЛЕ решения о движении выше, ДО того, как физика на этом тике проинтегрирует
            // скорость в позицию. Только вне Tossed — брошенный рукой житель обязан иметь
            // возможность улететь за карту (FallKillHeight — намеренный последний рубеж именно для
            // этого случая, см. RecoverFromFall), кламп здесь его бы аннулировал.
            if (_currentState != VillagerState.Tossed)
            {
                ClampBodyToBounds();

                // Пятый playtest шага 3: после ClampBodyToBounds — если кламп погасил составляющую
                // скорости у границы, ApplyGroundedRotation должна довернуть по УЖЕ клампнутому
                // направлению (сползание вдоль края), а не по исходному, устремлённому в пустоту.
                ApplyGroundedRotation();
            }

            if (_animator != null)
            {
                // Д2 (ревью плейтеста шага 3): раньше MoveSpeed считался от Rigidbody.linearVelocity
                // безусловно — в Tossed рука разгоняет тело velocity-follow'ом (или бросок/гравитация
                // после отпускания), и это читалось Animator'ом как "бежит по воздуху". В Tossed/
                // Stunned скорость Rigidbody на MoveSpeed не влияет вовсе (ForcesZeroMoveSpeed).
                var moveSpeedValue = 0f;
                if (!VillagerPoseRules.ForcesZeroMoveSpeed(_currentState))
                {
                    var horizontalVelocity = _body.linearVelocity;
                    horizontalVelocity.y = 0f;
                    moveSpeedValue = horizontalVelocity.magnitude;
                }

                _animator.SetFloat(AnimatorHashes.MoveSpeed, moveSpeedValue);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_initialized) return;
            if (_isGrabbed) return; // в руке — глушим приземление (§13, тот же контракт, что Throwable)
            if (_currentState != VillagerState.Tossed) return; // приземление имеет смысл только после броска

            var impactSpeed = collision.relativeVelocity.magnitude;
            var isHardLanding = impactSpeed >= _config.HardLandingSpeedThreshold;

            _stateService.ReportStimulus(_villagerId, isHardLanding ? VillagerStimulus.HardLanded : VillagerStimulus.Landed);
            _eventBus.Publish(new ObjectLandedEvent(_grabbableId, impactSpeed, false));
        }

        private void OnVillagerStateChanged(VillagerStateChangedEvent evt)
        {
            if (evt.Id != _villagerId) return;

            _currentState = evt.To;

            // П1 (второй playtest шага 3): любая НОВАЯ транзиция отменяет предыдущий лок на
            // вставание — например, если жителя схватили рукой прямо во время проигрывания StandUp
            // (Idle пока ещё не "устоялся"), Tossed должен получить управление немедленно, а не
            // после того, как FixedUpdate закончит принудительно обнулять его скорость.
            _isStandingUp = false;

            var firedStandUp = ApplyStateVisuals(evt.From, evt.To);
            ApplyPhysicsForState(evt.To);

            if (firedStandUp)
            {
                _isStandingUp = true;
                _standUpTimer = _config.StandUpDuration;
            }

            // Смена состояния — новая фаза блуждания: старая цель (если была) больше не актуальна.
            _hasWanderTarget = false;
            _wanderTimer = 0f;
            _wanderTargetElapsed = 0f;
            _stuckTimer = 0f;

            // Вычитка диффа целиком: источник паники устаревает при выходе из Panic — иначе
            // следующий стимул без SetPanicSource (фолбэк-ветка UpdateWander в Panic) погонит
            // жителя от точки, которая уже не актуальна.
            if (evt.From == VillagerState.Panic)
            {
                _hasPanicSource = false;
            }
        }

        /// <summary>Возвращает true, если сработал триггер StandUp (используется в OnVillagerStateChanged, чтобы завести лок на движение — П1).</summary>
        private bool ApplyStateVisuals(VillagerState from, VillagerState to)
        {
            if (_animator == null) return false;

            _animator.SetBool(AnimatorHashes.IsWorshipping, to == VillagerState.Worship);

            // Шестой playtest шага 3: IsStunned и IsLying раньше были одним и тем же признаком
            // (IsStunnedVisual, "оглушённая поза") — владелец развёл две склеенные позы. IsStunned
            // (Dizzy стоя) — ТОЛЬКО в руке/в полёте (Tossed). IsLying (лежит на земле) — только
            // после жёсткого приземления (Stunned), до срабатывания StandUp.
            _animator.SetBool(AnimatorHashes.IsStunned, to == VillagerState.Tossed);
            _animator.SetBool(AnimatorHashes.IsLying, to == VillagerState.Stunned);

            if (to == VillagerState.Panic)
            {
                _animator.SetTrigger(AnimatorHashes.Scared);
            }

            // ShouldPlayStandUp — осознанное решение владельца-уровня (шестой playtest шага 3):
            // StandUp стреляет ТОЛЬКО из настоящего лежания (Stunned), не из мягкой постановки
            // (Tossed -> Idle через Landed). Житель, аккуратно поставленный рукой, приземляется на
            // ноги и сразу идёт — вставать ему не с чего, он и не падал. Раньше здесь работало
            // общее правило "любой выход из позы с IsStunned==true" (IsStunnedVisual), которое
            // покрывало Tossed тоже, — с разделением поз это стало неверно: IsStunned (Dizzy) и
            // IsLying теперь разные предикаты, и только IsLying-состояние (Stunned) подразумевает
            // "надо встать".
            var firesStandUp = VillagerPoseRules.ShouldPlayStandUp(from, to);
            if (firesStandUp)
            {
                _animator.SetTrigger(AnimatorHashes.StandUp);
            }

            return firesStandUp;
        }

        private void ApplyPhysicsForState(VillagerState state)
        {
            if (state == VillagerState.Tossed)
            {
                // Разморожен полностью ради тумбла при броске.
                _body.constraints = RigidbodyConstraints.None;

                // В6 (ревью шага 3): Continuous на весь полёт — быстрый бросок иначе может "пробить"
                // тонкий коллайдер за один физтик (тот же приём, что ThrowableObjectController).
                // Раньше это ставилось только в OnGrabbed и никогда не возвращалось в Discrete —
                // здесь единая точка входа/выхода из Tossed, симметрично для обоих направлений.
                _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            else
            {
                // Д1 (ревью плейтеста шага 3): раньше морозили только X/Z — Y оставался свободным,
                // и столкновения жителей друг с другом раскручивали их вокруг вертикали прямо в Idle
                // (владелец видел это на плейтесте). На земле (Idle/Panic/Worship/Stunned) вращение
                // заморожено ПОЛНОСТЬЮ — поворот к цели/выпрямление делает только код (ApplyGroundedRotation),
                // физика вращения тела не касается вовсе.
                _body.constraints = RigidbodyConstraints.FreezeRotation;
                _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
        }

        private void UpdateWander(float radius, Vector3 center, float moveSpeed, float pauseMin, float pauseMax)
        {
            if (!_hasWanderTarget)
            {
                if (_wanderTimer > 0f)
                {
                    _wanderTimer -= Time.fixedDeltaTime;
                    StopHorizontalMovement();
                    return;
                }

                PickWanderTarget(radius, center);
            }

            var toTarget = _wanderTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= WanderArrivalThreshold)
            {
                _hasWanderTarget = false;
                _wanderTimer = UnityEngine.Random.Range(pauseMin, pauseMax);
                StopHorizontalMovement();
                return;
            }

            // П3 (второй playtest шага 3): без NavMesh цель может быть недостижима (перекрыта другим
            // жителем) — без страховки житель упирается и ждёт вечно (владелец прислал скриншот
            // залипших вплотную жителей). Два независимых предохранителя:
            // 1) общий таймаут на попытку (WanderTargetTimeout) — ловит "медленно, но неизбежно застрял";
            // 2) быстрый детект "почти не двигается" дольше StuckDetectionSeconds — ловит явный упор
            //    в препятствие раньше общего таймаута, не дожидаясь его целиком.
            _wanderTargetElapsed += Time.fixedDeltaTime;

            var currentHorizontalSpeed = new Vector3(_body.linearVelocity.x, 0f, _body.linearVelocity.z).magnitude;
            _stuckTimer = currentHorizontalSpeed < StuckSpeedThreshold ? _stuckTimer + Time.fixedDeltaTime : 0f;

            if (_wanderTargetElapsed >= _config.WanderTargetTimeout || _stuckTimer >= StuckDetectionSeconds)
            {
                // Не выбираем новую цель прямо здесь — следующий вызов сам её подберёт через ветку
                // "!_hasWanderTarget" выше (единственное место, где вызывается PickWanderTarget).
                _hasWanderTarget = false;
                StopHorizontalMovement();
                return;
            }

            var direction = toTarget.normalized;
            var horizontalVelocity = direction * moveSpeed;
            _body.linearVelocity = new Vector3(horizontalVelocity.x, _body.linearVelocity.y, horizontalVelocity.z);
        }

        /// <summary>
        /// П2 (второй playtest шага 3): бегство прочь от точки опасности — в отличие от
        /// UpdateWander, здесь нет "цели" и "прибытия": направление пересчитывается каждый тик,
        /// житель просто непрерывно убегает, пока не сменится состояние (Panic не длится вечно —
        /// PanicDuration).
        /// </summary>
        private void UpdateFlee(Vector3 awayFrom, float moveSpeed)
        {
            var away = transform.position - awayFrom;
            away.y = 0f;

            // Житель ровно в точке источника — направление не определено, берём текущий forward
            // вместо normalized нулевого вектора (который дал бы NaN).
            var direction = away.sqrMagnitude > VillagerMovementRules.DirectionEpsilonSqr ? away.normalized : transform.forward;

            // Критический дефект третьего playtest'а: "прочь от источника" у обрыва гарантированно
            // уводит за физический край земли — направление клампится по границе уровня, у самой
            // границы разворачиваясь вдоль неё, а не толкая дальше в пустоту.
            direction = VillagerMovementRules.ClampFleeDirectionToBounds(transform.position, direction, _layout.VillagerBoundsHalfExtent);

            var horizontalVelocity = direction * moveSpeed;
            _body.linearVelocity = new Vector3(horizontalVelocity.x, _body.linearVelocity.y, horizontalVelocity.z);
        }

        /// <summary>
        /// Единая точка вращения тела вне Tossed (правка пятого playtest'а шага 3 — "выпрямление
        /// vs довернуть к цели, п.3"). Раньше "довернуть к направлению движения" и "выпрямить
        /// корпус" были бы двумя отдельными MoveRotation-вызовами за тик — это удвоило бы бюджет
        /// поворота (TurnSpeedDegreesPerSecond дважды за кадр) и дало бы дёрганье. Решение —
        /// ОБЪЕДИНИТЬ в одну цель за тик: если житель реально движется (горизонтальная скорость
        /// ≥ MinSpeedForRotation) — цель "смотреть по направлению движения, up = мировой верх"
        /// (Quaternion.LookRotation сама ставит корпус вертикально по ходу поворота — отдельного
        /// выпрямления не требуется, довернуть и выпрямиться — один и тот же RotateTowards); иначе —
        /// "тот же yaw, но корпус вертикален" (VillagerMovementRules.ComputeUprightRotation). Второй случай покрывает
        /// Worship/Stunned/паузы блуждания и, что важно по требованию задачи, окно _isStandingUp:
        /// лок блокирует ходьбу и доворот к цели (велосити принудительно нулевая — StopHorizontalMovement),
        /// но не выпрямление — иначе мягкое приземление на бок с мгновенным StandUp осталось бы кривым
        /// (ровно один из путей со скриншота владельца).
        /// </summary>
        private void ApplyGroundedRotation()
        {
            var horizontalVelocity = new Vector3(_body.linearVelocity.x, 0f, _body.linearVelocity.z);
            var current = _body.rotation;

            Quaternion target;
            if (horizontalVelocity.magnitude >= MinSpeedForRotation)
            {
                target = Quaternion.LookRotation(horizontalVelocity.normalized, Vector3.up);
            }
            else if (Vector3.Angle(current * Vector3.up, Vector3.up) > UprightAngleEpsilonDegrees)
            {
                target = VillagerMovementRules.ComputeUprightRotation(current);
            }
            else
            {
                return; // уже вертикален и не движется — доворачивать нечего
            }

            var maxDegreesDelta = _config.TurnSpeedDegreesPerSecond * Time.fixedDeltaTime;
            _body.MoveRotation(Quaternion.RotateTowards(current, target, maxDegreesDelta));
        }

        private void PickWanderTarget(float radius, Vector3 center)
        {
            var offset = UnityEngine.Random.insideUnitCircle * radius;
            var rawTarget = center + new Vector3(offset.x, 0f, offset.y);

            // Критический дефект третьего playtest'а шага 3: WanderRadius от точки спавна уводил
            // цель за физический край земли (спавны у края + радиус давали точку на метр дальше
            // границы карты) — житель шёл к ней и падал в бездну. Кламп — по границе уровня
            // (VillageLayoutConfig.VillagerBoundsHalfExtent), а не по радиусу от спавна.
            _wanderTarget = VillagerMovementRules.ClampToVillageBounds(rawTarget, _layout.VillagerBoundsHalfExtent);
            _hasWanderTarget = true;

            // П3: новая попытка — таймауты считаем заново, старая "застрявшая" попытка не должна
            // немедленно засчитаться против свежевыбранной цели.
            _wanderTargetElapsed = 0f;
            _stuckTimer = 0f;
        }

        private void StopHorizontalMovement()
        {
            _body.linearVelocity = new Vector3(0f, _body.linearVelocity.y, 0f);
        }

        /// <summary>
        /// Жёсткий кламп позиции/скорости у границы уровня (правка после живого прогона: кламп
        /// ТОЛЬКО цели блуждания/бегства не спас — столкновение с другим жителем у края или овершут
        /// по инерции всё равно выталкивали за границу, лог подтвердил регулярные срабатывания
        /// FallKillHeight). Позиция возвращается на границу тем же
        /// <see cref="VillagerMovementRules.ClampToVillageBounds"/>, что и цель блуждания (единственный
        /// источник границы, не дублируем формулу); скорость —
        /// <see cref="VillagerMovementRules.ClampVelocityToBounds"/> гасит только выталкивающую наружу
        /// составляющую, тангенциальная (вдоль границы) остаётся нетронутой — житель скользит вдоль
        /// края, а не "примерзает" к нему.
        /// </summary>
        private void ClampBodyToBounds()
        {
            var halfExtent = _layout.VillagerBoundsHalfExtent;
            var position = transform.position;

            var clampedPosition = VillagerMovementRules.ClampToVillageBounds(position, halfExtent);
            if (clampedPosition != position)
            {
                // Правка по итогам вычитки диффа целиком: transform.position= на живом Rigidbody
                // обходит физику — тот же класс проблемы, что чинили в В7 (transform.rotation ->
                // MoveRotation). Тут коррекция мелкая (снап на границу), не телепорт — интерполяция
                // не даёт заметного артефакта, поэтому без временного отключения interpolation
                // (в отличие от RecoverFromFall, см. там).
                _body.position = clampedPosition;
            }

            _body.linearVelocity = VillagerMovementRules.ClampVelocityToBounds(position, _body.linearVelocity, halfExtent);
        }

        /// <summary>
        /// Страховка от падения в бездну (критический дефект третьего playtest'а шага 3):
        /// телепорт на ближайшую точку спавна, полный сброс скорости/физики и явный возврат FSM в
        /// Idle. Debug.LogWarning — обязателен по замыслу: маскировать симптом молча значило бы
        /// скрыть первопричину вместо её починки (та же логика, что и в остальных предохранителях
        /// этого шага — Н3, В5).
        /// </summary>
        private void RecoverFromFall()
        {
            var recoveryPosition = VillagerMovementRules.FindNearestSpawnPosition(transform.position, _layout.VillagerSpawnPositions);

            Debug.LogWarning(
                $"[VillagerController] Житель {_villagerId} провалился ниже FallKillHeight " +
                $"({_config.FallKillHeight}, текущий Y={transform.position.y:F1}) — это симптом " +
                $"другого дефекта (недостижимая цель блуждания/бегства вне границ уровня), не штатное " +
                $"поведение. Телепортирован на {recoveryPosition}, состояние сброшено в Idle.");

            // Телепорт на сотни метров — при RigidbodyInterpolation.Interpolate физика на секунду
            // "домотает" визуал от старой позиции к новой, давая однокадровый шлейф. Отключаем
            // интерполяцию на время самого телепорта и возвращаем как было (правка по итогам
            // вычитки диффа целиком). transform.position= заменён на _body.position= — тот же
            // класс проблемы, что чинили в В7 (transform.rotation -> MoveRotation): прямая запись
            // в Transform обходит физику.
            var previousInterpolation = _body.interpolation;
            _body.interpolation = RigidbodyInterpolation.None;
            _body.position = recoveryPosition;
            _body.interpolation = previousInterpolation;

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;

            _isStandingUp = false;
            _standUpTimer = 0f;
            _hasWanderTarget = false;
            _wanderTimer = 0f;
            _wanderTargetElapsed = 0f;
            _stuckTimer = 0f;
            _hasPanicSource = false;
            _wanderOrigin = recoveryPosition;

            var previousState = _currentState;
            _currentState = VillagerState.Idle;
            ApplyPhysicsForState(VillagerState.Idle);

            // П1-регрессия (вычитка диффа целиком): раньше возвращаемое значение ApplyStateVisuals
            // здесь игнорировалось. Шестой playtest шага 3 сузил правило до
            // VillagerPoseRules.ShouldPlayStandUp (только из Stunned) — если жителя телепортировало
            // из Tossed, previousState==Tossed, StandUp корректно НЕ стреляет (телепортирован стоя,
            // вставать не с чего); если из Stunned — стреляет, и лок движения ниже обязан взвестись,
            // иначе житель поедет по земле поверх клипа вставания. Обрабатываем возврат так же, как в
            // OnVillagerStateChanged.
            if (ApplyStateVisuals(previousState, VillagerState.Idle))
            {
                _isStandingUp = true;
                _standUpTimer = _config.StandUpDuration;
            }

            // IVillagerStateService.ReportStimulus работает только по таблице легальных переходов
            // (game-design §5) — стимула "телепортировали из бездны" в этой таблице нет и не должно
            // быть. Unregister+Register — единственный способ форсировать запись сервиса в Idle в
            // обход таблицы; событие при этом не публикуется, поэтому _currentState выше выставлен
            // вручную, а не через подписку на VillagerStateChangedEvent.
            _stateService.Unregister(_villagerId);
            _stateService.Register(_villagerId);
        }
    }
}
