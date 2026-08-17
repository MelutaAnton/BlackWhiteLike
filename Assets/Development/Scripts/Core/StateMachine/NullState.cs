namespace BlackWhiteLike.Core.StateMachine
{
    /// <summary>
    /// Заглушка для ключей FSM, чьё поведение ещё не реализовано отдельным классом
    /// (см. <see cref="StateMachine{TKey}"/>). Enter/Exit/Tick пустые — ключ можно держать
    /// в стеке (Current, push-pop) уже сейчас, а полноценный <see cref="IState"/> подставить позже,
    /// не трогая вызывающий код.
    /// </summary>
    public sealed class NullState : IState
    {
        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public void Tick()
        {
        }
    }
}
