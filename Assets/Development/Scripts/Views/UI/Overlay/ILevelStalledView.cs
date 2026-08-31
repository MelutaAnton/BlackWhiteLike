using System;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>MVP-View soft-fail оверлея (game-design §9, §10, §13, шаг 5).</summary>
    public interface ILevelStalledView
    {
        void Show();
        void Hide();

        /// <summary>Клик по кнопке "Пополнить веру" — rewarded-заглушка (// ADS_STUB).</summary>
        event Action RefillClicked;
    }
}
