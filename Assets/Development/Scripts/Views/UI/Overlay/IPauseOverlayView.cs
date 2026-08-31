using System;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>MVP-View оверлея паузы (game-design §9, §10, §13, шаг 5).</summary>
    public interface IPauseOverlayView
    {
        void Show();
        void Hide();

        /// <summary>Клик по кнопке Resume — презентер снимает GameState.Paused.</summary>
        event Action ResumeClicked;
    }
}
