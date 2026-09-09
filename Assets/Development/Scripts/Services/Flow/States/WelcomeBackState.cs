using BlackWhiteLike.Core.StateMachine;
using UnityEngine;

namespace BlackWhiteLike.Services.Flow.States
{
    /// <summary>
    /// Оверлей "с возвращением" — офлайн-доход (game-design §9, §13, §15, шаг 8). PUSH-состояние
    /// (как <see cref="PausedState"/>): показ по <c>IGameFlowService.PushState(GameState.WelcomeBack)</c>,
    /// закрытие — <c>PopState()</c>. Тонкое, вся логика в <c>WelcomeBackPresenter</c>/<c>IIdleIncomeService</c>.
    /// Заменяет <c>NullState</c>-заглушку шага 5.
    /// </summary>
    public sealed class WelcomeBackState : IState
    {
        public void Enter() => Debug.Log("[WelcomeBackState] Enter");

        public void Exit() => Debug.Log("[WelcomeBackState] Exit");

        public void Tick()
        {
        }
    }
}
