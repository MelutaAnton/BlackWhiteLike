using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="LevelEventService"/> (game-design §5, §6, §19, шаг 3, шаг 5).</summary>
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

        /// <summary>
        /// Шаг 10: AfterVillagerTossed/AfterPreviousResolved взводятся РЕАЛЬНОЙ подпиской сервиса на
        /// шину (см. LevelEventService.ctor) — FakeEventBus.Subscribe не рассылает (см. FakeEventBus.cs),
        /// поэтому для этих сценариев нужен настоящий <see cref="EventBus"/>, тот же приём, что
        /// WorshipServiceTests.
        /// </summary>
        private static LevelEventService CreateServiceWithRealBus(out EventBus eventBus, params NeedEventEntry[] timeline)
        {
            eventBus = new EventBus();
            var config = CreateLevelConfig(timeline);
            return new LevelEventService(eventBus, config);
        }

        // --- Playtest-фикс шага 10 (2026-09-11): "бросок жителя рукой" теперь пара ObjectGrabbedEvent
        // + ObjectThrownEvent (VillagerTossRules.IsVillagerThrow), не VillagerStateChangedEvent — тот
        // детектил сам ЗАХВАТ (FSM жителя входит в Tossed уже при захвате), а не бросок. ---

        private static readonly GrabbableId TestVillagerGrabId = GrabbableId.FromInstanceId(1);
        private static readonly Vector3 ThrowVelocity = new Vector3(5f, 0f, 0f); // заведомо выше ThrowVelocityEpsilonSqr
        private static readonly Vector3 SoftPlacementVelocity = Vector3.zero; // мягкая постановка — не бросок

        /// <summary>Публикует полную пару "захват жителя -> реальный бросок" (см. блок выше).</summary>
        private static void PublishVillagerThrow(EventBus eventBus)
        {
            eventBus.Publish(new ObjectGrabbedEvent(TestVillagerGrabId, GrabbableKind.Villager));
            eventBus.Publish(new ObjectThrownEvent(TestVillagerGrabId, ThrowVelocity));
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

            service.ResolveNeed(NeedType.Hunger, 3, byPlayer: true, causeMiracle: MiracleType.Rain);

            var evt = eventBus.PublishedEvents.OfType<NeedResolvedEvent>().Single();
            Assert.AreEqual(NeedType.Hunger, evt.Type);
            Assert.AreEqual(3, evt.SiteIndex);
            Assert.IsTrue(evt.ByPlayer);
        }

        [Test]
        public void ResolveNeed_PublishesCauseMiracle()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 1f, targetSiteIndex = 0, duration = 10f };
            var service = CreateService(out var eventBus, entry);
            service.Tick(1f);
            eventBus.PublishedEvents.Clear();

            service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);

            var evt = eventBus.PublishedEvents.OfType<NeedResolvedEvent>().Single();
            Assert.AreEqual(MiracleType.Rain, evt.CauseMiracle);
        }

        [Test]
        public void ResolveNeed_NotByPlayer_PublishesFalse()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 0f, targetSiteIndex = 0, duration = 10f };
            var service = CreateService(out var eventBus, entry);
            service.Tick(0f);
            eventBus.PublishedEvents.Clear();

            service.ResolveNeed(NeedType.Fire, 0, byPlayer: false, causeMiracle: default);

            var evt = eventBus.PublishedEvents.OfType<NeedResolvedEvent>().Single();
            Assert.IsFalse(evt.ByPlayer);
        }

        [Test]
        public void ResolveNeed_NoActiveMatch_DoesNotPublish()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 5f, targetSiteIndex = 0, duration = 10f };
            var service = CreateService(out var eventBus, entry);
            // Ещё не сработало (elapsed < triggerTime) — активной нужды нет.

            service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);

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
            service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);
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

            service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<NeedResolvedEvent>());
        }

        // --- Шаг 4: TriggerNeed — рантайм-запись поверх таймлайна (например, Fireball поджёг дом) ---

        [Test]
        public void TriggerNeed_PublishesNeedTriggeredEvent_WithByPlayerTrue()
        {
            var service = CreateService(out var eventBus); // пустой таймлайн

            service.TriggerNeed(NeedType.Fire, 5, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);

            var evt = eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Single();
            Assert.AreEqual(NeedType.Fire, evt.Type);
            Assert.AreEqual(5, evt.SiteIndex);
            Assert.IsTrue(evt.ByPlayer);
        }

        [Test]
        public void TriggerNeed_PublishesCauseMiracle()
        {
            var service = CreateService(out var eventBus);

            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);

            var evt = eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Single();
            Assert.AreEqual(MiracleType.Fireball, evt.CauseMiracle);
        }

        [Test]
        public void TriggerNeed_WhileAlreadyActiveOnSameSite_DoesNotPublishSecondEvent()
        {
            var service = CreateService(out var eventBus);
            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);
            eventBus.PublishedEvents.Clear();

            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball); // "дом уже горит" — штатный no-op

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<NeedTriggeredEvent>());
        }

        [Test]
        public void TriggerNeed_DifferentSiteIndex_TriggersIndependently()
        {
            var service = CreateService(out var eventBus);
            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);
            eventBus.PublishedEvents.Clear();

            service.TriggerNeed(NeedType.Fire, 1, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);

            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Count());
        }

        [Test]
        public void TriggerNeed_ExpiresAfterDuration_WithByPlayerTrue()
        {
            var service = CreateService(out var eventBus);
            service.TriggerNeed(NeedType.Fire, 0, duration: 5f, byPlayer: true, causeMiracle: MiracleType.Fireball);
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
            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);
            eventBus.PublishedEvents.Clear();

            service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);

            var evt = eventBus.PublishedEvents.OfType<NeedResolvedEvent>().Single();
            Assert.AreEqual(NeedType.Fire, evt.Type);
            Assert.AreEqual(0, evt.SiteIndex);
            Assert.IsTrue(evt.ByPlayer);
        }

        [Test]
        public void TriggerNeed_AfterPreviousRuntimeEntryExpired_TriggersAgain()
        {
            var service = CreateService(out var eventBus);
            service.TriggerNeed(NeedType.Fire, 0, duration: 5f, byPlayer: true, causeMiracle: MiracleType.Fireball);
            service.Tick(5.1f); // истекает
            eventBus.PublishedEvents.Clear();

            service.TriggerNeed(NeedType.Fire, 0, duration: 5f, byPlayer: true, causeMiracle: MiracleType.Fireball);

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
                       service.TriggerNeed(NeedType.Hunger, 99, duration: 5f, byPlayer: true, causeMiracle: MiracleType.Fireball);
                   }))
            {
                Assert.DoesNotThrow(() => service.Tick(1f));
            }
        }

        // --- Шаг 10: NeedTriggerMode.AtTime — поведение бит-в-бит как раньше (явный TriggerMode) ---

        [Test]
        public void AtTime_ExplicitTriggerMode_BehavesSameAsDefault()
        {
            var entry = new NeedEventEntry
                { type = NeedType.Fire, triggerTime = 5f, targetSiteIndex = 2, duration = 10f, TriggerMode = NeedTriggerMode.AtTime };
            var service = CreateService(out var eventBus, entry);

            service.Tick(5f);

            var evt = eventBus.PublishedEvents.OfType<NeedTriggeredEvent>().Single();
            Assert.AreEqual(NeedType.Fire, evt.Type);
            Assert.AreEqual(2, evt.SiteIndex);
        }

        // --- Шаг 10: NeedTriggerMode.AfterVillagerTossed — взводится первым броском жителя рукой ---

        [Test]
        public void AfterVillagerTossed_DoesNotTriggerByTime()
        {
            var entry = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, entry);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                service.Tick(1_000_000f); // сколько угодно времени — triggerTime для этого режима не читается
            }

            Assert.IsEmpty(triggered);
        }

        [Test]
        public void AfterVillagerTossed_TriggersImmediately_OnVillagerTossedByHandEvent()
        {
            var entry = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 2, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, entry);

            NeedTriggeredEvent? captured = null;
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => captured = e))
            {
                PublishVillagerThrow(eventBus);
            }

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(NeedType.Fire, captured.Value.Type);
            Assert.AreEqual(2, captured.Value.SiteIndex);
            Assert.IsFalse(captured.Value.ByPlayer); // скриптованная запись таймлайна — не рантайм-триггер игрока
        }

        [Test]
        public void AfterVillagerTossed_GrabWithoutThrow_DoesNotTrigger()
        {
            // Регресс playtest-бага шага 10 (2026-09-11): ЗАХВАТ сам по себе — не бросок. Раньше
            // (VillagerStateChangedEvent, To=Tossed) этот же момент ложно взводил запись — дом
            // "загорался" от одного взятия жителя в руку.
            var entry = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, entry);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                eventBus.Publish(new ObjectGrabbedEvent(TestVillagerGrabId, GrabbableKind.Villager)); // только захват, без отпускания
            }

            Assert.IsEmpty(triggered);
        }

        [Test]
        public void AfterVillagerTossed_SoftPlacement_DoesNotTrigger()
        {
            // Мягкая постановка (ObjectThrownEvent.Velocity ~ ноль) — не бросок.
            var entry = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, entry);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                eventBus.Publish(new ObjectGrabbedEvent(TestVillagerGrabId, GrabbableKind.Villager));
                eventBus.Publish(new ObjectThrownEvent(TestVillagerGrabId, SoftPlacementVelocity));
            }

            Assert.IsEmpty(triggered);
        }

        [Test]
        public void AfterVillagerTossed_ThrowableObjectThrown_DoesNotTrigger()
        {
            // Бросок Throwable-предмета (бочка/камень) — не житель, VillagerTossRules.IsVillagerThrow
            // фильтрует по GrabbableKind.
            var entry = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, entry);
            var throwableId = GrabbableId.FromInstanceId(2);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                eventBus.Publish(new ObjectGrabbedEvent(throwableId, GrabbableKind.Throwable));
                eventBus.Publish(new ObjectThrownEvent(throwableId, ThrowVelocity));
            }

            Assert.IsEmpty(triggered);
        }

        // --- Шаг 10: NeedTriggerMode.AfterPreviousResolved — взводится разрешением предыдущей записи ---

        [Test]
        public void AfterPreviousResolved_TriggersOnlyAfterSpecificPreviousEntryResolved()
        {
            var previous = new NeedEventEntry
                { type = NeedType.Fire, triggerTime = 0f, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AtTime };
            var next = new NeedEventEntry
                { type = NeedType.Disease, targetSiteIndex = 1, duration = 10f, TriggerMode = NeedTriggerMode.AfterPreviousResolved };
            var service = CreateServiceWithRealBus(out var eventBus, previous, next);
            service.Tick(0f); // взводит previous (Fire) по времени

            var diseaseTriggers = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e =>
                   {
                       if (e.Type == NeedType.Disease) diseaseTriggers.Add(e);
                   }))
            {
                // Разрешение ЧУЖОЙ записи (не previous по (type, siteIndex)) — next не взводится.
                eventBus.Publish(new NeedResolvedEvent(NeedType.Hunger, 99, byPlayer: false, default));
                Assert.IsEmpty(diseaseTriggers);

                // Разрешение previous — взводит next немедленно.
                service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);
            }

            Assert.AreEqual(1, diseaseTriggers.Count);
            Assert.AreEqual(1, diseaseTriggers[0].SiteIndex);
        }

        [Test]
        public void AfterPreviousResolved_FirstEntryInTimeline_LogsWarning_NeverTriggers()
        {
            // Первая запись не может ждать разрешения несуществующей предыдущей — конфигурационная
            // ошибка (game-design §6), сервис логирует предупреждение и оставляет запись Pending навсегда.
            var entry = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterPreviousResolved };

            LogAssert.Expect(LogType.Warning, new Regex("AfterPreviousResolved"));
            var service = CreateServiceWithRealBus(out var eventBus, entry);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            }

            Assert.IsEmpty(triggered); // запись так и не взводится — навсегда остаётся Pending
        }

        // --- Шаг 10: duration <= 0 — нужда не истекает никогда, для любого TriggerMode ---

        [TestCase(0f)]
        [TestCase(-5f)]
        public void Duration_LessOrEqualZero_NeverExpires_AtTime(float duration)
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 0f, targetSiteIndex = 0, duration = duration };
            var service = CreateService(out var eventBus, entry);
            service.Tick(0f); // trigger
            eventBus.PublishedEvents.Clear();

            service.Tick(1_000_000f); // сколько угодно времени

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<NeedExpiredEvent>());
        }

        [Test]
        public void Duration_LessOrEqualZero_NeverExpires_AfterVillagerTossed()
        {
            var entry = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 0f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, entry);

            var triggered = new List<NeedTriggeredEvent>();
            var expired = new List<NeedExpiredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            using (eventBus.Subscribe<NeedExpiredEvent>(e => expired.Add(e)))
            {
                PublishVillagerThrow(eventBus);
                Assert.AreEqual(1, triggered.Count);

                service.Tick(1_000_000f);

                Assert.IsEmpty(expired); // не истекла
            }
        }

        // --- Шаг 10: полная цепочка — Toss->Fire, Fire resolved->Disease, Disease resolved->Hunger ---

        [Test]
        public void Chain_TossThenFireResolvedThenDiseaseResolved_TriggersEachStepInOrder()
        {
            var fire = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 0f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var disease = new NeedEventEntry
                { type = NeedType.Disease, targetSiteIndex = 1, duration = 0f, TriggerMode = NeedTriggerMode.AfterPreviousResolved };
            var hunger = new NeedEventEntry
                { type = NeedType.Hunger, targetSiteIndex = 2, duration = 0f, TriggerMode = NeedTriggerMode.AfterPreviousResolved };
            var service = CreateServiceWithRealBus(out var eventBus, fire, disease, hunger);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                // Шаг 1: бросок жителя рукой взводит Fire — Disease/Hunger ещё Pending.
                PublishVillagerThrow(eventBus);
                Assert.AreEqual(1, triggered.Count);
                Assert.AreEqual(NeedType.Fire, triggered[0].Type);

                // Шаг 2: разрешение Fire взводит Disease.
                service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);
                Assert.AreEqual(2, triggered.Count);
                Assert.AreEqual(NeedType.Disease, triggered[1].Type);

                // Шаг 3: разрешение Disease взводит Hunger.
                service.ResolveNeed(NeedType.Disease, 1, byPlayer: true, causeMiracle: MiracleType.Heal);
                Assert.AreEqual(3, triggered.Count);
                Assert.AreEqual(NeedType.Hunger, triggered[2].Type);
            }

            // duration = 0 на каждой записи — цепочка не истекает сама по себе, сколько ни тикай.
            // Hunger остаётся Active, ждёт явного ResolveNeed — проверяем через отсутствие NeedExpiredEvent.
            var expired = new List<NeedExpiredEvent>();
            using (eventBus.Subscribe<NeedExpiredEvent>(e => expired.Add(e)))
            {
                service.Tick(1_000_000f);
            }

            Assert.IsEmpty(expired);
        }

        // --- Блокер ревью шага 10: сопоставление AfterPreviousResolved по ССЫЛКЕ на объект, не по копии ключа ---

        [Test]
        public void AfterPreviousResolved_RuntimeEntryWithSameKeyResolvedBeforePrevious_DoesNotTriggerNext()
        {
            // previous ещё Pending (житель не брошен) — next (AfterPreviousResolved) не должен
            // взводиться, даже если рантайм-запись TriggerNeed с ТЕМ ЖЕ (type, siteIndex), что у
            // previous, создаётся и резолвится раньше самой previous. Сценарий блокера: Fireball
            // поджигает дом (TriggerNeed) до скриптованного взведения Fire@0, Rain тушит именно
            // рантайм-запись (она Active, previous ещё Pending) — Disease не должен взвестись.
            var previous = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var next = new NeedEventEntry
                { type = NeedType.Disease, targetSiteIndex = 1, duration = 10f, TriggerMode = NeedTriggerMode.AfterPreviousResolved };
            var service = CreateServiceWithRealBus(out var eventBus, previous, next);

            var diseaseTriggers = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e =>
                   {
                       if (e.Type == NeedType.Disease) diseaseTriggers.Add(e);
                   }))
            {
                // Рантайм-запись Fire@0 — ДРУГОЙ EntryState, чем скриптованная previous (та ещё Pending,
                // житель не брошен).
                service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);

                // Резолвит именно рантайм-запись (она Active), а не previous (Pending, не Active —
                // ResolveNeed её не находит).
                service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);
            }

            Assert.IsEmpty(diseaseTriggers); // регресс блокера: без фикса Disease взводился бы по совпадению ключа
        }

        // --- Ревью шага 10 (важное): Dispose реально отписывается от шины ---

        [Test]
        public void Dispose_EventsNoLongerTriggerEntries()
        {
            var entry = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, entry);

            service.Dispose();

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                PublishVillagerThrow(eventBus);
            }

            Assert.IsEmpty(triggered);
        }

        // --- Ревью шага 10 (важное): LevelCompletedEvent блокирует дальнейшее событийное взведение ---

        [Test]
        public void LevelCompleted_VillagerTossedEvent_DoesNotTriggerEntry()
        {
            var entry = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, entry);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                eventBus.Publish(new LevelCompletedEvent(default, 0, 0, 0f));
                PublishVillagerThrow(eventBus);
            }

            Assert.IsEmpty(triggered);
        }

        [Test]
        public void LevelCompleted_NeedResolvedEvent_DoesNotTriggerNextInChain()
        {
            var previous = new NeedEventEntry
                { type = NeedType.Fire, triggerTime = 0f, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AtTime };
            var next = new NeedEventEntry
                { type = NeedType.Disease, targetSiteIndex = 1, duration = 10f, TriggerMode = NeedTriggerMode.AfterPreviousResolved };
            var service = CreateServiceWithRealBus(out var eventBus, previous, next);
            service.Tick(0f); // взводит previous (Fire) по времени

            eventBus.Publish(new LevelCompletedEvent(default, 0, 0, 0f));

            var diseaseTriggers = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e =>
                   {
                       if (e.Type == NeedType.Disease) diseaseTriggers.Add(e);
                   }))
            {
                service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);
            }

            Assert.IsEmpty(diseaseTriggers);
        }

        // --- Ревью шага 10 (важное): двойная активация ключа — отложенное (Armed) взведение ---

        [Test]
        public void DoubleActivation_ScriptedEntryDeferred_ActivatesWhenRuntimeEntryResolved()
        {
            // Блокер: Fireball@site0 создаёт рантайм-нужду ДО того, как скриптованная запись на тот
            // же ключ (Fire, site0) взводится броском жителя — она НЕ должна создать вторую Active-
            // запись на тот же ключ, а должна дождаться освобождения (Rain резолвит рантайм-нужду).
            var scripted = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, scripted);

            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                // Условие AfterVillagerTossed наступило, но ключ занят рантайм-записью — деферится (Armed).
                PublishVillagerThrow(eventBus);

                Assert.IsEmpty(triggered); // скриптованная запись НЕ активировалась — ключ занят

                // Rain резолвит именно рантайм-запись — освобождает ключ.
                service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);
            }

            Assert.AreEqual(1, triggered.Count); // скриптованная запись активировалась НЕМЕДЛЕННО при освобождении
            Assert.AreEqual(0, triggered[0].SiteIndex);
            Assert.IsFalse(triggered[0].ByPlayer); // скриптованная запись — не рантайм-триггер игрока
        }

        [Test]
        public void DoubleActivation_ScriptedEntryDeferred_ActivatesWhenRuntimeEntryExpiresByDuration()
        {
            // Вариант блокера: ключ освобождается не разрешением, а истечением duration рантайм-записи.
            var scripted = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, scripted);

            service.TriggerNeed(NeedType.Fire, 0, duration: 5f, byPlayer: true, causeMiracle: MiracleType.Fireball);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                PublishVillagerThrow(eventBus);
                Assert.IsEmpty(triggered);

                service.Tick(5.1f); // рантайм-запись истекает — ключ освобождается
            }

            Assert.AreEqual(1, triggered.Count); // скриптованная запись теперь держит ключ
            Assert.IsFalse(triggered[0].ByPlayer);
        }

        [Test]
        public void DoubleActivation_KeyFreedByUnrelatedResolve_DoesNotActivateArmedEntry()
        {
            // Освобождение ЧУЖОГО ключа не должно затрагивать Armed-запись, ждущую другой ключ.
            var scripted = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var unrelated = new NeedEventEntry
                { type = NeedType.Hunger, triggerTime = 0f, targetSiteIndex = 9, duration = 10f };
            var service = CreateServiceWithRealBus(out var eventBus, scripted, unrelated);

            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);
            service.Tick(0f); // взводит unrelated (AtTime)

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                PublishVillagerThrow(eventBus);
                Assert.IsEmpty(triggered); // scripted деферится (Armed)

                service.ResolveNeed(NeedType.Hunger, 9, byPlayer: true, causeMiracle: MiracleType.Rain); // чужой ключ
            }

            Assert.IsEmpty(triggered); // scripted всё ещё Armed — ключ (Fire, 0) не освобождён
        }

        // --- Ревью шага 10 (решение владельца): HasBlockingNeeds — soft-fail достижим на L01 ---

        [Test]
        public void HasBlockingNeeds_EmptyTimeline_False()
        {
            var service = CreateService(out _);

            Assert.IsFalse(service.HasBlockingNeeds);
        }

        [Test]
        public void HasBlockingNeeds_ActiveEntryWithPositiveDuration_True()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 0f, targetSiteIndex = 0, duration = 15f };
            var service = CreateService(out _, entry);
            service.Tick(0f); // trigger

            Assert.IsTrue(service.HasBlockingNeeds);
        }

        [Test]
        public void HasBlockingNeeds_ActiveEntryWithZeroDuration_False()
        {
            // L01: вечная нужда (duration <= 0) не блокирует сталл — иначе soft-fail недостижим,
            // пока игрок сам её не разрешит.
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 0f, targetSiteIndex = 0, duration = 0f };
            var service = CreateService(out _, entry);
            service.Tick(0f); // trigger — запись стала Active (вечная, duration <= 0)

            Assert.IsFalse(service.HasBlockingNeeds); // не блокирует сталл
        }

        [Test]
        public void HasBlockingNeeds_PendingAtTimeEntry_True()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 100f, targetSiteIndex = 0, duration = 10f };
            var service = CreateService(out _, entry);
            // ещё не наступило — Pending, TriggerMode == AtTime (дефолт).

            Assert.IsTrue(service.HasBlockingNeeds);
        }

        [Test]
        public void HasBlockingNeeds_PendingEventTriggerModeEntry_False()
        {
            // L01: запись цепочки (AfterPreviousResolved), ждущая разрешения предыдущей, не блокирует
            // сталл — иначе soft-fail недостижим, пока игрок не разрешит первую нужду вручную.
            var entry = new NeedEventEntry
                { type = NeedType.Disease, targetSiteIndex = 1, duration = 10f, TriggerMode = NeedTriggerMode.AfterPreviousResolved };
            var service = CreateService(out _, entry);
            // Запись остаётся Pending (ждёт разрешения предыдущей, не время).

            Assert.IsFalse(service.HasBlockingNeeds); // не блокирует сталл
        }

        [Test]
        public void HasBlockingNeeds_ResolvedEntry_False()
        {
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 0f, targetSiteIndex = 0, duration = 15f };
            var service = CreateService(out _, entry);
            service.Tick(0f);

            service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);

            Assert.IsFalse(service.HasBlockingNeeds);
        }

        [Test]
        public void HasBlockingNeeds_AtTimeEntryWithLevel2To5Duration_TrueBothPendingAndActive()
        {
            // Уровни 2-5: все записи AtTime с duration 15 — HasBlockingNeeds остаётся true и в Pending
            // (ждёт своего triggerTime), и в Active (истечёт сама) состоянии — профиль таймлайна L2-5
            // не должен сломаться (зафиксировано тестом по требованию оркестратора шага 10).
            var entry = new NeedEventEntry { type = NeedType.Fire, triggerTime = 0f, targetSiteIndex = 0, duration = 15f };
            var service = CreateService(out _, entry);

            Assert.IsTrue(service.HasBlockingNeeds); // Pending, AtTime — блокирует

            service.Tick(0f); // trigger
            Assert.IsTrue(service.HasBlockingNeeds); // Active, duration>0 — блокирует
        }

        // --- Добивка ревью шага 10: недостающие Armed-сценарии ---

        [Test]
        public void DoubleActivation_TwoArmedEntriesSameKey_ActivateInEntryOrder()
        {
            // Две скриптованные записи на ОДИН ключ (Fire, 0), обе AfterVillagerTossed — обе деферятся
            // в Armed, пока ключ занят рантайм-записью. При освобождении ключа активируется ПЕРВАЯ по
            // порядку в _entries (порядок таймлайна) — вторая остаётся Armed, ждёт следующего
            // освобождения (см. TryActivateArmedEntries — переиспользует ActivateEntry, который
            // проверяет занятость среди ВСЕХ записей, включая только что активированные в этом же проходе).
            var scripted1 = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var scripted2 = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, scripted1, scripted2);

            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                // Условие AfterVillagerTossed наступило для обеих — ключ занят рантайм-записью, обе Armed.
                PublishVillagerThrow(eventBus);
                Assert.IsEmpty(triggered);

                // Освобождение ключа рантайм-записью — активируется только ПЕРВАЯ Armed-запись.
                service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);
                Assert.AreEqual(1, triggered.Count); // scripted1 активировалась, scripted2 всё ещё Armed

                // Освобождение ключа scripted1 — теперь активируется scripted2 (вторая по очереди).
                service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain);
            }

            Assert.AreEqual(2, triggered.Count);
        }

        [Test]
        public void Armed_LevelFinished_DoesNotActivateWhenKeyFreed()
        {
            // Armed-запись НЕ активируется после LevelCompletedEvent, даже когда ключ, который она
            // ждёт, освобождается — TryActivateArmedEntries гейтится _levelFinished (как и
            // OnVillagerStateChanged/OnNeedResolved).
            var scripted = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, scripted);

            service.TriggerNeed(NeedType.Fire, 0, duration: 10f, byPlayer: true, causeMiracle: MiracleType.Fireball);

            var triggered = new List<NeedTriggeredEvent>();
            using (eventBus.Subscribe<NeedTriggeredEvent>(e => triggered.Add(e)))
            {
                PublishVillagerThrow(eventBus);
                Assert.IsEmpty(triggered); // scripted деферится (Armed) — ключ занят рантайм-записью

                eventBus.Publish(new LevelCompletedEvent(default, 0, 0, 0f));

                service.ResolveNeed(NeedType.Fire, 0, byPlayer: true, causeMiracle: MiracleType.Rain); // освобождает ключ
            }

            Assert.IsEmpty(triggered); // уровень завершён — Armed-запись НЕ активировалась, несмотря на освобождение ключа
        }

        [Test]
        public void HasBlockingNeeds_ArmedEntry_False()
        {
            // Armed-запись сама по себе не входит ни в Active, ни в Pending — HasBlockingNeeds её не
            // учитывает. Запись, держащая ключ, к тому же бесконечна (duration <= 0) — итог false для
            // обеих, осознанно: бесконечная нужда разрешается только явным действием игрока (Faith на
            // чудо), а не временем.
            var scripted = new NeedEventEntry
                { type = NeedType.Fire, targetSiteIndex = 0, duration = 10f, TriggerMode = NeedTriggerMode.AfterVillagerTossed };
            var service = CreateServiceWithRealBus(out var eventBus, scripted);

            service.TriggerNeed(NeedType.Fire, 0, duration: 0f, byPlayer: true, causeMiracle: MiracleType.Fireball); // бесконечная

            PublishVillagerThrow(eventBus);
            // scripted -> Armed (ключ занят holder'ом, который сам никогда не истечёт).

            Assert.IsFalse(service.HasBlockingNeeds);
        }
    }
}
