namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Периодический режим нужд уровня (game-design §5/§6, решение владельца 2026-09-12, уровни 2–5):
    /// вместо скриптованного таймлайна <c>LevelConfig.EventTimeline</c> каждые
    /// <c>LevelConfig.EventIntervalSeconds</c> секунд срабатывает случайная СВОБОДНАЯ нужда — L01
    /// остаётся на событийной цепочке (интервал 0, режим выключен, см. <see cref="IsEnabled"/>).
    /// Pure C# — реализация <c>PeriodicNeedService</c> сама решает, что "свободно" (сверка с
    /// <c>ILevelEventService.IsNeedActive</c>/<c>INeedSiteRegistry.CollectSites</c>/
    /// <c>IVillagerDiseaseService.HasEligibleCandidate</c>), контроллеры этот сервис не трогают вовсе.
    /// </summary>
    public interface IPeriodicNeedService
    {
        /// <summary>true — <c>LevelConfig.EventIntervalSeconds &gt; 0</c>, режим активен для этого уровня.</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Продвигает таймер периодического режима. Если <see cref="IsEnabled"/> == false — no-op.
        /// За один вызов выполняется максимум один спавн (см. remarks реализации) — остаток лага
        /// сохраняется, а не отбрасывается.
        /// </summary>
        void Tick(float deltaTime);
    }
}
