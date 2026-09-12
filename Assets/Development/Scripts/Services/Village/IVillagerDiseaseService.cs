using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Disease переезжает с NeedSite на случайного жителя (game-design §5, шаг переноса болезни):
    /// вместо точки в сцене (<c>NeedSiteController</c>, как у Fire/Hunger) больной — сам житель,
    /// выбранный случайно среди зарегистрированных (<see cref="IVillagerStateService.CollectRegistered"/>).
    /// Единственный источник "кто сейчас болен" и "какому сайту (записи таймлайна) соответствует
    /// болезнь" — <c>MiracleImpactController</c> (лечение) и <c>VillagerController</c> (визуал/поза)
    /// читают состояние только через этот интерфейс, напрямую подписки на события Disease не делают.
    /// </summary>
    public interface IVillagerDiseaseService
    {
        /// <summary>true — <paramref name="id"/> сейчас болен (см. VillagerSicknessChangedEvent).</summary>
        bool IsSick(VillagerId id);

        /// <summary>
        /// Активная нужда, которой болеет <paramref name="id"/>: тип (villager-hosted по
        /// <c>NeedHostRules</c>) и siteIndex записи таймлайна — false, если не болен. Тип отдаётся
        /// вместе с индексом, чтобы лечащая сторона (<c>MiracleImpactController</c>) сверяла его с
        /// <c>MiracleConfig.ResolvesNeeds</c>, а не зашивала <c>NeedType.Disease</c> в код (ревью).
        /// </summary>
        bool TryGetActiveNeed(VillagerId id, out NeedType type, out int siteIndex);

        /// <summary>Больной жителем на записи <paramref name="siteIndex"/> — false, если сайт не заражён (Pending/отложен/разрешён).</summary>
        bool TryGetSickVillager(int siteIndex, out VillagerId id);

        /// <summary>
        /// Шаг периодических нужд (уровни 2–5): true — есть хотя бы один зарегистрированный житель,
        /// не Tossed и не больной, пригодный для назначения новой Disease. Читается
        /// <c>PeriodicNeedService</c> перед тем, как включать Disease в список свободных целей —
        /// у неё нет NeedSite в сцене (<see cref="NeedHostRules.IsVillagerHosted"/>).
        /// </summary>
        bool HasEligibleCandidate { get; }
    }
}
