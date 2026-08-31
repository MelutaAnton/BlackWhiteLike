using System;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>Soft-fail оверлей (game-design §9, §10, §13, шаг 5) — только UI, без бизнес-логики.</summary>
    public sealed class LevelStalledView : MonoBehaviour, ILevelStalledView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _refillButton;

        public event Action RefillClicked;

        private void Awake()
        {
            if (_refillButton != null) _refillButton.onClick.AddListener(OnRefillButtonClicked);
        }

        private void OnDestroy()
        {
            if (_refillButton != null) _refillButton.onClick.RemoveListener(OnRefillButtonClicked);
        }

        private void OnRefillButtonClicked() => RefillClicked?.Invoke();

        public void Show()
        {
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
