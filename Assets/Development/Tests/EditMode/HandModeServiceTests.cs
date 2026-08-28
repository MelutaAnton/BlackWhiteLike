using System.Linq;
using NUnit.Framework;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Hand;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="HandModeService"/> (game-design §3, §4, §19, шаг 4).</summary>
    public class HandModeServiceTests
    {
        [Test]
        public void Mode_StartsAsGrab()
        {
            var service = new HandModeService(new FakeEventBus());

            Assert.AreEqual(HandMode.Grab, service.Mode);
            Assert.IsFalse(service.TryGetSelectedMiracle(out _));
        }

        [Test]
        public void SelectMiracle_SwitchesToMiracleMode_AndPublishesSelectedTrue()
        {
            var eventBus = new FakeEventBus();
            var service = new HandModeService(eventBus);

            service.SelectMiracle(MiracleType.Rain);

            Assert.AreEqual(HandMode.Miracle, service.Mode);
            Assert.IsTrue(service.TryGetSelectedMiracle(out var type));
            Assert.AreEqual(MiracleType.Rain, type);

            var evt = eventBus.PublishedEvents.OfType<MiracleSelectedEvent>().Single();
            Assert.AreEqual(MiracleType.Rain, evt.Type);
            Assert.IsTrue(evt.Selected);
        }

        [Test]
        public void SelectMiracle_SameTypeAgain_CancelsSelection_ReturnsToGrab()
        {
            var eventBus = new FakeEventBus();
            var service = new HandModeService(eventBus);
            service.SelectMiracle(MiracleType.Rain);
            eventBus.PublishedEvents.Clear();

            service.SelectMiracle(MiracleType.Rain);

            Assert.AreEqual(HandMode.Grab, service.Mode);
            Assert.IsFalse(service.TryGetSelectedMiracle(out _));

            var evt = eventBus.PublishedEvents.OfType<MiracleSelectedEvent>().Single();
            Assert.AreEqual(MiracleType.Rain, evt.Type);
            Assert.IsFalse(evt.Selected);
        }

        [Test]
        public void SelectMiracle_DifferentType_PublishesTwoEvents_DeselectOldSelectNew()
        {
            var eventBus = new FakeEventBus();
            var service = new HandModeService(eventBus);
            service.SelectMiracle(MiracleType.Rain);
            eventBus.PublishedEvents.Clear();

            service.SelectMiracle(MiracleType.Fireball);

            Assert.AreEqual(HandMode.Miracle, service.Mode);
            Assert.IsTrue(service.TryGetSelectedMiracle(out var type));
            Assert.AreEqual(MiracleType.Fireball, type);

            var events = eventBus.PublishedEvents.OfType<MiracleSelectedEvent>().ToList();
            Assert.AreEqual(2, events.Count);
            Assert.IsTrue(events.Any(e => e.Type == MiracleType.Rain && !e.Selected));
            Assert.IsTrue(events.Any(e => e.Type == MiracleType.Fireball && e.Selected));
        }

        [Test]
        public void ClearSelection_ReturnsToGrab_AndPublishesSelectedFalse()
        {
            var eventBus = new FakeEventBus();
            var service = new HandModeService(eventBus);
            service.SelectMiracle(MiracleType.Rain);
            eventBus.PublishedEvents.Clear();

            service.ClearSelection();

            Assert.AreEqual(HandMode.Grab, service.Mode);
            var evt = eventBus.PublishedEvents.OfType<MiracleSelectedEvent>().Single();
            Assert.AreEqual(MiracleType.Rain, evt.Type);
            Assert.IsFalse(evt.Selected);
        }

        [Test]
        public void ClearSelection_WhenAlreadyGrab_DoesNotPublish()
        {
            var eventBus = new FakeEventBus();
            var service = new HandModeService(eventBus);

            service.ClearSelection();

            Assert.AreEqual(HandMode.Grab, service.Mode);
            Assert.IsEmpty(eventBus.PublishedEvents);
        }
    }
}
