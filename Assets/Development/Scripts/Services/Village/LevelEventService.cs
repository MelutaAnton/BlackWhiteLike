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

            /// <summary>
            /// Шаг 4: true — запись создана рантайм-триггером игрока (<see cref="LevelEventService.TriggerNeed"/>),
            /// false — запись скриптованного таймлайна LevelConfig. Хранится в EntryState, а не в
            /// <see cref="NeedEventEntry"/> — та остаётся простой data-структурой инспектора уровня
            /// (game-design §6), рантайм-происхождение к ней не относится.
            /// </summary>
            public bool ByPlayer;
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
                // ByPlayer не выставляется явно — дефолт bool (false) уже верен для записей таймлайна.
                _entries.Add(new EntryState { Entry = entry, Status = EntryStatus.Pending });
            }
        }

        public void Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            // Незначительное (ревью шага 4): foreach здесь бросал бы InvalidOperationException,
            // если TriggerNeed вызван синхронно из обработчика NeedTriggeredEvent, опубликованного
            // этим же Tick (например, будущим NeedSiteController-подобным подписчиком) — TriggerNeed
            // добавляет в _entries, а foreach не переживает модификацию коллекции во время итерации.
            // Индексный for по снимку длины на момент старта Tick: запись, добавленная реентерантным
            // вызовом во время этого Tick, попадёт в обработку со следующего вызова, не с этого же.
            var count = _entries.Count;
            for (var i = 0; i < count; i++)
            {
                var state = _entries[i];
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
                        // ByPlayer записи скриптованного таймлайна всегда false — CauseMiracle не
                        // валиден для неё (см. Events.Village.cs), передаём default.
                        _eventBus.Publish(new NeedTriggeredEvent(state.Entry.type, state.Entry.targetSiteIndex, state.ByPlayer, default));
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
                    _eventBus.Publish(new NeedExpiredEvent(state.Entry.type, state.Entry.targetSiteIndex, state.ByPlayer));
                }
            }

            // Незначительное (ревью шага 4): рост списка — рантайм-записи (ByPlayer, см. TriggerNeed)
            // в терминальном статусе больше не нужны, их (type, siteIndex) снова станет доступен
            // TriggerNeed'у при следующем срабатывании. Таймлайновые записи (ByPlayer=false) НЕ
            // удаляются — их финальный статус (Resolved/Expired) остаётся частью состояния уровня
            // как есть (game-design §6), сравнивать их количество с EventTimeline может понадобиться.
            _entries.RemoveAll(state =>
                state.ByPlayer && (state.Status == EntryStatus.Resolved || state.Status == EntryStatus.Expired));
        }

        public void ResolveNeed(NeedType type, int siteIndex, bool byPlayer, MiracleType causeMiracle)
        {
            foreach (var state in _entries)
            {
                if (state.Status != EntryStatus.Active) continue;
                if (state.Entry.type != type || state.Entry.targetSiteIndex != siteIndex) continue;

                state.Status = EntryStatus.Resolved;
                _eventBus.Publish(new NeedResolvedEvent(type, siteIndex, byPlayer, causeMiracle));
                return; // одновременно активна максимум одна запись на (type, siteIndex) — таймлайн скриптован вручную
            }
        }

        /// <summary>
        /// Шаг 4: рантайм-запись поверх скриптованного таймлайна (Fireball поджёг дом и т.п.).
        /// Активна максимум одна запись на (type, siteIndex) — тот же контракт, что у таймлайна
        /// (см. ResolveNeed): повторный вызов, пока запись ещё Active, — штатный no-op ("дом уже
        /// горит"), не ошибка и не повод для LogWarning.
        /// </summary>
        public void TriggerNeed(NeedType type, int siteIndex, float duration, bool byPlayer, MiracleType causeMiracle)
        {
            foreach (var state in _entries)
            {
                if (state.Status != EntryStatus.Active) continue;
                if (state.Entry.type != type || state.Entry.targetSiteIndex != siteIndex) continue;
                return; // уже активна — штатный случай
            }

            var newState = new EntryState
            {
                Entry = new NeedEventEntry
                {
                    type = type,
                    targetSiteIndex = siteIndex,
                    duration = duration,
                    triggerTime = _elapsed // не участвует в логике (запись сразу Active), но пусть содержит осмысленное значение
                },
                Status = EntryStatus.Active,
                TimeSinceTriggered = 0f,
                ByPlayer = byPlayer
            };
            _entries.Add(newState);

            _eventBus.Publish(new NeedTriggeredEvent(type, siteIndex, byPlayer, causeMiracle));
        }

        /// <summary>Мост для VContainer ITickable — единственное место сервиса, читающее UnityEngine.Time.</summary>
        public void Tick() => Tick(UnityEngine.Time.deltaTime);

        public bool HasActiveNeeds
        {
            get
            {
                foreach (var state in _entries)
                {
                    if (state.Status == EntryStatus.Active) return true;
                }

                return false;
            }
        }

        public bool HasPendingNeeds
        {
            get
            {
                foreach (var state in _entries)
                {
                    if (state.Status == EntryStatus.Pending) return true;
                }

                return false;
            }
        }
    }
}
