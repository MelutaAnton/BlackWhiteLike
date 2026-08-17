namespace BlackWhiteLike.Core.StateMachine
{
    /// <summary>Состояние generic FSM (см. <see cref="StateMachine{TKey}"/>).</summary>
    public interface IState
    {
        void Enter();
        void Exit();
        void Tick();
    }
}
