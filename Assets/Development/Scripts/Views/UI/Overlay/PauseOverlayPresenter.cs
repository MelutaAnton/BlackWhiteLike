using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Services.Flow;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>MVP-Presenter оверлея паузы (game-design §9, §10, §13, шаг 5). Pure C#.</summary>
    public sealed class PauseOverlayPresenter : IStartable, IDisposable
    {
        private readonly IPauseOverlayView _view;
        private readonly IGameFlowService _gameFlow;
        private readonly IEventBus _eventBus;

        private IDisposable _pausedSubscription;
        private IDisposable _resumedSubscription;

        public PauseOverlayPresenter(IPauseOverlayView view, IGameFlowService gameFlow, IEventBus eventBus)
        {
            _view = view;
            _gameFlow = gameFlow;
            _eventBus = eventBus;
        }

        public void Start()
        {
            _view.Hide();
            _view.ResumeClicked += OnResumeClicked;
            _pausedSubscription = _eventBus.Subscribe<GamePausedEvent>(OnPaused);
            _resumedSubscription = _eventBus.Subscribe<GameResumedEvent>(OnResumed);
        }

        public void Dispose()
        {
            _view.ResumeClicked -= OnResumeClicked;
            _pausedSubscription?.Dispose();
            _resumedSubscription?.Dispose();
        }

        private void OnPaused(GamePausedEvent evt) => _view.Show();

        private void OnResumed(GameResumedEvent evt) => _view.Hide();

        private void OnResumeClicked()
        {
            if (_gameFlow.IsPaused) _gameFlow.PopState();
        }
    }
}
