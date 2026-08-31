using System;
using NUnit.Framework;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Views.UI.Overlay;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="LevelStalledPresenter"/> (game-design §9, §10, §19, шаг 5).</summary>
    public class LevelStalledPresenterTests
    {
        private sealed class FakeLevelStalledView : ILevelStalledView
        {
            public bool IsVisible { get; private set; }
            public int ShowCount { get; private set; }
            public int HideCount { get; private set; }

            public event Action RefillClicked;

            public void Show()
            {
                IsVisible = true;
                ShowCount++;
            }

            public void Hide()
            {
                IsVisible = false;
                HideCount++;
            }

            public void RaiseRefillClicked() => RefillClicked?.Invoke();
        }

        private sealed class FakeFaithService : IFaithService
        {
            public float Current { get; set; }
            public float Capacity { get; set; } = 100f;
            public int RefillCallCount { get; private set; }

            public bool CanAfford(float amount) => Current >= amount;

            public bool TrySpend(float amount)
            {
                if (Current < amount) return false;
                Current -= amount;
                return true;
            }

            public void Tick(float deltaTime)
            {
            }

            public void RefillToCapacity()
            {
                Current = Capacity;
                RefillCallCount++;
            }
        }

        [Test]
        public void Start_HidesViewInitially()
        {
            var view = new FakeLevelStalledView();
            var presenter = new LevelStalledPresenter(view, new FakeEventBus(), new FakeFaithService());

            presenter.Start();

            Assert.AreEqual(1, view.HideCount);
            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void LevelStalledEvent_ShowsView()
        {
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var presenter = new LevelStalledPresenter(view, eventBus, new FakeFaithService());
            presenter.Start();

            eventBus.Publish(new LevelStalledEvent());

            Assert.IsTrue(view.IsVisible);
        }

        [Test]
        public void RefillClicked_CallsRefillToCapacity_AndHidesView()
        {
            var view = new FakeLevelStalledView();
            var faith = new FakeFaithService { Current = 0f, Capacity = 100f };
            var presenter = new LevelStalledPresenter(view, new FakeEventBus(), faith);
            presenter.Start();
            view.Show(); // симулируем видимость перед кликом

            view.RaiseRefillClicked();

            Assert.AreEqual(1, faith.RefillCallCount);
            Assert.AreEqual(100f, faith.Current);
            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void FaithChangedEvent_PositiveNewValue_HidesView()
        {
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var presenter = new LevelStalledPresenter(view, eventBus, new FakeFaithService());
            presenter.Start();
            view.Show();

            eventBus.Publish(new FaithChangedEvent(0f, 10f, 100f));

            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void FaithChangedEvent_ZeroNewValue_DoesNotHide()
        {
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var presenter = new LevelStalledPresenter(view, eventBus, new FakeFaithService());
            presenter.Start();
            view.Show();
            var hideCountBefore = view.HideCount;

            eventBus.Publish(new FaithChangedEvent(5f, 0f, 100f));

            Assert.AreEqual(hideCountBefore, view.HideCount);
        }

        [Test]
        public void Dispose_UnsubscribesFromView()
        {
            var view = new FakeLevelStalledView();
            var faith = new FakeFaithService();
            var presenter = new LevelStalledPresenter(view, new FakeEventBus(), faith);
            presenter.Start();

            presenter.Dispose();
            view.RaiseRefillClicked();

            Assert.AreEqual(0, faith.RefillCallCount);
        }
    }
}
