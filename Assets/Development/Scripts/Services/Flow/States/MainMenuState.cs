using UnityEngine;
using BlackWhiteLike.Core.StateMachine;

namespace BlackWhiteLike.Services.Flow.States
{
    /// <summary>
    /// Единственное состояние FSM на шаге 1 (game-design §19, шаг 1). Пока без поведения —
    /// только логирование входа/выхода, чтобы визуально подтвердить работу FSM в консоли.
    /// </summary>
    public sealed class MainMenuState : IState
    {
        public void Enter() => Debug.Log("[MainMenuState] Enter");

        public void Exit() => Debug.Log("[MainMenuState] Exit");

        public void Tick()
        {
            // Поведения нет — панель Main Menu появится в шаге 8/10 (UI-слой).
        }
    }
}
