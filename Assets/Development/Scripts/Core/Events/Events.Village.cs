using BlackWhiteLike.Models;
using UnityEngine;

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
    /// <see cref="WorldPosition"/>/<see cref="HasWorldPosition"/> (шаг 10, волна 2) — мировая позиция
    /// жителя в момент стимула, репортится издателем (<c>VillagerController</c>/
    /// <c>MiracleImpactController</c> знают <c>transform.position</c>) через опциональный параметр
    /// <c>IVillagerStateService.ReportStimulus</c>; переходы по таймеру (<c>VillagerStateService.Tick</c>)
    /// позиции не несут (<c>HasWorldPosition</c> == false) — используется <c>WorshipService</c> для
    /// якорения попапов Fear к месту жёсткого приземления/удара молнии.
    /// </summary>
    public readonly struct VillagerStateChangedEvent
    {
        public readonly VillagerId Id;
        public readonly VillagerState From;
        public readonly VillagerState To;
        public readonly VillagerStimulus Cause;
        public readonly Vector3 WorldPosition;
        public readonly bool HasWorldPosition;

        public VillagerStateChangedEvent(VillagerId id, VillagerState from, VillagerState to, VillagerStimulus cause,
            Vector3 worldPosition = default, bool hasWorldPosition = false)
        {
            Id = id;
            From = from;
            To = to;
            Cause = cause;
            WorldPosition = worldPosition;
            HasWorldPosition = hasWorldPosition;
        }
    }

    /// <summary>
    /// Публикуется <c>VillagerDiseaseService</c> при заражении/выздоровлении жителя (game-design §5 —
    /// Disease переезжает с NeedSite на случайного жителя, шаг переноса болезни). В отличие от
    /// остальных нужд (Fire/Hunger — точка в сцене, <c>NeedSiteController</c>), у Disease нет
    /// реального NeedSite в сцене (см. <c>Services.Village.NeedHostRules</c>) — <see cref="SiteIndex"/>
    /// остаётся тем же индексом записи <c>LevelConfig.EventTimeline</c>, что и в
    /// <c>NeedTriggeredEvent</c>/<c>NeedResolvedEvent</c>/<c>NeedExpiredEvent</c> для этой Disease,
    /// просто виртуальный (без соответствующего <c>NeedSiteController</c> в сцене). Подписчики —
    /// <c>VillagerController</c> (визуал/поза/регистрация мировой позиции больного в
    /// <c>INeedSiteRegistry</c>), тесты.
    /// </summary>
    public readonly struct VillagerSicknessChangedEvent
    {
        public readonly VillagerId Id;
        public readonly int SiteIndex;
        public readonly bool IsSick;

        public VillagerSicknessChangedEvent(VillagerId id, int siteIndex, bool isSick)
        {
            Id = id;
            SiteIndex = siteIndex;
            IsSick = isSick;
        }
    }
}
