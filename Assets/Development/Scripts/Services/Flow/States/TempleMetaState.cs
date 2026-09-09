using BlackWhiteLike.Core.StateMachine;
using UnityEngine;

namespace BlackWhiteLike.Services.Flow.States
{
    /// <summary>
    /// Мета-состояние Temple (game-design §7-§9, шаг 8) — тонкое, вся логика в <c>TemplePresenter</c>/
    /// <c>EconomyService</c>. Заменяет <c>NullState</c>-заглушку шага 5.
    /// </summary>
    public sealed class TempleMetaState : IState
    {
        public void Enter() => Debug.Log("[TempleMetaState] Enter");

        public void Exit() => Debug.Log("[TempleMetaState] Exit");

        public void Tick()
        {
        }
    }
}
