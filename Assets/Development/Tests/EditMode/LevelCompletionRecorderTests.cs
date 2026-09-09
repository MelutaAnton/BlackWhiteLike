using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Meta;
using BlackWhiteLike.Services.Worship;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="LevelCompletionRecorder"/> (game-design §7-§9, шаг 7; §8, шаг 8 — награда и
    /// персист alignment). FakeEventBus.Publish не рассылает подписчикам (см. FakeEventBus.cs) — для
    /// проверки реальной подписки/рассылки нужен настоящий синхронный <see cref="EventBus"/> (тот же
    /// приём, что LevelFlowServiceTests/LevelEventServiceTests).
    /// </summary>
    public class LevelCompletionRecorderTests
    {
        private sealed class FakeLevelProgressService : ILevelProgressService
        {
            public readonly List<LevelId> MarkCompletedCalls = new List<LevelId>();

            public void MarkCompleted(LevelId id) => MarkCompletedCalls.Add(id);

            public bool IsCompleted(LevelId id) => MarkCompletedCalls.Contains(id);

            public bool IsUnlocked(LevelId id) => true;
        }

        private sealed class FakeEconomyService : IEconomyService
        {
            public readonly List<LevelId> LevelIds = new List<LevelId>();
            public readonly List<int> BaseCoins = new List<int>();
            public readonly List<bool> FirstClearFlags = new List<bool>();

            public void GrantLevelReward(LevelId id, int baseCoins, bool firstClear)
            {
                LevelIds.Add(id);
                BaseCoins.Add(baseCoins);
                FirstClearFlags.Add(firstClear);
            }

            public bool TryPurchaseUpgrade(UpgradeCategory category) => false;

            public void GrantIdleIncome(int coins)
            {
            }
        }

        private sealed class FakeAlignmentService : IAlignmentService
        {
            public float Score { get; set; }
            public float MinScore => -100f;
            public float MaxScore => 100f;
        }

        // FakeMetaProgressService — общий фейк тестового проекта, см. FakeMetaProgressService.cs.

        private static LevelConfig CreateLevelConfig(int coinsReward = 10)
        {
            var config = ScriptableObject.CreateInstance<LevelConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_coinsReward").intValue = coinsReward;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private static LevelCompletionRecorder CreateRecorder(
            EventBus eventBus,
            FakeLevelProgressService progress,
            out FakeEconomyService economy,
            out FakeAlignmentService alignment,
            out FakeMetaProgressService metaProgress,
            int coinsReward = 10)
        {
            economy = new FakeEconomyService();
            alignment = new FakeAlignmentService();
            metaProgress = new FakeMetaProgressService();
            return new LevelCompletionRecorder(eventBus, progress, economy, CreateLevelConfig(coinsReward), alignment, metaProgress);
        }

        [Test]
        public void Start_ThenLevelCompletedEvent_CallsMarkCompletedExactlyOnce()
        {
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = CreateRecorder(eventBus, progress, out _, out _, out _);

            recorder.Start();
            eventBus.Publish(new LevelCompletedEvent(LevelId.Level02, aweTotal: 10, fearTotal: 2, duration: 30f));

            Assert.AreEqual(1, progress.MarkCompletedCalls.Count);
            Assert.AreEqual(LevelId.Level02, progress.MarkCompletedCalls[0]);
        }

        [Test]
        public void WithoutStart_LevelCompletedEvent_DoesNotCallMarkCompleted()
        {
            // Подписка происходит в Start() (IStartable), не в конструкторе — до вызова Start()
            // рекордер не должен реагировать на события (тот же принцип, что у остальных
            // presenter'ов/EntryPoint'ов проекта).
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = CreateRecorder(eventBus, progress, out _, out _, out _);

            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, aweTotal: 5, fearTotal: 0, duration: 20f));

            Assert.IsEmpty(progress.MarkCompletedCalls);
        }

        [Test]
        public void Dispose_ThenLevelCompletedEvent_DoesNotCallMarkCompleted()
        {
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = CreateRecorder(eventBus, progress, out _, out _, out _);

            recorder.Start();
            recorder.Dispose();
            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, aweTotal: 5, fearTotal: 0, duration: 20f));

            Assert.IsEmpty(progress.MarkCompletedCalls);
        }

        [Test]
        public void Dispose_TwiceInARow_DoesNotThrow()
        {
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = CreateRecorder(eventBus, progress, out _, out _, out _);

            recorder.Start();
            recorder.Dispose();

            Assert.DoesNotThrow(() => recorder.Dispose());
        }

        [Test]
        public void Start_MultipleLevelCompletedEvents_RecordsEachId()
        {
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = CreateRecorder(eventBus, progress, out _, out _, out _);

            recorder.Start();
            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, aweTotal: 10, fearTotal: 0, duration: 15f));
            eventBus.Publish(new LevelCompletedEvent(LevelId.Level02, aweTotal: 20, fearTotal: 0, duration: 25f));

            CollectionAssert.AreEqual(new[] { LevelId.Level01, LevelId.Level02 }, progress.MarkCompletedCalls);
        }

        // --- Шаг 8: награда через IEconomyService, first-clear, персист alignment ---

        [Test]
        public void LevelCompleted_FirstTime_GrantsReward_WithFirstClearTrue()
        {
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = CreateRecorder(eventBus, progress, out var economy, out _, out _, coinsReward: 42);

            recorder.Start();
            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, aweTotal: 10, fearTotal: 0, duration: 15f));

            Assert.AreEqual(1, economy.LevelIds.Count);
            Assert.AreEqual(LevelId.Level01, economy.LevelIds[0]);
            Assert.AreEqual(42, economy.BaseCoins[0]);
            Assert.IsTrue(economy.FirstClearFlags[0]);
        }

        [Test]
        public void LevelCompleted_SecondTimeSameLevel_GrantsReward_WithFirstClearFalse()
        {
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = CreateRecorder(eventBus, progress, out var economy, out _, out _);

            recorder.Start();
            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, aweTotal: 10, fearTotal: 0, duration: 15f));
            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, aweTotal: 10, fearTotal: 0, duration: 15f));

            Assert.AreEqual(2, economy.FirstClearFlags.Count);
            Assert.IsTrue(economy.FirstClearFlags[0]);
            Assert.IsFalse(economy.FirstClearFlags[1]);
        }

        [Test]
        public void LevelCompleted_PersistsAlignmentScore_IntoMetaProgress()
        {
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = CreateRecorder(eventBus, progress, out _, out var alignment, out var metaProgress);
            alignment.Score = 37f;

            recorder.Start();
            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, aweTotal: 10, fearTotal: 0, duration: 15f));

            Assert.AreEqual(37f, metaProgress.AlignmentScore, 0.001f);
        }
    }
}
