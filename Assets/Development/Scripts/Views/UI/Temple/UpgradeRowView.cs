using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.Temple
{
    /// <summary>
    /// Одна строка апгрейда в Temple (game-design §8, шаг 8) — только UI, маршрутизацию по индексу
    /// строки делает <c>TempleView</c>. Только UI, никакой бизнес-логики (CLAUDE.md: Views — только
    /// для UI).
    /// </summary>
    public sealed class UpgradeRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _effectText;
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private Button _buyButton;

        [Tooltip("Текст на месте цены, когда категория достигла максимума каталога.")]
        [SerializeField] private string _maxedLabel = "MAX";

        public event Action BuyClicked;

        private void Awake()
        {
            if (_buyButton != null) _buyButton.onClick.AddListener(OnBuyClicked);
        }

        private void OnDestroy()
        {
            if (_buyButton != null) _buyButton.onClick.RemoveListener(OnBuyClicked);
        }

        private void OnBuyClicked() => BuyClicked?.Invoke();

        public void SetTitle(string title)
        {
            if (_titleText != null) _titleText.text = title;
        }

        public void SetLevel(int level, int maxLevel)
        {
            if (_levelText != null) _levelText.text = $"{level}/{maxLevel}";
        }

        public void SetEffect(string effectText)
        {
            if (_effectText != null) _effectText.text = effectText;
        }

        public void SetPriceOrMaxed(int price, bool isMaxed)
        {
            if (_priceText != null) _priceText.text = isMaxed ? _maxedLabel : price.ToString();
            if (_buyButton != null) _buyButton.interactable = !isMaxed;
        }
    }
}
