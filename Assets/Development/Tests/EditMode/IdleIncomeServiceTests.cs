using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Idle;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Idle;
using BlackWhiteLike.Services.Meta;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="IdleIncomeService"/> (game-design §15, шаг 8).</summary>
    public class IdleIncomeServiceTests
    {
        private sealed class FakeTimeProvider : ITimeProvider
        {
            public DateTime UtcNow { get; set; }
        }

        private sealed class FakeEconomyService : IEconomyService
        {
            public readonly List<int> GrantIdleIncomeCalls = new List<int>();

            public void GrantLevelReward(LevelId id, int baseCoins, bool firstClear)
            {
            }

            public bool TryPurchaseUpgrade(UpgradeCategory category) => false;

            public void GrantIdleIncome(int coins) => GrantIdleIncomeCalls.Add(coins);
        }

        private static IdleIncomeConfig CreateConfig(float coinsPerHour = 60f, float capHours = 8f, float minOfflineMinutes = 5f)
        {
            var config = ScriptableObject.CreateInstance<IdleIncomeConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_coinsPerHour").floatValue = coinsPerHour;
            so.FindProperty("_capHours").floatValue = capHours;
            so.FindProperty("_minOfflineMinutes").floatValue = minOfflineMinutes;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        [Test]
        public void Start_FirstEverRun_LastSeenIsDefault_OnlyStamps_NoEventPublished()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService(); // LastSeenUtc == default
            var time = new FakeTimeProvider { UtcNow = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc) };
            var service = new IdleIncomeService(eventBus, meta, new FakeEconomyService(), time, CreateConfig());

            service.Start();

            Assert.AreEqual(time.UtcNow, meta.LastSeenUtc);
            Assert.IsEmpty(eventBus.PublishedEvents.OfType<IdleIncomeReadyEvent>());
        }

        [Test]
        public void Start_ElapsedBelowMinOffline_DoesNotStamp_NoEventPublished()
        {
            // Ревью шага 8, ВАЖНО 5: штамп больше НЕ ставится "на входе" — только в Collect()
            // (и на первом запуске). Ниже минимума LastSeenUtc остаётся прежним, чтобы следующий
            // вход досчитал тот же интервал плюс добавившееся время.
            var eventBus = new FakeEventBus();
            var originalLastSeen = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
            var meta = new FakeMetaProgressService { LastSeenUtc = originalLastSeen };
            var time = new FakeTimeProvider { UtcNow = originalLastSeen.AddMinutes(2) }; // < MinOfflineMinutes(5)
            var service = new IdleIncomeService(eventBus, meta, new FakeEconomyService(), time, CreateConfig(minOfflineMinutes: 5f));

            service.Start();

            Assert.AreEqual(originalLastSeen, meta.LastSeenUtc);
            Assert.IsEmpty(eventBus.PublishedEvents.OfType<IdleIncomeReadyEvent>());
        }

        [Test]
        public void Start_ElapsedAboveMinOffline_PublishesIdleIncomeReadyEvent_WithComputedCoins()
        {
            var eventBus = new FakeEventBus();
            var originalLastSeen = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
            var meta = new FakeMetaProgressService { LastSeenUtc = originalLastSeen };
            var time = new FakeTimeProvider { UtcNow = originalLastSeen.AddHours(2) }; // 2 часа оффлайна
            var service = new IdleIncomeService(eventBus, meta, new FakeEconomyService(), time, CreateConfig(coinsPerHour: 60f, capHours: 8f, minOfflineMinutes: 5f));

            service.Start();

            var evt = eventBus.PublishedEvents.OfType<IdleIncomeReadyEvent>().Single();
            Assert.AreEqual(120, evt.CoinAmount); // 2ч * 60/ч
            Assert.AreEqual(2f, evt.OfflineHours, 0.01f);

            // Штамп откладывается до Collect() (game-design §15, ревью шага 8) — несобранная
            // награда не сгорает при повторном входе без нажатия кнопки сбора.
            Assert.AreEqual(originalLastSeen, meta.LastSeenUtc);
        }

        [Test]
        public void Start_ClockMovedBackward_DoesNotAccrue_DoesNotStamp_LogsWarning()
        {
            var eventBus = new FakeEventBus();
            var originalLastSeen = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
            var meta = new FakeMetaProgressService { LastSeenUtc = originalLastSeen };
            var time = new FakeTimeProvider { UtcNow = originalLastSeen.AddHours(-1) }; // часы переведены назад
            var service = new IdleIncomeService(eventBus, meta, new FakeEconomyService(), time, CreateConfig());

            LogAssert.Expect(LogType.Warning, "[IdleIncomeService] Текущее время раньше LastSeenUtc (часы переведены назад?) — офлайн-доход не начисляется, штамп не обновляется.");
            service.Start();

            Assert.AreEqual(originalLastSeen, meta.LastSeenUtc);
            Assert.IsEmpty(eventBus.PublishedEvents.OfType<IdleIncomeReadyEvent>());
        }

        [Test]
        public void Start_ElapsedAboveMinOffline_CoinsRoundToZero_DoesNotPublish_NorSetPending()
        {
            // MinOfflineMinutes(5) пройден, но при малом CoinsPerHour Mathf.FloorToInt округляет в 0 —
            // легальный "доход есть, но меньше одной монеты", не публикуем и не считаем pending.
            var eventBus = new FakeEventBus();
            var originalLastSeen = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
            var meta = new FakeMetaProgressService { LastSeenUtc = originalLastSeen };
            var time = new FakeTimeProvider { UtcNow = originalLastSeen.AddMinutes(5) };
            var economy = new FakeEconomyService();
            var service = new IdleIncomeService(eventBus, meta, economy, time, CreateConfig(coinsPerHour: 1f, minOfflineMinutes: 5f));

            service.Start();

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<IdleIncomeReadyEvent>());
            Assert.AreEqual(originalLastSeen, meta.LastSeenUtc);
            Assert.IsFalse(service.HasPendingReward);

            service.Collect();
            Assert.IsEmpty(economy.GrantIdleIncomeCalls);
        }

        [Test]
        public void Start_ElapsedAboveCap_ClampsToCapHours()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService { LastSeenUtc = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc) };
            var time = new FakeTimeProvider { UtcNow = meta.LastSeenUtc.AddHours(20) }; // больше cap(8ч)
            var service = new IdleIncomeService(eventBus, meta, new FakeEconomyService(), time, CreateConfig(coinsPerHour: 60f, capHours: 8f, minOfflineMinutes: 5f));

            service.Start();

            var evt = eventBus.PublishedEvents.OfType<IdleIncomeReadyEvent>().Single();
            Assert.AreEqual(480, evt.CoinAmount); // 8ч(cap) * 60/ч
        }

        [Test]
        public void Start_WithoutCollecting_RepeatedEntry_RecomputesSameReward_NotStamped()
        {
            // Ревью шага 8, ВАЖНО 5 (новая семантика): "повторный вход" БЕЗ сбора награды — второй
            // сервис (условно вторая загрузка мета-сцены той же сессии) пересчитывает от ТОГО ЖЕ
            // (не обновлённого) LastSeenUtc и публикует ТОТ ЖЕ результат — доход не сгорает и не
            // суммируется задвоением, просто ещё не собран.
            var originalLastSeen = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
            var meta = new FakeMetaProgressService { LastSeenUtc = originalLastSeen };
            var time = new FakeTimeProvider { UtcNow = originalLastSeen.AddHours(2) };
            var firstService = new IdleIncomeService(new FakeEventBus(), meta, new FakeEconomyService(), time, CreateConfig());
            firstService.Start();

            Assert.AreEqual(originalLastSeen, meta.LastSeenUtc); // не проштамповано — see выше

            var secondEventBus = new FakeEventBus();
            var secondService = new IdleIncomeService(secondEventBus, meta, new FakeEconomyService(), time, CreateConfig());
            secondService.Start();

            var evt = secondEventBus.PublishedEvents.OfType<IdleIncomeReadyEvent>().Single();
            Assert.AreEqual(120, evt.CoinAmount); // тот же расчёт, не задвоенный
        }

        [Test]
        public void Collect_WithPendingReward_GrantsThroughEconomy_ExactlyOnce_AndStampsLastSeen()
        {
            var eventBus = new FakeEventBus();
            var originalLastSeen = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
            var meta = new FakeMetaProgressService { LastSeenUtc = originalLastSeen };
            var time = new FakeTimeProvider { UtcNow = originalLastSeen.AddHours(2) };
            var economy = new FakeEconomyService();
            var service = new IdleIncomeService(eventBus, meta, economy, time, CreateConfig(coinsPerHour: 60f));
            service.Start();

            service.Collect();
            service.Collect(); // повторный вызов — no-op

            Assert.AreEqual(1, economy.GrantIdleIncomeCalls.Count);
            Assert.AreEqual(120, economy.GrantIdleIncomeCalls[0]);

            // Штамп — ТОЛЬКО в Collect() (ревью шага 8, ВАЖНО 5), моментом, посчитанным в Start().
            Assert.AreEqual(time.UtcNow, meta.LastSeenUtc);
        }

        [Test]
        public void Collect_WithoutPendingReward_DoesNotCallEconomy()
        {
            var eventBus = new FakeEventBus();
            var meta = new FakeMetaProgressService(); // первый запуск — нет pending reward
            var time = new FakeTimeProvider { UtcNow = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc) };
            var economy = new FakeEconomyService();
            var service = new IdleIncomeService(eventBus, meta, economy, time, CreateConfig());
            service.Start();

            service.Collect();

            Assert.IsEmpty(economy.GrantIdleIncomeCalls);
        }
    }
}
