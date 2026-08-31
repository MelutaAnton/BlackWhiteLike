using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Faith;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;
using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Services.Faith
{
    /// <summary>
    /// Реализация <see cref="IFaithService"/>. Pure C#, тот же прецедент, что <c>VillagerStateService</c>:
    /// SO (<see cref="FaithConfig"/>) и другой pure C#-сервис (<see cref="IVillagerStateService"/>)
    /// как источники данных, без обращений к остальному UnityEngine кроме Mathf/моста Tick().
    /// </summary>
    public sealed class FaithService : IFaithService, ITickable
    {
        private readonly IEventBus _eventBus;
        private readonly FaithConfig _config;
        private readonly IVillagerStateService _villagerStateService;

        private float _current;

        public FaithService(IEventBus eventBus, FaithConfig config, IVillagerStateService villagerStateService)
        {
            _eventBus = eventBus;
            _config = config;
            _villagerStateService = villagerStateService;

            _current = _config.BaseCapacity * (_config.StartingFaithPercent / 100f);
        }

        public float Current => _current;
        public float Capacity => _config.BaseCapacity;

        public bool CanAfford(float amount) => amount <= 0f || _current >= amount;

        public bool TrySpend(float amount)
        {
            if (amount <= 0f) return true; // нечего списывать — легальный no-op

            if (_current < amount) return false;

            SetCurrent(_current - amount);
            return true;
        }

        public void Tick(float deltaTime)
        {
            var worshippers = _villagerStateService.CountInState(VillagerState.Worship);
            if (worshippers <= 0) return; // нет молящихся — нечего регенерировать, без публикации события

            var regen = _config.RegenPerWorshipper * worshippers * deltaTime;
            SetCurrent(Mathf.Min(_current + regen, Capacity));
        }

        /// <summary>Мост для VContainer ITickable — единственное место сервиса, читающее UnityEngine.Time.</summary>
        public void Tick() => Tick(UnityEngine.Time.deltaTime);

        public void RefillToCapacity() => SetCurrent(Capacity);

        private void SetCurrent(float value)
        {
            if (Mathf.Approximately(value, _current)) return; // реального изменения нет — событие не публикуем

            var old = _current;
            _current = value;
            _eventBus.Publish(new FaithChangedEvent(old, _current, Capacity));
        }
    }
}
