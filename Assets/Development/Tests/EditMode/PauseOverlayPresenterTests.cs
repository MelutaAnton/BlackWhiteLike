using System;
using NUnit.Framework;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Views.UI.Overlay;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="PauseOverlayPresenter"/> (game-design §9, §10, §19, шаг 5).</summary>
    public class PauseOverlayPresenterTests
    {
        private sealed class FakePauseOverlayView : IPauseOverlayView
        {
            public bool IsVisible { get; private set; }
            public int HideCount { get; private set; }

            public event Action ResumeClicked;

            public void Show() => IsVisible = true;

            public void Hide()
            {
                IsVisible = false;
                HideCount++;
            }

            public void RaiseResumeClicked() => ResumeClicked?.Invoke();
        }

        private sealed class FakeGameFlowService : IGameFlowService
        {
            public GameState Current { get; set; }
            public bool IsPaused => Current == GameState.Paused;
            public int PopCount { get; private set; }

            public void Start(GameState initial) => Current = initial;
            public void ChangeState(GameState state) => Current = state;
            public void PushState(GameState state) => Current = state;

            public void PopState()
            {
                Current = GameState.LevelPlaying;
                PopCount++;
            }

            public void Tick()
            {
            }
        }

        [Test]
        public void Start_HidesViewInitially()
        {
            var view = new FakePauseOverlayView();
            var presenter = new PauseOverlayPresenter(view, new FakeGameFlowService(), new FakeEventBus());

            presenter.Start();

            Assert.AreEqual(1, view.HideCount);
            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void GamePausedEvent_ShowsView()
        {
            var view = new FakePauseOverlayView();
            var eventBus = new EventBus();
            var presenter = new PauseOverlayPresenter(view, new FakeGameFlowService(), eventBus);
            presenter.Start();

            eventBus.Publish(new GamePausedEvent());

            Assert.IsTrue(view.IsVisible);
        }

        [Test]
        public void GameResumedEvent_HidesView()
        {
            var view = new FakePauseOverlayView();
            var eventBus = new EventBus();
            var presenter = new PauseOverlayPresenter(view, new FakeGameFlowService(), eventBus);
            presenter.Start();
            eventBus.Publish(new GamePausedEvent());

            eventBus.Publish(new GameResumedEvent());

            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void ResumeClicked_WhenPaused_PopsState()
        {
            var view = new FakePauseOverlayView();
            var gameFlow = new FakeGameFlowService { Current = GameState.Paused };
            var presenter = new PauseOverlayPresenter(view, gameFlow, new FakeEventBus());
            presenter.Start();

            view.RaiseResumeClicked();

            Assert.AreEqual(1, gameFlow.PopCount);
        }

        [Test]
        public void ResumeClicked_NotPaused_DoesNotPop()
        {
            var view = new FakePauseOverlayView();
            var gameFlow = new FakeGameFlowService { Current = GameState.LevelPlaying };
            var presenter = new PauseOverlayPresenter(view, gameFlow, new FakeEventBus());
            presenter.Start();

            view.RaiseResumeClicked();

            Assert.AreEqual(0, gameFlow.PopCount);
        }

        [Test]
        public void Dispose_UnsubscribesFromView()
        {
            var view = new FakePauseOverlayView();
            var gameFlow = new FakeGameFlowService { Current = GameState.Paused };
            var presenter = new PauseOverlayPresenter(view, gameFlow, new FakeEventBus());
            presenter.Start();

            presenter.Dispose();
            view.RaiseResumeClicked();

            Assert.AreEqual(0, gameFlow.PopCount);
        }
    }
}
