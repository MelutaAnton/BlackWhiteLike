using BlackWhiteLike.Data.Configs.Upgrades;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>Реализация <see cref="IEffectiveStatsService"/> (game-design §8, §14.2, шаг 8).</summary>
    public sealed class EffectiveStatsService : IEffectiveStatsService
    {
        private readonly IMetaProgressService _metaProgress;
        private readonly UpgradeCatalogConfig _catalog;

        public EffectiveStatsService(IMetaProgressService metaProgress, UpgradeCatalogConfig catalog)
        {
            _metaProgress = metaProgress;
            _catalog = catalog;
        }

        public float HandForceMultiplier => Multiplier(UpgradeCategory.HandPower);
        public float HandMassMultiplier => Multiplier(UpgradeCategory.HandPower);
        public float FaithCapacityBonus => Bonus(UpgradeCategory.FaithCapacity);
        public float FaithRegenMultiplier => Multiplier(UpgradeCategory.FaithCapacity);
        public float MiraclePowerMultiplier => Multiplier(UpgradeCategory.MiraclePower);
        public float MiracleRadiusMultiplier => Multiplier(UpgradeCategory.MiraclePower);

        private float Multiplier(UpgradeCategory category) => 1f + GetEffectValue(category) / 100f;

        private float Bonus(UpgradeCategory category) => GetEffectValue(category);

        private float GetEffectValue(UpgradeCategory category)
        {
            // Без root/каталога (тестовая сцена, конфиг не прошит) — нейтральные значения, а не NRE.
            if (_metaProgress == null || _catalog == null) return 0f;

            var level = _metaProgress.GetUpgradeLevel(category);
            if (level <= 0) return 0f; // апгрейд не куплен — каталог не читается

            return _catalog.TryGetLevelEntry(category, level, out var entry) ? entry.EffectValue : 0f;
        }
    }
}
