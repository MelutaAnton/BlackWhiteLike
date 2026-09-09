using BlackWhiteLike.Models;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>
    /// Публикуется <c>EconomyService</c> при начислении coins (level reward / idle income),
    /// game-design §8, §13, шаг 8. <see cref="XpAmount"/> всегда 0 — предохранитель §15 (система
    /// прокачки/энергии не реализована в этом срезе).
    /// </summary>
    public readonly struct RewardEarnedEvent
    {
        public readonly RewardSource Source;
        public readonly int CoinAmount;
        public readonly int XpAmount;

        public RewardEarnedEvent(RewardSource source, int coinAmount, int xpAmount)
        {
            Source = source;
            CoinAmount = coinAmount;
            XpAmount = xpAmount;
        }
    }

    /// <summary>Публикуется <c>EconomyService</c> при любом изменении coins — единый сигнал для UI (Temple, HUD).</summary>
    public readonly struct CurrencyChangedEvent
    {
        public readonly int OldValue;
        public readonly int NewValue;
        public readonly int Delta;

        public CurrencyChangedEvent(int oldValue, int newValue, int delta)
        {
            OldValue = oldValue;
            NewValue = newValue;
            Delta = delta;
        }
    }

    /// <summary>Публикуется <c>EconomyService</c> при успешной покупке ступени апгрейда в Temple.</summary>
    public readonly struct UpgradePurchasedEvent
    {
        public readonly UpgradeCategory Category;
        public readonly int NewLevel;

        public UpgradePurchasedEvent(UpgradeCategory category, int newLevel)
        {
            Category = category;
            NewLevel = newLevel;
        }
    }

    /// <summary>Публикуется <c>IdleIncomeService</c> на старте мета-сцены — офлайн-доход посчитан и ждёт Collect().</summary>
    public readonly struct IdleIncomeReadyEvent
    {
        public readonly int CoinAmount;
        public readonly float OfflineHours;

        public IdleIncomeReadyEvent(int coinAmount, float offlineHours)
        {
            CoinAmount = coinAmount;
            OfflineHours = offlineHours;
        }
    }

    /// <summary>Публикуется <c>EconomyService</c> при зачислении офлайн-дохода (после <c>IIdleIncomeService.Collect()</c>).</summary>
    public readonly struct IdleIncomeCollectedEvent
    {
        public readonly int CoinAmount;

        public IdleIncomeCollectedEvent(int coinAmount)
        {
            CoinAmount = coinAmount;
        }
    }
}
