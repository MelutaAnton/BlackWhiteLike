using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Rewards;
using BlackWhiteLike.Data.Configs.Upgrades;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>Реализация <see cref="IEconomyService"/> (game-design §8, §13, шаг 8).</summary>
    public sealed class EconomyService : IEconomyService
    {
        private readonly IEventBus _eventBus;
        private readonly IMetaProgressService _metaProgress;
        private readonly UpgradeCatalogConfig _upgradeCatalog;
        private readonly RewardConfig _rewardConfig;

        public EconomyService(
            IEventBus eventBus,
            IMetaProgressService metaProgress,
            UpgradeCatalogConfig upgradeCatalog,
            RewardConfig rewardConfig)
        {
            _eventBus = eventBus;
            _metaProgress = metaProgress;
            _upgradeCatalog = upgradeCatalog;
            _rewardConfig = rewardConfig;
        }

        public void GrantLevelReward(LevelId id, int baseCoins, bool firstClear)
        {
            var coins = firstClear ? Mathf.RoundToInt(baseCoins * _rewardConfig.FirstClearMultiplier) : baseCoins;
            Debug.Log($"[EconomyService] Награда за {id} (firstClear={firstClear}): {coins} монет.");
            GrantCoins(coins, RewardSource.LevelComplete);
        }

        public bool TryPurchaseUpgrade(UpgradeCategory category)
        {
            var nextLevel = _metaProgress.GetUpgradeLevel(category) + 1;

            if (!_upgradeCatalog.TryGetLevelEntry(category, nextLevel, out var entry)) return false; // максимум уровня либо запись отсутствует
            if (!_metaProgress.TrySpendCoins(entry.Price)) return false;

            _metaProgress.IncrementUpgradeLevel(category);
            _eventBus.Publish(new UpgradePurchasedEvent(category, nextLevel));
            PublishCurrencyChanged(-entry.Price);
            return true;
        }

        public void GrantIdleIncome(int coins)
        {
            GrantCoins(coins, RewardSource.IdleIncome);

            // Симметрия с остальными публикациями (ревью шага 8, ВАЖНО 6): те же условия, что у
            // RewardEarnedEvent/CurrencyChangedEvent внутри GrantCoins — coins <= 0 не публикуется.
            if (coins > 0) _eventBus.Publish(new IdleIncomeCollectedEvent(coins));
        }

        private void GrantCoins(int coins, RewardSource source)
        {
            if (coins <= 0) return;

            _metaProgress.AddCoins(coins);
            // xpAmount = 0 всегда — предохранитель §15: система прокачки/энергии в этом срезе не реализована.
            _eventBus.Publish(new RewardEarnedEvent(source, coins, xpAmount: 0));
            PublishCurrencyChanged(coins);
        }

        private void PublishCurrencyChanged(int delta)
        {
            var newValue = _metaProgress.Coins;
            var oldValue = newValue - delta;
            _eventBus.Publish(new CurrencyChangedEvent(oldValue, newValue, delta));
        }
    }
}
