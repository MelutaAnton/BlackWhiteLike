using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.Interaction;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Models;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Village
{
    /// <summary>
    /// Бросаемый физический объект (game-design §7, §12): бочки, камни, декор тестовой сцены.
    /// Единственная на шаге 2 реализация <see cref="IGrabbable"/> — берётся и швыряется <c>GodHandController</c>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThrowableObjectController : MonoBehaviour, IGrabbable
    {
        private IEventBus _eventBus;
        private HandConfig _handConfig;
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
        public void Construct(IEventBus eventBus, HandConfig handConfig)
        {
            _eventBus = eventBus;
            _handConfig = handConfig;

            _body = GetComponent<Rigidbody>();
            _id = GrabbableId.FromInstanceId(gameObject.GetInstanceID());

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
                if (impactSpeed >= _handConfig.LandingImpactThreshold)
                {
                    // TODO шаг 3: HitStructure — проверка тега структуры (дом и т.п.), когда структуры появятся в сцене.
                    _eventBus.Publish(new ObjectLandedEvent(_id, impactSpeed, false));
                }

                // В4а — после первого столкновения (приземление/удар) считаем полёт завершённым:
                // Continuous дороже по перфомансу, а риск "пробить" пол актуален только в полёте.
                _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
        }
    }
}
