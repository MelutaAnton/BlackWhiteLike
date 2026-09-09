using System;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.MainMenu
{
    /// <summary>Главное меню (game-design §7, шаг 7; §8, шаг 8 — вход в Temple) — только UI, без бизнес-логики.</summary>
    public sealed class MainMenuView : MonoBehaviour, IMainMenuView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _templeButton;

        public event Action PlayClicked;
        public event Action TempleClicked;

        private void Awake()
        {
            if (_playButton != null) _playButton.onClick.AddListener(OnPlayButtonClicked);
            if (_templeButton != null) _templeButton.onClick.AddListener(OnTempleButtonClicked);
        }

        private void OnDestroy()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(OnPlayButtonClicked);
            if (_templeButton != null) _templeButton.onClick.RemoveListener(OnTempleButtonClicked);
        }

        private void OnPlayButtonClicked() => PlayClicked?.Invoke();
        private void OnTempleButtonClicked() => TempleClicked?.Invoke();

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }
    }
}
