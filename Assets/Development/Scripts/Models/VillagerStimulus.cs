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
        HardLanded
    }
}
