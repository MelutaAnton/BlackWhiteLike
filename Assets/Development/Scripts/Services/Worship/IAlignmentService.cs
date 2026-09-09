namespace BlackWhiteLike.Services.Worship
{
    /// <summary>
    /// Alignment уровня −100..+100 (game-design §4, §7, §12, §13, шаг 5) — сдвигается
    /// AlignmentShift'ами действий игрока (исход применения чуда, жёсткое приземление жителя от
    /// броска рукой). Pure C#. Публикует <c>AlignmentChangedEvent</c> при реальном изменении
    /// <see cref="Score"/> — подписчики: визуал §11.1 (шаг 6) и будущее существо (§15).
    /// </summary>
    public interface IAlignmentService
    {
        /// <summary>
        /// Текущий AlignmentScore, <see cref="MinScore"/>..<see cref="MaxScore"/>. Стартует со
        /// значения <c>IMetaProgressService.AlignmentScore</c> на момент конструирования (game-design
        /// §8, шаг 8) — не всегда 0.
        /// </summary>
        float Score { get; }

        /// <summary>
        /// Нижняя граница диапазона <see cref="Score"/> (game-design §12, шаг 5, аудит п.12) —
        /// единственный источник, презентер нормализует через неё, а не хардкодит ±100 у себя.
        /// </summary>
        float MinScore { get; }

        /// <summary>Верхняя граница диапазона <see cref="Score"/> — см. <see cref="MinScore"/>.</summary>
        float MaxScore { get; }
    }
}
