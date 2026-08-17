using NUnit.Framework;
using BlackWhiteLike.Core.StateMachine;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты generic FSM <see cref="StateMachine{TKey}"/> (game-design §19, шаг 1).</summary>
    public class StateMachineTests
    {
        private enum TestKey
        {
            A,
            B,
            C
        }

        private sealed class SpyState : IState
        {
            public int EnterCount;
            public int ExitCount;
            public int TickCount;

            public void Enter() => EnterCount++;
            public void Exit() => ExitCount++;
            public void Tick() => TickCount++;
        }

        [Test]
        public void ChangeState_CallsExitOnCurrentAndEnterOnNew()
        {
            var machine = new StateMachine<TestKey>();
            var stateA = new SpyState();
            var stateB = new SpyState();
            machine.Register(TestKey.A, stateA);
            machine.Register(TestKey.B, stateB);

            machine.ChangeState(TestKey.A);
            machine.ChangeState(TestKey.B);

            Assert.AreEqual(1, stateA.EnterCount);
            Assert.AreEqual(1, stateA.ExitCount);
            Assert.AreEqual(1, stateB.EnterCount);
            Assert.AreEqual(0, stateB.ExitCount);
            Assert.AreEqual(TestKey.B, machine.Current);
        }

        [Test]
        public void Push_DoesNotExitLowerState()
        {
            var machine = new StateMachine<TestKey>();
            var stateA = new SpyState();
            var stateB = new SpyState();
            machine.Register(TestKey.A, stateA);
            machine.Register(TestKey.B, stateB);
            machine.ChangeState(TestKey.A);

            machine.Push(TestKey.B);

            Assert.AreEqual(0, stateA.ExitCount);
            Assert.AreEqual(1, stateB.EnterCount);
            Assert.AreEqual(TestKey.B, machine.Current);
        }

        [Test]
        public void Pop_ReturnsToLowerState_WithoutReEnter()
        {
            var machine = new StateMachine<TestKey>();
            var stateA = new SpyState();
            var stateB = new SpyState();
            machine.Register(TestKey.A, stateA);
            machine.Register(TestKey.B, stateB);
            machine.ChangeState(TestKey.A);
            machine.Push(TestKey.B);

            machine.Pop();

            Assert.AreEqual(1, stateB.ExitCount);
            Assert.AreEqual(1, stateA.EnterCount); // Enter вызывался только один раз — при ChangeState
            Assert.AreEqual(TestKey.A, machine.Current);
        }

        [Test]
        public void Pop_OnSingleState_IsSafeNoOp()
        {
            var machine = new StateMachine<TestKey>();
            var stateA = new SpyState();
            machine.Register(TestKey.A, stateA);
            machine.ChangeState(TestKey.A);

            Assert.DoesNotThrow(() => machine.Pop());
            Assert.AreEqual(0, stateA.ExitCount);
            Assert.AreEqual(TestKey.A, machine.Current);
        }

        [Test]
        public void Pop_OnEmptyStack_IsSafeNoOp()
        {
            var machine = new StateMachine<TestKey>();

            Assert.DoesNotThrow(() => machine.Pop());
        }

        [Test]
        public void Tick_OnlyTicksTopState()
        {
            var machine = new StateMachine<TestKey>();
            var stateA = new SpyState();
            var stateB = new SpyState();
            machine.Register(TestKey.A, stateA);
            machine.Register(TestKey.B, stateB);
            machine.ChangeState(TestKey.A);
            machine.Push(TestKey.B);

            machine.Tick();

            Assert.AreEqual(0, stateA.TickCount);
            Assert.AreEqual(1, stateB.TickCount);
        }
    }
}
