using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.Interaction;
using BlackWhiteLike.Data.Configs.Throwable;
using BlackWhiteLike.Models;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Village
{
    /// <summary>
    /// Бросаемый физический объект (game-design §7, §12): бочки, камни, декор тестовой сцены.
    /// Одна из двух реализаций <see cref="IGrabbable"/> (вторая — <c>VillagerController</c>, шаг 3) —
    /// берётся и швыряется <c>GodHandController</c>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThrowableObjectController : MonoBehaviour, IGrabbable
    {
        [Tooltip(
            "Порог приземления и прочие параметры броскового объекта — прямая ссылка на префабе " +
            "(не через DI): разные виды декора (бочка/камень) держат разные .asset без правки " +
            "Composition Root (шаг 3, вынесено из HandConfig — см. ThrowableConfig).")]
        [SerializeField] private ThrowableConfig _throwableConfig;

        private IEventBus _eventBus;
        private bool _initialized;

        private Rigidbody _body;
        private GrabbableId _id;

        // В4в (ревью шага 2): исходное значение useGravity запоминаем на момент захвата и
        // восстанавливаем при отпускании — раньше OnReleased безусловно ставил true, что тихо
        // "чинило" объекты, у которых гравитация была выключена по другой причине (не рукой).
        private bool _gravityBeforeGrab;

        // В1 (ревью шага 2): пока объект в руке, velocity-follow может разгонять его до
        // FollowMaxSpeed (до 15 м/с) — любое касание земли/соседнего куба во время переноса иначе
        // ложно триггерит ObjectLandedEvent. Приземление имеет смысл считать только после броска.
        private bool _isGrabbed;

        public GrabbableId Id => _id;
        public GrabbableKind Kind => GrabbableKind.Throwable;
        public Rigidbody Body => _body;

        // До завершения Construct() объект не готов к захвату (Body/Id ещё не проставлены) —
        // OnEnable вызывается раньше Construct (см. CLAUDE.md).
        public bool CanBeGrabbed => _initialized;

        [Inject]
        public void Construct(IEventBus eventBus)
        {
            _eventBus = eventBus;

            _body = GetComponent<Rigidbody>();
            _id = GrabbableId.FromInstanceId(gameObject.GetInstanceID());

            if (_throwableConfig == null)
            {
                Debug.LogWarning("[ThrowableObjectController] _throwableConfig не прошит — ObjectLandedEvent при приземлении публиковаться не будет.");
            }

            _initialized = true;
        }

        public void OnGrabbed()
        {
            _isGrabbed = true;
            _gravityBeforeGrab = _body.useGravity;

            // В руке — без гравитации, коллизии остаются (velocity-follow может задевать другие объекты).
            _body.useGravity = false;

            // В4б — гасим случайное вращение от контактов при захвате, иначе объект "крутится"
            // в ладони и после броска летит с неожиданным угловым моментом.
            _body.angularVelocity = Vector3.zero;

            // В4а — быстрый перенос/бросок (до FollowMaxSpeed/MaxThrowForce, порядка 15-20 м/с)
            // при дискретной детекции может "пробить" тонкий коллайдер за один физтик. Continuous
            // держим на весь захват+полёт, обратно на Discrete переключаемся при первом приземлении
            // (см. OnCollisionEnter) — риск актуален только пока объект в движении.
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void OnReleased(Vector3 throwVelocity)
        {
            _isGrabbed = false;
            _body.useGravity = _gravityBeforeGrab;
            _body.linearVelocity = throwVelocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_initialized) return;

            if (!_isGrabbed)
            {
                var impactSpeed = collision.relativeVelocity.magnitude;
                if (_throwableConfig != null && impactSpeed >= _throwableConfig.LandingImpactThreshold)
                {
                    // TODO шаг 4+: HitStructure — проверка тега структуры (дом и т.п.), когда структуры появятся в сцене.
                    _eventBus.Publish(new ObjectLandedEvent(_id, impactSpeed, false));
                }

                // В4а — после первого столкновения (приземление/удар) считаем полёт завершённым:
                // Continuous дороже по перфомансу, а риск "пробить" пол актуален только в полёте.
                _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
        }
    }
}
