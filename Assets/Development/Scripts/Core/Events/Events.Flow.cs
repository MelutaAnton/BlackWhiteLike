using BlackWhiteLike.Models;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>
    /// Публикуется <c>GameFlowService</c> при каждой смене состояния игрового потока —
    /// ChangeState, Push и Pop (game-design §13).
    /// </summary>
    public readonly struct GameStateChangedEvent
    {
        public readonly GameState From;
        public readonly GameState To;

        public GameStateChangedEvent(GameState from, GameState to)
        {
            From = from;
            To = to;
        }
    }

    /// <summary>Публикуется <c>GameFlowService</c> при PushState(GameState.Paused) (game-design §13).</summary>
    public readonly struct GamePausedEvent
    {
    }

    /// <summary>Публикуется <c>GameFlowService</c> при PopState() из GameState.Paused (game-design §13).</summary>
    public readonly struct GameResumedEvent
    {
    }
}
