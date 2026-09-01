using System.Collections.Generic;
using NUnit.Framework;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Meta;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="LevelCompletionRecorder"/> (game-design §7-§9, шаг 7). FakeEventBus.Publish
    /// не рассылает подписчикам (см. FakeEventBus.cs) — для проверки реальной подписки/рассылки
    /// нужен настоящий синхронный <see cref="EventBus"/> (тот же приём, что LevelFlowServiceTests/
    /// LevelEventServiceTests).
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

        [Test]
        public void Start_ThenLevelCompletedEvent_CallsMarkCompletedExactlyOnce()
        {
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = new LevelCompletionRecorder(eventBus, progress);

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
            var recorder = new LevelCompletionRecorder(eventBus, progress);

            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, aweTotal: 5, fearTotal: 0, duration: 20f));

            Assert.IsEmpty(progress.MarkCompletedCalls);
        }

        [Test]
        public void Dispose_ThenLevelCompletedEvent_DoesNotCallMarkCompleted()
        {
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = new LevelCompletionRecorder(eventBus, progress);

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
            var recorder = new LevelCompletionRecorder(eventBus, progress);

            recorder.Start();
            recorder.Dispose();

            Assert.DoesNotThrow(() => recorder.Dispose());
        }

        [Test]
        public void Start_MultipleLevelCompletedEvents_RecordsEachId()
        {
            var eventBus = new EventBus();
            var progress = new FakeLevelProgressService();
            var recorder = new LevelCompletionRecorder(eventBus, progress);

            recorder.Start();
            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, aweTotal: 10, fearTotal: 0, duration: 15f));
            eventBus.Publish(new LevelCompletedEvent(LevelId.Level02, aweTotal: 20, fearTotal: 0, duration: 25f));

            CollectionAssert.AreEqual(new[] { LevelId.Level01, LevelId.Level02 }, progress.MarkCompletedCalls);
        }
    }
}
