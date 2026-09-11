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
    /// Волна 2: вопрос читается из общей таблицы локализации (<c>ILocalizationService.GetText</c>,
    /// ключ <see cref="LocalizationKeys.LevelStalledQuestion"/>) — единственный источник текста,
    /// локальный <c>LocalizedString</c> убран (был тем же приёмом, что <c>TutorialStep.Text</c>).
    /// </summary>
    /// <remarks>
    /// Два независимых пути показа (шаг 10, волна 2, решение владельца):
    /// 1. <c>LevelStalledEvent</c> — автоматический soft-fail (<c>LevelFlowService.Tick</c>), с
    ///    кулдауном <c>LevelFlowService.StallReshowCooldownAfterDecline</c> после отказа.
    /// 2. <c>MiracleDeniedEvent</c> — явный тап игрока по чуду, на которое не хватило Faith
    ///    (<c>MiracleService.BeginCast</c>) — показывает окно НЕМЕДЛЕННО, кулдаун автоматического
    ///    пути на него не распространяется (он никогда не проверяется в <see cref="OnMiracleDenied"/>).
    /// Оба пути идут через один и тот же <see cref="_isVisible"/>-гейт: если окно уже открыто —
    /// повторный показ (из любого пути) — no-op.
    /// </remarks>
    public sealed class LevelStalledPresenter : IStartable, IDisposable
    {
        private readonly ILevelStalledView _view;
        private readonly IEventBus _eventBus;
        private readonly IFaithService _faithService;
        private readonly ILevelFlowService _levelFlowService;
        private readonly ILocalizationService _localizationService;

        private IDisposable _levelStalledSubscription;
        private IDisposable _miracleDeniedSubscription;
        private IDisposable _faithChangedSubscription;

        // Презентер сам отслеживает видимость (ILevelStalledView — тупой UI без геттера состояния,
        // не плодим его ради этого) — нужен, чтобы OnMiracleDenied не переоткрывал уже показанное
        // окно и чтобы отличать "уже открыто" от "открыть снова".
        private bool _isVisible;

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
            _isVisible = false;
            _view.RefillClicked += OnRefillClicked;
            _view.DeclineClicked += OnDeclineClicked;
            _levelStalledSubscription = _eventBus.Subscribe<LevelStalledEvent>(OnLevelStalled);
            _miracleDeniedSubscription = _eventBus.Subscribe<MiracleDeniedEvent>(OnMiracleDenied);
            _faithChangedSubscription = _eventBus.Subscribe<FaithChangedEvent>(OnFaithChanged);
        }

        public void Dispose()
        {
            _view.RefillClicked -= OnRefillClicked;
            _view.DeclineClicked -= OnDeclineClicked;
            _levelStalledSubscription?.Dispose();
            _miracleDeniedSubscription?.Dispose();
            _faithChangedSubscription?.Dispose();
        }

        private void OnLevelStalled(LevelStalledEvent evt) => ShowQuestion();

        /// <summary>
        /// Шаг 10, волна 2: явный тап по чуду без хватающей Faith — показываем НЕМЕДЛЕННО, минуя
        /// кулдаун автоматического soft-fail (см. remarks класса). Уже открытое окно — no-op.
        /// </summary>
        private void OnMiracleDenied(MiracleDeniedEvent evt)
        {
            if (_isVisible) return;

            ShowQuestion();
        }

        private void ShowQuestion()
        {
            _view.SetQuestionText(_localizationService.GetText(LocalizationKeys.LevelStalledQuestion));
            _view.Show();
            _isVisible = true;
        }

        private void OnRefillClicked()
        {
            // ADS_STUB: здесь будет показ rewarded-рекламы (SDK позже); по факту "просмотра" (согласия
            // игрока) — мгновенное пополнение Faith, как если бы реклама досмотрена до конца.
            _faithService.RefillToCapacity();
            _view.Hide();
            _isVisible = false;
        }

        /// <summary>
        /// Шаг 10: отказ — оверлей скрывается БЕЗ пополнения веры, игрок продолжает без Faith.
        /// Защёлка показа LevelFlowService сбрасывается с кулдауном (AcknowledgeStallDeclined) —
        /// повторный АВТОМАТИЧЕСКИЙ сталл покажет окно снова, но не мгновенно на следующем кадре;
        /// AcknowledgeStallDeclined — no-op, если сталла сейчас нет (например, окно было открыто
        /// через MiracleDeniedEvent) — кулдаун путь MiracleDeniedEvent не затрагивает.
        /// </summary>
        private void OnDeclineClicked()
        {
            _levelFlowService.AcknowledgeStallDeclined();
            _view.Hide();
            _isVisible = false;
        }

        private void OnFaithChanged(FaithChangedEvent evt)
        {
            if (evt.NewValue > 0f)
            {
                _view.Hide();
                _isVisible = false;
            }
        }
    }
}
