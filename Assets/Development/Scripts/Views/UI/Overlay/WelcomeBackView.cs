using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>Оверлей "с возвращением" — офлайн-доход (game-design §9, §13, §15, шаг 8) — только UI, без бизнес-логики.</summary>
    public sealed class WelcomeBackView : MonoBehaviour, IWelcomeBackView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _coinsText;
        [SerializeField] private Button _collectButton;
        [SerializeField] private Button _closeButton;

        public event Action CollectClicked;
        public event Action CloseClicked;

        private void Awake()
        {
            if (_collectButton != null) _collectButton.onClick.AddListener(OnCollectClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDestroy()
        {
            if (_collectButton != null) _collectButton.onClick.RemoveListener(OnCollectClicked);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnCollectClicked() => CollectClicked?.Invoke();
        private void OnCloseClicked() => CloseClicked?.Invoke();

        public void Show(int coinAmount)
        {
            if (_coinsText != null) _coinsText.text = coinAmount.ToString();
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
