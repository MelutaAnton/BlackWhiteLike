namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Режим взведения записи скриптованного таймлайна уровня (<c>NeedEventEntry</c>, game-design
    /// §5, §6, шаг 10 «событийная цепочка вместо таймеров», решение владельца) — единственный
    /// источник значений. Читается <c>LevelEventService</c>.
    /// </summary>
    public enum NeedTriggerMode
    {
        /// <summary>
        /// По времени — запись срабатывает, когда накопленный <c>Tick</c>-таймер уровня достигает
        /// <c>NeedEventEntry.triggerTime</c>. Дефолт (0) — уровни 2-5 не меняют поведение при
        /// добавлении этого enum.
        /// </summary>
        AtTime,

        /// <summary>
        /// Сразу при первом БРОСКЕ жителя рукой игрока за уровень — захват (<c>ObjectGrabbedEvent</c>)
        /// и мягкая постановка (<c>ObjectThrownEvent.Velocity</c> ~ ноль) не считаются, взводит только
        /// реальный бросок (<c>VillagerTossRules.IsVillagerThrow</c>, playtest-фикс 2026-09-11 — до
        /// этого предикат ловил уже сам факт захвата через <c>VillagerStateChangedEvent</c>).
        /// <c>NeedEventEntry.triggerTime</c> не читается.
        /// </summary>
        AfterVillagerTossed,

        /// <summary>
        /// Сразу по разрешению записи, идущей непосредственно перед этой в
        /// <c>LevelConfig.EventTimeline</c> (<c>NeedResolvedEvent</c>, сопоставление по type+siteIndex
        /// предыдущей записи; <c>ByPlayer</c> не важен). <c>NeedEventEntry.triggerTime</c> не читается.
        /// Первая запись таймлайна не может использовать этот режим — предыдущей не существует.
        /// </summary>
        AfterPreviousResolved
    }
}
