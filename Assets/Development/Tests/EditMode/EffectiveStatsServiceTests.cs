using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Data.Configs.Upgrades;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Meta;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="EffectiveStatsService"/> (game-design §8, §14.2, шаг 8).</summary>
    public class EffectiveStatsServiceTests
    {
        private static void SetField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static UpgradeLevelEntry CreateLevelEntry(int level, float effectValue, int price = 0)
        {
            var entry = new UpgradeLevelEntry();
            SetField(entry, "_level", level);
            SetField(entry, "_effectValue", effectValue);
            SetField(entry, "_price", price);
            return entry;
        }

        private static UpgradeCategoryEntry CreateCategoryEntry(UpgradeCategory category, params UpgradeLevelEntry[] levels)
        {
            var entry = new UpgradeCategoryEntry();
            SetField(entry, "_category", category);
            SetField(entry, "_levels", levels);
            return entry;
        }

        private static UpgradeCatalogConfig CreateCatalog(params UpgradeCategoryEntry[] categories)
        {
            var catalog = ScriptableObject.CreateInstance<UpgradeCatalogConfig>();
            SetField(catalog, "_categories", categories);
            return catalog;
        }

        [Test]
        public void LevelZero_AllMultipliersAreOne_AllBonusesAreZero()
        {
            var meta = new FakeMetaProgressService();
            var service = new EffectiveStatsService(meta, CreateCatalog());

            Assert.AreEqual(1f, service.HandForceMultiplier, 0.001f);
            Assert.AreEqual(1f, service.HandMassMultiplier, 0.001f);
            Assert.AreEqual(0f, service.FaithCapacityBonus, 0.001f);
            Assert.AreEqual(1f, service.FaithRegenMultiplier, 0.001f);
            Assert.AreEqual(1f, service.MiraclePowerMultiplier, 0.001f);
            Assert.AreEqual(1f, service.MiracleRadiusMultiplier, 0.001f);
        }

        [Test]
        public void HandPowerLevel1_ScalesHandForceAndMassMultipliers()
        {
            var meta = new FakeMetaProgressService();
            meta.IncrementUpgradeLevel(UpgradeCategory.HandPower); // -> level 1
            var catalog = CreateCatalog(CreateCategoryEntry(UpgradeCategory.HandPower, CreateLevelEntry(1, 10f))); // +10%
            var service = new EffectiveStatsService(meta, catalog);

            Assert.AreEqual(1.1f, service.HandForceMultiplier, 0.001f);
            Assert.AreEqual(1.1f, service.HandMassMultiplier, 0.001f);
        }

        [Test]
        public void FaithCapacityLevel1_BonusIsAbsolute_RegenIsPercent()
        {
            var meta = new FakeMetaProgressService();
            meta.IncrementUpgradeLevel(UpgradeCategory.FaithCapacity);
            var catalog = CreateCatalog(CreateCategoryEntry(UpgradeCategory.FaithCapacity, CreateLevelEntry(1, 25f)));
            var service = new EffectiveStatsService(meta, catalog);

            Assert.AreEqual(25f, service.FaithCapacityBonus, 0.001f); // абсолют
            Assert.AreEqual(1.25f, service.FaithRegenMultiplier, 0.001f); // процент
        }

        [Test]
        public void MiraclePowerLevel1_ScalesPowerAndRadiusMultipliers()
        {
            var meta = new FakeMetaProgressService();
            meta.IncrementUpgradeLevel(UpgradeCategory.MiraclePower);
            var catalog = CreateCatalog(CreateCategoryEntry(UpgradeCategory.MiraclePower, CreateLevelEntry(1, 20f)));
            var service = new EffectiveStatsService(meta, catalog);

            Assert.AreEqual(1.2f, service.MiraclePowerMultiplier, 0.001f);
            Assert.AreEqual(1.2f, service.MiracleRadiusMultiplier, 0.001f);
        }

        [Test]
        public void WithoutMetaProgress_ReturnsNeutralValues()
        {
            var service = new EffectiveStatsService(null, CreateCatalog());

            Assert.AreEqual(1f, service.HandForceMultiplier, 0.001f);
            Assert.AreEqual(0f, service.FaithCapacityBonus, 0.001f);
        }

        [Test]
        public void WithoutCatalog_ReturnsNeutralValues()
        {
            var meta = new FakeMetaProgressService();
            meta.IncrementUpgradeLevel(UpgradeCategory.HandPower);
            var service = new EffectiveStatsService(meta, null);

            Assert.AreEqual(1f, service.HandForceMultiplier, 0.001f);
        }

        [Test]
        public void LevelAboveCatalogMax_MissingEntry_ReturnsNeutralForThatLevel()
        {
            var meta = new FakeMetaProgressService();
            meta.IncrementUpgradeLevel(UpgradeCategory.HandPower);
            meta.IncrementUpgradeLevel(UpgradeCategory.HandPower); // -> level 2, каталог знает только про 1
            var catalog = CreateCatalog(CreateCategoryEntry(UpgradeCategory.HandPower, CreateLevelEntry(1, 10f)));
            var service = new EffectiveStatsService(meta, catalog);

            Assert.AreEqual(1f, service.HandForceMultiplier, 0.001f); // отсутствующая запись -> 0 -> множитель 1
        }
    }
}
