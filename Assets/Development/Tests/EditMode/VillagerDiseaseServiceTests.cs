using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="VillagerDiseaseService"/> (game-design §5, шаг переноса Disease с NeedSite на
    /// случайного жителя). Использует настоящий <see cref="VillagerStateService"/> (не фейк) на общей
    /// шине — VillagerDiseaseService подписан на VillagerStateChangedEvent, который публикует именно
    /// он, тот же приём, что LevelEventServiceTests.CreateServiceWithRealBus.
    /// </summary>
    public class VillagerDiseaseServiceTests
    {
        private static VillagerStateService CreateStateService(EventBus eventBus)
        {
            var config = ScriptableObject.CreateInstance<VillagerConfig>();
            return new VillagerStateService(eventBus, config);
        }

        [Test]
        public void NeedTriggered_Disease_InfectsOneOfRegisteredVillagers()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var idA = VillagerId.FromInstanceId(1);
            var idB = VillagerId.FromInstanceId(2);
            stateService.Register(idA);
            stateService.Register(idB);
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));

            VillagerSicknessChangedEvent? captured = null;
            using (eventBus.Subscribe<VillagerSicknessChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.IsTrue(captured.Value.Id == idA || captured.Value.Id == idB);
            Assert.AreEqual(0, captured.Value.SiteIndex);
            Assert.IsTrue(captured.Value.IsSick);

            Assert.IsTrue(service.IsSick(captured.Value.Id));
            Assert.IsTrue(service.TryGetActiveNeed(captured.Value.Id, out var needType, out var site));
            Assert.AreEqual(NeedType.Disease, needType);
            Assert.AreEqual(0, site);
            Assert.IsTrue(service.TryGetSickVillager(0, out var sick));
            Assert.AreEqual(captured.Value.Id, sick);
        }

        [Test]
        public void NeedTriggered_Disease_TossedVillagerIsNotSelected()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var tossed = VillagerId.FromInstanceId(1);
            var eligible = VillagerId.FromInstanceId(2);
            stateService.Register(tossed);
            stateService.Register(eligible);
            stateService.ReportStimulus(tossed, VillagerStimulus.Grabbed); // -> Tossed, занят рукой
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default));

            Assert.IsTrue(service.TryGetSickVillager(0, out var sick));
            Assert.AreEqual(eligible, sick);
        }

        [Test]
        public void NeedTriggered_Disease_AlreadySickVillagerIsNotSelectedAgain()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var alreadySick = VillagerId.FromInstanceId(1);
            var eligible = VillagerId.FromInstanceId(2);
            stateService.Register(alreadySick);
            stateService.Register(eligible);
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));
            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default));
            service.TryGetSickVillager(0, out var firstSick);

            // Второй сайт заражает — не должен повторно выбрать уже больного первым сайтом жителя.
            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 1, false, default));

            Assert.IsTrue(service.TryGetSickVillager(1, out var secondSick));
            Assert.AreNotEqual(firstSick, secondSick);
        }

        [Test]
        public void NeedTriggered_Disease_NoEligibleCandidates_DefersUntilVillagerLands()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var id = VillagerId.FromInstanceId(1);
            stateService.Register(id);
            stateService.ReportStimulus(id, VillagerStimulus.Grabbed); // -> Tossed, единственный житель — кандидатов нет
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default));

            Assert.IsFalse(service.TryGetSickVillager(0, out _)); // отложено

            stateService.ReportStimulus(id, VillagerStimulus.Landed); // -> Idle, публикует VillagerStateChangedEvent

            Assert.IsTrue(service.TryGetSickVillager(0, out var sick));
            Assert.AreEqual(id, sick);
        }

        [Test]
        public void NeedTriggered_Disease_RepeatedOnSameSiteWhileSick_IsNoOp()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var idA = VillagerId.FromInstanceId(1);
            var idB = VillagerId.FromInstanceId(2);
            stateService.Register(idA);
            stateService.Register(idB);
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));
            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default));
            service.TryGetSickVillager(0, out var firstSick);

            var events = new List<VillagerSicknessChangedEvent>();
            using (eventBus.Subscribe<VillagerSicknessChangedEvent>(e => events.Add(e)))
            {
                // LevelEventService не допускает двух Active на один ключ — защитная проверка на
                // случай прямого вызова/будущего изменения вызывающего.
                eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default));
            }

            Assert.IsEmpty(events);
            Assert.IsTrue(service.TryGetSickVillager(0, out var stillSick));
            Assert.AreEqual(firstSick, stillSick);
        }

        [Test]
        public void NeedResolved_Disease_PublishesRecoveryEvent_AndClearsSickState()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var id = VillagerId.FromInstanceId(1);
            stateService.Register(id);
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));
            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default));

            VillagerSicknessChangedEvent? captured = null;
            using (eventBus.Subscribe<VillagerSicknessChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new NeedResolvedEvent(NeedType.Disease, 0, true, MiracleType.Heal));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(id, captured.Value.Id);
            Assert.AreEqual(0, captured.Value.SiteIndex);
            Assert.IsFalse(captured.Value.IsSick);

            Assert.IsFalse(service.IsSick(id));
            Assert.IsFalse(service.TryGetSickVillager(0, out _));
        }

        [Test]
        public void NeedExpired_Disease_PublishesRecoveryEvent_SameAsResolve()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var id = VillagerId.FromInstanceId(1);
            stateService.Register(id);
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));
            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default));

            VillagerSicknessChangedEvent? captured = null;
            using (eventBus.Subscribe<VillagerSicknessChangedEvent>(e => captured = e))
            {
                // Истечение = выздоровление без последствий (решение владельца 2026-09-12).
                eventBus.Publish(new NeedExpiredEvent(NeedType.Disease, 0, false));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(id, captured.Value.Id);
            Assert.IsFalse(captured.Value.IsSick);
            Assert.IsFalse(service.IsSick(id));
        }

        [Test]
        public void NeedResolved_DeferredSite_ClearsDeferral_SubsequentStateChangeDoesNotInfect()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var id = VillagerId.FromInstanceId(1);
            stateService.Register(id);
            stateService.ReportStimulus(id, VillagerStimulus.Grabbed); // -> Tossed, нет кандидатов
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default)); // откладывается (Armed-аналог)
            Assert.IsFalse(service.TryGetSickVillager(0, out _));

            // Нужда закрылась раньше, чем нашёлся кандидат.
            eventBus.Publish(new NeedResolvedEvent(NeedType.Disease, 0, true, MiracleType.Heal));

            var events = new List<VillagerSicknessChangedEvent>();
            using (eventBus.Subscribe<VillagerSicknessChangedEvent>(e => events.Add(e)))
            {
                stateService.ReportStimulus(id, VillagerStimulus.Landed); // -> Idle, публикует VillagerStateChangedEvent
            }

            Assert.IsEmpty(events); // отложка снята — заражения не происходит
        }

        [Test]
        public void NeedExpired_DeferredSite_ClearsDeferral_SubsequentStateChangeDoesNotInfect()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var id = VillagerId.FromInstanceId(1);
            stateService.Register(id);
            stateService.ReportStimulus(id, VillagerStimulus.Grabbed); // -> Tossed, нет кандидатов
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default));
            eventBus.Publish(new NeedExpiredEvent(NeedType.Disease, 0, false));

            var events = new List<VillagerSicknessChangedEvent>();
            using (eventBus.Subscribe<VillagerSicknessChangedEvent>(e => events.Add(e)))
            {
                stateService.ReportStimulus(id, VillagerStimulus.Landed);
            }

            Assert.IsEmpty(events);
        }

        [TestCase(NeedType.Fire)]
        [TestCase(NeedType.Hunger)]
        public void NeedTriggered_NonDisease_IsIgnored(NeedType type)
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var id = VillagerId.FromInstanceId(1);
            stateService.Register(id);
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));

            var events = new List<VillagerSicknessChangedEvent>();
            using (eventBus.Subscribe<VillagerSicknessChangedEvent>(e => events.Add(e)))
            {
                eventBus.Publish(new NeedTriggeredEvent(type, 0, false, default));
            }

            Assert.IsEmpty(events);
            Assert.IsFalse(service.TryGetSickVillager(0, out _));
        }

        [TestCase(NeedType.Fire)]
        [TestCase(NeedType.Hunger)]
        public void NeedResolvedAndExpired_NonDisease_IsIgnored(NeedType type)
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));

            var events = new List<VillagerSicknessChangedEvent>();
            using (eventBus.Subscribe<VillagerSicknessChangedEvent>(e => events.Add(e)))
            {
                eventBus.Publish(new NeedResolvedEvent(type, 0, true, MiracleType.Rain));
                eventBus.Publish(new NeedExpiredEvent(type, 0, false));
            }

            Assert.IsEmpty(events);
        }

        [Test]
        public void IsSick_UnknownVillager_ReturnsFalse()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));

            Assert.IsFalse(service.IsSick(VillagerId.FromInstanceId(999)));
            Assert.IsFalse(service.TryGetActiveNeed(VillagerId.FromInstanceId(999), out _, out _));
        }

        [Test]
        public void Dispose_EventsNoLongerAffectService()
        {
            var eventBus = new EventBus();
            var stateService = CreateStateService(eventBus);
            var id = VillagerId.FromInstanceId(1);
            stateService.Register(id);
            var service = new VillagerDiseaseService(eventBus, stateService, new System.Random(1));

            service.Dispose();

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 0, false, default));

            Assert.IsFalse(service.TryGetSickVillager(0, out _));
        }
    }
}
