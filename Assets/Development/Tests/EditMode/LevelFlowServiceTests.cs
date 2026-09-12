using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Village;
using BlackWhiteLike.Services.Worship;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="LevelFlowService"/> (game-design §6, §9, §19, шаг 5).</summary>
    public class LevelFlowServiceTests
    {
        private sealed class FakeFaithService : IFaithService
        {
            public float Current { get; set; }
            public float Capacity { get; set; } = 100f;

            public bool CanAfford(float amount) => Current >= amount;

            public bool TrySpend(float amount)
            {
                if (Current < amount) return false;
                Current -= amount;
                return true;
            }

            public void Tick(float deltaTime)
            {
            }

            public void RefillToCapacity() => Current = Capacity;
        }

        private sealed class FakeLevelEventService : ILevelEventService
        {
            /// <summary>Единственное поле, которое реально читает LevelFlowService.Tick для stall.</summary>
            public bool HasBlockingNeeds { get; set; }

            public bool IsNeedActive(NeedType type, int siteIndex) => false;

            public void ResolveNeed(NeedType type, int siteIndex, bool byPlayer, MiracleType causeMiracle)
            {
            }

            public void TriggerNeed(NeedType type, int siteIndex, float duration, bool byPlayer, MiracleType causeMiracle)
            {
            }

            public void Tick(float deltaTime)
            {
            }
        }

        private sealed class FakeWorshipService : IWorshipService
        {
            public int Current { get; set; }
            public int Target { get; set; } = 100;
            public int AweTotal { get; set; }
            public int FearTotal { get; set; }
            public bool IsTargetReached => Current >= Target;

            public void Tick(float dt)
            {
            }
        }

        private static LevelFlowService CreateService(
            IEventBus eventBus,
            out FakeWorshipService worship,
            out FakeFaithService faith,
            out FakeLevelEventService levelEvents)
        {
            worship = new FakeWorshipService();
            faith = new FakeFaithService();
            levelEvents = new FakeLevelEventService();
            var levelConfig = ScriptableObject.CreateInstance<LevelConfig>();
            return new LevelFlowService(eventBus, worship, faith, levelEvents, levelConfig);
        }

        // --- StartLevel ---

        [Test]
        public void StartLevel_ResetsElapsedAndFlags()
        {
            var service = CreateService(new FakeEventBus(), out _, out _, out _);

            service.StartLevel();

            Assert.AreEqual(0f, service.Elapsed);
            Assert.IsTrue(service.IsRunning);
            Assert.IsFalse(service.IsStalled);
        }

        // --- Win (подписка на WorshipChangedEvent — нужна реальная EventBus, FakeEventBus не рассылает) ---

        [Test]
        public void WorshipReachesTarget_PublishesLevelCompletedEvent()
        {
            var eventBus = new EventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            worship.Current = 100;
            worship.Target = 100;
            worship.AweTotal = 60;
            worship.FearTotal = 20;

            LevelCompletedEvent? captured = null;
            using (eventBus.Subscribe<LevelCompletedEvent>(e => captured = e))
            {
                eventBus.Publish(new WorshipChangedEvent(90, 100, 100, WorshipSource.Awe));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(60, captured.Value.AweTotal);
            Assert.AreEqual(20, captured.Value.FearTotal);
            Assert.IsFalse(service.IsRunning); // аудит п.8: IsRunning — "между StartLevel и достижением Worship.Target"
        }

        [Test]
        public void WorshipReachesTarget_TwiceInARow_PublishesLevelCompletedEventOnlyOnce()
        {
            var eventBus = new EventBus();
            var service = CreateService(eventBus, out var worship, out _, out _);
            service.StartLevel();
            worship.Current = 100;
            worship.Target = 100;

            var completedCount = 0;
            using (eventBus.Subscribe<LevelCompletedEvent>(_ => completedCount++))
            {
                eventBus.Publish(new WorshipChangedEvent(90, 100, 100, WorshipSource.Awe));
                eventBus.Publish(new WorshipChangedEvent(100, 100, 100, WorshipSource.Awe)); // повторный (например, decay -> обратно рост)
            }

            Assert.AreEqual(1, completedCount);
        }

        [Test]
        public void WorshipBelowTarget_DoesNotComplete()
        {
            var eventBus = new EventBus();
            var service = CreateService(eventBus, out var worship, out _, out _);
            service.StartLevel();
            worship.Target = 100;

            var completedCount = 0;
            using (eventBus.Subscribe<LevelCompletedEvent>(_ => completedCount++))
            {
                eventBus.Publish(new WorshipChangedEvent(50, 80, 100, WorshipSource.Awe));
            }

            Assert.AreEqual(0, completedCount);
        }

        [Test]
        public void WorshipReachesTarget_BeforeStartLevel_DoesNotComplete()
        {
            var eventBus = new EventBus();
            var service = CreateService(eventBus, out _, out _, out _);
            // StartLevel() не вызывался — IsRunning == false.

            var completedCount = 0;
            using (eventBus.Subscribe<LevelCompletedEvent>(_ => completedCount++))
            {
                eventBus.Publish(new WorshipChangedEvent(0, 100, 100, WorshipSource.Awe));
            }

            Assert.AreEqual(0, completedCount);
        }

        // --- Tick: Elapsed, no-op вне IsRunning ---

        [Test]
        public void Tick_WhileRunning_AccumulatesElapsed()
        {
            var service = CreateService(new FakeEventBus(), out _, out _, out _);
            service.StartLevel();

            service.Tick(1.5f);
            service.Tick(0.5f);

            Assert.AreEqual(2f, service.Elapsed, 0.001f);
        }

        [Test]
        public void Tick_BeforeStartLevel_IsNoOp()
        {
            var service = CreateService(new FakeEventBus(), out _, out _, out _);

            service.Tick(5f);

            Assert.AreEqual(0f, service.Elapsed);
            Assert.IsFalse(service.IsRunning);
        }

        [Test]
        public void Tick_AfterCompletion_DoesNotAccumulateElapsed_OrPublishStall()
        {
            var eventBus = new EventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            worship.Current = 100;
            worship.Target = 100;
            eventBus.Publish(new WorshipChangedEvent(0, 100, 100, WorshipSource.Awe)); // завершает уровень

            var elapsedBefore = service.Elapsed;
            faith.Current = 0f;
            levelEvents.HasBlockingNeeds = false;

            service.Tick(10f);

            Assert.AreEqual(elapsedBefore, service.Elapsed);
        }

        // --- Stall (soft-fail): все 4 условия, однократность, сброс при Faith>0 ---

        [Test]
        public void Tick_AllFourStallConditionsMet_PublishesLevelStalledEvent()
        {
            var eventBus = new FakeEventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            faith.Current = 0f;
            levelEvents.HasBlockingNeeds = false;
            worship.Current = 10;
            worship.Target = 100;

            service.Tick(1f);

            Assert.IsTrue(service.IsStalled);
            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<LevelStalledEvent>().Count());
        }

        [Test]
        public void Tick_FaithAboveEpsilon_DoesNotStall()
        {
            var eventBus = new FakeEventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            faith.Current = 5f;
            levelEvents.HasBlockingNeeds = false;
            worship.Current = 10;
            worship.Target = 100;

            service.Tick(1f);

            Assert.IsFalse(service.IsStalled);
        }

        // Шаг 10: LevelFlowService.Tick читает HasBlockingNeeds (единый флаг), не HasActiveNeeds/
        // HasPendingNeeds — тонкая семантика (какая именно нужда блокирует) тестируется на реальном
        // LevelEventService в LevelEventServiceTests, эта Fake-версия проверяет только факт чтения.
        [Test]
        public void Tick_HasBlockingNeeds_DoesNotStall()
        {
            var eventBus = new FakeEventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            faith.Current = 0f;
            levelEvents.HasBlockingNeeds = true;
            worship.Current = 10;
            worship.Target = 100;

            service.Tick(1f);

            Assert.IsFalse(service.IsStalled);
        }

        [Test]
        public void Tick_WorshipAtOrAboveTarget_DoesNotStall()
        {
            var eventBus = new FakeEventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            faith.Current = 0f;
            levelEvents.HasBlockingNeeds = false;
            worship.Current = 100;
            worship.Target = 100;

            service.Tick(1f);

            Assert.IsFalse(service.IsStalled);
        }

        [Test]
        public void Tick_AlreadyStalled_DoesNotPublishStallEventAgain()
        {
            var eventBus = new FakeEventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            faith.Current = 0f;
            levelEvents.HasBlockingNeeds = false;
            worship.Current = 10;
            worship.Target = 100;

            service.Tick(1f);
            eventBus.PublishedEvents.Clear();
            service.Tick(1f);

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<LevelStalledEvent>());
        }

        [Test]
        public void Tick_FaithRestoredAboveEpsilon_ResetsStalled_AllowingStallAgain()
        {
            var eventBus = new FakeEventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            faith.Current = 0f;
            levelEvents.HasBlockingNeeds = false;
            worship.Current = 10;
            worship.Target = 100;

            service.Tick(1f); // stall
            Assert.IsTrue(service.IsStalled);

            faith.Current = 50f; // например, RefillToCapacity() по кнопке
            service.Tick(1f);
            Assert.IsFalse(service.IsStalled);

            faith.Current = 0f; // Faith снова истрачен
            eventBus.PublishedEvents.Clear();
            service.Tick(1f);

            Assert.IsTrue(service.IsStalled);
            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<LevelStalledEvent>().Count());
        }

        // --- Шаг 10: AcknowledgeStallDeclined — окно rewarded-согласия, отказ игрока ---

        [Test]
        public void AcknowledgeStallDeclined_WhileStalled_ResetsIsStalled()
        {
            var eventBus = new FakeEventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            faith.Current = 0f;
            levelEvents.HasBlockingNeeds = false;
            worship.Current = 10;
            worship.Target = 100;
            service.Tick(1f); // stall
            Assert.IsTrue(service.IsStalled);

            service.AcknowledgeStallDeclined();

            Assert.IsFalse(service.IsStalled);
        }

        [Test]
        public void AcknowledgeStallDeclined_ConditionStillTrue_DoesNotRepublishBeforeCooldownElapses()
        {
            var eventBus = new FakeEventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            faith.Current = 0f;
            levelEvents.HasBlockingNeeds = false;
            worship.Current = 10;
            worship.Target = 100;
            service.Tick(1f); // stall, LevelStalledEvent #1
            service.AcknowledgeStallDeclined();
            eventBus.PublishedEvents.Clear();

            // Faith по-прежнему 0 — условие сталла остаётся истинным каждый кадр, но кулдаун не даёт
            // мгновенно переоткрыть окно раньше отведённого времени.
            service.Tick(LevelFlowService.StallReshowCooldownAfterDecline - 0.5f);

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<LevelStalledEvent>());
            Assert.IsFalse(service.IsStalled);
        }

        [Test]
        public void AcknowledgeStallDeclined_AfterCooldownElapses_RepublishesLevelStalledEvent()
        {
            var eventBus = new FakeEventBus();
            var service = CreateService(eventBus, out var worship, out var faith, out var levelEvents);
            service.StartLevel();
            faith.Current = 0f;
            levelEvents.HasBlockingNeeds = false;
            worship.Current = 10;
            worship.Target = 100;
            service.Tick(1f); // stall, LevelStalledEvent #1
            service.AcknowledgeStallDeclined();
            eventBus.PublishedEvents.Clear();

            service.Tick(LevelFlowService.StallReshowCooldownAfterDecline + 1f); // кулдаун истёк — условие всё ещё истинно (Faith == 0)

            Assert.IsTrue(service.IsStalled);
            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<LevelStalledEvent>().Count());
        }

        [Test]
        public void AcknowledgeStallDeclined_WhileNotStalled_IsNoOp()
        {
            var service = CreateService(new FakeEventBus(), out _, out _, out _);
            service.StartLevel();

            Assert.DoesNotThrow(() => service.AcknowledgeStallDeclined());
            Assert.IsFalse(service.IsStalled);
        }
    }
}
