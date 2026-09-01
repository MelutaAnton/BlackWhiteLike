using System;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.MainMenu
{
    /// <summary>Главное меню (game-design §7, шаг 7) — только UI, без бизнес-логики.</summary>
    public sealed class MainMenuView : MonoBehaviour, IMainMenuView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _playButton;

        public event Action PlayClicked;

        private void Awake()
        {
            if (_playButton != null) _playButton.onClick.AddListener(OnPlayButtonClicked);
        }

        private void OnDestroy()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(OnPlayButtonClicked);
        }

        private void OnPlayButtonClicked() => PlayClicked?.Invoke();

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }
    }
}
