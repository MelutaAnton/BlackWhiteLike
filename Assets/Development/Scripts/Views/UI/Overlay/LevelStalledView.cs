using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>
    /// Soft-fail оверлей (game-design §9, §10, §13, шаг 5) — только UI, без бизнес-логики. Шаг 10:
    /// окно rewarded-согласия — вопрос (<see cref="_questionText"/>) + кнопка согласия
    /// (<see cref="_refillButton"/>, существующая роль сохранена) + кнопка отказа
    /// (<see cref="_declineButton"/>). Editor-проводка (контракт имён полей для editor-скрипта):
    /// узлы канваса <c>LevelStalledOverlay/Panel/QuestionText</c>, <c>.../DeclineButton</c>.
    /// </summary>
    public sealed class LevelStalledView : MonoBehaviour, ILevelStalledView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _questionText;
        [SerializeField] private Button _refillButton;
        [SerializeField] private Button _declineButton;

        public event Action RefillClicked;
        public event Action DeclineClicked;

        private void Awake()
        {
            if (_refillButton != null) _refillButton.onClick.AddListener(OnRefillButtonClicked);
            if (_declineButton != null) _declineButton.onClick.AddListener(OnDeclineButtonClicked);
        }

        private void OnDestroy()
        {
            if (_refillButton != null) _refillButton.onClick.RemoveListener(OnRefillButtonClicked);
            if (_declineButton != null) _declineButton.onClick.RemoveListener(OnDeclineButtonClicked);
        }

        private void OnRefillButtonClicked() => RefillClicked?.Invoke();
        private void OnDeclineButtonClicked() => DeclineClicked?.Invoke();

        public void SetQuestionText(string text)
        {
            if (_questionText != null) _questionText.text = text;
        }

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
