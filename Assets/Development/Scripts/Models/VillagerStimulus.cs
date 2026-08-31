namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Внешние стимулы, меняющие состояние FSM жителя (game-design §5). Репортятся через
    /// <c>IVillagerStateService.ReportStimulus</c> источниками — <c>VillagerController</c>
    /// (Grabbed/Landed/HardLanded, физические события) и будущими подписчиками VFX/чудес
    /// (FireNearby/ExplosionNearby/NeedResolvedNearby — шаги 4-6). Единственный источник значений.
    /// </summary>
    public enum VillagerStimulus
    {
        FireNearby,
        ExplosionNearby,
        NeedResolvedNearby,
        Grabbed,
        Landed,
        HardLanded,

        /// <summary>Взрыв чуда Fireball разбрасывает жителя (game-design §4, шаг 4) — переход в Tossed, та же ветка, что Grabbed.</summary>
        Blasted,

        /// <summary>
        /// Удар молнии чуда Lightning (game-design §4, шаг 6) — мгновенный Stunned, без физического
        /// отброса (в отличие от Blasted, который ведёт в Tossed). Решение владельца 2026-09-01.
        /// </summary>
        LightningStruck,

        /// <summary>
        /// Нет внешнего стимула — переход случился по таймеру FSM (game-design §5, шаг 5, аудит
        /// ВАЖНО 5): <c>VillagerStateService.Tick</c> сам переводит Panic/Worship/Stunned/Tossed в
        /// Idle по истечении длительности, без <c>ReportStimulus</c>. Единственное значение,
        /// подставляемое сервисом, а не источником стимула.
        /// </summary>
        None
    }
}
