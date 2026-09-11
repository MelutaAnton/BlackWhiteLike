namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Типы триггеров шага туториала (шаг 10 «Polish») — единственный источник значений.
    /// Используется и для активации шага (когда показать), и для его завершения (когда считать
    /// выполненным), см. <c>TutorialTrigger</c>/<c>TutorialStep</c>.
    /// </summary>
    public enum TutorialTriggerType
    {
        /// <summary>Нет триггера — шаг активируется сразу (не ждёт события), см. <c>TutorialStep.ActivationTrigger</c>.</summary>
        None,

        /// <summary>
        /// Житель БРОШЕН рукой игрока — <c>ObjectGrabbedEvent</c>+<c>ObjectThrownEvent</c> с
        /// <c>VillagerTossRules.IsVillagerThrow</c> (захват и мягкая постановка не считаются;
        /// playtest-фикс 2026-09-11 — до этого триггер ловил сам факт захвата через
        /// <c>VillagerStateChangedEvent</c>).
        /// </summary>
        VillagerTossedByHand,

        /// <summary>Сработала нужда (<c>NeedTriggeredEvent</c>), фильтр по <c>TutorialTrigger.NeedType</c>.</summary>
        NeedTriggered,

        /// <summary>Игрок выбрал чудо в панели (<c>MiracleSelectedEvent</c>, Selected=true), фильтр по <c>TutorialTrigger.MiracleType</c>.</summary>
        MiracleSelected,

        /// <summary>Нужда разрешена игроком (<c>NeedResolvedEvent</c>, ByPlayer=true), фильтр по <c>TutorialTrigger.NeedType</c>.</summary>
        NeedResolvedByPlayer,

        /// <summary>Worship достиг порога (<c>WorshipChangedEvent.NewValue &gt;= TutorialTrigger.WorshipThreshold</c>).</summary>
        WorshipReached
    }
}
