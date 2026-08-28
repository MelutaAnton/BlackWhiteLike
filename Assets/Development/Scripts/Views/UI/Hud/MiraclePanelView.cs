using System;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// MVP-View панели чудес (game-design §4, §10) — маршрутизация по <see cref="MiracleButtonView.Type"/>
    /// к конкретной кнопке. Неизвестный/непрошитый тип — тихий no-op (не у каждого чуда обязана
    /// быть кнопка на шаге 4, где в каталоге только Rain и Fireball).
    /// </summary>
    public sealed class MiraclePanelView : MonoBehaviour, IMiraclePanelView
    {
        [SerializeField] private MiracleButtonView[] _buttons;

        public event Action<MiracleType> MiracleClicked;

        private void Awake()
        {
            if (_buttons == null) return;

            foreach (var button in _buttons)
            {
                if (button == null) continue;
                button.Clicked += OnButtonClicked;
            }
        }

        private void OnDestroy()
        {
            if (_buttons == null) return;

            foreach (var button in _buttons)
            {
                if (button == null) continue;
                button.Clicked -= OnButtonClicked;
            }
        }

        private void OnButtonClicked(MiracleType type) => MiracleClicked?.Invoke(type);

        public void SetAvailable(MiracleType type, bool available)
        {
            if (TryFindButton(type, out var button)) button.SetAvailable(available);
        }

        public void SetSelected(MiracleType type, bool selected)
        {
            if (TryFindButton(type, out var button)) button.SetSelected(selected);
        }

        public void SetAffordable(MiracleType type, bool affordable)
        {
            if (TryFindButton(type, out var button)) button.SetAffordable(affordable);
        }

        public void SetCost(MiracleType type, int cost)
        {
            if (TryFindButton(type, out var button)) button.SetCost(cost);
        }

        public void SetIcon(MiracleType type, Sprite icon)
        {
            if (TryFindButton(type, out var button)) button.SetIcon(icon);
        }

        private bool TryFindButton(MiracleType type, out MiracleButtonView button)
        {
            if (_buttons != null)
            {
                foreach (var candidate in _buttons)
                {
                    if (candidate == null) continue;
                    if (candidate.Type != type) continue;

                    button = candidate;
                    return true;
                }
            }

            button = null;
            return false;
        }
    }
}
