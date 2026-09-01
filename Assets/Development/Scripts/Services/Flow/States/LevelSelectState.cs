using UnityEngine;
using BlackWhiteLike.Core.StateMachine;

namespace BlackWhiteLike.Services.Flow.States
{
    /// <summary>
    /// Состояние FSM LevelSelect (game-design §7, §9, шаг 7) — тонкая заглушка по образцу
    /// <c>MainMenuState</c>: переключение панелей MainMenu ⇄ LevelSelect делает UI-слой
    /// (<c>MainMenuPresenter</c>/<c>LevelSelectPresenter</c>) по <c>GameStateChangedEvent</c>, само
    /// состояние только логирует переход. Заменяет заглушку <c>NullState</c>, использовавшуюся для
    /// этого ключа до шага 7.
    /// </summary>
    public sealed class LevelSelectState : IState
    {
        public void Enter() => Debug.Log("[LevelSelectState] Enter");

        public void Exit() => Debug.Log("[LevelSelectState] Exit");

        public void Tick()
        {
            // Поведения нет — вся логика в LevelSelectPresenter (UI-слой).
        }
    }
}
