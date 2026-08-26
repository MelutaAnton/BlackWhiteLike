using BlackWhiteLike.Models;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>Публикуется <c>LevelEventService</c> при срабатывании нужды по таймлайну LevelConfig (game-design §5, §13).</summary>
    public readonly struct NeedTriggeredEvent
    {
        public readonly NeedType Type;
        public readonly int SiteIndex;

        public NeedTriggeredEvent(NeedType type, int siteIndex)
        {
            Type = type;
            SiteIndex = siteIndex;
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

    /// <summary>Публикуется <c>LevelEventService</c>, если нужда не разрешена до истечения <c>duration</c> (game-design §5, §13).</summary>
    public readonly struct NeedExpiredEvent
    {
        public readonly NeedType Type;
        public readonly int SiteIndex;

        public NeedExpiredEvent(NeedType type, int siteIndex)
        {
            Type = type;
            SiteIndex = siteIndex;
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
