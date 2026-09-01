using System;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.LevelSelect
{
    /// <summary>Экран выбора уровня (game-design §7, шаг 7) — только UI, без бизнес-логики.</summary>
    public sealed class LevelSelectView : MonoBehaviour, ILevelSelectView
    {
        [SerializeField] private LevelCardView[] _cards;
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _backButton;

        public event Action<int> CardClicked;
        public event Action BackClicked;

        public int CardCount => _cards != null ? _cards.Length : 0;

        private void Awake()
        {
            if (_backButton != null) _backButton.onClick.AddListener(OnBackButtonClicked);

            if (_cards == null) return;

            for (var i = 0; i < _cards.Length; i++)
            {
                var card = _cards[i];
                if (card == null) continue;

                card.SetIndex(i);
                card.Clicked += OnCardClicked;
            }
        }

        private void OnDestroy()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBackButtonClicked);

            if (_cards == null) return;

            foreach (var card in _cards)
            {
                if (card == null) continue;
                card.Clicked -= OnCardClicked;
            }
        }

        private void OnCardClicked(int index) => CardClicked?.Invoke(index);

        private void OnBackButtonClicked() => BackClicked?.Invoke();

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }

        public void BindCard(int index, string title, Sprite preview, bool locked, bool completed)
        {
            if (_cards == null || index < 0 || index >= _cards.Length) return;

            var card = _cards[index];
            if (card == null) return;

            card.SetVisible(true);
            card.SetTitle(title);
            card.SetPreview(preview);
            card.SetLocked(locked);
            card.SetCompleted(completed);
        }

        public void HideCard(int index)
        {
            if (_cards == null || index < 0 || index >= _cards.Length) return;

            var card = _cards[index];
            if (card != null) card.SetVisible(false);
        }
    }
}
