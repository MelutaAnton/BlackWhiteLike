using BlackWhiteLike.Models;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>
    /// Публикуется <c>WorshipService</c> при любом реальном изменении <c>Current</c> (game-design
    /// §4, §7, §13, шаг 5) — приростом (Awe/Fear) или пассивным затуханием. <see cref="Source"/> при
    /// затухании — источник последнего реального прироста (затухание само по себе не источник),
    /// см. xml-doc <c>WorshipService</c>.
    /// </summary>
    public readonly struct WorshipChangedEvent
    {
        public readonly int OldValue;
        public readonly int NewValue;
        public readonly int Target;
        public readonly WorshipSource Source;

        public WorshipChangedEvent(int oldValue, int newValue, int target, WorshipSource source)
        {
            OldValue = oldValue;
            NewValue = newValue;
            Target = target;
            Source = source;
        }
    }

    /// <summary>
    /// Публикуется <c>AlignmentService</c> при любом реальном изменении <c>Score</c> (game-design
    /// §4, §7, §13, шаг 5). Подписчики — визуал §11.1 (шаг 6) и будущее существо (§15).
    /// </summary>
    public readonly struct AlignmentChangedEvent
    {
        public readonly float OldScore;
        public readonly float NewScore;

        public AlignmentChangedEvent(float oldScore, float newScore)
        {
            OldScore = oldScore;
            NewScore = newScore;
        }
    }

    /// <summary>
    /// Публикуется <c>LevelFlowService</c> при достижении Worship порога уровня (game-design §6,
    /// §9, §13, шаг 5) — единственный сигнал победы. <see cref="AweTotal"/>/<see cref="FearTotal"/> —
    /// некламплённые накопленные суммы (для экрана итогов), не совпадают с итоговым <c>Worship.Current</c>.
    /// </summary>
    public readonly struct LevelCompletedEvent
    {
        public readonly LevelId Id;
        public readonly int AweTotal;
        public readonly int FearTotal;
        public readonly float Duration;

        public LevelCompletedEvent(LevelId id, int aweTotal, int fearTotal, float duration)
        {
            Id = id;
            AweTotal = aweTotal;
            FearTotal = fearTotal;
            Duration = duration;
        }
    }

    /// <summary>
    /// Публикуется <c>LevelFlowService</c> при soft-fail (game-design §9, §13, шаг 5): Faith ≈ 0,
    /// нет активных и ожидающих нужд, Worship ниже порога уровня. UI-реакция —
    /// rewarded-заглушка "Пополнить веру" (см. <c>LevelStalledPresenter</c>).
    /// </summary>
    public readonly struct LevelStalledEvent
    {
    }
}
