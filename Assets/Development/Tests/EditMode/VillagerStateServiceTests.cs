using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="VillagerStateService"/> (game-design §5, §19, шаг 3). Длительности
    /// Panic/Worship/Stunned берутся из дефолтов <see cref="VillagerConfig"/> (4с/3с/2с
    /// соответственно) — тесты сознательно не подкручивают приватные поля конфига, чтобы не плодить
    /// reflection-хаки там, где хватает документированных дефолтов SO.
    /// </summary>
    public class VillagerStateServiceTests
    {
        private static VillagerStateService CreateService(out FakeEventBus eventBus)
        {
            eventBus = new FakeEventBus();
            var config = ScriptableObject.CreateInstance<VillagerConfig>();
            return new VillagerStateService(eventBus, config);
        }

        [Test]
        public void Register_NewVillager_StartsIdle()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);

            service.Register(id);

            Assert.AreEqual(VillagerState.Idle, service.GetState(id));
        }

        [Test]
        public void ReportStimulus_FireNearby_FromIdle_TransitionsToPanic_AndPublishesEvent()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);

            service.ReportStimulus(id, VillagerStimulus.FireNearby);

            Assert.AreEqual(VillagerState.Panic, service.GetState(id));
            var evt = eventBus.PublishedEvents.OfType<VillagerStateChangedEvent>().Single();
            Assert.AreEqual(id, evt.Id);
            Assert.AreEqual(VillagerState.Idle, evt.From);
            Assert.AreEqual(VillagerState.Panic, evt.To);
        }

        [Test]
        public void ReportStimulus_ExplosionNearby_FromWorship_TransitionsToPanic()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby); // -> Worship

            service.ReportStimulus(id, VillagerStimulus.ExplosionNearby);

            Assert.AreEqual(VillagerState.Panic, service.GetState(id));
        }

        [Test]
        public void ReportStimulus_NeedResolvedNearby_FromIdle_TransitionsToWorship()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);

            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby);

            Assert.AreEqual(VillagerState.Worship, service.GetState(id));
        }

        [Test]
        public void ReportStimulus_NeedResolvedNearby_FromPanic_TransitionsToWorship()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.FireNearby); // -> Panic

            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby);

            Assert.AreEqual(VillagerState.Worship, service.GetState(id));
        }

        [Test]
        public void ReportStimulus_Grabbed_FromAnyState_TransitionsToTossed()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby); // -> Worship

            service.ReportStimulus(id, VillagerStimulus.Grabbed);

            Assert.AreEqual(VillagerState.Tossed, service.GetState(id));
        }

        [Test]
        public void ReportStimulus_Landed_FromTossed_TransitionsToIdle()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.Grabbed); // -> Tossed

            service.ReportStimulus(id, VillagerStimulus.Landed);

            Assert.AreEqual(VillagerState.Idle, service.GetState(id));
        }

        [Test]
        public void ReportStimulus_HardLanded_FromTossed_TransitionsToStunned()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.Grabbed); // -> Tossed

            service.ReportStimulus(id, VillagerStimulus.HardLanded);

            Assert.AreEqual(VillagerState.Stunned, service.GetState(id));
        }

        [Test]
        public void ReportStimulus_FireNearby_WhileTossed_IsIgnored()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.Grabbed); // -> Tossed
            eventBus.PublishedEvents.Clear();

            service.ReportStimulus(id, VillagerStimulus.FireNearby);

            Assert.AreEqual(VillagerState.Tossed, service.GetState(id));
            Assert.IsEmpty(eventBus.PublishedEvents);
        }

        [Test]
        public void ReportStimulus_Landed_WhileIdle_IsIgnored()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            eventBus.PublishedEvents.Clear();

            service.ReportStimulus(id, VillagerStimulus.Landed);

            Assert.AreEqual(VillagerState.Idle, service.GetState(id));
            Assert.IsEmpty(eventBus.PublishedEvents);
        }

        [Test]
        public void ReportStimulus_NeedResolvedNearby_WhileStunned_IsIgnored()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.Grabbed);
            service.ReportStimulus(id, VillagerStimulus.HardLanded); // -> Stunned
            eventBus.PublishedEvents.Clear();

            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby);

            Assert.AreEqual(VillagerState.Stunned, service.GetState(id));
            Assert.IsEmpty(eventBus.PublishedEvents);
        }

        [Test]
        public void Tick_PanicDurationElapsed_ReturnsToIdle()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.FireNearby); // -> Panic, таймер = VillagerConfig.PanicDuration (дефолт 4с)
            eventBus.PublishedEvents.Clear();

            service.Tick(3.9f);
            Assert.AreEqual(VillagerState.Panic, service.GetState(id));

            service.Tick(0.2f); // суммарно 4.1с — перекрывает дефолтный PanicDuration
            Assert.AreEqual(VillagerState.Idle, service.GetState(id));

            var evt = eventBus.PublishedEvents.OfType<VillagerStateChangedEvent>().Single();
            Assert.AreEqual(VillagerState.Panic, evt.From);
            Assert.AreEqual(VillagerState.Idle, evt.To);
        }

        [Test]
        public void Tick_WorshipDurationElapsed_ReturnsToIdle()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby); // -> Worship, дефолт WorshipDuration = 3с

            service.Tick(3.1f);

            Assert.AreEqual(VillagerState.Idle, service.GetState(id));
        }

        [Test]
        public void Tick_StunDurationElapsed_ReturnsToIdle()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.Grabbed);
            service.ReportStimulus(id, VillagerStimulus.HardLanded); // -> Stunned, дефолт StunDuration = 2с

            service.Tick(2.1f);

            Assert.AreEqual(VillagerState.Idle, service.GetState(id));
        }

        [Test]
        public void Tick_Idle_NeverAutoTransitions()
        {
            // Н3 (ревью шага 3): Tossed больше не входит в этот тест — у него теперь есть
            // страховочный таймаут (см. Tick_TossedTimeoutElapsed_ReturnsToIdle ниже). Idle
            // по-прежнему без таймера — ждёт стимула сколько угодно долго.
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id); // Idle
            eventBus.PublishedEvents.Clear();

            service.Tick(1000f);

            Assert.AreEqual(VillagerState.Idle, service.GetState(id));
            Assert.IsEmpty(eventBus.PublishedEvents);
        }

        [Test]
        public void StateChangedEvent_PublishedExactlyOncePerTransition()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);

            service.ReportStimulus(id, VillagerStimulus.FireNearby);

            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<VillagerStateChangedEvent>().Count());
        }

        [Test]
        public void ReportStimulus_SameTargetAsCurrentState_DoesNotPublish()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby); // -> Worship
            eventBus.PublishedEvents.Clear();

            // Повторный стимул того же назначения (Worship) — продление таймера, не транзиция.
            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby);

            Assert.AreEqual(VillagerState.Worship, service.GetState(id));
            Assert.IsEmpty(eventBus.PublishedEvents.OfType<VillagerStateChangedEvent>());
        }

        [Test]
        public void TwoVillagers_StatesAreIndependent()
        {
            var service = CreateService(out _);
            var idA = VillagerId.FromInstanceId(1);
            var idB = VillagerId.FromInstanceId(2);
            service.Register(idA);
            service.Register(idB);

            service.ReportStimulus(idA, VillagerStimulus.FireNearby);

            Assert.AreEqual(VillagerState.Panic, service.GetState(idA));
            Assert.AreEqual(VillagerState.Idle, service.GetState(idB));
        }

        [Test]
        public void Unregister_ClearsRecord_ReportStimulusAfterwardsIsIgnored()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.FireNearby); // -> Panic
            service.Unregister(id);
            eventBus.PublishedEvents.Clear();

            LogAssert.Expect(LogType.Warning, new Regex("незарегистрированного жителя"));
            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby);

            Assert.IsEmpty(eventBus.PublishedEvents);

            // GetState после Unregister — снова "неизвестный" id, безопасный дефолт Idle
            // (не хвост старого Panic — запись действительно вычищена).
            LogAssert.Expect(LogType.Warning, new Regex("не зарегистрирован"));
            Assert.AreEqual(VillagerState.Idle, service.GetState(id));
        }

        // --- В9 (ревью шага 3): недостающие граничные случаи ---

        [Test]
        public void Register_Twice_LogsWarning_AndDoesNotResetExistingRecord()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.FireNearby); // -> Panic

            LogAssert.Expect(LogType.Warning, new Regex("уже зарегистрирован"));
            service.Register(id);

            // Поведение заявлено в xml-doc IVillagerStateService.Register: повторная регистрация —
            // предупреждение, БЕЗ изменения существующей записи (не откатывает Panic обратно в Idle).
            Assert.AreEqual(VillagerState.Panic, service.GetState(id));
        }

        [Test]
        public void Tick_TwoVillagers_SimultaneousExpiry_BothTransitionAndEachPublishesOneEvent()
        {
            var service = CreateService(out var eventBus);
            var idA = VillagerId.FromInstanceId(1);
            var idB = VillagerId.FromInstanceId(2);
            service.Register(idA);
            service.Register(idB);
            service.ReportStimulus(idA, VillagerStimulus.FireNearby); // -> Panic, таймер = PanicDuration (дефолт 4с)
            service.ReportStimulus(idB, VillagerStimulus.FireNearby); // -> Panic, тот же таймер
            eventBus.PublishedEvents.Clear();

            // Один Tick перекрывает дефолтный PanicDuration для ОБОИХ одновременно.
            service.Tick(4.1f);

            Assert.AreEqual(VillagerState.Idle, service.GetState(idA));
            Assert.AreEqual(VillagerState.Idle, service.GetState(idB));

            var changedEvents = eventBus.PublishedEvents.OfType<VillagerStateChangedEvent>().ToList();
            Assert.AreEqual(1, changedEvents.Count(e => e.Id.Equals(idA) && e.From == VillagerState.Panic && e.To == VillagerState.Idle));
            Assert.AreEqual(1, changedEvents.Count(e => e.Id.Equals(idB) && e.From == VillagerState.Panic && e.To == VillagerState.Idle));
        }

        [Test]
        public void Tick_TossedTimeoutElapsed_ReturnsToIdle()
        {
            // Н3 (ревью шага 3): страховочный таймаут Tossed -> Idle, если Landed/HardLanded так и
            // не пришёл (дефолт VillagerConfig.TossedTimeout = 6с).
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.Grabbed); // -> Tossed

            service.Tick(6.1f);

            Assert.AreEqual(VillagerState.Idle, service.GetState(id));
        }

        // --- TryGetPanicStimulus (рефакторинг по итогам архитектурного аудита после шага 3:
        // таблица "какая нужда пугает жителей рядом" переехала из NeedSiteController в сервис) ---

        [Test]
        public void TryGetPanicStimulus_Fire_ReturnsFireNearby()
        {
            var service = CreateService(out _);

            var result = service.TryGetPanicStimulus(NeedType.Fire, out var stimulus);

            Assert.IsTrue(result);
            Assert.AreEqual(VillagerStimulus.FireNearby, stimulus);
        }

        [TestCase(NeedType.Disease)]
        [TestCase(NeedType.Hunger)]
        public void TryGetPanicStimulus_DiseaseAndHunger_ReturnsFalse(NeedType type)
        {
            var service = CreateService(out _);

            var result = service.TryGetPanicStimulus(type, out _);

            Assert.IsFalse(result);
        }

        // --- Шаг 4: Blasted (взрыв чуда Fireball разбрасывает жителей, game-design §4) ---

        [Test]
        public void ReportStimulus_Blasted_FromWorship_TransitionsToTossed()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby); // -> Worship

            service.ReportStimulus(id, VillagerStimulus.Blasted);

            Assert.AreEqual(VillagerState.Tossed, service.GetState(id));
        }

        [Test]
        public void ReportStimulus_Blasted_FromIdle_TransitionsToTossed_AndPublishesEvent()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);

            service.ReportStimulus(id, VillagerStimulus.Blasted);

            Assert.AreEqual(VillagerState.Tossed, service.GetState(id));
            var evt = eventBus.PublishedEvents.OfType<VillagerStateChangedEvent>().Single();
            Assert.AreEqual(VillagerState.Idle, evt.From);
            Assert.AreEqual(VillagerState.Tossed, evt.To);
        }

        [Test]
        public void ReportStimulus_Blasted_WhileAlreadyTossed_IsSelfLoop_WithoutEvent()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.Grabbed); // -> Tossed
            eventBus.PublishedEvents.Clear();

            service.ReportStimulus(id, VillagerStimulus.Blasted);

            Assert.AreEqual(VillagerState.Tossed, service.GetState(id));
            Assert.IsEmpty(eventBus.PublishedEvents.OfType<VillagerStateChangedEvent>());
        }

        // --- Шаг 4: CountInState (FaithService считает молящихся) ---

        [Test]
        public void CountInState_NoVillagers_ReturnsZero()
        {
            var service = CreateService(out _);

            Assert.AreEqual(0, service.CountInState(VillagerState.Worship));
        }

        [Test]
        public void CountInState_CountsOnlyMatchingState()
        {
            var service = CreateService(out _);
            var idA = VillagerId.FromInstanceId(1);
            var idB = VillagerId.FromInstanceId(2);
            var idC = VillagerId.FromInstanceId(3);
            service.Register(idA);
            service.Register(idB);
            service.Register(idC);
            service.ReportStimulus(idA, VillagerStimulus.NeedResolvedNearby); // -> Worship
            service.ReportStimulus(idB, VillagerStimulus.NeedResolvedNearby); // -> Worship
            service.ReportStimulus(idC, VillagerStimulus.FireNearby); // -> Panic

            Assert.AreEqual(2, service.CountInState(VillagerState.Worship));
            Assert.AreEqual(1, service.CountInState(VillagerState.Panic));
            Assert.AreEqual(0, service.CountInState(VillagerState.Idle));
        }

        [Test]
        public void CountInState_UpdatesAfterTransitionOut()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby); // -> Worship
            Assert.AreEqual(1, service.CountInState(VillagerState.Worship));

            service.Tick(3.1f); // дефолт WorshipDuration = 3с -> Idle

            Assert.AreEqual(0, service.CountInState(VillagerState.Worship));
        }

        // --- Шаг 6: LightningStruck (удар молнии чуда Lightning, game-design §4) ---

        [Test]
        public void ReportStimulus_LightningStruck_FromIdle_TransitionsToStunned_AndPublishesEvent()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);

            service.ReportStimulus(id, VillagerStimulus.LightningStruck);

            Assert.AreEqual(VillagerState.Stunned, service.GetState(id));
            var evt = eventBus.PublishedEvents.OfType<VillagerStateChangedEvent>().Single();
            Assert.AreEqual(VillagerState.Idle, evt.From);
            Assert.AreEqual(VillagerState.Stunned, evt.To);
            Assert.AreEqual(VillagerStimulus.LightningStruck, evt.Cause);
        }

        [Test]
        public void ReportStimulus_LightningStruck_FromPanic_TransitionsToStunned()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.FireNearby); // -> Panic

            service.ReportStimulus(id, VillagerStimulus.LightningStruck);

            Assert.AreEqual(VillagerState.Stunned, service.GetState(id));
        }

        [Test]
        public void ReportStimulus_LightningStruck_FromWorship_TransitionsToStunned()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.NeedResolvedNearby); // -> Worship

            service.ReportStimulus(id, VillagerStimulus.LightningStruck);

            Assert.AreEqual(VillagerState.Stunned, service.GetState(id));
        }

        [Test]
        public void ReportStimulus_LightningStruck_WhileTossed_IsIgnored()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.Grabbed); // -> Tossed
            eventBus.PublishedEvents.Clear();

            service.ReportStimulus(id, VillagerStimulus.LightningStruck);

            Assert.AreEqual(VillagerState.Tossed, service.GetState(id));
            Assert.IsEmpty(eventBus.PublishedEvents);
        }

        [Test]
        public void ReportStimulus_LightningStruck_WhileAlreadyStunned_IsSelfLoop_WithoutEvent_AndExtendsTimer()
        {
            var service = CreateService(out var eventBus);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);
            service.ReportStimulus(id, VillagerStimulus.LightningStruck); // -> Stunned, таймер = StunDuration (дефолт 2с)
            eventBus.PublishedEvents.Clear();

            service.Tick(1.5f); // ещё не истёк
            service.ReportStimulus(id, VillagerStimulus.LightningStruck); // self-loop — продлевает таймер заново до 2с

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<VillagerStateChangedEvent>());

            service.Tick(1.5f); // суммарно с момента продления — 1.5с, дефолт 2с ещё не истёк
            Assert.AreEqual(VillagerState.Stunned, service.GetState(id));

            service.Tick(0.6f); // перекрывает продлённый таймер
            Assert.AreEqual(VillagerState.Idle, service.GetState(id));
        }

        // --- Шаг переноса Disease: CollectRegistered (VillagerDiseaseService выбирает кандидата) ---

        [Test]
        public void CollectRegistered_ReturnsAllRegisteredIds()
        {
            var service = CreateService(out _);
            var idA = VillagerId.FromInstanceId(1);
            var idB = VillagerId.FromInstanceId(2);
            service.Register(idA);
            service.Register(idB);

            var buffer = new List<VillagerId>();
            service.CollectRegistered(buffer);

            Assert.AreEqual(2, buffer.Count);
            Assert.IsTrue(buffer.Contains(idA));
            Assert.IsTrue(buffer.Contains(idB));
        }

        [Test]
        public void CollectRegistered_ClearsBufferBeforeFilling()
        {
            var service = CreateService(out _);
            var id = VillagerId.FromInstanceId(1);
            service.Register(id);

            var buffer = new List<VillagerId> { VillagerId.FromInstanceId(999) }; // мусор от предыдущего использования

            service.CollectRegistered(buffer);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(id, buffer[0]);
        }

        [Test]
        public void CollectRegistered_NoRegisteredVillagers_ClearsBufferAndReturnsEmpty()
        {
            var service = CreateService(out _);
            var buffer = new List<VillagerId> { VillagerId.FromInstanceId(1) };

            service.CollectRegistered(buffer);

            Assert.IsEmpty(buffer);
        }
    }
}
