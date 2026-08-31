using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Flow
{
    /// <summary>
    /// Стартовое состояние FSM (game-design §9, шаг 5) — задаётся Composition Root'ом за
    /// конкретную сцену и передаётся в <c>GameBootstrap</c> через DI вместо хардкода GameState.MainMenu.
    /// </summary>
    public sealed class BootstrapSettings
    {
        public GameState InitialState { get; }

        public BootstrapSettings(GameState initialState)
        {
            InitialState = initialState;
        }
    }
}
