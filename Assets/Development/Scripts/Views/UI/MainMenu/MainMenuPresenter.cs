using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Level;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.MainMenu
{
    /// <summary>
    /// MVP-Presenter главного меню (game-design §7, §9, шаг 7; §8, шаг 8 — вход в Temple). Pure C#.
    /// Показывает панель только в <c>GameState.MainMenu</c> (по <see cref="GameStateChangedEvent"/>) —
    /// переключение панелей MainMenu ⇄ LevelSelect делает UI-слой, сцена одна (решение владельца, шаг 7).
    /// </summary>
    /// <remarks>
    /// Порядок IStartable: этот презентер читает <see cref="IGameFlowService.Current"/> уже в своём
    /// <see cref="Start"/> — GameBootstrap обязан выставить стартовое состояние РАНЬШЕ (см. remarks
    /// GameLifetimeScope.Configure, где RegisterEntryPoint&lt;GameBootstrap&gt; поэтому перенесён в
    /// начало метода). Возврат с уровня (Continue на LevelComplete) определяется через
    /// <see cref="ILevelSelectionService.ConsumeReturnToLevelSelect"/> — сервис зарегистрирован
    /// безусловно (root или локальный fallback, см. GameLifetimeScope), поэтому обычный required
    /// конструктор-параметр, без опциональности.
    /// </remarks>
    public sealed class MainMenuPresenter : IStartable, IDisposable
    {
        private readonly IMainMenuView _view;
        private readonly IGameFlowService _gameFlow;
        private readonly IEventBus _eventBus;
        private readonly ILevelSelectionService _selection;
        private readonly ISceneLoaderService _sceneLoader;

        private IDisposable _stateChangedSubscription;

        public MainMenuPresenter(
            IMainMenuView view,
            IGameFlowService gameFlow,
            IEventBus eventBus,
            ILevelSelectionService selection,
            ISceneLoaderService sceneLoader)
        {
            _view = view;
            _gameFlow = gameFlow;
            _eventBus = eventBus;
            _selection = selection;
            _sceneLoader = sceneLoader;
        }

        public void Start()
        {
            _view.PlayClicked += OnPlayClicked;
            _view.TempleClicked += OnTempleClicked;
            _stateChangedSubscription = _eventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

            if (_selection.ConsumeReturnToLevelSelect())
            {
                // Возврат из уровня (game-design §7): сразу открыть панель выбора, минуя показ
                // MainMenu на один кадр.
                _gameFlow.ChangeState(GameState.LevelSelect);
            }
            else
            {
                _view.SetVisible(_gameFlow.Current == GameState.MainMenu);
            }
        }

        public void Dispose()
        {
            _view.PlayClicked -= OnPlayClicked;
            _view.TempleClicked -= OnTempleClicked;
            _stateChangedSubscription?.Dispose();
        }

        private void OnPlayClicked() => _gameFlow.ChangeState(GameState.LevelSelect);

        private void OnTempleClicked() => _sceneLoader.Load(SceneNames.Temple);

        // Ревью шага 8, ВАЖНО 7: WelcomeBack — PUSH-оверлей ПОВЕРХ меню (тот же паттерн, что
        // PauseOverlay не прячет HUD), панель меню должна остаться под ним, а не скрыться.
        private void OnGameStateChanged(GameStateChangedEvent evt) =>
            _view.SetVisible(evt.To == GameState.MainMenu || evt.To == GameState.WelcomeBack);
    }
}
