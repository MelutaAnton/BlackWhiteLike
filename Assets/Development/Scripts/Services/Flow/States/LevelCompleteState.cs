using UnityEngine;
using BlackWhiteLike.Core.StateMachine;

namespace BlackWhiteLike.Services.Flow.States
{
    /// <summary>
    /// Состояние экрана итогов уровня (game-design §9, шаг 5) — сам экран собирает
    /// <c>LevelCompletePresenter</c> (подписан на <c>LevelCompletedEvent</c> напрямую, не на Enter
    /// этого состояния), здесь только логи входа/выхода — задел на будущее поведение (например,
    /// блокировку ввода уровня).
    /// </summary>
    public sealed class LevelCompleteState : IState
    {
        public void Enter() => Debug.Log("[LevelCompleteState] Enter");

        public void Exit() => Debug.Log("[LevelCompleteState] Exit");

        public void Tick()
        {
        }
    }
}
