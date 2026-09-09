using System;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>MVP-View оверлея "с возвращением" — офлайн-доход (game-design §9, §13, §15, шаг 8).</summary>
    public interface IWelcomeBackView
    {
        void Show(int coinAmount);
        void Hide();

        event Action CollectClicked;
        event Action CloseClicked;
    }
}
