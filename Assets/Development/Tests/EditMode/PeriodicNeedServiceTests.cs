using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="PeriodicNeedService"/> (game-design §5/§6, решение владельца 2026-09-12,
    /// уровни 2–5). Использует настоящие <see cref="EventBus"/>/<see cref="LevelEventService"/>/
    /// <see cref="NeedSiteRegistry"/> — только <see cref="IVillagerDiseaseService"/> подменяется
    /// фейком (<see cref="FakeVillagerDiseaseService"/>) ради прямого контроля над HasEligibleCandidate,
    /// тот же приём, что у остальных сервисов Village (см. VillagerDiseaseServiceTests).
    /// </summary>
    public class PeriodicNeedServiceTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        private Transform CreateSite(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.transform;
        }

        /// <summary>
        /// EventIntervalSeconds/PeriodicNeedDuration — приватные SerializeField на LevelConfig (см.
        /// LevelConfig.cs) — прошиваются через reflection, тот же приём, что EventTimeline в
        /// LevelEventServiceTests.
        /// </summary>
        private static LevelConfig CreateLevelConfig(float intervalSeconds, float duration = 10f)
        {
            var config = ScriptableObject.CreateInstance<LevelConfig>();
            typeof(LevelConfig).GetField("_eventIntervalSeconds", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(config, intervalSeconds);
            typeof(LevelConfig).GetField("_periodicNeedDuration", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(config, duration);
            return config;
        }

        [Test]
        public void IsEnabled_ZeroInterval_False()
        {
            var config = CreateLevelConfig(0f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var service = new PeriodicNeedService(eventBus, config, levelEventService, new NeedSiteRegistry(), new FakeVillagerDiseaseService(), new System.Random(1));

            Assert.IsFalse(service.IsEnabled);
        }

        [Test]
        public void IsEnabled_PositiveInterval_True()
        {
            var config = CreateLevelConfig(10f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var service = new PeriodicNeedService(eventBus, config, levelEventService, new NeedSiteRegistry(), new FakeVillagerDiseaseService(), new System.Random(1));

            Assert.IsTrue(service.IsEnabled);
        }

        [Test]
        public void Tick_ZeroInterval_DoesNotPublish_AndDoesNotThrow()
        {
            var config = CreateLevelConfig(0f); // L01 — режим выключен
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var registry = new NeedSiteRegistry();
            registry.Register(0, NeedType.Fire, CreateSite("Fire0"));
            var service = new PeriodicNeedService(eventBus, config, levelEventService, registry, new FakeVillagerDiseaseService(), new System.Random(1));

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                Assert.DoesNotThrow(() => service.Tick(1000f));
            }

            Assert.IsEmpty(triggered);
        }

        [Test]
        public void Tick_AtIntervalSeconds_PublishesExactlyOnce_NotEarlier()
        {
            var config = CreateLevelConfig(5f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var registry = new NeedSiteRegistry();
            registry.Register(0, NeedType.Fire, CreateSite("Fire0"));
            var disease = new FakeVillagerDiseaseService { HasEligibleCandidate = false };
            var service = new PeriodicNeedService(eventBus, config, levelEventService, registry, disease, new System.Random(1));

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                service.Tick(1f);
                service.Tick(1f);
                service.Tick(1f);
                service.Tick(1f);
                Assert.IsEmpty(triggered); // суммарно 4с — ещё не наступило

                service.Tick(1f); // суммарно ровно 5с
            }

            Assert.AreEqual(1, triggered.Count);
            Assert.AreEqual(NeedType.Fire, triggered[0].Type);
            Assert.AreEqual(0, triggered[0].SiteIndex);
            Assert.IsFalse(triggered[0].ByPlayer); // спавн периодического режима — не действие игрока
        }

        [Test]
        public void Tick_ChoosesOnlyFreeSite_SkipsActiveOne()
        {
            var config = CreateLevelConfig(5f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var registry = new NeedSiteRegistry();
            registry.Register(0, NeedType.Fire, CreateSite("Fire0"));
            registry.Register(1, NeedType.Fire, CreateSite("Fire1"));
            levelEventService.TriggerNeed(NeedType.Fire, 0, duration: 100f, byPlayer: true, causeMiracle: default); // site 0 занят
            var disease = new FakeVillagerDiseaseService { HasEligibleCandidate = false };
            var service = new PeriodicNeedService(eventBus, config, levelEventService, registry, disease, new System.Random(1));

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                service.Tick(5f);
            }

            Assert.AreEqual(1, triggered.Count);
            Assert.AreEqual(1, triggered[0].SiteIndex); // единственный свободный
        }

        [Test]
        public void Tick_DiseaseCandidate_UsesVirtualSlot()
        {
            var config = CreateLevelConfig(5f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var registry = new NeedSiteRegistry(); // Fire/Hunger сайтов нет — доступен только Disease
            var disease = new FakeVillagerDiseaseService { HasEligibleCandidate = true };
            var service = new PeriodicNeedService(eventBus, config, levelEventService, registry, disease, new System.Random(1));

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                service.Tick(5f);
            }

            Assert.AreEqual(1, triggered.Count);
            Assert.AreEqual(NeedType.Disease, triggered[0].Type);
            Assert.GreaterOrEqual(triggered[0].SiteIndex, NeedHostRules.VirtualSiteIndexBase);
        }

        [Test]
        public void Tick_DiseaseCandidate_SecondSpawnWhileFirstStillActive_UsesNextFreeVirtualSlot()
        {
            var config = CreateLevelConfig(5f, duration: 10f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var registry = new NeedSiteRegistry();
            var disease = new FakeVillagerDiseaseService { HasEligibleCandidate = true };
            var service = new PeriodicNeedService(eventBus, config, levelEventService, registry, disease, new System.Random(1));

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                service.Tick(5f); // первый спавн — слот 1000, активен ещё 10с
                service.Tick(5f); // второй спавн (суммарно 10с) — 1000 всё ещё Active -> 1001
            }

            Assert.AreEqual(2, triggered.Count);
            Assert.AreEqual(NeedHostRules.VirtualSiteIndexBase, triggered[0].SiteIndex);
            Assert.AreEqual(NeedHostRules.VirtualSiteIndexBase + 1, triggered[1].SiteIndex);
        }

        [Test]
        public void Tick_NoFreeTargetsAtAll_DoesNotPublish_AndDoesNotThrow()
        {
            var config = CreateLevelConfig(5f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var registry = new NeedSiteRegistry(); // ни одной точки Fire/Hunger
            var disease = new FakeVillagerDiseaseService { HasEligibleCandidate = false }; // и Disease недоступна
            var service = new PeriodicNeedService(eventBus, config, levelEventService, registry, disease, new System.Random(1));

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                Assert.DoesNotThrow(() => service.Tick(5f));
            }

            Assert.IsEmpty(triggered);
        }

        [Test]
        public void Tick_AllSitesActive_DoesNotPublish()
        {
            var config = CreateLevelConfig(5f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var registry = new NeedSiteRegistry();
            registry.Register(0, NeedType.Fire, CreateSite("Fire0"));
            levelEventService.TriggerNeed(NeedType.Fire, 0, duration: 100f, byPlayer: true, causeMiracle: default);
            var disease = new FakeVillagerDiseaseService { HasEligibleCandidate = false };
            var service = new PeriodicNeedService(eventBus, config, levelEventService, registry, disease, new System.Random(1));

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                service.Tick(5f);
            }

            Assert.IsEmpty(triggered);
        }

        [Test]
        public void Tick_AfterLevelCompleted_DoesNotPublish()
        {
            var config = CreateLevelConfig(5f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var registry = new NeedSiteRegistry();
            registry.Register(0, NeedType.Fire, CreateSite("Fire0"));
            var disease = new FakeVillagerDiseaseService { HasEligibleCandidate = false };
            var service = new PeriodicNeedService(eventBus, config, levelEventService, registry, disease, new System.Random(1));

            eventBus.Publish(new LevelCompletedEvent(default, 0, 0, 0f));

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                service.Tick(1000f);
            }

            Assert.IsEmpty(triggered);
        }

        [Test]
        public void Tick_SeededRandom_IsDeterministic()
        {
            NeedTriggeredEvent RunOnce(int seed)
            {
                var config = CreateLevelConfig(5f);
                var eventBus = new EventBus();
                var levelEventService = new LevelEventService(eventBus, config);
                var registry = new NeedSiteRegistry();
                registry.Register(0, NeedType.Fire, CreateSite("Fire0"));
                registry.Register(1, NeedType.Hunger, CreateSite("Hunger1"));
                var disease = new FakeVillagerDiseaseService { HasEligibleCandidate = false };
                var service = new PeriodicNeedService(eventBus, config, levelEventService, registry, disease, new System.Random(seed));

                NeedTriggeredEvent? captured = null;
                using (eventBus.Subscribe<NeedTriggeredEvent>(e => captured = e))
                {
                    service.Tick(5f);
                }

                return captured.Value;
            }

            var first = RunOnce(42);
            var second = RunOnce(42);

            Assert.AreEqual(first.Type, second.Type);
            Assert.AreEqual(first.SiteIndex, second.SiteIndex);
        }

        [Test]
        public void Tick_LargeDeltaTime_PublishesExactlyOnce()
        {
            var config = CreateLevelConfig(5f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var registry = new NeedSiteRegistry();
            registry.Register(0, NeedType.Fire, CreateSite("Fire0"));
            var disease = new FakeVillagerDiseaseService { HasEligibleCandidate = false };
            var service = new PeriodicNeedService(eventBus, config, levelEventService, registry, disease, new System.Random(1));

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                service.Tick(15f); // 3 интервала за один вызов — максимум один спавн (см. remarks PeriodicNeedService)
            }

            Assert.AreEqual(1, triggered.Count);
        }

        [Test]
        public void Dispose_DoesNotThrow()
        {
            var config = CreateLevelConfig(5f);
            var eventBus = new EventBus();
            var levelEventService = new LevelEventService(eventBus, config);
            var service = new PeriodicNeedService(eventBus, config, levelEventService, new NeedSiteRegistry(), new FakeVillagerDiseaseService(), new System.Random(1));

            Assert.DoesNotThrow(() => service.Dispose());
        }
    }
}
