using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Worship;
using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>
    /// MVP-Presenter экрана итогов уровня (game-design §10, §13, шаг 5; §7, шаг 7 — возврат в
    /// кампанию; §9, шаг 8 — переход в Temple).
    /// </summary>
    public sealed class LevelCompletePresenter : IStartable, IDisposable
    {
        private readonly ILevelCompleteView _view;
        private readonly IEventBus _eventBus;
        private readonly LevelConfig _levelConfig;
        private readonly IWorshipService _worshipService;
        private readonly IGameFlowService _gameFlow;
        private readonly ILevelSelectionService _selection;
        private readonly ISceneLoaderService _sceneLoader;
        private readonly CampaignContext _campaignContext;

        private IDisposable _levelCompletedSubscription;

        public LevelCompletePresenter(
            ILevelCompleteView view,
            IEventBus eventBus,
            LevelConfig levelConfig,
            IWorshipService worshipService,
            IGameFlowService gameFlow,
            ILevelSelectionService selection,
            ISceneLoaderService sceneLoader,
            CampaignContext campaignContext)
        {
            _view = view;
            _eventBus = eventBus;
            _levelConfig = levelConfig;
            _worshipService = worshipService;
            _gameFlow = gameFlow;
            _selection = selection;
            _sceneLoader = sceneLoader;
            _campaignContext = campaignContext;
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

        /// <summary>
        /// Шаг 8: "реальный кампания-флоу доступен" — явный <see cref="CampaignContext.HasRoot"/>
        /// (закрывает долг шага 7 — раньше это выводилось косвенно через
        /// <c>ILevelSelectionService.TryGetSelected(out _)</c>, см. историю в CampaignContext).
        /// Выбор уровня сбрасывается всегда — актуален только до следующего LevelSelect.
        /// </summary>
        private void OnContinueClicked()
        {
            _view.Hide();
            _selection.ClearSelection();

            if (_campaignContext.HasRoot)
            {
                // Реальный кампания-флоу (game-design §9, шаг 8): после уровня — в Temple, не в MainMenu.
                _sceneLoader.Load(SceneNames.Temple);
            }
            else
            {
                Debug.Log("[LevelCompletePresenter] Root отсутствует (тестовая сцена) — переход в TempleMeta локально, как на шаге 5.");
                _gameFlow.ChangeState(GameState.TempleMeta);
            }
        }
    }
}
