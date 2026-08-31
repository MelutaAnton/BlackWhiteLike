using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Worship;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>MVP-Presenter экрана итогов уровня (game-design §10, §13, шаг 5). Pure C#.</summary>
    public sealed class LevelCompletePresenter : IStartable, IDisposable
    {
        private readonly ILevelCompleteView _view;
        private readonly IEventBus _eventBus;
        private readonly LevelConfig _levelConfig;
        private readonly IWorshipService _worshipService;
        private readonly IGameFlowService _gameFlow;

        private IDisposable _levelCompletedSubscription;

        public LevelCompletePresenter(
            ILevelCompleteView view,
            IEventBus eventBus,
            LevelConfig levelConfig,
            IWorshipService worshipService,
            IGameFlowService gameFlow)
        {
            _view = view;
            _eventBus = eventBus;
            _levelConfig = levelConfig;
            _worshipService = worshipService;
            _gameFlow = gameFlow;
        }

        public void Start()
        {
            _view.Hide();
            _view.ContinueClicked += OnContinueClicked;
            _levelCompletedSubscription = _eventBus.Subscribe<LevelCompletedEvent>(OnLevelCompleted);
        }

        public void Dispose()
        {
            _view.ContinueClicked -= OnContinueClicked;
            _levelCompletedSubscription?.Dispose();
        }

        private void OnLevelCompleted(LevelCompletedEvent evt)
        {
            _view.Show(_worshipService.Current, _worshipService.Target, _worshipService.AweTotal, _worshipService.FearTotal, _levelConfig.CoinsReward);
        }

        private void OnContinueClicked()
        {
            _view.Hide();
            // TODO шаг 8: сцена Temple — GameState.TempleMeta пока заглушка NullState в GameLifetimeScope.
            _gameFlow.ChangeState(GameState.TempleMeta);
        }
    }
}
