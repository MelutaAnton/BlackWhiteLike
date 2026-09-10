using BlackWhiteLike.Core.Input;
using BlackWhiteLike.Core.Interaction;
using BlackWhiteLike.Data.Configs.Camera;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Hand;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Camera
{
    /// <summary>
    /// Pan/zoom изометрической камеры (game-design §3, §11). Только Unity API — правила уже
    /// зафиксированы в <see cref="IsometricCameraConfig"/>, здесь только их применение.
    /// <para>
    /// Pan поддержан двумя путями (ревью шага 2, В14 — решение владельца):
    /// основной — drag по пустому месту (пальцем/ЛКМ), паритет между тачем и PC-отладкой; и
    /// альтернативный — <see cref="IInputProvider.Pan"/> (ПКМ-drag на PC), как и обещано §3.
    /// Оба используют один и тот же raycast-механизм 1:1 с миром через <see cref="IPointerWorldProjector"/>
    /// (не "дельта экрана × скорость").
    /// </para>
    /// <para>
    /// Конфликт с захватом решается по первому касанию (game-design §3): если на PointerDown под
    /// pointer'ом есть <see cref="IGrabbable"/> (маска <see cref="_grabbableMask"/>), drag-pan в этот
    /// жест не включается — объектом займётся <c>GodHandController</c>. Оба пути pan'а, кроме того,
    /// не работают, пока рука что-то держит (<see cref="IHandInteractionService.IsHolding"/>,
    /// ревью шага 2, Б1+Б3) — иначе камера "уезжает" вместе с захваченным объектом.
    /// </para>
    /// <para>
    /// Zoom — дистанция dolly вдоль локальной оси Z камеры-ребёнка pivot'а, в пределах
    /// MinZoom..MaxZoom (владелец: перспективная камера, зум не через FOV).
    /// </para>
    /// </summary>
    /// <remarks>
    /// Внутри этого файла (namespace заканчивается на "Camera") <c>UnityEngine.Camera</c> всегда
    /// квалифицируется полностью, а не через bare "Camera" (CS0118, см. CLAUDE.md, п.3 чек-листа).
    /// </remarks>
    public sealed class IsometricPanZoomBehaviour : MonoBehaviour, ICameraBehaviour
    {
        [Tooltip("Pivot-трансформ рига — по нему двигается pan (мировая плоскость XZ).")]
        [SerializeField] private Transform _pivot;

        [Tooltip("Трансформ Cinemachine-камеры (ребёнок pivot'а) — по его локальной Z меняется зум (dolly).")]
        [SerializeField] private Transform _zoomTransform;

        [Tooltip("Слой земли для проекции плоскости pan'а/зума (fallback внутри IPointerWorldProjector — плоскость y=0).")]
        [SerializeField] private LayerMask _groundMask;

        [Tooltip("Слой IGrabbable-объектов — если под pointer'ом на PointerDown есть объект этого слоя, drag-pan не начинается.")]
        [SerializeField] private LayerMask _grabbableMask;

        // Порог аномалии pan-дельты (ревью шага 2, В5) — не прямой множитель обычной дельты (иначе
        // клампится штатный флик), а множитель над "базовой" PanSpeed из конфига: типичный жест
        // может быть в разы быстрее номинала, а вот на порядок быстрее — уже похоже на сбой
        // raycast'а (просадка FPS, скачок точки), не на жест пользователя.
        private const float PanAnomalyMultiplier = 10f;

        private IInputProvider _inputProvider;
        private IInputRouter _inputRouter;
        private IsometricCameraConfig _config;
        private IHandInteractionService _handInteractionService;
        private IPointerWorldProjector _pointerWorldProjector;
        private IHandModeService _handModeService;
        private bool _initialized;
        private bool _isActive;

        // Состояние drag-pan'а (основной путь, паритет с тачем): мировая точка под pointer'ом на
        // момент начала жеста. Двигаем pivot так, чтобы эта точка снова оказалась под pointer'ом.
        private bool _dragPanEngaged;
        private Vector3 _dragPanWorldAnchor;

        // Состояние RMB-pan'а (альтернативный путь, В14): у IInputProvider.Pan нет отдельного
        // Down-события — границей служит переход "было 0 → стало не 0".
        private bool _rmbPanEngaged;
        private Vector3 _rmbPanWorldAnchor;

        private float _currentZoomDistance;

        public bool IsActive => _isActive;

        [Inject]
        public void Construct(
            IInputProvider inputProvider,
            IInputRouter inputRouter,
            IsometricCameraConfig config,
            IHandInteractionService handInteractionService,
            IPointerWorldProjector pointerWorldProjector,
            IHandModeService handModeService)
        {
            _inputProvider = inputProvider;
            _inputRouter = inputRouter;
            _config = config;
            _handInteractionService = handInteractionService;
            _pointerWorldProjector = pointerWorldProjector;
            _handModeService = handModeService;
            _currentZoomDistance = config.DefaultZoomDistance;
            ApplyZoomDistance();

            _initialized = true;

            // TODO шаг 6: активацию/деактивацию возьмёт на себя CameraService по Priority виртуальных
            // камер Cinemachine (game-design §11), когда поведений камеры станет 2+. Пока это
            // единственное поведение — активно сразу после инициализации.
            _isActive = true;
        }

        public void Activate() => _isActive = true;

        public void Deactivate() => _isActive = false;

        private void Update()
        {
            if (!_initialized || !_isActive) return;
            if (_pivot == null) return;

            if (!_inputRouter.IsGameplay)
            {
                // Шаг 5: контекст ушёл из Gameplay (пауза/меню) — гасим оба пути pan'а, чтобы после
                // возврата жест не продолжился рывком от устаревшего anchor'а (тот же приём, что
                // и для IHandInteractionService.IsHolding в HandlePan).
                _dragPanEngaged = false;
                _rmbPanEngaged = false;
                return;
            }

            HandlePan();
            HandleZoom();
        }

        private void HandlePan()
        {
            if (_handInteractionService.IsHolding)
            {
                // Б1+Б3 (ревью шага 2): рука что-то держит — камера не панит, иначе она "уезжает"
                // вместе с захваченным объектом. Гасим состояние обоих путей, чтобы после отпускания
                // жест не продолжился рывком от устаревшего anchor'а.
                _dragPanEngaged = false;
                _rmbPanEngaged = false;
                return;
            }

            HandleDragPan();
            HandleRmbPan();
            ClampPanPosition();
        }

        private void HandleDragPan()
        {
            if (_inputProvider.PointerDown)
            {
                // Шаг 4: в режиме Miracle тап по земле — это каст (game-design §3), не pan; тап по
                // HUD тоже не должен утаскивать камеру. Оба условия проверяем ДО TryPickGrabbable —
                // раньше выхода незачем гонять лишний raycast.
                if (_handModeService.Mode != HandMode.Grab || _inputProvider.IsPointerOverUi)
                {
                    _dragPanEngaged = false;
                }
                else
                {
                    // Первое касание решает конфликт с захватом (game-design §3): под pointer'ом есть
                    // IGrabbable — drag-pan в этом жесте не участвует. Единый критерий с рукой —
                    // TryPickGrabbable того же IPointerWorldProjector, что использует GodHandController.
                    var grabbableUnderPointer = _pointerWorldProjector.TryPickGrabbable(
                        _inputProvider.PointerPosition, _grabbableMask, out _);
                    _dragPanEngaged = !grabbableUnderPointer &&
                                       _pointerWorldProjector.TryProjectToGround(_inputProvider.PointerPosition, _groundMask, out _dragPanWorldAnchor);
                }
            }

            if (_dragPanEngaged && _inputProvider.IsPointerHeld)
            {
                if (_pointerWorldProjector.TryProjectToGround(_inputProvider.PointerPosition, _groundMask, out var currentWorldPoint))
                {
                    ApplyClampedPanDelta(_dragPanWorldAnchor - currentWorldPoint);
                }
            }

            if (_inputProvider.PointerUp)
            {
                _dragPanEngaged = false;
            }
        }

        private void HandleRmbPan()
        {
            var panActive = _inputProvider.Pan.sqrMagnitude > 0f;

            // Пока идёт жест основного пальца/ЛКМ (IsPointerHeld), этот путь выключен полностью —
            // жестом владеет HandleDragPan со всеми его гейтами (режим руки, HUD, IGrabbable под
            // pointer'ом). Провайдер пишет Pan на любом одиночном тач-свайпе
            // (PointerInputProvider.UpdateTouchGestures), и до этого гейта на мобиле во время
            // свайпа работали ОБА якорных пути с разными якорями: две коррекции за кадр по ещё не
            // сдвинутой камере (CinemachineBrain двигает её в LateUpdate) давали незатухающее
            // перепрыгивание pivot'а через цель — тряску камеры каждый кадр (playtest шага 9).
            // Прежний узкий гейт (Miracle-режим / HUD, ревью шага 4) этим покрыт как частный
            // случай. На PC IsPointerHeld относится к основной кнопке, а Pan приходит только от
            // RMB — чистый RMB-drag работает как раньше.
            var blockedByPrimaryGesture = _inputProvider.IsPointerHeld;

            if (panActive && !_rmbPanEngaged && !blockedByPrimaryGesture)
            {
                _rmbPanEngaged = _pointerWorldProjector.TryProjectToGround(
                    _inputProvider.PointerPosition, _groundMask, out _rmbPanWorldAnchor);
            }
            else if (!panActive || blockedByPrimaryGesture)
            {
                // blockedByPrimaryGesture сбрасывает и уже взведённый pan — начало касания посреди
                // RMB-жеста не должно оставлять его висеть до следующего кадра.
                _rmbPanEngaged = false;
            }

            if (_rmbPanEngaged &&
                _pointerWorldProjector.TryProjectToGround(_inputProvider.PointerPosition, _groundMask, out var currentWorldPoint))
            {
                ApplyClampedPanDelta(_rmbPanWorldAnchor - currentWorldPoint);
            }
        }

        private void ApplyClampedPanDelta(Vector3 worldDelta)
        {
            // В5 (ревью шага 2): раньше кламп считался как PanSpeed * unscaledDeltaTime — при
            // PanSpeed=30 это ~0.5 мира за кадр на 60 fps, а штатный флик легко превышает 1.5-2
            // мира за кадр. Кламп срабатывал постоянно и ломал обещанный 1:1. Порог аномалии не
            // зависит от deltaTime — это разовый скачок raycast'а, а не "скорость".
            var maxDelta = _config.PanSpeed * PanAnomalyMultiplier;
            if (worldDelta.magnitude > maxDelta)
            {
                Debug.LogWarning(
                    $"[IsometricPanZoomBehaviour] Аномальная pan-дельта {worldDelta.magnitude:F1} за кадр — " +
                    $"кламплю до {maxDelta:F1}. Возможна просадка FPS или скачок raycast'а.");
                worldDelta = worldDelta.normalized * maxDelta;
            }

            _pivot.position += worldDelta;
        }

        private void ClampPanPosition()
        {
            var half = _config.PanBoundsHalfExtent;
            var position = _pivot.position;
            position.x = Mathf.Clamp(position.x, -half, half);
            position.z = Mathf.Clamp(position.z, -half, half);
            _pivot.position = position;
        }

        private void HandleZoom()
        {
            var zoomDelta = _inputProvider.Zoom;
            if (Mathf.Approximately(zoomDelta, 0f)) return;

            _currentZoomDistance = Mathf.Clamp(
                _currentZoomDistance - zoomDelta * _config.ZoomSpeed,
                _config.MinZoom,
                _config.MaxZoom);

            ApplyZoomDistance();
        }

        private void ApplyZoomDistance()
        {
            if (_zoomTransform == null) return;

            var localPosition = _zoomTransform.localPosition;
            localPosition.z = -_currentZoomDistance;
            _zoomTransform.localPosition = localPosition;
        }
    }
}
