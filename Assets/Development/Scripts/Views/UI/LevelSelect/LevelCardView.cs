using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.LevelSelect
{
    /// <summary>
    /// Одна карточка уровня в LevelSelect (game-design §7, шаг 7) — только UI, маршрутизацию по
    /// индексу делает <c>LevelSelectView</c>/<c>LevelSelectPresenter</c> (тот же приём, что
    /// <c>MiracleButtonView</c>/<c>MiraclePanelView</c>). Без своего интерфейса — как и
    /// MiracleButtonView, потребитель работает с ней только через хозяина (ILevelSelectView).
    /// </summary>
    public sealed class LevelCardView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _preview;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private GameObject _lockedOverlay;
        [SerializeField] private GameObject _completedMark;

        private int _index;

        /// <summary>Клик по карточке — индекс задаётся один раз через <see cref="SetIndex"/> при инициализации LevelSelectView.</summary>
        public event Action<int> Clicked;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(OnButtonClicked);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnButtonClicked);
        }

        private void OnButtonClicked() => Clicked?.Invoke(_index);

        public void SetIndex(int index) => _index = index;

        public void SetPreview(Sprite preview)
        {
            if (_preview != null) _preview.sprite = preview;
        }

        public void SetTitle(string title)
        {
            if (_titleText != null) _titleText.text = title;
        }

        public void SetLocked(bool locked)
        {
            if (_lockedOverlay != null) _lockedOverlay.SetActive(locked);
            if (_button != null) _button.interactable = !locked;
        }

        public void SetCompleted(bool completed)
        {
            if (_completedMark != null) _completedMark.SetActive(completed);
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
