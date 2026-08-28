using System;
using BlackWhiteLike.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// Одна кнопка панели чудес (game-design §10) — визуальный компонент, маршрутизацию по типу
    /// чуда делает <c>MiraclePanelView</c>. Только UI, никакой бизнес-логики (CLAUDE.md: Views —
    /// только для UI).
    /// </summary>
    public sealed class MiracleButtonView : MonoBehaviour
    {
        [Tooltip("Множитель альфы CanvasGroup, когда Faith не хватает на BaseFaithCost.")]
        [SerializeField] private float _unaffordableAlpha = 0.4f;

        [Tooltip("Множитель альфы CanvasGroup, когда чудо недоступно в этом каталоге (Heal/Lightning на шаге 4).")]
        [SerializeField] private float _unavailableAlpha = 0.25f;

        [SerializeField] private MiracleType _type;
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _costLabel;
        [SerializeField] private GameObject _selectedFrame;
        [SerializeField] private CanvasGroup _canvasGroup;

        public MiracleType Type => _type;

        public event Action<MiracleType> Clicked;

        // Незначительное (ревью шага 4): SetAvailable(true) раньше не трогал alpha вовсе — если
        // до этого SetAvailable(false) выставил _unavailableAlpha, кнопка навсегда оставалась
        // притушенной, даже когда чудо снова доступно. Оба флага пересчитываются одним ApplyAlpha,
        // чтобы порядок вызовов SetAvailable/SetAffordable не имел значения.
        private bool _isAvailable = true;
        private bool _isAffordable = true;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClicked);
        }

        private void OnClicked() => Clicked?.Invoke(_type);

        public void SetSelected(bool selected)
        {
            if (_selectedFrame != null) _selectedFrame.SetActive(selected);
        }

        public void SetAffordable(bool affordable)
        {
            _isAffordable = affordable;
            ApplyAlpha();
        }

        public void SetAvailable(bool available)
        {
            _isAvailable = available;
            if (_button != null) _button.interactable = available;
            ApplyAlpha();
        }

        /// <summary>Приоритет: недоступность (каталог) перекрывает нехватку Faith — та же логика, что раньше была неявной в двух местах.</summary>
        private void ApplyAlpha()
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = !_isAvailable ? _unavailableAlpha : (_isAffordable ? 1f : _unaffordableAlpha);
        }

        public void SetCost(int cost)
        {
            if (_costLabel != null) _costLabel.text = cost.ToString();
        }

        public void SetIcon(Sprite icon)
        {
            if (_icon != null) _icon.sprite = icon;
        }
    }
}
