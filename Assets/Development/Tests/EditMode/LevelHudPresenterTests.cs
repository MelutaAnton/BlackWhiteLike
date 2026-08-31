using System;
using NUnit.Framework;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Worship;
using BlackWhiteLike.Views.UI.Hud;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="LevelHudPresenter"/> (game-design §10, §19, шаг 5).</summary>
    public class LevelHudPresenterTests
    {
        private sealed class FakeLevelHudView : ILevelHudView
        {
            public int WorshipCurrent { get; private set; }
            public int WorshipTarget { get; private set; }
            public float AlignmentScore { get; private set; }

            public event Action PauseClicked;

            public void SetWorship(int current, int target)
            {
                WorshipCurrent = current;
                WorshipTarget = target;
            }

            public void SetAlignment(float score) => AlignmentScore = score;

            public void RaisePauseClicked() => PauseClicked?.Invoke();
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

        private sealed class FakeAlignmentService : IAlignmentService
        {
            public float Score { get; set; }
            public float MinScore { get; set; } = -100f;
            public float MaxScore { get; set; } = 100f;
        }

        private sealed class FakeGameFlowService : IGameFlowService
        {
            public GameState Current { get; set; }
            public bool IsPaused => Current == GameState.Paused;
            public int PushCount { get; private set; }

            public void Start(GameState initial) => Current = initial;
            public void ChangeState(GameState state) => Current = state;

            public void PushState(GameState state)
            {
                Current = state;
                PushCount++;
            }

            public void PopState()
            {
            }

            public void Tick()
            {
            }
        }

        [Test]
        public void Start_SetsInitialValuesFromServices()
        {
            var view = new FakeLevelHudView();
            var worship = new FakeWorshipService { Current = 30, Target = 100 };
            var alignment = new FakeAlignmentService { Score = 15f };
            var presenter = new LevelHudPresenter(view, worship, alignment, new FakeGameFlowService(), new FakeEventBus());

            presenter.Start();

            Assert.AreEqual(30, view.WorshipCurrent);
            Assert.AreEqual(100, view.WorshipTarget);
            Assert.AreEqual(0.15f, view.AlignmentScore, 0.001f); // нормализовано −1..+1 (шаг 5, аудит п.12): 15 / MaxScore(100)
        }

        [Test]
        public void WorshipChangedEvent_UpdatesView()
        {
            var view = new FakeLevelHudView();
            var eventBus = new EventBus();
            var presenter = new LevelHudPresenter(view, new FakeWorshipService(), new FakeAlignmentService(), new FakeGameFlowService(), eventBus);
            presenter.Start();

            eventBus.Publish(new WorshipChangedEvent(10, 40, 100, WorshipSource.Awe));

            Assert.AreEqual(40, view.WorshipCurrent);
            Assert.AreEqual(100, view.WorshipTarget);
        }

        [Test]
        public void AlignmentChangedEvent_UpdatesView()
        {
            var view = new FakeLevelHudView();
            var eventBus = new EventBus();
            var presenter = new LevelHudPresenter(view, new FakeWorshipService(), new FakeAlignmentService(), new FakeGameFlowService(), eventBus);
            presenter.Start();

            eventBus.Publish(new AlignmentChangedEvent(0f, -20f));

            Assert.AreEqual(-0.2f, view.AlignmentScore, 0.001f); // нормализовано −1..+1: -20 / MaxScore(100)
        }

        [Test]
        public void AlignmentAtMinScore_NormalizesToMinusOne()
        {
            var view = new FakeLevelHudView();
            var alignment = new FakeAlignmentService { Score = -100f };
            var presenter = new LevelHudPresenter(view, new FakeWorshipService(), alignment, new FakeGameFlowService(), new FakeEventBus());

            presenter.Start();

            Assert.AreEqual(-1f, view.AlignmentScore, 0.001f);
        }

        [Test]
        public void AlignmentAtMaxScore_NormalizesToPlusOne()
        {
            var view = new FakeLevelHudView();
            var alignment = new FakeAlignmentService { Score = 100f };
            var presenter = new LevelHudPresenter(view, new FakeWorshipService(), alignment, new FakeGameFlowService(), new FakeEventBus());

            presenter.Start();

            Assert.AreEqual(1f, view.AlignmentScore, 0.001f);
        }

        [Test]
        public void PauseClicked_NotPaused_PushesPausedState()
        {
            var view = new FakeLevelHudView();
            var gameFlow = new FakeGameFlowService { Current = GameState.LevelPlaying };
            var presenter = new LevelHudPresenter(view, new FakeWorshipService(), new FakeAlignmentService(), gameFlow, new FakeEventBus());
            presenter.Start();

            view.RaisePauseClicked();

            Assert.AreEqual(GameState.Paused, gameFlow.Current);
            Assert.AreEqual(1, gameFlow.PushCount);
        }

        [Test]
        public void PauseClicked_AlreadyPaused_DoesNotPushAgain()
        {
            var view = new FakeLevelHudView();
            var gameFlow = new FakeGameFlowService { Current = GameState.Paused };
            var presenter = new LevelHudPresenter(view, new FakeWorshipService(), new FakeAlignmentService(), gameFlow, new FakeEventBus());
            presenter.Start();

            view.RaisePauseClicked();

            Assert.AreEqual(0, gameFlow.PushCount);
        }

        [Test]
        public void Dispose_UnsubscribesFromView()
        {
            var view = new FakeLevelHudView();
            var gameFlow = new FakeGameFlowService { Current = GameState.LevelPlaying };
            var presenter = new LevelHudPresenter(view, new FakeWorshipService(), new FakeAlignmentService(), gameFlow, new FakeEventBus());
            presenter.Start();

            presenter.Dispose();
            view.RaisePauseClicked();

            Assert.AreEqual(0, gameFlow.PushCount);
        }
    }
}
