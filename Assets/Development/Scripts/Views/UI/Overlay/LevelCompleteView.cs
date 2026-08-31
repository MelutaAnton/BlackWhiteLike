using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>Экран итогов уровня (game-design §10, §13, шаг 5) — только UI, без бизнес-логики.</summary>
    public sealed class LevelCompleteView : MonoBehaviour, ILevelCompleteView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TextMeshProUGUI _worshipLabel;
        [SerializeField] private TextMeshProUGUI _aweLabel;
        [SerializeField] private TextMeshProUGUI _fearLabel;
        [SerializeField] private TextMeshProUGUI _coinsLabel;
        [SerializeField] private Button _continueButton;

        public event Action ContinueClicked;

        private void Awake()
        {
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinueButtonClicked);
        }

        private void OnDestroy()
        {
            if (_continueButton != null) _continueButton.onClick.RemoveListener(OnContinueButtonClicked);
        }

        private void OnContinueButtonClicked() => ContinueClicked?.Invoke();

        public void Show(int worship, int target, int awe, int fear, int coins)
        {
            if (_worshipLabel != null) _worshipLabel.text = string.Format("{0} / {1}", worship, target);
            if (_aweLabel != null) _aweLabel.text = awe.ToString();
            if (_fearLabel != null) _fearLabel.text = fear.ToString();
            if (_coinsLabel != null) _coinsLabel.text = coins.ToString();

            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
