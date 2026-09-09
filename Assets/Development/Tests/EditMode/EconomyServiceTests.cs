using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Rewards;
using BlackWhiteLike.Data.Configs.Upgrades;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Meta;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="EconomyService"/> (game-design §8, §13, шаг 8).</summary>
    public class EconomyServiceTests
    {
        private static void SetField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static UpgradeLevelEntry CreateLevelEntry(int level, float effectValue, int price)
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

        private static RewardConfig CreateRewardConfig(float firstClearMultiplier = 2f)
        {
            var config = ScriptableObject.CreateInstance<RewardConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_firstClearMultiplier").floatValue = firstClearMultiplier;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        [Test]
        public void GrantLevelReward_FirstClear_MultipliesBaseCoins()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService();
            var service = new EconomyService(eventBus, meta, CreateCatalog(), CreateRewardConfig(firstClearMultiplier: 2f));

            service.GrantLevelReward(LevelId.Level01, baseCoins: 10, firstClear: true);

            Assert.AreEqual(20, meta.Coins);
        }

        [Test]
        public void GrantLevelReward_NotFirstClear_GrantsBaseCoinsAsIs()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService();
            var service = new EconomyService(eventBus, meta, CreateCatalog(), CreateRewardConfig(firstClearMultiplier: 2f));

            service.GrantLevelReward(LevelId.Level01, baseCoins: 10, firstClear: false);

            Assert.AreEqual(10, meta.Coins);
        }

        [Test]
        public void GrantLevelReward_PublishesRewardEarnedEvent_WithXpAmountZero()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService();
            var service = new EconomyService(eventBus, meta, CreateCatalog(), CreateRewardConfig());

            service.GrantLevelReward(LevelId.Level01, baseCoins: 10, firstClear: false);

            var evt = eventBus.PublishedEvents.OfType<RewardEarnedEvent>().Single();
            Assert.AreEqual(RewardSource.LevelComplete, evt.Source);
            Assert.AreEqual(10, evt.CoinAmount);
            Assert.AreEqual(0, evt.XpAmount);
        }

        [Test]
        public void GrantLevelReward_PublishesCurrencyChangedEvent()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService { Coins = 5 };
            var service = new EconomyService(eventBus, meta, CreateCatalog(), CreateRewardConfig());

            service.GrantLevelReward(LevelId.Level01, baseCoins: 10, firstClear: false);

            var evt = eventBus.PublishedEvents.OfType<CurrencyChangedEvent>().Single();
            Assert.AreEqual(5, evt.OldValue);
            Assert.AreEqual(15, evt.NewValue);
            Assert.AreEqual(10, evt.Delta);
        }

        [Test]
        public void GrantLevelReward_ZeroBaseCoins_DoesNotPublish_AndDoesNotChangeCoins()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService();
            var service = new EconomyService(eventBus, meta, CreateCatalog(), CreateRewardConfig());

            service.GrantLevelReward(LevelId.Level01, baseCoins: 0, firstClear: false);

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<RewardEarnedEvent>());
            Assert.AreEqual(0, meta.Coins);
        }

        [Test]
        public void TryPurchaseUpgrade_EnoughCoins_Succeeds_SpendsAndIncrementsLevel()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService { Coins = 100 };
            var catalog = CreateCatalog(CreateCategoryEntry(UpgradeCategory.HandPower, CreateLevelEntry(1, 10f, 50)));
            var service = new EconomyService(eventBus, meta, catalog, CreateRewardConfig());

            var purchased = service.TryPurchaseUpgrade(UpgradeCategory.HandPower);

            Assert.IsTrue(purchased);
            Assert.AreEqual(50, meta.Coins);
            Assert.AreEqual(1, meta.GetUpgradeLevel(UpgradeCategory.HandPower));
        }

        [Test]
        public void TryPurchaseUpgrade_NotEnoughCoins_Fails_DoesNotChangeLevel()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService { Coins = 10 };
            var catalog = CreateCatalog(CreateCategoryEntry(UpgradeCategory.HandPower, CreateLevelEntry(1, 10f, 50)));
            var service = new EconomyService(eventBus, meta, catalog, CreateRewardConfig());

            var purchased = service.TryPurchaseUpgrade(UpgradeCategory.HandPower);

            Assert.IsFalse(purchased);
            Assert.AreEqual(10, meta.Coins);
            Assert.AreEqual(0, meta.GetUpgradeLevel(UpgradeCategory.HandPower));
        }

        [Test]
        public void TryPurchaseUpgrade_AtMaxLevel_Fails()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService { Coins = 1000 };
            meta.IncrementUpgradeLevel(UpgradeCategory.HandPower); // -> level 1, каталог знает только про 1
            var catalog = CreateCatalog(CreateCategoryEntry(UpgradeCategory.HandPower, CreateLevelEntry(1, 10f, 50)));
            var service = new EconomyService(eventBus, meta, catalog, CreateRewardConfig());

            var purchased = service.TryPurchaseUpgrade(UpgradeCategory.HandPower);

            Assert.IsFalse(purchased);
            Assert.AreEqual(1000, meta.Coins);
        }

        [Test]
        public void TryPurchaseUpgrade_Success_PublishesCurrencyChangedEvent_WithNegativeDelta()
        {
            // Ревью шага 8: восстановленный при слиянии кейс — путь покупки (в отличие от
            // GrantLevelReward_PublishesCurrencyChangedEvent выше) даёт ОТРИЦАТЕЛЬНУЮ дельту,
            // old = new + price (симметрично PublishCurrencyChanged в EconomyService).
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService { Coins = 100 };
            var catalog = CreateCatalog(CreateCategoryEntry(UpgradeCategory.HandPower, CreateLevelEntry(1, 10f, 50)));
            var service = new EconomyService(eventBus, meta, catalog, CreateRewardConfig());

            service.TryPurchaseUpgrade(UpgradeCategory.HandPower);

            var evt = eventBus.PublishedEvents.OfType<CurrencyChangedEvent>().Single();
            Assert.AreEqual(-50, evt.Delta);
            Assert.AreEqual(50, evt.NewValue);
            Assert.AreEqual(100, evt.OldValue);
        }

        [Test]
        public void TryPurchaseUpgrade_Success_PublishesUpgradePurchasedEvent()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService { Coins = 100 };
            var catalog = CreateCatalog(CreateCategoryEntry(UpgradeCategory.MiraclePower, CreateLevelEntry(1, 15f, 30)));
            var service = new EconomyService(eventBus, meta, catalog, CreateRewardConfig());

            service.TryPurchaseUpgrade(UpgradeCategory.MiraclePower);

            var evt = eventBus.PublishedEvents.OfType<UpgradePurchasedEvent>().Single();
            Assert.AreEqual(UpgradeCategory.MiraclePower, evt.Category);
            Assert.AreEqual(1, evt.NewLevel);
        }

        [Test]
        public void GrantIdleIncome_AddsCoins_AndPublishesEvents()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService();
            var service = new EconomyService(eventBus, meta, CreateCatalog(), CreateRewardConfig());

            service.GrantIdleIncome(25);

            Assert.AreEqual(25, meta.Coins);
            var rewardEvt = eventBus.PublishedEvents.OfType<RewardEarnedEvent>().Single();
            Assert.AreEqual(RewardSource.IdleIncome, rewardEvt.Source);
            Assert.AreEqual(0, rewardEvt.XpAmount);
            Assert.IsNotEmpty(eventBus.PublishedEvents.OfType<IdleIncomeCollectedEvent>());
        }
    }
}
