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
    /// <summary>MVP-Presenter экрана итогов уровня (game-design §10, §13, шаг 5; §7, шаг 7 — возврат в кампанию). Pure C#.</summary>
    public sealed class LevelCompletePresenter : IStartable, IDisposable
    {
        private readonly ILevelCompleteView _view;
        private readonly IEventBus _eventBus;
        private readonly LevelConfig _levelConfig;
        private readonly IWorshipService _worshipService;
        private readonly IGameFlowService _gameFlow;
        private readonly ILevelSelectionService _selection;
        private readonly ISceneLoaderService _sceneLoader;

        private IDisposable _levelCompletedSubscription;

        public LevelCompletePresenter(
            ILevelCompleteView view,
            IEventBus eventBus,
            LevelConfig levelConfig,
            IWorshipService worshipService,
            IGameFlowService gameFlow,
            ILevelSelectionService selection,
            ISceneLoaderService sceneLoader)
        {
            _view = view;
            _eventBus = eventBus;
            _levelConfig = levelConfig;
            _worshipService = worshipService;
            _gameFlow = gameFlow;
            _selection = selection;
            _sceneLoader = sceneLoader;
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
        /// Шаг 7: "доступны selection+sceneLoader" (реальный кампания-флоу, root есть) определяется
        /// через <see cref="ILevelSelectionService.TryGetSelected"/> — сервис зарегистрирован
        /// безусловно (root или локальный fallback, см. remarks GameLifetimeScope), но
        /// <c>Select</c> на нём вызывает только <c>LevelSelectPresenter.OnCardClicked</c> — то есть
        /// реальный переход через экран выбора уровня. В тестовой сцене без root/LevelSelect (level
        /// прошит вручную на GameLifetimeScope) выбора никогда не было — сигнал совпадает с "root
        /// отсутствует" без прямой зависимости presenter'а на Installers.RootLifetimeScope.
        /// </summary>
        private void OnContinueClicked()
        {
            _view.Hide();

            if (_selection.TryGetSelected(out _))
            {
                // Реальный кампания-флоу: назад в MainMenu, сразу на панель выбора уровня.
                // GameState.TempleMeta (Temple, шаг 8) здесь больше не используется.
                _selection.RequestReturnToLevelSelect();
                _sceneLoader.Load(SceneNames.MainMenu);
            }
            else
            {
                Debug.Log("[LevelCompletePresenter] Уровень не был выбран через LevelSelect (тестовая сцена без root) — переход в TempleMeta, как на шаге 5.");
                _gameFlow.ChangeState(GameState.TempleMeta);
            }
        }
    }
}
