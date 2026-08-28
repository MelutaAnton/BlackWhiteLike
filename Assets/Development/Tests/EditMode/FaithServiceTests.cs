using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Faith;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="FaithService"/> (game-design §4, §19, шаг 4).</summary>
    public class FaithServiceTests
    {
        /// <summary>
        /// Единственная реализация IVillagerStateService в тестах, кроме настоящего сервиса —
        /// обязана реализовывать ВСЕ члены интерфейса, включая CountInState/TryGetPanicStimulus.
        /// </summary>
        private sealed class FakeVillagerStateService : IVillagerStateService
        {
            public int WorshipCount { get; set; }

            public void Register(VillagerId id)
            {
            }

            public void Unregister(VillagerId id)
            {
            }

            public VillagerState GetState(VillagerId id) => VillagerState.Idle;

            public void ReportStimulus(VillagerId id, VillagerStimulus stimulus)
            {
            }

            public void Tick(float deltaTime)
            {
            }

            public bool TryGetPanicStimulus(NeedType type, out VillagerStimulus stimulus)
            {
                stimulus = default;
                return false;
            }

            public int CountInState(VillagerState state) => state == VillagerState.Worship ? WorshipCount : 0;
        }

        private static FaithConfig CreateConfig(
            float baseCapacity = 100f,
            float regenPerWorshipper = 2f,
            float startingFaithPercent = 50f)
        {
            var config = ScriptableObject.CreateInstance<FaithConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_baseCapacity").floatValue = baseCapacity;
            so.FindProperty("_regenPerWorshipper").floatValue = regenPerWorshipper;
            so.FindProperty("_startingFaithPercent").floatValue = startingFaithPercent;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        [Test]
        public void Constructor_StartsAtCapacityTimesStartingPercent()
        {
            var service = new FaithService(new FakeEventBus(), CreateConfig(baseCapacity: 100f, startingFaithPercent: 50f), new FakeVillagerStateService());

            Assert.AreEqual(50f, service.Current, 0.001f);
            Assert.AreEqual(100f, service.Capacity, 0.001f);
        }

        [Test]
        public void Tick_RegenIsProportionalToWorshipCount()
        {
            var villagers = new FakeVillagerStateService { WorshipCount = 3 };
            var service = new FaithService(new FakeEventBus(), CreateConfig(baseCapacity: 1000f, regenPerWorshipper: 2f, startingFaithPercent: 0f), villagers);

            service.Tick(1f); // 2 * 3 * 1 = 6

            Assert.AreEqual(6f, service.Current, 0.001f);
        }

        [Test]
        public void Tick_NoWorshippers_DoesNotChangeOrPublish()
        {
            var villagers = new FakeVillagerStateService { WorshipCount = 0 };
            var eventBus = new FakeEventBus();
            var service = new FaithService(eventBus, CreateConfig(startingFaithPercent: 10f), villagers);

            service.Tick(1f);

            Assert.AreEqual(10f, service.Current, 0.001f); // baseCapacity(100) * 10%
            Assert.IsEmpty(eventBus.PublishedEvents.OfType<FaithChangedEvent>());
        }

        [Test]
        public void Tick_ClampsAtCapacity()
        {
            var villagers = new FakeVillagerStateService { WorshipCount = 100 };
            var service = new FaithService(new FakeEventBus(), CreateConfig(baseCapacity: 50f, regenPerWorshipper: 10f, startingFaithPercent: 90f), villagers);

            service.Tick(10f); // регена с запасом на переполнение

            Assert.AreEqual(50f, service.Current, 0.001f);
        }

        [Test]
        public void Tick_PublishesFaithChangedEvent_WithOldAndNewValue()
        {
            var villagers = new FakeVillagerStateService { WorshipCount = 1 };
            var eventBus = new FakeEventBus();
            var service = new FaithService(eventBus, CreateConfig(baseCapacity: 1000f, regenPerWorshipper: 5f, startingFaithPercent: 0f), villagers);

            service.Tick(1f);

            var evt = eventBus.PublishedEvents.OfType<FaithChangedEvent>().Single();
            Assert.AreEqual(0f, evt.OldValue, 0.001f);
            Assert.AreEqual(5f, evt.NewValue, 0.001f);
            Assert.AreEqual(1000f, evt.Capacity, 0.001f);
        }

        [Test]
        public void CanAfford_EnoughCurrent_ReturnsTrue()
        {
            var service = new FaithService(new FakeEventBus(), CreateConfig(baseCapacity: 100f, startingFaithPercent: 50f), new FakeVillagerStateService());

            Assert.IsTrue(service.CanAfford(50f));
        }

        [Test]
        public void CanAfford_NotEnoughCurrent_ReturnsFalse()
        {
            var service = new FaithService(new FakeEventBus(), CreateConfig(baseCapacity: 100f, startingFaithPercent: 50f), new FakeVillagerStateService());

            Assert.IsFalse(service.CanAfford(50.1f));
        }

        [Test]
        public void TrySpend_EnoughCurrent_SpendsAllOrNothing_ReturnsTrue()
        {
            var eventBus = new FakeEventBus();
            var service = new FaithService(eventBus, CreateConfig(baseCapacity: 100f, startingFaithPercent: 50f), new FakeVillagerStateService());

            var spent = service.TrySpend(30f);

            Assert.IsTrue(spent);
            Assert.AreEqual(20f, service.Current, 0.001f);
            Assert.IsNotEmpty(eventBus.PublishedEvents.OfType<FaithChangedEvent>());
        }

        [Test]
        public void TrySpend_NotEnoughCurrent_ReturnsFalse_AndDoesNotChange()
        {
            var eventBus = new FakeEventBus();
            var service = new FaithService(eventBus, CreateConfig(baseCapacity: 100f, startingFaithPercent: 50f), new FakeVillagerStateService());

            var spent = service.TrySpend(50.1f);

            Assert.IsFalse(spent);
            Assert.AreEqual(50f, service.Current, 0.001f);
            Assert.IsEmpty(eventBus.PublishedEvents.OfType<FaithChangedEvent>());
        }

        [Test]
        public void TrySpend_ZeroOrNegative_ReturnsTrue_WithoutChange()
        {
            var eventBus = new FakeEventBus();
            var service = new FaithService(eventBus, CreateConfig(baseCapacity: 100f, startingFaithPercent: 50f), new FakeVillagerStateService());

            Assert.IsTrue(service.TrySpend(0f));
            Assert.IsTrue(service.TrySpend(-5f));
            Assert.AreEqual(50f, service.Current, 0.001f);
            Assert.IsEmpty(eventBus.PublishedEvents.OfType<FaithChangedEvent>());
        }
    }
}
