using System;
using NUnit.Framework;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Localization;
using BlackWhiteLike.Views.UI.Overlay;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="LevelStalledPresenter"/> (game-design §9, §10, §19, шаг 5, шаг 10).</summary>
    public class LevelStalledPresenterTests
    {
        private sealed class FakeLevelStalledView : ILevelStalledView
        {
            public bool IsVisible { get; private set; }
            public int ShowCount { get; private set; }
            public int HideCount { get; private set; }
            public string LastQuestionText { get; private set; }

            public event Action RefillClicked;
            public event Action DeclineClicked;

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

            public void SetQuestionText(string text) => LastQuestionText = text;

            public void RaiseRefillClicked() => RefillClicked?.Invoke();
            public void RaiseDeclineClicked() => DeclineClicked?.Invoke();
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

        private sealed class FakeLevelFlowService : ILevelFlowService
        {
            public int AcknowledgeStallDeclinedCallCount { get; private set; }

            public float Elapsed { get; set; }
            public bool IsRunning { get; set; }
            public bool IsStalled { get; set; }

            public void StartLevel()
            {
            }

            public void Tick(float dt)
            {
            }

            public void AcknowledgeStallDeclined() => AcknowledgeStallDeclinedCallCount++;
        }

        private sealed class FakeLocalizationService : ILocalizationService
        {
            public GameLocale CurrentLocale { get; set; } = GameLocale.Ru;

            // Тот же текст, что раньше был захардкожен в LevelStalledPresenter.QuestionText —
            // презентер теперь резолвит его через GetText(LocalizationKeys.LevelStalledQuestion).
            public string GetText(string key)
            {
                if (key == LocalizationKeys.LevelStalledQuestion)
                {
                    return CurrentLocale == GameLocale.En
                        ? "Watch an ad to refill your faith?"
                        : "Посмотреть рекламу, чтобы пополнить веру?";
                }

                return key;
            }
        }

        private static LevelStalledPresenter CreatePresenter(
            FakeLevelStalledView view, IEventBus eventBus, FakeFaithService faith,
            FakeLevelFlowService levelFlow = null, FakeLocalizationService localization = null) =>
            new LevelStalledPresenter(view, eventBus, faith, levelFlow ?? new FakeLevelFlowService(),
                localization ?? new FakeLocalizationService());

        [Test]
        public void Start_HidesViewInitially()
        {
            var view = new FakeLevelStalledView();
            var presenter = CreatePresenter(view, new FakeEventBus(), new FakeFaithService());

            presenter.Start();

            Assert.AreEqual(1, view.HideCount);
            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void LevelStalledEvent_ShowsView()
        {
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService());
            presenter.Start();

            eventBus.Publish(new LevelStalledEvent());

            Assert.IsTrue(view.IsVisible);
        }

        [Test]
        public void LevelStalledEvent_SetsQuestionText_BeforeShow()
        {
            // Шаг 10: оверлей — окно rewarded-согласия с вопросом, не простой рефил.
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService());
            presenter.Start();

            eventBus.Publish(new LevelStalledEvent());

            Assert.IsFalse(string.IsNullOrEmpty(view.LastQuestionText));
        }

        // --- Добивка ревью: вопрос резолвится через ILocalizationService, а не хардкод-константу ---

        [Test]
        public void LevelStalledEvent_RuLocale_SetsRussianQuestionText()
        {
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var localization = new FakeLocalizationService { CurrentLocale = GameLocale.Ru };
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService(), localization: localization);
            presenter.Start();

            eventBus.Publish(new LevelStalledEvent());

            Assert.AreEqual("Посмотреть рекламу, чтобы пополнить веру?", view.LastQuestionText);
        }

        [Test]
        public void LevelStalledEvent_EnLocale_SetsEnglishQuestionText()
        {
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var localization = new FakeLocalizationService { CurrentLocale = GameLocale.En };
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService(), localization: localization);
            presenter.Start();

            eventBus.Publish(new LevelStalledEvent());

            Assert.AreEqual("Watch an ad to refill your faith?", view.LastQuestionText);
        }

        [Test]
        public void RefillClicked_CallsRefillToCapacity_AndHidesView()
        {
            var view = new FakeLevelStalledView();
            var faith = new FakeFaithService { Current = 0f, Capacity = 100f };
            var presenter = CreatePresenter(view, new FakeEventBus(), faith);
            presenter.Start();
            view.Show(); // симулируем видимость перед кликом

            view.RaiseRefillClicked();

            Assert.AreEqual(1, faith.RefillCallCount);
            Assert.AreEqual(100f, faith.Current);
            Assert.IsFalse(view.IsVisible);
        }

        // --- Шаг 10: DeclineClicked — отказ от rewarded-согласия, без пополнения веры ---

        [Test]
        public void DeclineClicked_HidesView_WithoutRefillingFaith()
        {
            var view = new FakeLevelStalledView();
            var faith = new FakeFaithService { Current = 0f, Capacity = 100f };
            var presenter = CreatePresenter(view, new FakeEventBus(), faith);
            presenter.Start();
            view.Show();

            view.RaiseDeclineClicked();

            Assert.AreEqual(0, faith.RefillCallCount);
            Assert.AreEqual(0f, faith.Current);
            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void DeclineClicked_CallsAcknowledgeStallDeclined_OnLevelFlowService()
        {
            var view = new FakeLevelStalledView();
            var levelFlow = new FakeLevelFlowService();
            var presenter = CreatePresenter(view, new FakeEventBus(), new FakeFaithService(), levelFlow);
            presenter.Start();

            view.RaiseDeclineClicked();

            Assert.AreEqual(1, levelFlow.AcknowledgeStallDeclinedCallCount);
        }

        [Test]
        public void FaithChangedEvent_PositiveNewValue_HidesView()
        {
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService());
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
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService());
            presenter.Start();
            view.Show();
            var hideCountBefore = view.HideCount;

            eventBus.Publish(new FaithChangedEvent(5f, 0f, 100f));

            Assert.AreEqual(hideCountBefore, view.HideCount);
        }

        // --- Шаг 10, волна 2: MiracleDeniedEvent — второй путь показа, независимый от кулдауна ---

        [Test]
        public void MiracleDeniedEvent_ShowsView()
        {
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService());
            presenter.Start();

            eventBus.Publish(new MiracleDeniedEvent(MiracleType.Rain, 20, 5f));

            Assert.IsTrue(view.IsVisible);
        }

        [Test]
        public void MiracleDeniedEvent_SetsQuestionText_BeforeShow()
        {
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService());
            presenter.Start();

            eventBus.Publish(new MiracleDeniedEvent(MiracleType.Rain, 20, 5f));

            Assert.IsFalse(string.IsNullOrEmpty(view.LastQuestionText));
        }

        [Test]
        public void MiracleDeniedEvent_WhileAlreadyVisible_IsNoOp()
        {
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService());
            presenter.Start();
            eventBus.Publish(new LevelStalledEvent());
            var showCountBefore = view.ShowCount;

            eventBus.Publish(new MiracleDeniedEvent(MiracleType.Rain, 20, 5f));

            Assert.AreEqual(showCountBefore, view.ShowCount);
        }

        [Test]
        public void MiracleDeniedEvent_IgnoresStallReshowCooldown()
        {
            // Кулдаун (LevelFlowService.StallReshowCooldownAfterDecline) относится только к
            // автоматическому LevelStalledEvent — презентер его вообще не читает для этого пути,
            // MiracleDeniedEvent всегда показывает окно немедленно (если оно ещё не открыто).
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService());
            presenter.Start();
            eventBus.Publish(new LevelStalledEvent());
            view.RaiseDeclineClicked(); // окно закрыто, "кулдаун" (в LevelFlowService) начался бы здесь

            eventBus.Publish(new MiracleDeniedEvent(MiracleType.Rain, 20, 5f));

            Assert.IsTrue(view.IsVisible);
        }

        [Test]
        public void DeclineClicked_AfterMiracleDeniedShow_CallsAcknowledgeStallDeclined_AndHides()
        {
            // Decline работает так же, откуда бы окно ни было показано.
            var view = new FakeLevelStalledView();
            var eventBus = new EventBus();
            var levelFlow = new FakeLevelFlowService();
            var presenter = CreatePresenter(view, eventBus, new FakeFaithService(), levelFlow);
            presenter.Start();
            eventBus.Publish(new MiracleDeniedEvent(MiracleType.Rain, 20, 5f));

            view.RaiseDeclineClicked();

            Assert.AreEqual(1, levelFlow.AcknowledgeStallDeclinedCallCount);
            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void Dispose_UnsubscribesFromView()
        {
            var view = new FakeLevelStalledView();
            var faith = new FakeFaithService();
            var levelFlow = new FakeLevelFlowService();
            var presenter = CreatePresenter(view, new FakeEventBus(), faith, levelFlow);
            presenter.Start();

            presenter.Dispose();
            view.RaiseRefillClicked();
            view.RaiseDeclineClicked();

            Assert.AreEqual(0, faith.RefillCallCount);
            Assert.AreEqual(0, levelFlow.AcknowledgeStallDeclinedCallCount);
        }
    }
}
