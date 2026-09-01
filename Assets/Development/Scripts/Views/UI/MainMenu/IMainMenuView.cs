using System;

namespace BlackWhiteLike.Views.UI.MainMenu
{
    /// <summary>MVP-View главного меню (game-design §7, шаг 7).</summary>
    public interface IMainMenuView
    {
        void SetVisible(bool visible);

        /// <summary>Клик по кнопке Play — презентер переключает FSM на GameState.LevelSelect.</summary>
        event Action PlayClicked;
    }
}
