using NUnit.Framework;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Flow.States;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="PausedState"/> (game-design §9, §15, §19, шаг 5).</summary>
    public class PausedStateTests
    {
        private sealed class FakeTimeScaleProvider : ITimeScaleProvider
        {
            public float TimeScale { get; set; } = 1f;
        }

        [Test]
        public void Enter_SetsTimeScaleToZero()
        {
            var provider = new FakeTimeScaleProvider { TimeScale = 1f };
            var state = new PausedState(provider);

            state.Enter();

            Assert.AreEqual(0f, provider.TimeScale);
        }

        [Test]
        public void Exit_RestoresPreviousTimeScale()
        {
            var provider = new FakeTimeScaleProvider { TimeScale = 1f };
            var state = new PausedState(provider);

            state.Enter();
            state.Exit();

            Assert.AreEqual(1f, provider.TimeScale);
        }

        [Test]
        public void EnterExit_WithNonDefaultPreviousScale_RestoresExactValue()
        {
            var provider = new FakeTimeScaleProvider { TimeScale = 0.5f };
            var state = new PausedState(provider);

            state.Enter();
            Assert.AreEqual(0f, provider.TimeScale);

            state.Exit();
            Assert.AreEqual(0.5f, provider.TimeScale);
        }

        [Test]
        public void EnterExit_PreviousScaleWasAlreadyZero_RestoresOne_AsSafetyFallback()
        {
            // Страховка (game-design §15): если TimeScale на момент Enter уже был 0 (повторный
            // Enter без Exit, либо игра уже была на паузе по другой причине) — Exit восстанавливает
            // 1, а не 0, иначе игра навсегда осталась бы физически на паузе.
            var provider = new FakeTimeScaleProvider { TimeScale = 0f };
            var state = new PausedState(provider);

            state.Enter();
            state.Exit();

            Assert.AreEqual(1f, provider.TimeScale);
        }
    }
}
