using BlackWhiteLike.Models;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>
    /// Публикуется <c>LevelEventService</c> при срабатывании нужды — как по таймлайну LevelConfig,
    /// так и рантайм-записью через <c>ILevelEventService.TriggerNeed</c> (game-design §5, §13, шаг 4).
    /// <see cref="ByPlayer"/> — true, если нужду создало действие игрока (например, Fireball поджёг
    /// дом); записи скриптованного таймлайна всегда несут false.
    /// </summary>
    public readonly struct NeedTriggeredEvent
    {
        public readonly NeedType Type;
        public readonly int SiteIndex;
        public readonly bool ByPlayer;

        public NeedTriggeredEvent(NeedType type, int siteIndex, bool byPlayer)
        {
            Type = type;
            SiteIndex = siteIndex;
            ByPlayer = byPlayer;
        }
    }

    /// <summary>
    /// Публикуется <c>LevelEventService</c> при разрешении нужды (game-design §5, §13).
    /// <see cref="ByPlayer"/> — true, если разрешено чудом игрока (источник Awe); false — прочие
    /// пути разрешения (сейчас не используются, задел на будущее).
    /// </summary>
    public readonly struct NeedResolvedEvent
    {
        public readonly NeedType Type;
        public readonly int SiteIndex;
        public readonly bool ByPlayer;

        public NeedResolvedEvent(NeedType type, int siteIndex, bool byPlayer)
        {
            Type = type;
            SiteIndex = siteIndex;
            ByPlayer = byPlayer;
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

    /// <summary>Публикуется <c>VillagerStateService</c> при каждой смене состояния FSM жителя (game-design §5, §13).</summary>
    public readonly struct VillagerStateChangedEvent
    {
        public readonly VillagerId Id;
        public readonly VillagerState From;
        public readonly VillagerState To;

        public VillagerStateChangedEvent(VillagerId id, VillagerState from, VillagerState to)
        {
            Id = id;
            From = from;
            To = to;
        }
    }
}
