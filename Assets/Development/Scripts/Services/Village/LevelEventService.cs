using System;
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
    /// <remarks>
    /// Шаг 10 (решение владельца, событийная цепочка вместо таймеров): помимо <c>AtTime</c>
    /// (поведение бит-в-бит как раньше) запись может взводиться событием —
    /// <see cref="NeedTriggerMode.AfterVillagerTossed"/> (первый БРОСОК жителя рукой за уровень,
    /// предикат <see cref="VillagerTossRules.IsVillagerThrow"/>, общий с <c>TutorialService</c>) или
    /// <see cref="NeedTriggerMode.AfterPreviousResolved"/> (разрешение записи, идущей непосредственно
    /// перед этой в исходном таймлайне — <see cref="EntryState.Previous"/>, прямая ссылка на
    /// объект-предыдущую запись, вычисляется один раз в конструкторе). Сервис поэтому подписан на
    /// шину (раньше не был) — паттерн подписки/отписки как у <c>WorshipService</c>.
    /// Блокер ревью шага 10: сопоставление по (type, siteIndex) — КОПИИ ключа предыдущей записи —
    /// давало ложное срабатывание, если рантайм-запись (<see cref="TriggerNeed"/>) с тем же ключом
    /// резолвилась раньше самой скриптованной предыдущей записи. Фикс — ссылка на объект
    /// <see cref="EntryState.Previous"/> и проверка именно его <c>Status == Resolved</c>, а не
    /// совпадения ключа с ЛЮБОЙ разрешённой записью.
    /// Playtest-фикс шага 10 (2026-09-11): <see cref="NeedTriggerMode.AfterVillagerTossed"/> раньше
    /// взводился подпиской на <c>VillagerStateChangedEvent</c> (<c>To==Tossed, Cause==Grabbed</c>) —
    /// это детектило ЗАХВАТ жителя рукой, не бросок (FSM входит в Tossed уже при захвате, см.
    /// game-design §5). Дом реагировал на взятие жителя в руку. Источник взведения теперь — пара
    /// <c>ObjectGrabbedEvent</c>/<c>ObjectThrownEvent</c> (см. <see cref="OnObjectGrabbed"/>/
    /// <see cref="OnObjectThrown"/>), тот же приём латча вида объекта, что <c>TutorialService</c>.
    /// </remarks>
    public sealed class LevelEventService : ILevelEventService, ITickable, IDisposable
    {
        private enum EntryStatus
        {
            Pending,

            /// <summary>
            /// Шаг 10 (ревью, важное — двойная активация ключа): условие взведения (время/событие)
            /// уже наступило, но ключ (type, targetSiteIndex) занят ДРУГОЙ Active-записью (типично —
            /// рантайм-нуждой от <see cref="TriggerNeed"/>) — запись ждёт освобождения ключа, не
            /// становится второй Active-записью на тот же ключ. Взводится немедленно, как только
            /// блокирующая запись становится терминальной (<see cref="ResolveNeed"/>/истечение в
            /// <see cref="Tick"/>) — см. <see cref="TryActivateArmedEntries"/>. <see cref="Tick"/> её
            /// не тикает (тот же <c>default: continue</c>, что Resolved/Expired) — она не истекает и
            /// не взводится сама по себе, только через освобождение ключа.
            /// </summary>
            Armed,

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

            /// <summary>
            /// Блокер ревью периодических нужд: «создана рантайм-триггером» и «создана игроком» —
            /// разные вещи. <see cref="ByPlayer"/> отвечает только за Awe/Fear/Alignment, а признак
            /// «запись временная, после терминального статуса её можно удалить» — этот флаг:
            /// периодический спавнер зовёт <see cref="LevelEventService.TriggerNeed"/> с
            /// <c>byPlayer=false</c> (Fear игроку не положен), и без отдельного флага его записи
            /// оседали бы в <c>_entries</c> навсегда (L05: ~8 записей/мин на весь Tick/HasBlockingNeeds).
            /// Таймлайновые записи — false: их финальный статус остаётся частью состояния уровня.
            /// </summary>
            public bool IsRuntime;

            /// <summary>
            /// Шаг 10: прямая ссылка на объект-предыдущую запись (по порядку в исходном
            /// <c>LevelConfig.EventTimeline</c>) — null, если предыдущей нет (первая запись таймлайна).
            /// Значимо только для <see cref="NeedTriggerMode.AfterPreviousResolved"/> — вычисляется
            /// один раз в конструкторе, а не индексным поиском в рантайме (рантайм-записи TriggerNeed
            /// добавляются в хвост списка и не должны сдвигать чужие "предыдущие"). Ссылка на ОБЪЕКТ,
            /// а не копия ключа (type, siteIndex) — блокер ревью шага 10: сопоставление по копии ключа
            /// ложно срабатывало, если рантайм-запись с тем же (type, siteIndex) резолвилась раньше
            /// самой скриптованной предыдущей записи (см. <see cref="OnNeedResolved"/>).
            /// </summary>
            public EntryState Previous;
        }

        private readonly IEventBus _eventBus;
        private readonly List<EntryState> _entries = new List<EntryState>();
        private float _elapsed;

        /// <summary>
        /// Playtest-фикс шага 10: латч (Id, Kind) ПОСЛЕДНЕГО захваченного рукой объекта —
        /// <c>ObjectThrownEvent</c> не несёт <c>GrabbableKind</c> (рука держит один объект
        /// одновременно), вид объекта берётся отсюда и сверяется по <c>Id</c> в
        /// <see cref="OnObjectThrown"/>. См. remarks класса.
        /// </summary>
        private GrabbableId _lastGrabbedId;
        private GrabbableKind _lastGrabbedKind;
        private bool _hasGrabbedLatch;

        /// <summary>
        /// Шаг 10 (ревью, важное): уровень завершён (см. <see cref="OnLevelCompleted"/>) — дальнейшие
        /// событийные взведения (OnVillagerStateChanged/OnNeedResolved) блокируются локальным флагом,
        /// не гейтом по <see cref="Services.Level.ILevelFlowService"/> (тот создал бы цикл DI, см.
        /// CLAUDE.md "Коллекции и циклы через Func-регистрации"). Tick НЕ гейтуется — не входит в
        /// это исправление.
        /// </summary>
        private bool _levelFinished;

        private readonly IDisposable _objectGrabbedSubscription;
        private readonly IDisposable _objectThrownSubscription;
        private readonly IDisposable _needResolvedSubscription;
        private readonly IDisposable _levelCompletedSubscription;

        public LevelEventService(IEventBus eventBus, LevelConfig levelConfig)
        {
            _eventBus = eventBus;

            var timeline = levelConfig.EventTimeline;
            if (timeline != null)
            {
                for (var i = 0; i < timeline.Length; i++)
                {
                    var entry = timeline[i];
                    // ByPlayer не выставляется явно — дефолт bool (false) уже верен для записей таймлайна.
                    var state = new EntryState { Entry = entry, Status = EntryStatus.Pending };

                    if (entry.TriggerMode == NeedTriggerMode.AfterPreviousResolved)
                    {
                        if (i > 0)
                        {
                            // Ссылка на УЖЕ созданный EntryState предыдущей записи (_entries на этот
                            // момент цикла содержит записи 0..i-1) — не копия ключа (type, siteIndex).
                            // Блокер ревью шага 10: копия ключа сопоставлялась с ЛЮБОЙ записью с
                            // совпадающим (type, siteIndex), включая рантайм-запись TriggerNeed,
                            // разрешённую раньше самой предыдущей скриптованной записи.
                            state.Previous = _entries[i - 1];
                        }
                        else
                        {
                            // Конфигурационная ошибка (game-design §6) — первая запись не может
                            // ждать разрешения несуществующей предыдущей. Не бросаем исключение —
                            // запись остаётся Pending навсегда, тот же принцип, что и остальные
                            // защитные LogWarning этого сервиса (ResolveNeed на несуществующую запись).
                            // Previous остаётся null (дефолт).
                            UnityEngine.Debug.LogWarning(
                                $"[LevelEventService] Запись #{i} ({entry.type}, site {entry.targetSiteIndex}) имеет TriggerMode.AfterPreviousResolved, но это первая запись таймлайна — предыдущей нет, запись никогда не взведётся.");
                        }
                    }

                    _entries.Add(state);
                }
            }

            _objectGrabbedSubscription = _eventBus.Subscribe<ObjectGrabbedEvent>(OnObjectGrabbed);
            _objectThrownSubscription = _eventBus.Subscribe<ObjectThrownEvent>(OnObjectThrown);
            _needResolvedSubscription = _eventBus.Subscribe<NeedResolvedEvent>(OnNeedResolved);
            _levelCompletedSubscription = _eventBus.Subscribe<LevelCompletedEvent>(OnLevelCompleted);
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
                        // Шаг 10: AfterVillagerTossed/AfterPreviousResolved взводятся событием
                        // (OnVillagerStateChanged/OnNeedResolved), не временем — Tick их не трогает.
                        if (state.Entry.TriggerMode != NeedTriggerMode.AtTime) continue;
                        if (_elapsed < state.Entry.triggerTime) continue; // ещё не наступило

                        // Н1 (ревью шага 3): раньше здесь стоял 0 — при большом deltaTime (просадка
                        // FPS, "перепрыгнувший" triggerTime Tick) часть duration молча терялась бы
                        // фантомно, потому что таймер истечения стартовал бы от нуля, а не от
                        // фактического момента срабатывания. _elapsed - triggerTime — реальный лаг.
                        ActivateEntry(state, _elapsed - state.Entry.triggerTime);
                        break;

                    case EntryStatus.Active:
                        state.TimeSinceTriggered += deltaTime;
                        break;

                    default:
                        // Resolved/Expired/Armed — не тикают: Armed ждёт освобождения ключа
                        // (см. TryActivateArmedEntries), не время; разрешённое не истекает.
                        continue;
                }

                // Общая проверка истечения — выполняется и для записи, только что ставшей Active в
                // ЭТОМ ЖЕ Tick (большой deltaTime мог перепрыгнуть сразу и triggerTime, и
                // triggerTime+duration за один вызов — В9 ревью шага 3), и для уже Active записи.
                // Шаг 10: duration <= 0 — нужда НЕ истекает никогда (бесконечная, до явного
                // ResolveNeed), для ЛЮБОГО TriggerMode — NeedExpiredEvent для такой записи не
                // публикуется вовсе.
                if (state.Entry.duration > 0f && state.TimeSinceTriggered >= state.Entry.duration)
                {
                    state.Status = EntryStatus.Expired;
                    _eventBus.Publish(new NeedExpiredEvent(state.Entry.type, state.Entry.targetSiteIndex, state.ByPlayer));
                    // Двойная активация ключа: истечение освобождает ключ для Armed-записи, ждавшей именно его.
                    TryActivateArmedEntries(state.Entry.type, state.Entry.targetSiteIndex);
                }
            }

            // Незначительное (ревью шага 4): рост списка — рантайм-записи (IsRuntime, см. TriggerNeed —
            // и игрока, и периодического спавнера) в терминальном статусе больше не нужны, их
            // (type, siteIndex) снова станет доступен TriggerNeed'у при следующем срабатывании.
            // Таймлайновые записи (IsRuntime=false) НЕ удаляются — их финальный статус (Resolved/Expired)
            // остаётся частью состояния уровня как есть (game-design §6), сравнивать их количество с
            // EventTimeline может понадобиться.
            _entries.RemoveAll(state =>
                state.IsRuntime && (state.Status == EntryStatus.Resolved || state.Status == EntryStatus.Expired));
        }

        /// <summary>
        /// Переводит Pending- (или Armed-) запись в Active и публикует <see cref="NeedTriggeredEvent"/> —
        /// общая точка для трёх путей взведения (Tick по AtTime, OnVillagerStateChanged по
        /// AfterVillagerTossed, OnNeedResolved по AfterPreviousResolved) и для повторной попытки из
        /// <see cref="TryActivateArmedEntries"/>. <paramref name="initialTimeSinceTriggered"/> —
        /// 0 для событийных режимов (взведение и есть "сейчас", лага не существует), реальный лаг
        /// (<c>_elapsed - triggerTime</c>) только для AtTime (см. Н1, ревью шага 3).
        /// </summary>
        /// <remarks>
        /// Шаг 10 (ревью, важное — двойная активация ключа): перед активацией проверяется, не занят
        /// ли ключ (type, targetSiteIndex) ДРУГОЙ Active-записью (например, рантайм-нуждой от
        /// <see cref="TriggerNeed"/>, взведённой раньше самой скриптованной записи). Если занят —
        /// запись переходит в <see cref="EntryStatus.Armed"/> вместо Active, без публикации события,
        /// и ждёт освобождения ключа. Метод безопасно переиспользуется для уже Armed-записи (вызов из
        /// <see cref="TryActivateArmedEntries"/>) — повторная проверка занятости корректно оставляет
        /// её Armed, если ключ занят ДРУГОЙ Armed-записью, только что успевшей активироваться раньше
        /// неё в том же проходе.
        /// </remarks>
        private void ActivateEntry(EntryState state, float initialTimeSinceTriggered = 0f)
        {
            if (IsKeyOccupiedByAnotherActiveEntry(state))
            {
                state.Status = EntryStatus.Armed;
                return;
            }

            state.Status = EntryStatus.Active;
            state.TimeSinceTriggered = initialTimeSinceTriggered;
            // ByPlayer записи скриптованного таймлайна всегда false — CauseMiracle не валиден для
            // неё (см. Events.Village.cs), передаём default. Публикация штатная (не рантайм-триггер
            // игрока) для всех трёх режимов — сам факт взведения по событию не делает запись "ByPlayer".
            _eventBus.Publish(new NeedTriggeredEvent(state.Entry.type, state.Entry.targetSiteIndex, state.ByPlayer, default));
        }

        /// <summary>
        /// true, если СРЕДИ ДРУГИХ (не <paramref name="state"/>) записей есть Active с тем же
        /// (type, targetSiteIndex) — ключ уже занят, вторая Active-запись на него недопустима
        /// (см. <see cref="ResolveNeed"/>: "одновременно активна максимум одна запись на ключ").
        /// </summary>
        private bool IsKeyOccupiedByAnotherActiveEntry(EntryState state)
        {
            foreach (var other in _entries)
            {
                if (ReferenceEquals(other, state)) continue;
                if (other.Status != EntryStatus.Active) continue;
                if (other.Entry.type != state.Entry.type || other.Entry.targetSiteIndex != state.Entry.targetSiteIndex) continue;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Шаг 10 (ревью, важное — двойная активация ключа): вызывается СРАЗУ после того, как запись,
        /// державшая ключ (<paramref name="type"/>, <paramref name="siteIndex"/>), переведена в
        /// терминальный статус (Resolved — из <see cref="ResolveNeed"/>, Expired — из истечения в
        /// <see cref="Tick"/>). Взводит первую найденную Armed-запись с тем же ключом через
        /// <see cref="ActivateEntry"/> (который сам переиспользует проверку занятости — если ключ уже
        /// занят другой, только что активированной в этом же проходе Armed-записью, она корректно
        /// остаётся Armed). Гейт <see cref="_levelFinished"/> действует и здесь — уровень завершён,
        /// отложенные записи больше не взводятся.
        /// </summary>
        private void TryActivateArmedEntries(NeedType type, int siteIndex)
        {
            if (_levelFinished) return;

            var count = _entries.Count;
            for (var i = 0; i < count; i++)
            {
                var state = _entries[i];
                if (state.Status != EntryStatus.Armed) continue;
                if (state.Entry.type != type || state.Entry.targetSiteIndex != siteIndex) continue;

                ActivateEntry(state);
            }
        }

        /// <summary>
        /// Playtest-фикс шага 10: латчит вид (<see cref="GrabbableKind"/>) и <c>Id</c> только что
        /// захваченного объекта — <c>ObjectThrownEvent</c> вид не несёт (см. remarks класса).
        /// Перезаписывает предыдущий латч безусловно: рука держит максимум один объект одновременно,
        /// новый захват возможен только после отпускания предыдущего.
        /// </summary>
        private void OnObjectGrabbed(ObjectGrabbedEvent evt)
        {
            _lastGrabbedId = evt.Id;
            _lastGrabbedKind = evt.Kind;
            _hasGrabbedLatch = true;
        }

        /// <summary>
        /// Playtest-фикс шага 10: БРОСОК жителя рукой (не захват, не мягкая постановка) взводит
        /// НЕМЕДЛЕННО все Pending-записи с <see cref="NeedTriggerMode.AfterVillagerTossed"/> — после
        /// первой активации запись перестаёт быть Pending, повторные броски её больше не находят
        /// (без отдельного флага "уже было" — естественная идемпотентность по статусу). Индексный for
        /// по снимку длины — та же реентерантная защита, что и в <see cref="Tick"/> (см. комментарий
        /// там).
        /// </summary>
        private void OnObjectThrown(ObjectThrownEvent evt)
        {
            if (_levelFinished) return; // Ревью шага 10: уровень завершён — таймлайн больше не взводится.

            // Сопоставление по Id с латчем последнего захвата — см. remarks класса. Рассинхрон
            // (латч не соответствует отпущенному объекту) невозможен при штатной работе руки (один
            // захват — одно отпускание), но защитная проверка дешева и однозначна.
            if (!_hasGrabbedLatch || evt.Id != _lastGrabbedId) return;
            if (!VillagerTossRules.IsVillagerThrow(_lastGrabbedKind, evt.Velocity)) return;

            var count = _entries.Count;
            for (var i = 0; i < count; i++)
            {
                var state = _entries[i];
                if (state.Status != EntryStatus.Pending) continue;
                if (state.Entry.TriggerMode != NeedTriggerMode.AfterVillagerTossed) continue;

                ActivateEntry(state);
            }
        }

        /// <summary>
        /// Шаг 10: разрешение записи взводит НЕМЕДЛЕННО все Pending-записи с
        /// <see cref="NeedTriggerMode.AfterPreviousResolved"/>, для которых разрешённая запись —
        /// "предыдущая" (<see cref="EntryState.Previous"/>, <see cref="NeedResolvedEvent.ByPlayer"/>
        /// не важен). Срабатывает и на события, опубликованные этим же сервисом из
        /// <see cref="ResolveNeed"/>, — реентерантно безопасно тем же приёмом, что <see cref="Tick"/>
        /// (индексный for по снимку длины, см. комментарий там).
        /// </summary>
        /// <remarks>
        /// Блокер ревью шага 10: сопоставление раньше шло по КОПИИ ключа (type, siteIndex),
        /// зафиксированной в конструкторе, — совпадала любая разрешённая запись с тем же ключом,
        /// включая рантайм-запись <see cref="TriggerNeed"/>, резолвнутую РАНЬШЕ самой скриптованной
        /// предыдущей записи (сценарий: Fireball создаёт рантайм Fire@0 до того, как скриптованная
        /// Fire@0 сама взведётся; Rain резолвит рантайм-запись — она Active, а скриптованная всё ещё
        /// Pending; следующая по цепочке запись ложно взводилась). Фикс — прямая ссылка на объект
        /// <see cref="EntryState.Previous"/> и проверка именно его <c>Status == Resolved</c>: если
        /// разрешена была не она (а, например, рантайм-запись с тем же ключом), <c>Previous.Status</c>
        /// остаётся Pending/Active, и next корректно НЕ взводится. <see cref="ResolveNeed"/> выставляет
        /// <c>Status = Resolved</c> ДО публикации <c>NeedResolvedEvent</c>, поэтому к моменту вызова
        /// этого обработчика (синхронно из Publish) состояние уже корректно.
        /// </remarks>
        private void OnNeedResolved(NeedResolvedEvent evt)
        {
            if (_levelFinished) return; // Ревью шага 10: уровень завершён — таймлайн больше не взводится.

            var count = _entries.Count;
            for (var i = 0; i < count; i++)
            {
                var state = _entries[i];
                if (state.Status != EntryStatus.Pending) continue;
                if (state.Entry.TriggerMode != NeedTriggerMode.AfterPreviousResolved) continue;

                var previous = state.Previous;
                if (previous == null) continue;
                if (previous.Entry.type != evt.Type || previous.Entry.targetSiteIndex != evt.SiteIndex) continue;
                if (previous.Status != EntryStatus.Resolved) continue;

                ActivateEntry(state);
            }
        }

        /// <summary>Шаг 10 (ревью, важное): уровень завершён — взведение таймлайна событиями прекращается.</summary>
        private void OnLevelCompleted(LevelCompletedEvent evt) => _levelFinished = true;

        public void ResolveNeed(NeedType type, int siteIndex, bool byPlayer, MiracleType causeMiracle)
        {
            // Незначительное (добивка ревью шага 10): индексный for по снимку длины, не foreach — та
            // же реентерантная защита, что у Tick/OnVillagerStateChanged/OnNeedResolved. После Publish
            // здесь стоит TryActivateArmedEntries, который вызывает ActivateEntry — тот сам публикует
            // NeedTriggeredEvent; если когда-нибудь появится подписчик, реентерантно добавляющий записи
            // в _entries прямо из этой цепочки публикаций, foreach упал бы InvalidOperationException.
            var count = _entries.Count;
            for (var i = 0; i < count; i++)
            {
                var state = _entries[i];
                if (state.Status != EntryStatus.Active) continue;
                if (state.Entry.type != type || state.Entry.targetSiteIndex != siteIndex) continue;

                state.Status = EntryStatus.Resolved;
                _eventBus.Publish(new NeedResolvedEvent(type, siteIndex, byPlayer, causeMiracle));
                // Двойная активация ключа: разрешение освобождает ключ для Armed-записи, ждавшей именно его.
                TryActivateArmedEntries(type, siteIndex);
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
                ByPlayer = byPlayer,
                IsRuntime = true
            };
            _entries.Add(newState);

            _eventBus.Publish(new NeedTriggeredEvent(type, siteIndex, byPlayer, causeMiracle));
        }

        /// <summary>Мост для VContainer ITickable — единственное место сервиса, читающее UnityEngine.Time.</summary>
        public void Tick() => Tick(UnityEngine.Time.deltaTime);

        /// <summary>
        /// Шаг 10 (ревью, решение владельца — soft-fail достижим на L01): считает блокирующими только
        /// нужды, способные разрешиться САМИ по времени — Active-запись с <c>duration &gt; 0</c>
        /// (истечёт сама) ИЛИ Pending-запись с <c>TriggerMode == AtTime</c> (наступит сама). Вечная
        /// Active-нужда (<c>duration &lt;= 0</c>, ждёт явного игрока) и событийные Pending-записи
        /// (AfterVillagerTossed/AfterPreviousResolved, ждут действия игрока/другого события) сталл НЕ
        /// блокируют — иначе на уровне вроде L01 (вечная нужда после первого броска, следующая запись
        /// ждёт разрешения предыдущей) soft-fail был бы недостижим навсегда. Armed-записи (см.
        /// <see cref="EntryStatus.Armed"/>) сами по себе тоже не блокируют — ни в статусе Active, ни в
        /// Pending они не находятся. Ревью (добивка): если запись, держащая ключ, бесконечна
        /// (<c>duration &lt;= 0</c>), а Armed-запись ждёт именно её — результат false для ОБЕИХ,
        /// осознанно: бесконечная нужда разрешается только явным действием игрока (потратить Faith на
        /// чудо), а не временем, которого у неё нет.
        /// </summary>
        public bool HasBlockingNeeds
        {
            get
            {
                foreach (var state in _entries)
                {
                    if (state.Status == EntryStatus.Active && state.Entry.duration > 0f) return true;
                    if (state.Status == EntryStatus.Pending && state.Entry.TriggerMode == NeedTriggerMode.AtTime) return true;
                }

                return false;
            }
        }

        /// <summary>Шаг периодических нужд (уровни 2–5) — см. <see cref="ILevelEventService.IsNeedActive"/>.</summary>
        public bool IsNeedActive(NeedType type, int siteIndex)
        {
            foreach (var state in _entries)
            {
                if (state.Status != EntryStatus.Active) continue;
                if (state.Entry.type != type || state.Entry.targetSiteIndex != siteIndex) continue;
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            _objectGrabbedSubscription?.Dispose();
            _objectThrownSubscription?.Dispose();
            _needResolvedSubscription?.Dispose();
            _levelCompletedSubscription?.Dispose();
        }
    }
}
