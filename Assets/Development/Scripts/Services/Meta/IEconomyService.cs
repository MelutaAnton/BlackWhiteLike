using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Экономика уровня/меты (game-design §8, §13, шаг 8) — сценовый pure C#-сервис, единственная
    /// точка начисления/списания coins с публикацией §13-событий (<c>RewardEarnedEvent</c>,
    /// <c>CurrencyChangedEvent</c>, <c>UpgradePurchasedEvent</c>, <c>IdleIncomeCollectedEvent</c>).
    /// Бизнес-правила (first-clear множитель, цена следующей ступени апгрейда) — здесь; сами данные
    /// хранятся в <see cref="IMetaProgressService"/> (root, переживает загрузку сцен).
    /// </summary>
    public interface IEconomyService
    {
        /// <summary>Награда за прохождение уровня. firstClear — умножается на RewardConfig.FirstClearMultiplier, иначе baseCoins как есть.</summary>
        void GrantLevelReward(LevelId id, int baseCoins, bool firstClear);

        /// <summary>
        /// Покупка следующей ступени апгрейда категории. false — в каталоге нет следующей записи
        /// (достигнут максимум) или не хватает coins; в обоих случаях состояние не меняется.
        /// </summary>
        bool TryPurchaseUpgrade(UpgradeCategory category);

        /// <summary>Зачисление офлайн-дохода (вызывается <c>IIdleIncomeService.Collect()</c>).</summary>
        void GrantIdleIncome(int coins);
    }
}
