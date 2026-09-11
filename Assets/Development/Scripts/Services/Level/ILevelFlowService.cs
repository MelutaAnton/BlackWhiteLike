namespace BlackWhiteLike.Services.Level
{
    /// <summary>
    /// Жизненный цикл уровня (game-design §6, §9, §13, шаг 5): старт, win-check по Worship,
    /// soft-fail (stall). Pure C#, время приходит аргументом в <see cref="Tick"/> — тестируется
    /// без Unity.
    /// </summary>
    public interface ILevelFlowService
    {
        /// <summary>Сбрасывает <see cref="Elapsed"/> и флаги (Completed/Stalled), начинает отсчёт уровня.</summary>
        void StartLevel();

        /// <summary>Время с последнего <see cref="StartLevel"/>, секунды.</summary>
        float Elapsed { get; }

        /// <summary>true между <see cref="StartLevel"/> и достижением Worship.Target.</summary>
        bool IsRunning { get; }

        /// <summary>true, если сейчас soft-fail (см. <c>LevelStalledEvent</c>).</summary>
        bool IsStalled { get; }

        /// <summary>Продвигает Elapsed и пересчитывает stall. Вне <see cref="IsRunning"/> — no-op.</summary>
        void Tick(float dt);

        /// <summary>
        /// Шаг 10 (окно rewarded-согласия): игрок отказался пополнить веру за рекламу — сбрасывает
        /// защёлку показа <c>LevelStalledEvent</c> с кулдауном (см. реализацию), чтобы повторный
        /// сталл снова показал окно, но не мгновенно на следующем кадре. Не в состоянии stall —
        /// безопасный no-op.
        /// </summary>
        void AcknowledgeStallDeclined();
    }
}
