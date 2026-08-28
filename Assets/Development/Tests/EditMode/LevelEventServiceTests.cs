using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="LevelEventService"/> (game-design §5, §6, §19, шаг 3).</summary>
    public class LevelEventServiceTests
    {
        /// <summary>
        /// EventTimeline — приватный SerializeField на LevelConfig (см. LevelConfig.cs, как и
        /// остальные SO проекта). В тестах прошивается через reflection — единственный способ
        /// задать таймлайн без Inspector/AssetDatabase; сам LevelEventService таймлайн не мутирует,
        /// только читает в конструкторе.
        /// </summary>
        private static LevelConfig CreateLevelConfig(params NeedEventEntry[] timeline)
        {
            var config = ScriptableObject.CreateInstance<LevelConfig>();
            var field = typeof(LevelConfig).GetField("_eventTimeline", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(config, timeline);
            return config;
        }

        private static LevelEventService CreateService(out FakeEventBus eventBus, params NeedEventEntry[] timeline)
        {
            eventBus = new FakeEventBus();
            var config = CreateLevelConfig(timeline);
            return new LevelEventService(eventBus, config);
        }

        [Test]
        public void Tick_BeforeTriggerTime_DoesNotPublish()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 5f, targetSiteIndex = 0, duration = 10f };
            var service = CreateService(out var eventBus, entry);

            service.Tick(4.9f);

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<NeedTriggeredEvent>());
        }

        [Test]
        public void Tick_AtTriggerTime_PublishesNeedTriggeredEvent()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 5f, targetSiteIndex = 2, duration = 10f };
            var service = CreateService(out var eventBus, entry);

            service.Tick(5f);

            var evt = eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Single();
            Assert.AreEqual(NeedType.Fire, evt.Type);
            Assert.AreEqual(2, evt.SiteIndex);
            Assert.IsFalse(evt.ByPlayer); // запись скриптованного таймлайна — всегда false
        }

        [Test]
        public void Tick_PastTriggerTime_DoesNotTriggerTwice()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 5f, targetSiteIndex = 0, duration = 10f };
            var service = CreateService(out var eventBus, entry);

            service.Tick(5f);
            eventBus.PublishedEvents.Clear();
            service.Tick(1f);
            service.Tick(1f);

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<NeedTriggeredEvent>());
        }

        [Test]
        public void ResolveNeed_ActiveEvent_PublishesNeedResolvedEvent_WithByPlayer()
        {
            var entry = new NeedEventEntry { type = NeedType.Hunger, triggerTime = 1f, targetSiteIndex = 3, duration = 10f };
            var service = CreateService(out var eventBus, entry);
            service.Tick(1f); // trigger
            eventBus.PublishedEvents.Clear();

            service.ResolveNeed(NeedType.Hunger, 3, byPlayer: true);

            var evt = eventBus.PublishedEvents.OfType<NeedResolvedEvent>().Single();
            Assert.AreEqual(NeedType.Hunger, evt.Type);
            Assert.AreEqual(3, evt.SiteIndex);
            Assert.IsTrue(evt.ByPlayer);
        }

        [Test]
        public void ResolveNeed_NotByPlayer_PublishesFalse()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 0f, targetSiteIndex = 0, duration = 10f };
            var service = CreateService(out var eventBus, entry);
            service.Tick(0f);
            eventBus.PublishedEvents.Clear();

            service.ResolveNeed(NeedType.Fire, 0, byPlayer: false);

            var evt = eventBus.PublishedEvents.OfType<NeedResolvedEvent>().Single();
            Assert.IsFalse(evt.ByPlayer);
        }

        [Test]
        public void ResolveNeed_NoActiveMatch_DoesNotPublish()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 5f, targetSiteIndex = 0, duration = 10f };
            var service = CreateService(out var eventBus, entry);
            // Ещё не сработало (elapsed < triggerTime) — активной нужды нет.

            service.ResolveNeed(NeedType.Fire, 0, byPlayer: true);

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<NeedResolvedEvent>());
        }

        [Test]
        public void Tick_DurationElapsedWithoutResolve_PublishesNeedExpiredEvent()
        {
            var entry = new NeedEventEntry { type = NeedType.Disease, triggerTime = 1f, targetSiteIndex = 0, duration = 3f };
            var service = CreateService(out var eventBus, entry);
            service.Tick(1f); // trigger
            eventBus.PublishedEvents.Clear();

            service.Tick(2.9f);
            Assert.IsEmpty(eventBus.PublishedEvents.OfType<NeedExpiredEvent>());

            service.Tick(0.2f); // суммарно 3.1с после триггера — перекрывает duration
            var evt = eventBus.PublishedEvents.OfType<NeedExpiredEvent>().Single();
            Assert.AreEqual(NeedType.Disease, evt.Type);
            Assert.AreEqual(0, evt.SiteIndex);
            Assert.IsFalse(evt.ByPlayer); // запись скриптованного таймлайна — всегда false
        }

        [Test]
        public void ResolvedEvent_DoesNotExpire()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 1f, targetSiteIndex = 0, duration = 2f };
            var service = CreateService(out var eventBus, entry);
            service.Tick(1f); // trigger
            service.ResolveNeed(NeedType.Fire, 0, byPlayer: true);
            eventBus.PublishedEvents.Clear();

            service.Tick(5f); // сильно больше duration

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<NeedExpiredEvent>());
        }

        [Test]
        public void MultipleEntries_TriggerIndependently()
        {
            var entryA = new NeedEventEntry { type = NeedType.Fire, triggerTime = 1f, targetSiteIndex = 0, duration = 10f };
            var entryB = new NeedEventEntry { type = NeedType.Hunger, triggerTime = 3f, targetSiteIndex = 1, duration = 10f };
            var service = CreateService(out var eventBus, entryA, entryB);

            service.Tick(1f); // только A
            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Count());

            service.Tick(2f); // суммарно 3с — теперь B
            Assert.AreEqual(2, eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Count());
        }

        // --- В9 (ревью шага 3): недостающие граничные случаи ---

        [Test]
        public void Tick_EmptyTimeline_DoesNotThrow_AndPublishesNothing()
        {
            var service = CreateService(out var eventBus);

            Assert.DoesNotThrow(() => service.Tick(100f));

            Assert.IsEmpty(eventBus.PublishedEvents);
        }

        [Test]
        public void Tick_TwoEntriesSameTriggerTime_BothTriggerInOneTick()
        {
            var entryA = new NeedEventEntry { type = NeedType.Fire, triggerTime = 5f, targetSiteIndex = 0, duration = 10f };
            var entryB = new NeedEventEntry { type = NeedType.Hunger, triggerTime = 5f, targetSiteIndex = 1, duration = 10f };
            var service = CreateService(out var eventBus, entryA, entryB);

            service.Tick(5f);

            var triggered = eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().ToList();
            Assert.AreEqual(2, triggered.Count);
            Assert.IsTrue(triggered.Any(e => e.Type == NeedType.Fire && e.SiteIndex == 0));
            Assert.IsTrue(triggered.Any(e => e.Type == NeedType.Hunger && e.SiteIndex == 1));
        }

        [Test]
        public void Tick_DeltaTimeOverjumpsTriggerAndDuration_PublishesBothTriggeredAndExpiredInSameTick()
        {
            // Н1 (ревью шага 3): TimeSinceTriggered = elapsed - triggerTime (не 0) — большой прыжок
            // deltaTime (просадка FPS) может перекрыть срабатывание И истечение за один Tick.
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 5f, targetSiteIndex = 0, duration = 2f };
            var service = CreateService(out var eventBus, entry);

            // За один вызов elapsed перепрыгивает и triggerTime(5), и triggerTime+duration(7).
            service.Tick(10f);

            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Count());
            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<NeedExpiredEvent>().Count());
        }

        [Test]
        public void ResolveNeed_AlreadyExpiredEntry_DoesNotPublish()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 1f, targetSiteIndex = 0, duration = 2f };
            var service = CreateService(out var eventBus, entry);
            service.Tick(1f); // trigger
            service.Tick(2.1f); // истекает (суммарно 3.1с после триггера > duration)
            eventBus.PublishedEvents.Clear();

            service.ResolveNeed(NeedType.Fire, 0, byPlayer: true);

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<NeedResolvedEvent>());
        }

        // --- Шаг 4: TriggerNeed — рантайм-запись поверх таймлайна (например, Fireball поджёг дом) ---

        [Test]
        public void TriggerNeed_PublishesNeedTriggeredEvent_WithByPlayerTrue()
        {
            var service = CreateService(out var eventBus); // пустой таймлайн

            service.TriggerNeed(NeedType.Fire, 5, duration: 10f, byPlayer: true);

            var evt = eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Single();
            Assert.AreEqual(NeedType.Fire, evt.Type);
            Assert.AreEqual(5, evt.SiteIndex);
            Assert.IsTrue(evt.ByPlayer);
        }

        [Test]
        public void TriggerNeed_WhileAlreadyActiveOnSameSite_DoesNotPublishSecondEvent()
        {
            var service = CreateService(out var eventBus);
            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true);
            eventBus.PublishedEvents.Clear();

            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true); // "дом уже горит" — штатный no-op

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<NeedTriggeredEvent>());
        }

        [Test]
        public void TriggerNeed_DifferentSiteIndex_TriggersIndependently()
        {
            var service = CreateService(out var eventBus);
            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true);
            eventBus.PublishedEvents.Clear();

            service.TriggerNeed(NeedType.Fire, 1, duration: 10f, byPlayer: true);

            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Count());
        }

        [Test]
        public void TriggerNeed_ExpiresAfterDuration_WithByPlayerTrue()
        {
            var service = CreateService(out var eventBus);
            service.TriggerNeed(NeedType.Fire, 0, duration: 5f, byPlayer: true);
            eventBus.PublishedEvents.Clear();

            service.Tick(5.1f);

            var evt = eventBus.PublishedEvents.OfType<NeedExpiredEvent>().Single();
            Assert.AreEqual(NeedType.Fire, evt.Type);
            Assert.AreEqual(0, evt.SiteIndex);
            Assert.IsTrue(evt.ByPlayer);
        }

        [Test]
        public void ResolveNeed_OnRuntimeTriggeredEntry_PublishesNeedResolvedEvent()
        {
            var service = CreateService(out var eventBus);
            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true);
            eventBus.PublishedEvents.Clear();

            service.ResolveNeed(NeedType.Fire, 0, byPlayer: true);

            var evt = eventBus.PublishedEvents.OfType<NeedResolvedEvent>().Single();
            Assert.AreEqual(NeedType.Fire, evt.Type);
            Assert.AreEqual(0, evt.SiteIndex);
            Assert.IsTrue(evt.ByPlayer);
        }

        [Test]
        public void TriggerNeed_AfterPreviousRuntimeEntryExpired_TriggersAgain()
        {
            var service = CreateService(out var eventBus);
            service.TriggerNeed(NeedType.Fire, 0, duration: 5f, byPlayer: true);
            service.Tick(5.1f); // истекает
            eventBus.PublishedEvents.Clear();

            service.TriggerNeed(NeedType.Fire, 0, duration: 5f, byPlayer: true);

            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Count());
        }

        // --- Незначительное (ревью шага 4): реентерантный TriggerNeed из обработчика Tick'а ---

        [Test]
        public void Tick_TriggerNeedCalledFromNeedTriggeredHandler_DoesNotThrow()
        {
            // FakeEventBus.Publish не рассылает подписчикам (см. FakeEventBus.cs) — для этого теста
            // нужен настоящий синхронный EventBus, чтобы подписчик реально исполнился ВНУТРИ Tick,
            // имитируя NeedSiteController-подобную реакцию на своё же NeedTriggeredEvent.
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 1f, targetSiteIndex = 0, duration = 10f };
            var config = CreateLevelConfig(entry);
            var eventBus = new EventBus();
            var service = new LevelEventService(eventBus, config);

            using (eventBus.Subscribe<NeedTriggeredEvent>(_ =>
                   {
                       // Другой (type, siteIndex) — не штатный no-op "уже активна", а реальное
                       // добавление в _entries во время текущего Tick.
                       service.TriggerNeed(NeedType.Hunger, 99, duration: 5f, byPlayer: true);
                   }))
            {
                Assert.DoesNotThrow(() => service.Tick(1f));
            }
        }
    }
}
