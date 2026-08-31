using BlackWhiteLike.Core.StateMachine;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Flow
{
    /// <summary>Реализация <see cref="IGameStateBinding"/> (game-design §9, шаг 5) — простая пара ключ/состояние.</summary>
    public sealed class GameStateBinding : IGameStateBinding
    {
        public GameState Key { get; }
        public IState State { get; }

        public GameStateBinding(GameState key, IState state)
        {
            Key = key;
            State = state;
        }
    }
}
