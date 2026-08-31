using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.StateMachine;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Flow.States;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="GameFlowService"/> (game-design §19, шаг 1, шаг 5).</summary>
    public class GameFlowServiceTests
    {
        // FakeEventBus — общий на все тесты проекта, см. FakeEventBus.cs (вынесен на шаге 3,
        // чтобы VillagerStateServiceTests/LevelEventServiceTests не плодили второй дубль).

        /// <summary>MainMenu + Paused(NullState) — минимальный набор биндингов, которого хватает существующим тестам этого файла.</summary>
        private static GameFlowService CreateFlow(out FakeEventBus eventBus)
        {
            return CreateFlow(out eventBus,
                new GameStateBinding(GameState.MainMenu, new MainMenuState()),
                new GameStateBinding(GameState.Paused, new NullState()));
        }

        private static GameFlowService CreateFlow(out FakeEventBus eventBus, params IGameStateBinding[] bindings)
        {
            eventBus = new FakeEventBus();
            return new GameFlowService(eventBus, bindings);
        }

        /// <summary>Вариант с реальной шиной — нужен там, где GameFlowService сам подписан на события
        /// (LevelCompletedEvent), а FakeEventBus.Subscribe — no-op и до обработчика не доходит.</summary>
        private static GameFlowService CreateFlowWithRealBus(out EventBus eventBus, params IGameStateBinding[] bindings)
        {
            eventBus = new EventBus();
            return new GameFlowService(eventBus, bindings);
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

            // LevelSelect в этом наборе биндингов не зарегистрирован —
            // StateMachine.TryGetState логирует это как ошибку (см. CLAUDE.md/StateMachine).
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

        // --- Шаг 5: сборка состояний из IGameStateBinding (пересмотр решения шага 1) ---

        [Test]
        public void Ctor_RegistersAllBindings()
        {
            var flow = CreateFlow(out _,
                new GameStateBinding(GameState.MainMenu, new MainMenuState()),
                new GameStateBinding(GameState.LevelSelect, new NullState()));

            flow.Start(GameState.LevelSelect);

            Assert.AreEqual(GameState.LevelSelect, flow.Current);
        }

        [Test]
        public void Ctor_DuplicateKey_LogsError_AndKeepsFirstBinding()
        {
            LogAssert.Expect(LogType.Error, "[GameFlowService] Дубликат биндинга для MainMenu — пропущен.");

            var flow = CreateFlow(out _,
                new GameStateBinding(GameState.MainMenu, new MainMenuState()),
                new GameStateBinding(GameState.MainMenu, new NullState()));

            // Не падает, и первый биндинг остаётся пригоден к использованию.
            Assert.DoesNotThrow(() => flow.Start(GameState.MainMenu));
            Assert.AreEqual(GameState.MainMenu, flow.Current);
        }

        [Test]
        public void IsPaused_TrueOnlyWhenCurrentIsPaused()
        {
            var flow = CreateFlow(out _);
            flow.Start(GameState.MainMenu);

            Assert.IsFalse(flow.IsPaused);

            flow.PushState(GameState.Paused);
            Assert.IsTrue(flow.IsPaused);

            flow.PopState();
            Assert.IsFalse(flow.IsPaused);
        }

        // --- Аудит после первого Play Mode: разрыв цикла DI — переход по LevelCompletedEvent живёт здесь,
        // а не в LevelFlowService (см. remarks GameFlowService). Нужна реальная EventBus — GameFlowService
        // подписывается на неё в ctor, FakeEventBus.Subscribe — no-op.

        [Test]
        public void LevelCompletedEvent_WhileLevelPlaying_ChangesStateToLevelComplete_AndPublishesGameStateChangedEvent()
        {
            var flow = CreateFlowWithRealBus(out var eventBus,
                new GameStateBinding(GameState.MainMenu, new MainMenuState()),
                new GameStateBinding(GameState.LevelPlaying, new NullState()),
                new GameStateBinding(GameState.LevelComplete, new NullState()));
            flow.Start(GameState.LevelPlaying);

            GameStateChangedEvent? captured = null;
            using (eventBus.Subscribe<GameStateChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, 60, 20, 42f));
            }

            Assert.AreEqual(GameState.LevelComplete, flow.Current);
            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(GameState.LevelPlaying, captured.Value.From);
            Assert.AreEqual(GameState.LevelComplete, captured.Value.To);
        }

        [Test]
        public void LevelCompletedEvent_OutsideLevelPlaying_DoesNotChangeState_AndLogsWarning()
        {
            var flow = CreateFlowWithRealBus(out var eventBus,
                new GameStateBinding(GameState.MainMenu, new MainMenuState()),
                new GameStateBinding(GameState.LevelComplete, new NullState()));
            flow.Start(GameState.MainMenu);

            LogAssert.Expect(LogType.Warning, "[GameFlowService] LevelCompletedEvent вне LevelPlaying — переход проигнорирован.");

            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, 60, 20, 42f));

            Assert.AreEqual(GameState.MainMenu, flow.Current);
        }
    }
}
