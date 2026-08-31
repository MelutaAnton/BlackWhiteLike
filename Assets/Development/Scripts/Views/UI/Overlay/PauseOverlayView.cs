using System;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>Оверлей паузы (game-design §9, §10, §13, шаг 5) — только UI, без бизнес-логики.</summary>
    public sealed class PauseOverlayView : MonoBehaviour, IPauseOverlayView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _resumeButton;

        public event Action ResumeClicked;

        private void Awake()
        {
            if (_resumeButton != null) _resumeButton.onClick.AddListener(OnResumeButtonClicked);
        }

        private void OnDestroy()
        {
            if (_resumeButton != null) _resumeButton.onClick.RemoveListener(OnResumeButtonClicked);
        }

        private void OnResumeButtonClicked() => ResumeClicked?.Invoke();

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
