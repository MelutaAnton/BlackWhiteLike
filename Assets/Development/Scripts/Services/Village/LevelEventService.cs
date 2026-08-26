using System.Collections.Generic;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using VContainer.Unity;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Реализация <see cref="ILevelEventService"/>. Pure C# — снимок таймлайна из
    /// <c>LevelConfig.EventTimeline</c> берётся один раз в конструкторе (SO как источник данных,
    /// тот же прецедент, что <c>VillagerStateService(VillagerConfig)</c>); каждой записи
    /// соответствует свой статус (Pending/Active/Resolved/Expired), индекс записи — не то же
    /// самое, что <c>NeedEventEntry.targetSiteIndex</c> (несколько записей таймлайна могут указывать
    /// на одну и ту же точку в разное время).
    /// </summary>
    public sealed class LevelEventService : ILevelEventService, ITickable
    {
        private enum EntryStatus
        {
            Pending,
            Active,
            Resolved,
            Expired
        }

        private sealed class EntryState
        {
            public NeedEventEntry Entry;
            public EntryStatus Status;
            public float TimeSinceTriggered;
        }

        private readonly IEventBus _eventBus;
        private readonly List<EntryState> _entries = new List<EntryState>();
        private float _elapsed;

        public LevelEventService(IEventBus eventBus, LevelConfig levelConfig)
        {
            _eventBus = eventBus;

            var timeline = levelConfig.EventTimeline;
            if (timeline == null) return;

            foreach (var entry in timeline)
            {
                _entries.Add(new EntryState { Entry = entry, Status = EntryStatus.Pending });
            }
        }

        public void Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            foreach (var state in _entries)
            {
                switch (state.Status)
                {
                    case EntryStatus.Pending:
                        if (_elapsed < state.Entry.triggerTime) continue; // ещё не наступило

                        state.Status = EntryStatus.Active;
                        // Н1 (ревью шага 3): раньше здесь стоял 0 — при большом deltaTime (просадка
                        // FPS, "перепрыгнувший" triggerTime Tick) часть duration молча терялась бы
                        // фантомно, потому что таймер истечения стартовал бы от нуля, а не от
                        // фактического момента срабатывания. _elapsed - triggerTime — реальный лаг.
                        state.TimeSinceTriggered = _elapsed - state.Entry.triggerTime;
                        _eventBus.Publish(new NeedTriggeredEvent(state.Entry.type, state.Entry.targetSiteIndex));
                        break;

                    case EntryStatus.Active:
                        state.TimeSinceTriggered += deltaTime;
                        break;

                    default:
                        continue; // Resolved/Expired — терминальные статусы, дальше не тикают (разрешённое не истекает)
                }

                // Общая проверка истечения — выполняется и для записи, только что ставшей Active в
                // ЭТОМ ЖЕ Tick (большой deltaTime мог перепрыгнуть сразу и triggerTime, и
                // triggerTime+duration за один вызов — В9 ревью шага 3), и для уже Active записи.
                if (state.TimeSinceTriggered >= state.Entry.duration)
                {
                    state.Status = EntryStatus.Expired;
                    _eventBus.Publish(new NeedExpiredEvent(state.Entry.type, state.Entry.targetSiteIndex));
                }
            }
        }

        public void ResolveNeed(NeedType type, int siteIndex, bool byPlayer)
        {
            foreach (var state in _entries)
            {
                if (state.Status != EntryStatus.Active) continue;
                if (state.Entry.type != type || state.Entry.targetSiteIndex != siteIndex) continue;

                state.Status = EntryStatus.Resolved;
                _eventBus.Publish(new NeedResolvedEvent(type, siteIndex, byPlayer));
                return; // одновременно активна максимум одна запись на (type, siteIndex) — таймлайн скриптован вручную
            }
        }

        /// <summary>Мост для VContainer ITickable — единственное место сервиса, читающее UnityEngine.Time.</summary>
        public void Tick() => Tick(UnityEngine.Time.deltaTime);
    }
}
