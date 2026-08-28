using BlackWhiteLike.Core.Input;
using BlackWhiteLike.Core.Interaction;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Hand;
using BlackWhiteLike.Services.Miracles;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Miracles
{
    /// <summary>
    /// Превью зоны действия чуда и трансляция ввода в сессию <see cref="IMiracleService"/>
    /// (game-design §4). Unity API only — правила накачки/каста живут в сервисе, здесь только
    /// проекция pointer'а на землю (через <see cref="IPointerWorldProjector"/>, единый источник
    /// ответа "что под pointer'ом") и позиционирование кольца-превью.
    /// </summary>
    /// <remarks>
    /// Вся логика — в <see cref="Update"/>, без FixedUpdate: каст не взаимодействует с физикой
    /// напрямую (эффект применяется подписчиками MiracleCastEvent), поэтому буферизация
    /// Update→FixedUpdate (.claude/rules/physics-input.md) здесь не нужна — one-shot PointerDown/Up
    /// потребляются в том же Update, где и читаются.
    /// </remarks>
    public sealed class MiracleTargetingController : MonoBehaviour
    {
        [Tooltip("Кольцо-превью зоны действия — масштабируется как localScale = (diameter, 1, diameter); ассет собран так, что при scale=1 диаметр равен 1м.")]
        [SerializeField] private Transform _previewRoot;

        [Tooltip("Слой земли для проекции точки под pointer'ом (fallback внутри IPointerWorldProjector — плоскость y=0).")]
        [SerializeField] private LayerMask _groundMask;

        private IInputProvider _inputProvider;
        private IHandModeService _handModeService;
        private IMiracleService _miracleService;
        private IPointerWorldProjector _pointerWorldProjector;
        private MiracleCatalogConfig _catalog;
        private bool _initialized;

        // Последняя валидная точка проекции на землю — нужна, чтобы UpdateCast/BeginCast могли
        // работать и в кадре, где TryProjectToGround промахнулся (pointer выше горизонта), пока
        // сессия каста уже открыта (Блокер 1, часть б — раньше здесь стоял ранний return).
        private Vector3 _lastValidPoint;

        [Inject]
        public void Construct(
            IInputProvider inputProvider,
            IHandModeService handModeService,
            IMiracleService miracleService,
            IPointerWorldProjector pointerWorldProjector,
            MiracleCatalogConfig catalog)
        {
            _inputProvider = inputProvider;
            _handModeService = handModeService;
            _miracleService = miracleService;
            _pointerWorldProjector = pointerWorldProjector;
            _catalog = catalog;

            _initialized = true;
        }

        private void OnDisable()
        {
            if (_initialized && _miracleService.IsCasting)
            {
                _miracleService.CancelCast();
            }

            SetPreviewActive(false);
        }

        private void Update()
        {
            if (!_initialized) return;

            if (_handModeService.Mode != HandMode.Miracle || !_handModeService.TryGetSelectedMiracle(out var type))
            {
                if (_miracleService.IsCasting) _miracleService.CancelCast();
                SetPreviewActive(false);
                return;
            }

            // Блокер 1 (ревью шага 4, .claude/rules/physics-input.md — "one-shot в одном кадре с
            // Up"): PointerDown и PointerUp нового Input System не взаимоисключающие в пределах
            // одного render-кадра (быстрый тап, просадка FPS могут дать оба true разом). Раньше
            // else-if-цепочка при PointerDown всегда выполняла только BeginCast и PointerUp этого
            // же кадра терялся безвозвратно (one-shot не повторится на следующем кадре) — сессия
            // оставалась открытой навсегда, а все дальнейшие BeginCast отклонялись сервисом.
            // Обрабатываем Up здесь безусловно и первым, до проекции на землю — если сессия была
            // открыта на предыдущем кадре и в этом пришёл Up, она закрывается сразу же.
            if (_miracleService.IsCasting && _inputProvider.PointerUp)
            {
                _miracleService.ReleaseCast();
            }

            var hasPoint = _pointerWorldProjector.TryProjectToGround(_inputProvider.PointerPosition, _groundMask, out var point);
            if (hasPoint)
            {
                _lastValidPoint = point;
                UpdatePreview(type, point);
            }
            else
            {
                // Блокер 1, часть б: раньше промах проекции был ранним return ДО обработки Up —
                // тем же способом навсегда подвешивал сессию (Up этого кадра терялся). Up уже
                // обработан выше; здесь только гасим превью — точку сессии не трогаем, накачка
                // (см. ветку ниже) продолжает тикать стоимость hold по последней валидной точке.
                SetPreviewActive(false);
            }

            // If/else-if, а не независимые if: в кадре, где PointerDown стал true, IsPointerHeld
            // тоже уже true (то же нажатие) — без ветвления UpdateCast списал бы HoldCostPerSecond
            // за этот же кадр поверх только что открытой сессии (holdTime должен стартовать с 0).
            // BeginCast требует hasPoint — без валидной точки на земле начинать сессию нечем.
            if (hasPoint && _inputProvider.PointerDown && !_inputProvider.IsPointerOverUi && !_miracleService.IsCasting)
            {
                _miracleService.BeginCast(type, point);
            }
            else if (_miracleService.IsCasting && _inputProvider.IsPointerHeld)
            {
                _miracleService.UpdateCast(Time.deltaTime, _lastValidPoint);
            }
            else if (_miracleService.IsCasting)
            {
                // Страховка (Блокер 1): сессия жива, а IsPointerHeld уже false, хотя Up в этом
                // кадре не пришёл (например, тот самый "Down и Up в одном кадре" случай — Up был
                // обработан ДО BeginCast этого же кадра, пока IsCasting ещё было false, и не мог
                // закрыть только что открытую сессию). Закрываем с задержкой в один кадр вместо
                // того, чтобы сессия повисла до следующего тапа.
                _miracleService.ReleaseCast();
            }
        }

        private void UpdatePreview(MiracleType type, Vector3 point)
        {
            float radius;
            if (_miracleService.IsCasting)
            {
                radius = _miracleService.CurrentRadius;
            }
            else if (_catalog.TryGet(type, out var config))
            {
                radius = config.BaseRadius;
            }
            else
            {
                // Выбран тип без конфига (не должно происходить — панель не даёт выбрать
                // недоступное чудо, MiraclePanelPresenter.SetAvailable(false)), но не рисуем превью впустую.
                SetPreviewActive(false);
                return;
            }

            SetPreviewActive(true);

            if (_previewRoot == null) return;

            _previewRoot.position = point;
            var diameter = radius * 2f;
            _previewRoot.localScale = new Vector3(diameter, 1f, diameter);
        }

        private void SetPreviewActive(bool active)
        {
            if (_previewRoot != null) _previewRoot.gameObject.SetActive(active);
        }
    }
}
