using System;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>
    /// MVP-View soft-fail оверлея (game-design §9, §10, §13, шаг 5). Шаг 10: оверлей — окно
    /// rewarded-согласия ("посмотреть рекламу за веру?"), а не простой рефил одной кнопкой.
    /// </summary>
    public interface ILevelStalledView
    {
        void Show();
        void Hide();

        /// <summary>Задаёт текст вопроса (шаг 10) — презентер вызывает перед <see cref="Show"/>.</summary>
        void SetQuestionText(string text);

        /// <summary>Клик по кнопке согласия ("Посмотреть рекламу") — rewarded-заглушка (// ADS_STUB).</summary>
        event Action RefillClicked;

        /// <summary>Шаг 10: клик по кнопке отказа — оверлей скрывается без пополнения веры.</summary>
        event Action DeclineClicked;
    }
}
