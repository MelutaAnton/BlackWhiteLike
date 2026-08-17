using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BlackWhiteLike.Core.Events;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="EventBus"/> (game-design §19, шаг 1).</summary>
    public class EventBusTests
    {
        private readonly struct TestEventA
        {
            public readonly int Value;
            public TestEventA(int value) => Value = value;
        }

        private readonly struct TestEventB
        {
            public readonly int Value;
            public TestEventB(int value) => Value = value;
        }

        [Test]
        public void Publish_DeliversEventToSubscriber()
        {
            var bus = new EventBus();
            var received = -1;
            bus.Subscribe<TestEventA>(evt => received = evt.Value);

            bus.Publish(new TestEventA(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Dispose_RemovesSubscription()
        {
            var bus = new EventBus();
            var callCount = 0;
            var subscription = bus.Subscribe<TestEventA>(_ => callCount++);

            subscription.Dispose();
            bus.Publish(new TestEventA(1));

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var bus = new EventBus();
            var subscription = bus.Subscribe<TestEventA>(_ => { });

            subscription.Dispose();

            Assert.DoesNotThrow(() => subscription.Dispose());
        }

        [Test]
        public void Unsubscribe_FromWithinHandler_DoesNotBreakIteration()
        {
            var bus = new EventBus();
            var secondHandlerCalled = false;
            var firstHandlerCallCount = 0;
            System.IDisposable firstSubscription = null;

            firstSubscription = bus.Subscribe<TestEventA>(_ =>
            {
                firstHandlerCallCount++;
                firstSubscription.Dispose();
            });
            bus.Subscribe<TestEventA>(_ => secondHandlerCalled = true);

            Assert.DoesNotThrow(() => bus.Publish(new TestEventA(1)));
            Assert.IsTrue(secondHandlerCalled);
            Assert.AreEqual(1, firstHandlerCallCount);

            // Повторная публикация — отписавшийся изнутри себя обработчик больше не вызывается.
            bus.Publish(new TestEventA(2));
            Assert.AreEqual(1, firstHandlerCallCount);
        }

        [Test]
        public void Publish_NestedPublishOfSameType_DoesNotBreakIteration()
        {
            var bus = new EventBus();
            var outerCalls = 0;

            // Обработчик события 1 сам публикует событие 2 того же типа — вложенный Publish
            // не должен ломать итерацию внешнего (счётчик глубины/снимок списка в EventChannel).
            bus.Subscribe<TestEventA>(evt =>
            {
                outerCalls++;
                if (evt.Value == 1)
                {
                    bus.Publish(new TestEventA(2));
                }
            });

            Assert.DoesNotThrow(() => bus.Publish(new TestEventA(1)));
            Assert.AreEqual(2, outerCalls); // сработал и на внешнем (1), и на вложенном (2) Publish
        }

        [Test]
        public void Unsubscribe_HandlerFurtherDownTheList_FromEarlierHandler_SkipsIt()
        {
            var bus = new EventBus();
            var firstCalled = false;
            var thirdCalled = false;
            System.IDisposable secondSubscription = null;

            // Первый обработчик отписывает ВТОРОЙ (стоящий дальше по списку, ещё не вызванный) —
            // не путать с самоотпиской из предыдущего теста.
            bus.Subscribe<TestEventA>(_ =>
            {
                firstCalled = true;
                secondSubscription.Dispose();
            });
            secondSubscription = bus.Subscribe<TestEventA>(
                _ => Assert.Fail("Отписанный до своего вызова обработчик не должен вызываться"));
            bus.Subscribe<TestEventA>(_ => thirdCalled = true);

            Assert.DoesNotThrow(() => bus.Publish(new TestEventA(1)));
            Assert.IsTrue(firstCalled);
            Assert.IsTrue(thirdCalled);
        }

        [Test]
        public void Publish_HandlerThrows_OtherHandlersStillCalled()
        {
            var bus = new EventBus();
            var secondCalled = false;

            bus.Subscribe<TestEventA>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<TestEventA>(_ => secondCalled = true);

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException.*boom"));
            Assert.DoesNotThrow(() => bus.Publish(new TestEventA(1)));
            Assert.IsTrue(secondCalled);
        }

        [Test]
        public void Subscribe_DuringPublish_DoesNotReceiveCurrentEvent()
        {
            var bus = new EventBus();
            var lateHandlerCalled = false;

            bus.Subscribe<TestEventA>(_ => bus.Subscribe<TestEventA>(__ => lateHandlerCalled = true));

            bus.Publish(new TestEventA(1));
            Assert.IsFalse(lateHandlerCalled);

            bus.Publish(new TestEventA(2));
            Assert.IsTrue(lateHandlerCalled);
        }

        [Test]
        public void DifferentEventTypes_DoNotCrossSubscribers()
        {
            var bus = new EventBus();
            var aCalled = false;
            var bCalled = false;

            bus.Subscribe<TestEventA>(_ => aCalled = true);
            bus.Subscribe<TestEventB>(_ => bCalled = true);

            bus.Publish(new TestEventB(1));

            Assert.IsFalse(aCalled);
            Assert.IsTrue(bCalled);
        }
    }
}
