using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.Input;
using BlackWhiteLike.Core.Interaction;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Hand;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Hand
{
    /// <summary>
    /// Рука бога (game-design §4, §12) — только Unity API: позиция руки и выбор захватываемого
    /// объекта идут через <see cref="IPointerWorldProjector"/> (единственный источник ответа
    /// "что под pointer'ом", общий с камерой — ревью шага 2, Б1+Б3), физический захват
    /// (velocity-follow, Open Question #1 решён без joint). Правила захвата и расчёт импульса
    /// броска — в <see cref="IHandInteractionService"/>.
    /// </summary>
    public sealed class GodHandController : MonoBehaviour
    {
        [Tooltip("Корень визуала руки — двигается за pointer'ом на CarryHeight над землёй.")]
        [SerializeField] private Transform _visualRoot;

        [Tooltip("Точка в ладони — цель velocity-follow для захваченного объекта.")]
        [SerializeField] private Transform _grabAnchor;

        [Tooltip("Animator руки — параметры см. AnimatorHashes (Grab/Release/Place/IsCarrying).")]
        [SerializeField] private Animator _animator;

        [Tooltip("Слой земли для проекции позиции руки (fallback внутри IPointerWorldProjector — плоскость y=0).")]
        [SerializeField] private LayerMask _groundMask;

        [Tooltip("Слой IGrabbable-объектов для поиска захвата через IPointerWorldProjector.TryPickGrabbable.")]
        [SerializeField] private LayerMask _grabbableMask;

        private IInputProvider _inputProvider;
        private IHandInteractionService _handInteractionService;
        private IEventBus _eventBus;
        private HandConfig _handConfig;
        private IPointerWorldProjector _pointerWorldProjector;
        private bool _initialized;

        // Буфер одноразового ввода до ближайшего физтика (см. .claude/rules/physics-input.md):
        // PointerDown/Up истинны ровно один render-кадр, а между двумя render-кадрами на высоком
        // FPS может пройти ноль FixedUpdate — без буферизации нажатие/отпускание сгорает.
        private bool _pointerDownQueued;
        private bool _pointerUpQueued;

        // НБ1 (ревью шага 2): Time.unscaledTime внутри FixedUpdate — это фактически render-время
        // последнего Update, а не физического тика; семплы истории тоже пишутся render-часами
        // (Update). Читаем время один раз в Update и переиспользуем в FixedUpdate — единый источник
        // времени на кадр вместо смешивания разных часов.
        private float _lastUnscaledTime;

        private IGrabbable _grabbedObject;

        [Inject]
        public void Construct(
            IInputProvider inputProvider,
            IHandInteractionService handInteractionService,
            IEventBus eventBus,
            HandConfig handConfig,
            IPointerWorldProjector pointerWorldProjector)
        {
            _inputProvider = inputProvider;
            _handInteractionService = handInteractionService;
            _eventBus = eventBus;
            _handConfig = handConfig;
            _pointerWorldProjector = pointerWorldProjector;

            // В11: _grabAnchor используется без null-проверки в каждом FixedUpdate (в отличие от
            // _visualRoot, который per-frame переживает отсутствие ссылки) — без него первое же
            // обращение даёт NRE. Валидируем обязательную ссылку один раз здесь.
            if (_grabAnchor == null)
            {
                Debug.LogError("[GodHandController] _grabAnchor не прошит — рука не будет работать.");
                return;
            }

            _initialized = true;
        }

        private void OnDisable()
        {
            // В12: буферы не должны переживать выключение/повторное включение (иначе "старое"
            // нажатие сработает после следующей активации), а удерживаемый объект не должен
            // остаться навсегда без гравитации при выключении руки/выгрузке сцены.
            _pointerDownQueued = false;
            _pointerUpQueued = false;

            if (_grabbedObject != null)
            {
                ReleaseGrabbedObject(Vector3.zero, publishEvent: false);
            }
        }

        private void Update()
        {
            if (!_initialized) return;

            _lastUnscaledTime = Time.unscaledTime;

            UpdateHandPosition();

            if (_inputProvider.PointerDown) _pointerDownQueued = true;
            if (_inputProvider.PointerUp) _pointerUpQueued = true;

            if (_grabbedObject != null)
            {
                _handInteractionService.RecordSample(_grabAnchor.position, _lastUnscaledTime);
            }
        }

        private void FixedUpdate()
        {
            if (!_initialized) return;

            var pointerDown = _pointerDownQueued;
            var pointerUp = _pointerUpQueued;
            // Сброс сразу, до ранних return — иначе "старое" нажатие прожжёт легальное действие позже.
            _pointerDownQueued = false;
            _pointerUpQueued = false;

            if (_grabbedObject == null)
            {
                // Б2 (ревью шага 2): раньше здесь стоял безусловный return после TryGrab(), из-за
                // чего pointerUp, попавший в тот же физтик, что и pointerDown (быстрый тап),
                // терялся — куб залипал в руке без гравитации. Теперь после успешного захвата тик
                // продолжается и Up того же тика обрабатывается ниже.
                if (!pointerDown) return;

                TryGrab();
                if (_grabbedObject == null) return; // не нашли что взять в этот тик
            }

            // В3: _grabbedObject — интерфейсная ссылка, перегруженный Unity "==" на неё не действует,
            // поэтому уничтоженный объект (например, житель ушёл в пул на шаге 3) не поймать через
            // "_grabbedObject == null". Body — настоящий Unity-объект, его "==" уже безопасен.
            if (_grabbedObject.Body == null)
            {
                ReleaseGrabbedObject(Vector3.zero, publishEvent: false);
                return;
            }

            // Удержание — velocity-follow к точке в ладони (Open Question #1: без joint).
            var followVelocity = _handInteractionService.CalculateFollowVelocity(
                _grabbedObject.Body.position, _grabAnchor.position);
            _grabbedObject.Body.linearVelocity = followVelocity;

            if (pointerUp)
            {
                var throwVelocity = _handInteractionService.CalculateThrowVelocity(_lastUnscaledTime);
                ReleaseGrabbedObject(throwVelocity, publishEvent: true);
            }
        }

        private void UpdateHandPosition()
        {
            if (_visualRoot == null) return;

            if (!_pointerWorldProjector.TryProjectToGround(_inputProvider.PointerPosition, _groundMask, out var groundPoint)) return;

            _visualRoot.position = groundPoint + Vector3.up * _handConfig.CarryHeight;
        }

        private void TryGrab()
        {
            if (!_pointerWorldProjector.TryPickGrabbable(_inputProvider.PointerPosition, _grabbableMask, out var grabbable)) return;
            if (!_handInteractionService.CanGrab(grabbable)) return;

            GrabObject(grabbable);
        }

        private void GrabObject(IGrabbable grabbable)
        {
            _grabbedObject = grabbable;
            grabbable.OnGrabbed();
            _handInteractionService.BeginHold(_grabAnchor.position, _lastUnscaledTime);

            if (_animator != null)
            {
                _animator.SetTrigger(AnimatorHashes.Grab);
                _animator.SetBool(AnimatorHashes.IsCarrying, true);
            }

            _eventBus.Publish(new ObjectGrabbedEvent(grabbable.Id, grabbable.Kind));
        }

        /// <summary>
        /// В12: единственный путь отпускания объекта — и штатный (pointerUp), и защитный
        /// (OnDisable, уничтоженный Body), чтобы им не разъехаться в будущем. <paramref name="grabbable"/>.Body
        /// может быть уже уничтожен (В3) — тогда OnReleased на нём не вызывается вовсе.
        /// </summary>
        private void ReleaseGrabbedObject(Vector3 throwVelocity, bool publishEvent)
        {
            var grabbable = _grabbedObject;
            _grabbedObject = null;

            if (grabbable.Body != null)
            {
                grabbable.OnReleased(throwVelocity);
            }

            _handInteractionService.EndHold();

            if (_animator != null)
            {
                _animator.SetBool(AnimatorHashes.IsCarrying, false);
                // В9: порог "мягкой постановки" — решение сервиса (HandConfig.SoftPlacementSpeedThreshold),
                // не сравнение с точным нулём в контроллере.
                var isSoftPlacement = _handInteractionService.IsSoftPlacement(throwVelocity);
                _animator.SetTrigger(isSoftPlacement ? AnimatorHashes.Place : AnimatorHashes.Release);
            }

            if (publishEvent)
            {
                _eventBus.Publish(new ObjectThrownEvent(grabbable.Id, throwVelocity));
            }
        }
    }
}
