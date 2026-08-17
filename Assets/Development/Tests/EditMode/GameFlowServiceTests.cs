using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Flow.States;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="GameFlowService"/> (game-design §19, шаг 1).</summary>
    public class GameFlowServiceTests
    {
        /// <summary>Фейковый IEventBus, собирающий опубликованные события в список (для проверки в тестах).</summary>
        private sealed class FakeEventBus : IEventBus
        {
            public readonly List<object> PublishedEvents = new List<object>();

            public void Publish<T>(in T evt) where T : struct => PublishedEvents.Add(evt);

            public IDisposable Subscribe<T>(Action<T> handler) where T : struct => new NoOpDisposable();

            private sealed class NoOpDisposable : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }

        private static GameFlowService CreateFlow(out FakeEventBus eventBus)
        {
            eventBus = new FakeEventBus();
            return new GameFlowService(eventBus, new MainMenuState());
        }

        [Test]
        public void Start_SetsCurrent_AndDoesNotPublish()
        {
            var flow = CreateFlow(out var eventBus);

            flow.Start(GameState.MainMenu);

            Assert.AreEqual(GameState.MainMenu, flow.Current);
            Assert.IsEmpty(eventBus.PublishedEvents);
        }

        [Test]
        public void ChangeState_PublishesGameStateChangedEvent()
        {
            var flow = CreateFlow(out var eventBus);
            flow.Start(GameState.MainMenu);
            eventBus.PublishedEvents.Clear();

            // LevelSelect на шаге 1 ещё не зарегистрирован как IState (появится в шаге 2+) —
            // StateMachine.TryGetState теперь логирует это как ошибку (см. CLAUDE.md/StateMachine).
            // Событие GameFlowService при этом всё равно публикуется — Enter/Exit не единственный
            // сигнал транзиции.
            LogAssert.Expect(LogType.Error, "[StateMachine] Состояние LevelSelect не зарегистрировано.");

            flow.ChangeState(GameState.LevelSelect);

            Assert.AreEqual(GameState.LevelSelect, flow.Current);
            var evt = eventBus.PublishedEvents.OfType<GameStateChangedEvent>().Single();
            Assert.AreEqual(GameState.MainMenu, evt.From);
            Assert.AreEqual(GameState.LevelSelect, evt.To);
        }

        [Test]
        public void PushState_Paused_PublishesChangedAndPausedEvents()
        {
            var flow = CreateFlow(out var eventBus);
            flow.Start(GameState.MainMenu);
            eventBus.PublishedEvents.Clear();

            flow.PushState(GameState.Paused);

            Assert.AreEqual(GameState.Paused, flow.Current);
            var changed = eventBus.PublishedEvents.OfType<GameStateChangedEvent>().Single();
            Assert.AreEqual(GameState.MainMenu, changed.From);
            Assert.AreEqual(GameState.Paused, changed.To);
            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<GamePausedEvent>().Count());
        }

        [Test]
        public void PopState_FromPaused_PublishesChangedAndResumedEvents()
        {
            var flow = CreateFlow(out var eventBus);
            flow.Start(GameState.MainMenu);
            flow.PushState(GameState.Paused);
            eventBus.PublishedEvents.Clear();

            flow.PopState();

            Assert.AreEqual(GameState.MainMenu, flow.Current);
            var changed = eventBus.PublishedEvents.OfType<GameStateChangedEvent>().Single();
            Assert.AreEqual(GameState.Paused, changed.From);
            Assert.AreEqual(GameState.MainMenu, changed.To);
            Assert.AreEqual(1, eventBus.PublishedEvents.OfType<GameResumedEvent>().Count());
        }

        [Test]
        public void PopState_WithoutPush_DoesNotPublish()
        {
            var flow = CreateFlow(out var eventBus);
            flow.Start(GameState.MainMenu);
            eventBus.PublishedEvents.Clear();

            // Стек содержит только MainMenu — Pop безопасный no-op, транзиции не было.
            flow.PopState();

            Assert.AreEqual(GameState.MainMenu, flow.Current);
            Assert.IsEmpty(eventBus.PublishedEvents);
        }
    }
}
