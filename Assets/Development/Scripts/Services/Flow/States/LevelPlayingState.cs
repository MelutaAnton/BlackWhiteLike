using BlackWhiteLike.Core.StateMachine;
using BlackWhiteLike.Services.Level;

namespace BlackWhiteLike.Services.Flow.States
{
    /// <summary>Состояние активной игры на уровне (game-design §9, шаг 5) — запускает жизненный цикл уровня через <see cref="ILevelFlowService"/>.</summary>
    public sealed class LevelPlayingState : IState
    {
        private readonly ILevelFlowService _levelFlowService;

        public LevelPlayingState(ILevelFlowService levelFlowService)
        {
            _levelFlowService = levelFlowService;
        }

        public void Enter() => _levelFlowService.StartLevel();

        public void Exit()
        {
        }

        public void Tick()
        {
            // Продвижение времени/stall-проверка уровня — отдельный ITickable (LevelFlowService),
            // не завязано на то, тикается ли именно это состояние FSM (симметрично FaithService).
        }
    }
}
