using System;

namespace BlackWhiteLike.Views.UI.Temple
{
    /// <summary>MVP-View экрана Temple (game-design §8, шаг 8).</summary>
    public interface ITempleView
    {
        void SetVisible(bool visible);
        void SetCoins(int coins);

        /// <summary>Показывает кнопку сбора офлайн-дохода (IdleIncomeReadyEvent).</summary>
        void ShowIdleReward(int coinAmount);
        void HideIdleReward();

        /// <summary>Число UI-строк апгрейдов, реально прошитых в инспекторе (см. <see cref="ConfigureRow"/>).</summary>
        int RowCount { get; }

        /// <summary>Заполняет строку апгрейда по индексу. isMaxed — цена скрыта, кнопка недоступна.</summary>
        void ConfigureRow(int index, string title, int level, int maxLevel, string effectText, int price, bool isMaxed);

        /// <summary>Клик по кнопке покупки строки — аргумент: индекс строки.</summary>
        event Action<int> BuyClicked;

        event Action CollectIdleClicked;
        event Action NextLevelClicked;
    }
}
