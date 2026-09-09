using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.Temple
{
    /// <summary>MVP-View экрана Temple (game-design §8, шаг 8) — только UI, без бизнес-логики.</summary>
    public sealed class TempleView : MonoBehaviour, ITempleView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _coinsText;
        [SerializeField] private Button _collectIdleButton;

        [Tooltip("Текст суммы офлайн-дохода рядом с кнопкой сбора (ревью шага 8, мелочь: раньше аргумент ShowIdleReward игнорировался). Опционально — null-check ниже, как у остальных полей View.")]
        [SerializeField] private TMP_Text _idleRewardText;

        [SerializeField] private UpgradeRowView[] _upgradeRows;
        [SerializeField] private CreatureTeaserView _creatureTeaser;
        [SerializeField] private Button _nextLevelButton;

        public event Action<int> BuyClicked;
        public event Action CollectIdleClicked;
        public event Action NextLevelClicked;

        public int RowCount => _upgradeRows?.Length ?? 0;

        private Action[] _rowBuyHandlers;

        private void Awake()
        {
            if (_collectIdleButton != null) _collectIdleButton.onClick.AddListener(OnCollectIdleClicked);
            if (_nextLevelButton != null) _nextLevelButton.onClick.AddListener(OnNextLevelClicked);

            if (_upgradeRows != null)
            {
                _rowBuyHandlers = new Action[_upgradeRows.Length];
                for (var i = 0; i < _upgradeRows.Length; i++)
                {
                    var row = _upgradeRows[i];
                    if (row == null) continue;

                    var index = i; // захват по значению — иначе все подписки сослались бы на последний i
                    _rowBuyHandlers[i] = () => OnRowBuyClicked(index);
                    row.BuyClicked += _rowBuyHandlers[i];
                }
            }
        }

        private void OnDestroy()
        {
            if (_collectIdleButton != null) _collectIdleButton.onClick.RemoveListener(OnCollectIdleClicked);
            if (_nextLevelButton != null) _nextLevelButton.onClick.RemoveListener(OnNextLevelClicked);

            if (_upgradeRows != null && _rowBuyHandlers != null)
            {
                for (var i = 0; i < _upgradeRows.Length; i++)
                {
                    if (_upgradeRows[i] != null && _rowBuyHandlers[i] != null)
                    {
                        _upgradeRows[i].BuyClicked -= _rowBuyHandlers[i];
                    }
                }
            }
        }

        private void OnRowBuyClicked(int index) => BuyClicked?.Invoke(index);
        private void OnCollectIdleClicked() => CollectIdleClicked?.Invoke();
        private void OnNextLevelClicked() => NextLevelClicked?.Invoke();

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }

        public void SetCoins(int coins)
        {
            if (_coinsText != null) _coinsText.text = coins.ToString();
        }

        public void ShowIdleReward(int coinAmount)
        {
            if (_collectIdleButton != null) _collectIdleButton.gameObject.SetActive(true);
            if (_idleRewardText != null) _idleRewardText.text = $"+{coinAmount}";
        }

        public void HideIdleReward()
        {
            if (_collectIdleButton != null) _collectIdleButton.gameObject.SetActive(false);
            if (_idleRewardText != null) _idleRewardText.text = string.Empty;
        }

        public void ConfigureRow(int index, string title, int level, int maxLevel, string effectText, int price, bool isMaxed)
        {
            if (_upgradeRows == null || index < 0 || index >= _upgradeRows.Length) return;

            var row = _upgradeRows[index];
            if (row == null) return;

            row.SetTitle(title);
            row.SetLevel(level, maxLevel);
            row.SetEffect(effectText);
            row.SetPriceOrMaxed(price, isMaxed);
        }
    }
}
