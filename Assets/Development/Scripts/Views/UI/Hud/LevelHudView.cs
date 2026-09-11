using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// HUD уровня (game-design §10, §13, шаг 5) — Worship-бар, Alignment-индикатор, кнопка паузы,
    /// хост попапов Worship (<see cref="IWorshipPopupHost"/>). Только UI, без бизнес-логики
    /// (см. <c>LevelHudPresenter</c>).
    /// </summary>
    public sealed class LevelHudView : MonoBehaviour, ILevelHudView, IWorshipPopupHost
    {
        [Tooltip("Image.Type.Filled — fillAmount = current / target.")]
        [SerializeField] private Image _worshipFill;

        [Tooltip("Текстовый лейбл \"{0} / {1}\".")]
        [SerializeField] private TextMeshProUGUI _worshipLabel;

        [Tooltip("Ползунок Alignment — anchoredPosition.x = clamp(score/100, -1, 1) * track.rect.width/2.")]
        [SerializeField] private RectTransform _alignmentKnob;

        [SerializeField] private RectTransform _alignmentTrack;

        [Tooltip("Шаг 10 (волна 2, UI-тинт по alignment) — метки по краям трека Alignment.")]
        [SerializeField] private TextMeshProUGUI _evilLabel;
        [SerializeField] private TextMeshProUGUI _goodLabel;

        [SerializeField] private Button _pauseButton;

        [SerializeField] private WorshipPopupView _popupPrefab;
        [SerializeField] private RectTransform _popupRoot;

        public event Action PauseClicked;

        public WorshipPopupView PopupPrefab => _popupPrefab;
        public RectTransform PopupRoot => _popupRoot;

        private void Awake()
        {
            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPauseButtonClicked);
        }

        private void OnDestroy()
        {
            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPauseButtonClicked);
        }

        private void OnPauseButtonClicked() => PauseClicked?.Invoke();

        public void SetWorship(int current, int target)
        {
            if (_worshipFill != null)
            {
                _worshipFill.fillAmount = target > 0 ? Mathf.Clamp01((float)current / target) : 0f;
            }

            if (_worshipLabel != null)
            {
                _worshipLabel.text = string.Format("{0} / {1}", current, target);
            }
        }

        public void SetAlignment(float normalized)
        {
            if (_alignmentKnob == null || _alignmentTrack == null) return;

            // Шаг 5 (аудит п.12): нормализацию −1..+1 делает презентер через IAlignmentService.Min/MaxScore —
            // здесь только клампим (страховка от float-погрешности) и раскладываем по ширине трека.
            var halfWidth = _alignmentTrack.rect.width / 2f;
            var x = Mathf.Clamp(normalized, -1f, 1f) * halfWidth;

            var pos = _alignmentKnob.anchoredPosition;
            pos.x = x;
            _alignmentKnob.anchoredPosition = pos;
        }

        /// <summary>
        /// Тинт по alignment-стейджу (отложка шага 6, шаг 10 волна 2) — fill шкалы Worship + метки
        /// Evil/Good. Null-гарды: сцена может не прошить какое-то из полей (например, старая
        /// собранная версия HUD без меток) — тинт применяется к тому, что прошито.
        /// </summary>
        public void SetAlignmentTint(Color color)
        {
            if (_worshipFill != null) _worshipFill.color = color;
            if (_evilLabel != null) _evilLabel.color = color;
            if (_goodLabel != null) _goodLabel.color = color;
        }
    }
}
