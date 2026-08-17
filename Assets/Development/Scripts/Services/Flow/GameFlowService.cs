using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.StateMachine;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow.States;
using VContainer.Unity;
using CoreStateMachine = BlackWhiteLike.Core.StateMachine.StateMachine<BlackWhiteLike.Models.GameState>;

namespace BlackWhiteLike.Services.Flow
{
    /// <summary>
    /// Реализация <see cref="IGameFlowService"/>. Pure C#, оборачивает generic FSM (game-design §9)
    /// и публикует <see cref="Core.Events.GameStateChangedEvent"/> / <see cref="GamePausedEvent"/> /
    /// <see cref="GameResumedEvent"/> через <see cref="IEventBus"/>. Реализует <see cref="ITickable"/> —
    /// регистрируется в GameLifetimeScope через RegisterEntryPoint, тикается VContainer каждый кадр.
    /// </summary>
    /// <remarks>
    /// Решение по сборке состояний FSM: регистрация происходит здесь, в конструкторе,
    /// а не через отдельный публичный Register-метод на <see cref="IGameFlowService"/>.
    /// Причина — интерфейс сервиса намеренно минимален (Current/Start/ChangeState/PushState/PopState/Tick,
    /// см. game-design §12), сборка FSM — деталь реализации Flow-слоя, а не то, что должен уметь
    /// потребитель (GameBootstrap, UI). По мере появления новых состояний (шаги 2+) конструктор
    /// будет принимать больше IState-зависимостей через DI; если список разрастётся —
    /// решение можно пересмотреть в пользу IEnumerable-коллекции именованных состояний.
    /// </remarks>
    public sealed class GameFlowService : IGameFlowService, ITickable
    {
        private readonly IEventBus _eventBus;
        private readonly CoreStateMachine _stateMachine = new CoreStateMachine();

        public GameFlowService(IEventBus eventBus, MainMenuState mainMenuState)
        {
            _eventBus = eventBus;
            _stateMachine.Register(GameState.MainMenu, mainMenuState);
            // TODO: заменить на PausedState, когда появится экран паузы.
            _stateMachine.Register(GameState.Paused, new NullState());
        }

        public GameState Current => _stateMachine.Current;

        public void Start(GameState initial)
        {
            // Стартовое состояние — не транзиция, "from" не существует, событие не публикуем.
            _stateMachine.ChangeState(initial);
        }

        public void ChangeState(GameState state)
        {
            var from = _stateMachine.Current;
            _stateMachine.ChangeState(state);
            _eventBus.Publish(new GameStateChangedEvent(from, state));
        }

        public void PushState(GameState state)
        {
            var from = _stateMachine.Current;
            _stateMachine.Push(state);
            _eventBus.Publish(new GameStateChangedEvent(from, state));

            if (state == GameState.Paused)
            {
                _eventBus.Publish(new GamePausedEvent());
            }
        }

        public void PopState()
        {
            var from = _stateMachine.Current;
            var popped = _stateMachine.Pop();

            // StateMachine.Pop() на пустом/единственном состоянии — безопасный no-op (лог-варнинг, стек не меняется).
            // Ориентируемся на возвращённый флаг, а не на сравнение from/to — они могут совпасть
            // и при реальной транзиции (Push(X) поверх X), тогда Exit реально отработал.
            if (!popped) return;

            var to = _stateMachine.Current;
            _eventBus.Publish(new GameStateChangedEvent(from, to));

            if (from == GameState.Paused)
            {
                _eventBus.Publish(new GameResumedEvent());
            }
        }

        public void Tick() => _stateMachine.Tick();
    }
}
