using NUnit.Framework;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.Input;
using BlackWhiteLike.Core.StateMachine;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Flow.States;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="InputRouter"/> (game-design §3, §13, §19, шаг 5).
    /// </summary>
    /// <remarks>
    /// Аудит после первого Play Mode (БЛОКЕР): <see cref="InputRouter"/> больше не кэширует
    /// Context в ctor и не подписывается на шину — он вычисляет его от
    /// <see cref="IGameFlowService.Current"/> при каждом обращении. Тесты воспроизводят реальную
    /// последовательность сцены: роутер создаётся ДО <see cref="GameFlowService.Start"/>
    /// (RegisterComponent-callback'и резолвят его раньше GameBootstrap.Start()), поэтому FakeGameFlowService
    /// больше не нужен — используется реальный <see cref="GameFlowService"/>.
    /// </remarks>
    public class InputRouterTests
    {
        private static GameFlowService CreateGameFlow()
        {
            var eventBus = new FakeEventBus();
            return new GameFlowService(eventBus,
                new IGameStateBinding[]
                {
                    new GameStateBinding(GameState.MainMenu, new MainMenuState()),
                    new GameStateBinding(GameState.LevelPlaying, new NullState()),
                    new GameStateBinding(GameState.Paused, new NullState()),
                });
        }

        [Test]
        public void CreatedBeforeStart_IsUi()
        {
            var gameFlow = CreateGameFlow();
            var router = new InputRouter(gameFlow);

            // GameFlowService.Start() ещё не вызывался — Current == default(GameState).
            Assert.AreEqual(InputContext.UI, router.Context);
            Assert.IsFalse(router.IsGameplay);
        }

        [Test]
        public void AfterStartLevelPlaying_SwitchesToGameplay()
        {
            var gameFlow = CreateGameFlow();
            var router = new InputRouter(gameFlow);

            // GameFlowService.Start() не публикует GameStateChangedEvent — раньше это было
            // ловушкой (Context кэшировался и никогда не обновлялся). Вычисляемое свойство
            // не зависит от события.
            gameFlow.Start(GameState.LevelPlaying);

            Assert.AreEqual(InputContext.Gameplay, router.Context);
            Assert.IsTrue(router.IsGameplay);
        }

        [Test]
        public void PushPaused_SwitchesToUi()
        {
            var gameFlow = CreateGameFlow();
            var router = new InputRouter(gameFlow);
            gameFlow.Start(GameState.LevelPlaying);

            gameFlow.PushState(GameState.Paused);

            Assert.AreEqual(InputContext.UI, router.Context);
            Assert.IsFalse(router.IsGameplay);
        }

        [Test]
        public void PopFromPaused_BackToLevelPlaying_SwitchesToGameplay()
        {
            var gameFlow = CreateGameFlow();
            var router = new InputRouter(gameFlow);
            gameFlow.Start(GameState.LevelPlaying);
            gameFlow.PushState(GameState.Paused);

            gameFlow.PopState();

            Assert.AreEqual(InputContext.Gameplay, router.Context);
            Assert.IsTrue(router.IsGameplay);
        }

        [Test]
        public void MainMenu_IsUi()
        {
            var gameFlow = CreateGameFlow();
            var router = new InputRouter(gameFlow);

            gameFlow.Start(GameState.MainMenu);

            Assert.AreEqual(InputContext.UI, router.Context);
            Assert.IsFalse(router.IsGameplay);
        }
    }
}
