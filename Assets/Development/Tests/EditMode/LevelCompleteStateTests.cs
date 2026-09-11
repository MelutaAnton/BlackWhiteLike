using NUnit.Framework;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Flow.States;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="LevelCompleteState"/> (game-design §9, шаг 5; шаг 10 волна 2 — заморозка мира).</summary>
    public class LevelCompleteStateTests
    {
        private sealed class FakeTimeScaleProvider : ITimeScaleProvider
        {
            public float TimeScale { get; set; } = 1f;
        }

        [Test]
        public void Enter_SetsTimeScaleToZero()
        {
            var provider = new FakeTimeScaleProvider { TimeScale = 1f };
            var state = new LevelCompleteState(provider);

            state.Enter();

            Assert.AreEqual(0f, provider.TimeScale);
        }

        [Test]
        public void Exit_RestoresPreviousTimeScale()
        {
            var provider = new FakeTimeScaleProvider { TimeScale = 1f };
            var state = new LevelCompleteState(provider);

            state.Enter();
            state.Exit();

            Assert.AreEqual(1f, provider.TimeScale);
        }

        [Test]
        public void EnterExit_WithNonDefaultPreviousScale_RestoresExactValue()
        {
            var provider = new FakeTimeScaleProvider { TimeScale = 0.5f };
            var state = new LevelCompleteState(provider);

            state.Enter();
            Assert.AreEqual(0f, provider.TimeScale);

            state.Exit();
            Assert.AreEqual(0.5f, provider.TimeScale);
        }

        [Test]
        public void EnterExit_PreviousScaleWasAlreadyZero_RestoresOne_AsSafetyFallback()
        {
            // Страховка (симметрично PausedStateTests): если TimeScale на момент Enter уже был 0
            // (например, LevelComplete наступил во время Paused) — Exit восстанавливает 1, а не 0.
            var provider = new FakeTimeScaleProvider { TimeScale = 0f };
            var state = new LevelCompleteState(provider);

            state.Enter();
            state.Exit();

            Assert.AreEqual(1f, provider.TimeScale);
        }
    }
}
