using UnityEngine;
using BlackWhiteLike.Services.Flow;
using VContainer.Unity;

namespace BlackWhiteLike
{
    /// <summary>
    /// Точка входа проекта (game-design §19, шаг 1). Pure C#, <see cref="IStartable"/> —
    /// в сцене отдельного GameObject не требует, запускается VContainer при старте контейнера.
    ///
    /// Ограничение порядка регистрации (ревью шага 7, косметика): <c>GameBootstrap</c> зарегистрирован
    /// одним из ПЕРВЫХ EntryPoint'ов в <c>GameLifetimeScope</c> (сразу после <c>GameFlowService</c>,
    /// строго до всех презентеров) — <see cref="Start"/> выставляет состояние FSM
    /// (<c>IGameFlowService.Start</c>) ДО того, как VContainer вызовет <c>Start()</c> у презентеров.
    /// Поэтому <c>IState.Enter()</c> состояния, которое выставляется этим первым переходом, НЕ должен
    /// публиковать события через <c>IEventBus</c> — презентеры-подписчики ещё не подписались (их
    /// <c>Start()</c> ещё не вызван). Если публикация из <c>Enter()</c> понадобится — сначала
    /// пересмотреть порядок регистрации EntryPoint'ов в GameLifetimeScope.
    /// </summary>
    public sealed class GameBootstrap : IStartable
    {
        private readonly IGameFlowService _gameFlowService;
        private readonly BootstrapSettings _settings;

        public GameBootstrap(IGameFlowService gameFlowService, BootstrapSettings settings)
        {
            _gameFlowService = gameFlowService;
            _settings = settings;
        }

        public void Start()
        {
            // Шаг 5: стартовое состояние берётся из Composition Root (BootstrapSettings), а не
            // захардкожено GameState.MainMenu — сцена уровня стартует сразу в LevelPlaying.
            _gameFlowService.Start(_settings.InitialState);
            Debug.Log($"[Bootstrap] Started, state = {_settings.InitialState}");
        }
    }
}
