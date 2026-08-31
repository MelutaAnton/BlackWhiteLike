using BlackWhiteLike.Core.StateMachine;

namespace BlackWhiteLike.Services.Flow.States
{
    /// <summary>
    /// Пауза (game-design §9, §15, шаг 5) — единственное состояние, ставящее время игры на паузу,
    /// через <see cref="ITimeScaleProvider"/> (единственная точка обращения к
    /// UnityEngine.Time.timeScale во всём проекте, см. <c>UnityTimeScaleProvider</c>). Запоминает
    /// TimeScale на момент Enter и восстанавливает его на Exit.
    /// </summary>
    public sealed class PausedState : IState
    {
        private readonly ITimeScaleProvider _timeScaleProvider;
        private float _previousTimeScale = 1f;

        public PausedState(ITimeScaleProvider timeScaleProvider)
        {
            _timeScaleProvider = timeScaleProvider;
        }

        public void Enter()
        {
            _previousTimeScale = _timeScaleProvider.TimeScale;
            _timeScaleProvider.TimeScale = 0f;
        }

        public void Exit()
        {
            // Страховка (game-design §15): если запомненное значение само было 0 (Enter вызван
            // повторно без промежуточного Exit, либо игра уже была на паузе по другой причине) —
            // восстанавливаем 1, а не 0, иначе игра навсегда осталась бы физически на паузе.
            _timeScaleProvider.TimeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
        }

        public void Tick()
        {
        }
    }
}
