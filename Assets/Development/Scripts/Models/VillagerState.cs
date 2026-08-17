namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Состояния FSM жителя (game-design §5): Idle → Panic → Worship → Tossed → Stunned → Idle.
    /// </summary>
    public enum VillagerState
    {
        Idle,
        Panic,
        Worship,
        Tossed,
        Stunned
    }
}
