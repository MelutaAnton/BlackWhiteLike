using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Правило "какая нужда живёт на жителе, а не на NeedSite" (game-design §5 — Disease переезжает
    /// с здания/поля на случайного жителя) — единственный источник этого предиката, тот же прецедент,
    /// что <see cref="VillagerPoseRules"/>/<see cref="VillagerTossRules"/>: чистая статическая функция
    /// без состояния и без DI, тестируется без Unity-сцены. Читает не только рантайм
    /// (<c>VillagerDiseaseService</c>), но и editor-валидатор <c>LevelConfig</c> (другой агент) —
    /// оба обязаны сверяться с этим же методом, а не дублировать список типов нужд самостоятельно.
    /// </summary>
    public static class NeedHostRules
    {
        /// <summary>
        /// true — нужда типа <paramref name="type"/> обслуживается не точкой в сцене
        /// (<c>NeedSiteController</c>), а случайным зарегистрированным жителем
        /// (<c>VillagerDiseaseService</c>). В срезе шага переноса болезни — только Disease.
        /// </summary>
        public static bool IsVillagerHosted(NeedType type) => type == NeedType.Disease;

        /// <summary>
        /// Базовый индекс диапазона "виртуальных" siteIndex villager-hosted нужд (Disease) —
        /// у такой нужды нет NeedSite в сцене, поэтому вместо индекса записи LevelConfig.EventTimeline
        /// (как у Fire/Hunger) используется свободный слот из этого диапазона, по одному на каждого
        /// одновременно больного (<c>PeriodicNeedService.FindFreeVirtualSlot</c>). L01-таймлайн Disease
        /// мигрирует с реального индекса 1 на виртуальный 1000 editor-агентом (шаг периодических нужд,
        /// уровни 2–5) — сам рантайм этой миграции не касается, он лишь резервирует диапазон.
        /// </summary>
        public const int VirtualSiteIndexBase = 1000;

        /// <summary>true — <paramref name="siteIndex"/> лежит в диапазоне виртуальных слотов (см. <see cref="VirtualSiteIndexBase"/>).</summary>
        public static bool IsVirtualSiteIndex(int siteIndex) => siteIndex >= VirtualSiteIndexBase;
    }
}
