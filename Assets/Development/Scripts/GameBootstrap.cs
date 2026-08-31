using UnityEngine;
using BlackWhiteLike.Services.Flow;
using VContainer.Unity;

namespace BlackWhiteLike
{
    /// <summary>
    /// Точка входа проекта (game-design §19, шаг 1). Pure C#, <see cref="IStartable"/> —
    /// в сцене отдельного GameObject не требует, запускается VContainer при старте контейнера.
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
