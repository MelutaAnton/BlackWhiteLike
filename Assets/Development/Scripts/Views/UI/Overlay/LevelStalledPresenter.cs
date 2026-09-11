using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Localization;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>
    /// MVP-Presenter soft-fail оверлея (game-design §9, §10, §13, шаг 5). Pure C#. Шаг 10: окно —
    /// вопрос rewarded-согласия ("посмотреть рекламу за веру?"), не простой рефил одной кнопкой.
    /// </summary>
    public sealed class LevelStalledPresenter : IStartable, IDisposable
    {
        // TODO: перенести в общую таблицу строк проекта следующей волной локализации (пока у каждого
        // презентера, использующего ILocalizationService, своя локальная LocalizedString — тот же
        // приём, что TutorialStep.Text).
        private static readonly LocalizedString QuestionText = new LocalizedString(
            "Посмотреть рекламу, чтобы пополнить веру?",
            "Watch an ad to refill your faith?");

        private readonly ILevelStalledView _view;
        private readonly IEventBus _eventBus;
        private readonly IFaithService _faithService;
        private readonly ILevelFlowService _levelFlowService;
        private readonly ILocalizationService _localizationService;

        private IDisposable _levelStalledSubscription;
        private IDisposable _faithChangedSubscription;

        public LevelStalledPresenter(
            ILevelStalledView view,
            IEventBus eventBus,
            IFaithService faithService,
            ILevelFlowService levelFlowService,
            ILocalizationService localizationService)
        {
            _view = view;
            _eventBus = eventBus;
            _faithService = faithService;
            _levelFlowService = levelFlowService;
            _localizationService = localizationService;
        }

        public void Start()
        {
            _view.Hide();
            _view.RefillClicked += OnRefillClicked;
            _view.DeclineClicked += OnDeclineClicked;
            _levelStalledSubscription = _eventBus.Subscribe<LevelStalledEvent>(OnLevelStalled);
            _faithChangedSubscription = _eventBus.Subscribe<FaithChangedEvent>(OnFaithChanged);
        }

        public void Dispose()
        {
            _view.RefillClicked -= OnRefillClicked;
            _view.DeclineClicked -= OnDeclineClicked;
            _levelStalledSubscription?.Dispose();
            _faithChangedSubscription?.Dispose();
        }

        private void OnLevelStalled(LevelStalledEvent evt)
        {
            _view.SetQuestionText(QuestionText.Resolve(_localizationService.CurrentLocale));
            _view.Show();
        }

        private void OnRefillClicked()
        {
            // ADS_STUB: здесь будет показ rewarded-рекламы (SDK позже); по факту "просмотра" (согласия
            // игрока) — мгновенное пополнение Faith, как если бы реклама досмотрена до конца.
            _faithService.RefillToCapacity();
            _view.Hide();
        }

        /// <summary>
        /// Шаг 10: отказ — оверлей скрывается БЕЗ пополнения веры, игрок продолжает без Faith.
        /// Защёлка показа LevelFlowService сбрасывается с кулдауном (AcknowledgeStallDeclined) —
        /// повторный сталл покажет окно снова, но не мгновенно на следующем кадре.
        /// </summary>
        private void OnDeclineClicked()
        {
            _levelFlowService.AcknowledgeStallDeclined();
            _view.Hide();
        }

        private void OnFaithChanged(FaithChangedEvent evt)
        {
            if (evt.NewValue > 0f) _view.Hide();
        }
    }
}
