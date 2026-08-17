using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Flow
{
    /// <summary>
    /// Обёртка над <see cref="Core.StateMachine.StateMachine{TKey}"/> для <see cref="GameState"/>
    /// (game-design §9). Публикует события смены состояния через <see cref="Core.Events.IEventBus"/>.
    /// </summary>
    public interface IGameFlowService
    {
        GameState Current { get; }

        /// <summary>Стартовое состояние FSM. Событие смены НЕ публикуется — до старта "from" не существует.</summary>
        void Start(GameState initial);

        /// <summary>Exit текущего → Enter нового (стек не растёт). Публикует GameStateChangedEvent.</summary>
        void ChangeState(GameState state);

        /// <summary>Текущее приостанавливается (без Exit), новое — Enter. Публикует GameStateChangedEvent
        /// и, для GameState.Paused, дополнительно GamePausedEvent.</summary>
        void PushState(GameState state);

        /// <summary>Exit верхнего, возврат к предыдущему без Enter. Публикует GameStateChangedEvent
        /// и, если снимается GameState.Paused, дополнительно GameResumedEvent.</summary>
        void PopState();

        /// <summary>Тикает текущее (верхнее) состояние.</summary>
        void Tick();
    }
}
