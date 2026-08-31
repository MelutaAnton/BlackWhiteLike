using BlackWhiteLike.Models;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>
    /// Публикуется <c>LevelEventService</c> при срабатывании нужды — как по таймлайну LevelConfig,
    /// так и рантайм-записью через <c>ILevelEventService.TriggerNeed</c> (game-design §5, §13, шаг 4).
    /// <see cref="ByPlayer"/> — true, если нужду создало действие игрока (например, Fireball поджёг
    /// дом); записи скриптованного таймлайна всегда несут false.
    /// <see cref="CauseMiracle"/> (шаг 5) валиден только при <see cref="ByPlayer"/> == true — какое
    /// чудо создало нужду (Fireball → Fire), используется <c>WorshipService</c>/<c>AlignmentService</c>
    /// для расчёта Fear/AlignmentShift; для записей скриптованного таймлайна (ByPlayer == false)
    /// несёт <c>default</c> и не должен читаться.
    /// </summary>
    public readonly struct NeedTriggeredEvent
    {
        public readonly NeedType Type;
        public readonly int SiteIndex;
        public readonly bool ByPlayer;
        public readonly MiracleType CauseMiracle;

        public NeedTriggeredEvent(NeedType type, int siteIndex, bool byPlayer, MiracleType causeMiracle)
        {
            Type = type;
            SiteIndex = siteIndex;
            ByPlayer = byPlayer;
            CauseMiracle = causeMiracle;
        }
    }

    /// <summary>
    /// Публикуется <c>LevelEventService</c> при разрешении нужды (game-design §5, §13).
    /// <see cref="ByPlayer"/> — true, если разрешено чудом игрока (источник Awe); false — прочие
    /// пути разрешения (сейчас не используются, задел на будущее).
    /// <see cref="CauseMiracle"/> (шаг 5) валиден только при <see cref="ByPlayer"/> == true — каким
    /// чудом разрешена нужда (Rain → Fire/Hunger, Heal → Disease), используется
    /// <c>WorshipService</c>/<c>AlignmentService</c> для расчёта Awe/AlignmentShift; для прочих
    /// путей разрешения несёт <c>default</c> и не должен читаться.
    /// </summary>
    public readonly struct NeedResolvedEvent
    {
        public readonly NeedType Type;
        public readonly int SiteIndex;
        public readonly bool ByPlayer;
        public readonly MiracleType CauseMiracle;

        public NeedResolvedEvent(NeedType type, int siteIndex, bool byPlayer, MiracleType causeMiracle)
        {
            Type = type;
            SiteIndex = siteIndex;
            ByPlayer = byPlayer;
            CauseMiracle = causeMiracle;
        }
    }

    /// <summary>
    /// Публикуется <c>LevelEventService</c>, если нужда не разрешена до истечения <c>duration</c>
    /// (game-design §5, §13). <see cref="ByPlayer"/> — тот же смысл, что у <see cref="NeedTriggeredEvent.ByPlayer"/>:
    /// true для рантайм-записей, созданных игроком, false для записей скриптованного таймлайна.
    /// </summary>
    public readonly struct NeedExpiredEvent
    {
        public readonly NeedType Type;
        public readonly int SiteIndex;
        public readonly bool ByPlayer;

        public NeedExpiredEvent(NeedType type, int siteIndex, bool byPlayer)
        {
            Type = type;
            SiteIndex = siteIndex;
            ByPlayer = byPlayer;
        }
    }

    /// <summary>
    /// Публикуется <c>VillagerStateService</c> при каждой смене состояния FSM жителя (game-design
    /// §5, §13). <see cref="Cause"/> (шаг 5, аудит ВАЖНО 5) — стимул, вызвавший ИМЕННО ЭТОТ переход
    /// (<c>VillagerStimulus.None</c> для переходов по таймеру в <c>VillagerStateService.Tick</c>).
    /// Нужен, чтобы отличить, ПОЧЕМУ житель попал в Tossed (рука игрока — <c>Grabbed</c>, взрыв
    /// чуда Fireball — <c>Blasted</c>): без этого <c>WorshipService</c>/<c>AlignmentService</c>
    /// начисляли Fear/сдвиг за жёсткое приземление (Tossed → Stunned) дважды для Blasted — свой
    /// Fear/сдвиг уже даёт <c>NeedTriggeredEvent</c> самого Fireball'а.
    /// </summary>
    public readonly struct VillagerStateChangedEvent
    {
        public readonly VillagerId Id;
        public readonly VillagerState From;
        public readonly VillagerState To;
        public readonly VillagerStimulus Cause;

        public VillagerStateChangedEvent(VillagerId id, VillagerState from, VillagerState to, VillagerStimulus cause)
        {
            Id = id;
            From = from;
            To = to;
            Cause = cause;
        }
    }
}
