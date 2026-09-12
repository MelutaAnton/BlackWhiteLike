using System;
using System.Collections.Generic;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using VContainer;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Реализация <see cref="IVillagerDiseaseService"/>. Pure C# — подписки на шину заводятся в
    /// конструкторе, отписка в <see cref="Dispose"/> (тот же приём, что <c>LevelEventService</c>).
    /// Обрабатывает только записи, для которых <see cref="NeedHostRules.IsVillagerHosted"/> == true
    /// (в срезе — только Disease); прочие типы нужд (Fire/Hunger) этот сервис игнорирует полностью,
    /// ими занимается <c>NeedSiteController</c>.
    /// </summary>
    /// <remarks>
    /// Двухсторонний словарь (site→id и id→site) — оба направления нужны публичному контракту
    /// (<see cref="TryGetSiteIndex"/> для лечения по жителю, <see cref="TryGetSickVillager"/> для
    /// обратного поиска) без линейного перебора на каждый запрос.
    /// <para>
    /// Отложенное заражение (<c>_pendingSites</c>): срабатывание Disease, когда среди
    /// зарегистрированных жителей нет ни одного пригодного кандидата (все Tossed или уже больны),
    /// не создаёт больного немедленно — сайт запоминается и повторная попытка делается на КАЖДОМ
    /// <c>VillagerStateChangedEvent</c> (кандидат мог приземлиться/перестать быть Tossed), пока не
    /// найдётся кандидат или нужда не закроется раньше (resolve/expire отложенного сайта просто снимает
    /// его из отложенных, без каких-либо последствий для несуществующего больного).
    /// </para>
    /// <para>
    /// Известное ограничение: <c>VillagerController.OnDisable</c> (любой <c>SetActive(false)</c>/возврат
    /// в пул) чистит свой <c>_isSick</c> и реестр позиций локально, ничего не сообщая сюда — запись
    /// (site→id) остаётся, нужда числится неразрешённой, пока не истечёт/не закроется. В срезе
    /// штатного пути выключения жителя посреди уровня нет (<c>IVillagerSpawnService.DespawnAll</c>
    /// никем не вызывается), поэтому починка (подписка на событие despawn'а) — за пределами шага.
    /// </para>
    /// <para>
    /// Реентерантность: обратный проход по <c>_pendingSites</c> в <see cref="OnVillagerStateChanged"/>
    /// безопасен, потому что цепочка <see cref="TryAssignCandidate"/> → <c>VillagerSicknessChangedEvent</c>
    /// → <c>VillagerController</c> НЕ публикует <c>VillagerStateChangedEvent</c> (смена болезни — не
    /// переход FSM). Если это когда-нибудь изменится, ретрай нужно переводить на снимок списка — та же
    /// оговорка, что у индексных for в <c>LevelEventService</c>.
    /// </para>
    /// </remarks>
    public sealed class VillagerDiseaseService : IVillagerDiseaseService, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly IVillagerStateService _stateService;
        private readonly Random _random;

        // Тип нужды хранится рядом с siteIndex: лечащая сторона сверяет его с MiracleConfig.ResolvesNeeds,
        // сам сервис по типу не ветвится (единственный фильтр — NeedHostRules.IsVillagerHosted).
        private readonly struct SickRecord
        {
            public readonly NeedType Type;
            public readonly int SiteIndex;

            public SickRecord(NeedType type, int siteIndex)
            {
                Type = type;
                SiteIndex = siteIndex;
            }
        }

        private readonly Dictionary<int, VillagerId> _siteToVillager = new Dictionary<int, VillagerId>();
        private readonly Dictionary<VillagerId, SickRecord> _villagerToSite = new Dictionary<VillagerId, SickRecord>();

        // Сайты Disease, взведённые, но пока не нашедшие кандидата — см. remarks класса.
        private readonly List<PendingSite> _pendingSites = new List<PendingSite>();

        // Переиспользуемые буферы (без аллокаций в обработчиках, кроме роста при первом использовании).
        private readonly List<VillagerId> _registeredBuffer = new List<VillagerId>();
        private readonly List<VillagerId> _eligibleBuffer = new List<VillagerId>();

        private readonly IDisposable _needTriggeredSubscription;
        private readonly IDisposable _needResolvedSubscription;
        private readonly IDisposable _needExpiredSubscription;
        private readonly IDisposable _villagerStateChangedSubscription;

        /// <summary>
        /// DI-конструктор (блокер ревью): VContainer резолвит КАЖДЫЙ параметр самого длинного
        /// конструктора и C#-дефолт <c>random = null</c> не понимает — без [Inject] на этой перегрузке
        /// контейнер падал бы на Build() с «No such registration of type: System.Random».
        /// </summary>
        [Inject]
        public VillagerDiseaseService(IEventBus eventBus, IVillagerStateService stateService)
            : this(eventBus, stateService, new Random())
        {
        }

        /// <summary>Тестовая перегрузка — seeded <see cref="Random"/> для детерминированного выбора кандидата.</summary>
        public VillagerDiseaseService(IEventBus eventBus, IVillagerStateService stateService, Random random)
        {
            _eventBus = eventBus;
            _stateService = stateService;
            _random = random;

            _needTriggeredSubscription = _eventBus.Subscribe<NeedTriggeredEvent>(OnNeedTriggered);
            _needResolvedSubscription = _eventBus.Subscribe<NeedResolvedEvent>(OnNeedResolved);
            _needExpiredSubscription = _eventBus.Subscribe<NeedExpiredEvent>(OnNeedExpired);
            _villagerStateChangedSubscription = _eventBus.Subscribe<VillagerStateChangedEvent>(OnVillagerStateChanged);
        }

        public bool IsSick(VillagerId id) => _villagerToSite.ContainsKey(id);

        public bool TryGetActiveNeed(VillagerId id, out NeedType type, out int siteIndex)
        {
            if (_villagerToSite.TryGetValue(id, out var record))
            {
                type = record.Type;
                siteIndex = record.SiteIndex;
                return true;
            }

            type = default;
            siteIndex = default;
            return false;
        }

        public bool TryGetSickVillager(int siteIndex, out VillagerId id) => _siteToVillager.TryGetValue(siteIndex, out id);

        private void OnNeedTriggered(NeedTriggeredEvent evt)
        {
            if (!NeedHostRules.IsVillagerHosted(evt.Type)) return;

            // LevelEventService не допускает двух Active-записей на один ключ (type, siteIndex) —
            // сайт с уже назначенным больным сюда попасть не должен, проверка защитная.
            if (_siteToVillager.ContainsKey(evt.SiteIndex)) return;
            if (IndexOfPending(evt.SiteIndex) >= 0) return;

            if (!TryAssignCandidate(evt.Type, evt.SiteIndex))
            {
                _pendingSites.Add(new PendingSite(evt.Type, evt.SiteIndex));
            }
        }

        // Отложенный сайт помнит и тип: при ретрае запись создаётся с тем же типом, что пришёл в NeedTriggered.
        private readonly struct PendingSite
        {
            public readonly NeedType Type;
            public readonly int SiteIndex;

            public PendingSite(NeedType type, int siteIndex)
            {
                Type = type;
                SiteIndex = siteIndex;
            }
        }

        private int IndexOfPending(int siteIndex)
        {
            for (var i = 0; i < _pendingSites.Count; i++)
            {
                if (_pendingSites[i].SiteIndex == siteIndex) return i;
            }

            return -1;
        }

        private void OnNeedResolved(NeedResolvedEvent evt) => OnNeedClosed(evt.Type, evt.SiteIndex);

        // Истечение = выздоровление без последствий (решение владельца 2026-09-12: таймеры уровней
        // уйдут следующим шагом) — обрабатывается тем же путём, что и разрешение.
        private void OnNeedExpired(NeedExpiredEvent evt) => OnNeedClosed(evt.Type, evt.SiteIndex);

        private void OnNeedClosed(NeedType type, int siteIndex)
        {
            if (!NeedHostRules.IsVillagerHosted(type)) return;

            // Нужда закрылась раньше, чем нашёлся кандидат — просто снимаем отложку, больного нет.
            var pendingIndex = IndexOfPending(siteIndex);
            if (pendingIndex >= 0)
            {
                _pendingSites.RemoveAt(pendingIndex);
                return;
            }

            if (!_siteToVillager.TryGetValue(siteIndex, out var id)) return;

            _siteToVillager.Remove(siteIndex);
            _villagerToSite.Remove(id);
            _eventBus.Publish(new VillagerSicknessChangedEvent(id, siteIndex, false));
        }

        /// <summary>
        /// Кандидат мог приземлиться (перестать быть Tossed) с последнего провала попытки — ретраим
        /// ВСЕ отложенные сайты на каждый переход FSM любого жителя (не только конкретного, чей переход
        /// пришёл: дешёвая проверка, срабатывает нечасто — отложенное состояние не штатное). Обратный
        /// проход по индексу — безопасное удаление найденных элементов во время итерации.
        /// </summary>
        private void OnVillagerStateChanged(VillagerStateChangedEvent evt)
        {
            if (_pendingSites.Count == 0) return;

            for (var i = _pendingSites.Count - 1; i >= 0; i--)
            {
                var pending = _pendingSites[i];
                if (TryAssignCandidate(pending.Type, pending.SiteIndex))
                {
                    _pendingSites.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Пытается найти и назначить случайного больного на <paramref name="siteIndex"/> среди
        /// зарегистрированных жителей, исключая уже больных (любым сайтом) и Tossed (в руке/в полёте —
        /// тот же аргумент, что DespawnVillagersNearby раньше в NeedSiteController: житель занят
        /// физическим событием). true — кандидат найден и назначен, событие опубликовано.
        /// </summary>
        private bool TryAssignCandidate(NeedType type, int siteIndex)
        {
            _stateService.CollectRegistered(_registeredBuffer);

            _eligibleBuffer.Clear();
            foreach (var id in _registeredBuffer)
            {
                if (_villagerToSite.ContainsKey(id)) continue;
                if (_stateService.GetState(id) == VillagerState.Tossed) continue;
                _eligibleBuffer.Add(id);
            }

            if (_eligibleBuffer.Count == 0) return false;

            var chosen = _eligibleBuffer[_random.Next(_eligibleBuffer.Count)];

            _siteToVillager[siteIndex] = chosen;
            _villagerToSite[chosen] = new SickRecord(type, siteIndex);
            _eventBus.Publish(new VillagerSicknessChangedEvent(chosen, siteIndex, true));
            return true;
        }

        public void Dispose()
        {
            _needTriggeredSubscription?.Dispose();
            _needResolvedSubscription?.Dispose();
            _needExpiredSubscription?.Dispose();
            _villagerStateChangedSubscription?.Dispose();
        }
    }
}
