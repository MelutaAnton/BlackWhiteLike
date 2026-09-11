using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>
    /// Публикуется <c>WorshipService</c> при любом реальном изменении <c>Current</c> (game-design
    /// §4, §7, §13, шаг 5) — приростом (Awe/Fear) или пассивным затуханием. <see cref="Source"/> при
    /// затухании — источник последнего реального прироста (затухание само по себе не источник),
    /// см. xml-doc <c>WorshipService</c>. <see cref="WorldPosition"/>/<see cref="HasWorldPosition"/>
    /// (шаг 10, волна 2) — мировая позиция источника прироста (NeedSite через
    /// <c>INeedSiteRegistry</c> для Awe/Fear-от-нужды, позиция жителя для Fear-от-террор-акта
    /// жителя): попап "+N Awe/Fear" якорится к ней, фоллбэк на прежнюю HUD-точку — если false или
    /// цель вне экрана (<c>WorshipPopupPresenter</c>). При затухании — позиция последнего реального
    /// прироста (тот же принцип, что <see cref="Source"/>).
    /// </summary>
    public readonly struct WorshipChangedEvent
    {
        public readonly int OldValue;
        public readonly int NewValue;
        public readonly int Target;
        public readonly WorshipSource Source;
        public readonly Vector3 WorldPosition;
        public readonly bool HasWorldPosition;

        public WorshipChangedEvent(int oldValue, int newValue, int target, WorshipSource source,
            Vector3 worldPosition = default, bool hasWorldPosition = false)
        {
            OldValue = oldValue;
            NewValue = newValue;
            Target = target;
            Source = source;
            WorldPosition = worldPosition;
            HasWorldPosition = hasWorldPosition;
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
    /// Публикуется <c>LevelFlowService</c> при АВТОМАТИЧЕСКОМ soft-fail (game-design §9, §13, шаг 5):
    /// Faith ≈ 0, нет блокирующих нужд (<c>ILevelEventService.HasBlockingNeeds</c>), Worship ниже
    /// порога уровня. Показ ограничен кулдауном после отказа игрока (см.
    /// <c>LevelFlowService.StallReshowCooldownAfterDecline</c>).
    /// UI-реакция — rewarded-окно (см. <c>LevelStalledPresenter</c>), у которого (шаг 10, волна 2)
    /// это ОДИН из ДВУХ путей показа: второй — явный тап игрока по чуду без хватающей Faith
    /// (<c>MiracleDeniedEvent</c>, публикуется <c>MiracleService.BeginCast</c>), показывается
    /// немедленно и кулдауном этого события не ограничен.
    /// </summary>
    public readonly struct LevelStalledEvent
    {
    }
}
