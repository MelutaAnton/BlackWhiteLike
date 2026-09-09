using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Idle;
using BlackWhiteLike.Services.Meta;
using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Services.Idle
{
    /// <summary>
    /// Реализация <see cref="IIdleIncomeService"/> (game-design §15, шаг 8). Сценовый — регистрируется
    /// только в мета-сценах (см. GameLifetimeScope). Время — исключительно через
    /// <see cref="ITimeProvider"/>, без прямого UnityEngine.Time (тестируется fake-временем).
    /// </summary>
    /// <remarks>
    /// Семантика штампа LastSeenUtc (правка ревью шага 8, ВАЖНО 5): штампуется ТОЛЬКО в
    /// <see cref="Collect"/> и на первом запуске (когда профиль ни разу не штамповался — начислять
    /// не от чего). Раньше <see cref="Start"/> штамповал "на входе", ДО фактического сбора — несобранная
    /// награда сгорала бы навсегда, если игрок закрыл сцену, не нажав кнопку сбора. Теперь повторный
    /// вход без сбора пересчитывает от того же (неизменного) LastSeenUtc — сумма не растёт бесконтрольно
    /// (клампится CapHours), просто не теряется. Collect() штампует момент, посчитанный в Start()
    /// (<see cref="_pendingStampUtc"/>), а не "текущее время сбора" — иначе секунды между Start() и
    /// нажатием кнопки в UI отъедались бы у следующего интервала офлайна.
    /// </remarks>
    public sealed class IdleIncomeService : IIdleIncomeService, IStartable
    {
        private readonly IEventBus _eventBus;
        private readonly IMetaProgressService _metaProgress;
        private readonly IEconomyService _economy;
        private readonly ITimeProvider _timeProvider;
        private readonly IdleIncomeConfig _config;

        private int _pendingCoins;
        private float _offlineHours;
        private bool _hasPendingReward;
        private bool _collected;
        private DateTime _pendingStampUtc;

        public IdleIncomeService(
            IEventBus eventBus,
            IMetaProgressService metaProgress,
            IEconomyService economy,
            ITimeProvider timeProvider,
            IdleIncomeConfig config)
        {
            _eventBus = eventBus;
            _metaProgress = metaProgress;
            _economy = economy;
            _timeProvider = timeProvider;
            _config = config;
        }

        public bool HasPendingReward => _hasPendingReward;
        public int PendingCoins => _pendingCoins;
        public float OfflineHours => _offlineHours;

        public void Start()
        {
            var lastSeen = _metaProgress.LastSeenUtc;
            var now = _timeProvider.UtcNow;

            // Первый запуск (профиль ещё не штамповался) — только штамп, начислять не от чего.
            if (lastSeen == default)
            {
                _metaProgress.StampLastSeen(now);
                return;
            }

            // Защита от перевода системных часов назад: не начисляем и НЕ штампуем — следующий
            // нормальный вход пересчитает от прежнего LastSeenUtc как ни в чём не бывало (elapsed
            // ниже гарантированно >= TimeSpan.Zero, если этот return не сработал).
            if (now < lastSeen)
            {
                Debug.LogWarning("[IdleIncomeService] Текущее время раньше LastSeenUtc (часы переведены назад?) — офлайн-доход не начисляется, штамп не обновляется.");
                return;
            }

            var elapsed = now - lastSeen;

            // Ниже минимума — штамп НЕ обновляется (см. remarks класса): следующий вход досчитает
            // тот же интервал плюс добавившееся время, а не обнулит его преждевременным штампом.
            if (elapsed.TotalMinutes < _config.MinOfflineMinutes) return;

            var cappedHours = Mathf.Min((float)elapsed.TotalHours, _config.CapHours);
            var coins = Mathf.FloorToInt(cappedHours * _config.CoinsPerHour);

            if (coins <= 0) return; // округлилось в 0 — штамп тоже не трогаем, тот же принцип "не сгорает"

            _pendingCoins = coins;
            _offlineHours = cappedHours;
            _hasPendingReward = true;
            _pendingStampUtc = now; // Collect() проштампует именно этот момент, а не время нажатия кнопки
            _eventBus.Publish(new IdleIncomeReadyEvent(coins, cappedHours));
        }

        public void Collect()
        {
            if (_collected || !_hasPendingReward) return; // однократно — повторный вызов no-op

            _collected = true;
            _metaProgress.StampLastSeen(_pendingStampUtc);
            _economy.GrantIdleIncome(_pendingCoins);
        }
    }
}
