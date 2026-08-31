using System;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>MVP-View экрана итогов уровня (game-design §10, §13, шаг 5).</summary>
    public interface ILevelCompleteView
    {
        void Show(int worship, int target, int awe, int fear, int coins);
        void Hide();

        /// <summary>Клик по кнопке продолжения — презентер переводит FSM в TempleMeta.</summary>
        event Action ContinueClicked;
    }
}
