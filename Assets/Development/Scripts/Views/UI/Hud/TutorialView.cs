using BlackWhiteLike.Models;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// UI онбординг-туториала уровня (шаг 10 «Polish») — плашка цели с прогрессом, screen-space
    /// указатель на мировую цель/UI-элемент, подсветка кнопки чуда. Только UI, без бизнес-логики
    /// (см. <c>TutorialPresenter</c>). Без интерфейса — презентер зависит от конкретного класса
    /// (контракт задачи), тот же приём, что <c>WorshipPopupFactory</c> зависит от конкретного
    /// <c>WorshipPopupView</c>.
    /// </summary>
    public sealed class TutorialView : MonoBehaviour
    {
        [Header("Плашка цели")]
        [SerializeField] private GameObject _objectivePanel;
        [SerializeField] private TMP_Text _objectiveText;
        [SerializeField] private TMP_Text _objectiveProgressText;

        [Header("Указатель")]
        [SerializeField] private RectTransform _pointer;

        // Playtest-фикс шага 10 (2026-09-11): поправка на разворот спрайта стрелки по умолчанию.
        // TutorialPointerRules отдаёт угол в математической системе (0° — вправо); прошитый
        // спрайт 03_Arrow_bot смотрит ВНИЗ (вектор (0,-1) = -90°), поэтому поправка +90°
        // (см. TutorialPointerRules.ToSpriteRotation и тесты на композицию). Значение — derived
        // от выбранного спрайта: штампуется сборкой (BuildTutorialPointer) рядом с image.sprite,
        // ручные правки в инспекторе перетираются прогоном меню осознанно.
        [SerializeField] private float _pointerRotationOffsetDeg = 90f;

        [Header("Цели-кнопки чудес (для указателя/подсветки)")]
        [SerializeField] private RectTransform _rainButtonTarget;
        [SerializeField] private RectTransform _healButtonTarget;
        [SerializeField] private RectTransform _fireballButtonTarget;
        [SerializeField] private RectTransform _lightningButtonTarget;

        [SerializeField] private RectTransform _worshipBarTarget;

        private Canvas _canvas;
        private RectTransform _pointerParent;

        /// <summary>Цель указателя "шкала Worship" — публичный доступ нужен презентеру (ShowPointerOverRect принимает произвольный RectTransform).</summary>
        public RectTransform WorshipBarTarget => _worshipBarTarget;

        private void Awake()
        {
            if (_pointer != null)
            {
                _canvas = _pointer.GetComponentInParent<Canvas>();
                _pointerParent = _pointer.parent as RectTransform;
            }
        }

        /// <summary>
        /// ВАЖНО 5 (ревью шага 10): пульс <see cref="SetMiracleHighlight"/> — DOTween-твин НА
        /// RectTransform кнопки чуда, не на этом GameObject; выключение View (сцена
        /// перезагружается/канвас гасится) само по себе твин не останавливает. Гасим все четыре
        /// возможные цели безусловно — дешевле, чем помнить, какая была подсвечена последней.
        /// </summary>
        private void OnDisable()
        {
            KillHighlightTween(_rainButtonTarget);
            KillHighlightTween(_healButtonTarget);
            KillHighlightTween(_fireballButtonTarget);
            KillHighlightTween(_lightningButtonTarget);
        }

        private static void KillHighlightTween(RectTransform target)
        {
            if (target == null) return;

            target.DOKill();
            target.localScale = Vector3.one;
        }

        public void SetObjective(string text, string progress)
        {
            if (_objectivePanel != null) _objectivePanel.SetActive(true);
            if (_objectiveText != null) _objectiveText.text = text;

            if (_objectiveProgressText != null)
            {
                var hasProgress = !string.IsNullOrEmpty(progress);
                _objectiveProgressText.gameObject.SetActive(hasProgress);
                _objectiveProgressText.text = hasProgress ? progress : string.Empty;
            }
        }

        public void Hide()
        {
            if (_objectivePanel != null) _objectivePanel.SetActive(false);
            HidePointer();
        }

        /// <summary>
        /// Проекция мировой точки, уже переведённой в screen-space (Camera.ViewportToScreenPoint по
        /// результату TutorialPointerRules.ComputeIndicator), на overlay-канвас указателя.
        /// </summary>
        /// <param name="screenPoint">Screen-space позиция указателя (уже прижата к краю экрана презентером, если цель вне кадра).</param>
        /// <param name="angleDeg">Угол разворота из TutorialPointerRules (математическая система, 0° — вправо); имеет смысл только при <paramref name="isClamped"/> == true.</param>
        /// <param name="isClamped">true — цель вне экрана/за камерой, стрелка разворачивается в её сторону; false — прежний вид, без разворота (висит над целью).</param>
        public void ShowPointerAtScreenPoint(Vector2 screenPoint, float angleDeg, bool isClamped)
        {
            if (_pointer == null) return;

            // Без родителя позицию не пересчитать — не включаем указатель в устаревшей точке
            // (ревью: SetActive(true) при непозиционированном указателе хуже, чем его отсутствие).
            if (_pointerParent == null) return;

            // Screen Space - Camera/World: конвертация через worldCamera канваса; Overlay — null (RectTransformUtility это учитывает).
            var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_pointerParent, screenPoint, camera, out var localPoint))
            {
                _pointer.anchoredPosition = localPoint;
            }

            // Playtest-фикс шага 10: цель за пределами экрана — стрелка разворачивается в её сторону;
            // на экране — прежний базовый вид (без поворота, просто висит над целью).
            // Композиция угла и поправки спрайта — в TutorialPointerRules (покрыта тестами).
            _pointer.localEulerAngles = new Vector3(0f, 0f,
                isClamped ? Core.UI.TutorialPointerRules.ToSpriteRotation(angleDeg, _pointerRotationOffsetDeg) : 0f);

            _pointer.gameObject.SetActive(true);
        }

        /// <summary>Указатель поверх UI-элемента (тот же канвас) — прямое совмещение world position RectTransform'ов, без разворота.</summary>
        public void ShowPointerOverRect(RectTransform target)
        {
            if (_pointer == null || target == null) return;

            _pointer.position = target.position;

            // Указатель переиспользуется между мировой целью (может быть развёрнут предыдущим
            // кадром) и UI-целями — сбрасываем поворот явно, иначе после off-screen мировой цели
            // кнопка чуда получит наследованный разворот стрелки.
            _pointer.localEulerAngles = Vector3.zero;

            _pointer.gameObject.SetActive(true);
        }

        public void HidePointer()
        {
            if (_pointer != null) _pointer.gameObject.SetActive(false);
        }

        /// <summary>Пульс scale DOTween на кнопке чуда. Kill предыдущего твина — повторный SetMiracleHighlight(true) не плодит твины на одном RectTransform.</summary>
        public void SetMiracleHighlight(MiracleType miracle, bool on)
        {
            var target = GetMiracleButtonTarget(miracle);
            if (target == null) return;

            target.DOKill();
            target.localScale = Vector3.one;

            if (on)
            {
                target.DOScale(1.15f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
        }

        public RectTransform GetMiracleButtonTarget(MiracleType miracle)
        {
            switch (miracle)
            {
                case MiracleType.Rain: return _rainButtonTarget;
                case MiracleType.Heal: return _healButtonTarget;
                case MiracleType.Fireball: return _fireballButtonTarget;
                case MiracleType.Lightning: return _lightningButtonTarget;
                default: return null;
            }
        }
    }
}
