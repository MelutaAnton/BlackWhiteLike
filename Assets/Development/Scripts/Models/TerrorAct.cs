namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Акты террора — источники Fear (game-design §7, §13, шаг 5). Единственный источник значений.
    /// <see cref="LightningStrike"/> и <see cref="ObjectHitStructure"/> заведены заранее как данные
    /// (запись в <c>WorshipConfig</c> может существовать уже сейчас) — сами источники (чудо
    /// Lightning, разрушение декора броском) появятся на шаге 6.
    /// </summary>
    public enum TerrorAct
    {
        FireballOnStructure,
        LightningStrike,
        VillagerHardLanding,
        ObjectHitStructure,

        /// <summary>
        /// Жёсткое приземление жителя, отброшенного взрывом чуда (Tossed -> Stunned, причина входа
        /// в Tossed — <see cref="VillagerStimulus.Blasted"/>). Отдельная ставка от
        /// <see cref="VillagerHardLanding"/> (бросок рукой) — решение владельца 2026-08-29: у взрыва
        /// своя ставка Fear, поджог дома (FireballOnStructure) — отдельный акт, двойного счёта нет.
        /// </summary>
        VillagerBlasted,

        /// <summary>
        /// Житель поражён ударом молнии (Idle/Panic/Worship -> Stunned, причина —
        /// <see cref="VillagerStimulus.LightningStruck"/>, game-design §4, шаг 6). Отдельная ставка
        /// от <see cref="LightningStrike"/> (поджог дома) — та же структура прецедента, что
        /// VillagerBlasted/FireballOnStructure: два разных акта, двойного счёта нет. Решение
        /// владельца 2026-09-01.
        /// </summary>
        VillagerStruckByLightning
    }
}
