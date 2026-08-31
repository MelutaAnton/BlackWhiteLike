using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Services.Faith;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>MVP-Presenter soft-fail оверлея (game-design §9, §10, §13, шаг 5). Pure C#.</summary>
    public sealed class LevelStalledPresenter : IStartable, IDisposable
    {
        private readonly ILevelStalledView _view;
        private readonly IEventBus _eventBus;
        private readonly IFaithService _faithService;

        private IDisposable _levelStalledSubscription;
        private IDisposable _faithChangedSubscription;

        public LevelStalledPresenter(ILevelStalledView view, IEventBus eventBus, IFaithService faithService)
        {
            _view = view;
            _eventBus = eventBus;
            _faithService = faithService;
        }

        public void Start()
        {
            _view.Hide();
            _view.RefillClicked += OnRefillClicked;
            _levelStalledSubscription = _eventBus.Subscribe<LevelStalledEvent>(OnLevelStalled);
            _faithChangedSubscription = _eventBus.Subscribe<FaithChangedEvent>(OnFaithChanged);
        }

        public void Dispose()
        {
            _view.RefillClicked -= OnRefillClicked;
            _levelStalledSubscription?.Dispose();
            _faithChangedSubscription?.Dispose();
        }

        private void OnLevelStalled(LevelStalledEvent evt) => _view.Show();

        private void OnRefillClicked()
        {
            // ADS_STUB: здесь будет rewarded-реклама, сейчас мгновенное пополнение Faith.
            _faithService.RefillToCapacity();
            _view.Hide();
        }

        private void OnFaithChanged(FaithChangedEvent evt)
        {
            if (evt.NewValue > 0f) _view.Hide();
        }
    }
}
