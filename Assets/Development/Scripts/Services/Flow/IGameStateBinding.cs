using BlackWhiteLike.Core.StateMachine;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Flow
{
    /// <summary>
    /// Именованный биндинг ключа FSM (<see cref="GameState"/>) на конкретный <see cref="IState"/>
    /// (game-design §9, шаг 5). Регистрируется в Composition Root как множественная реализация
    /// (<c>IEnumerable&lt;IGameStateBinding&gt;</c>) — <c>GameFlowService</c> собирает их все в
    /// конструкторе, а не хранит список состояний в своих зависимостях напрямую.
    /// </summary>
    public interface IGameStateBinding
    {
        GameState Key { get; }
        IState State { get; }
    }
}
