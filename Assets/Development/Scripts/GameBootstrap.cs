using UnityEngine;
using BlackWhiteLike.Models;
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

        public GameBootstrap(IGameFlowService gameFlowService)
        {
            _gameFlowService = gameFlowService;
        }

        public void Start()
        {
            _gameFlowService.Start(GameState.MainMenu);
            Debug.Log("[Bootstrap] Started, state = MainMenu");

            // TODO шаг 2: убрать smoke-тест push-pop — временная проверка, что push-pop FSM
            // работает end-to-end через IGameFlowService уже с первого шага (game-design §15).
            _gameFlowService.PushState(GameState.Paused);
            Debug.Log($"[Bootstrap] Smoke-test push-pop: pushed Paused, Current = {_gameFlowService.Current}");
            _gameFlowService.PopState();
            Debug.Log($"[Bootstrap] Smoke-test push-pop: popped, Current = {_gameFlowService.Current}");
        }
    }
}
