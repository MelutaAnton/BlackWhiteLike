using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Services.Meta;
using UnityEngine;

namespace BlackWhiteLike.Services.Miracles
{
    /// <summary>
    /// Реализация <see cref="IMiracleService"/>. Pure C# — держит ровно одну активную сессию каста
    /// (см. xml-doc интерфейса), тот же прецедент, что <c>HandInteractionService</c>. Шаг 8: сила
    /// эффекта и радиус модифицируются мета-апгрейдом MiraclePower через
    /// <see cref="IEffectiveStatsService"/>.
    /// </summary>
    public sealed class MiracleService : IMiracleService
    {
        private readonly IEventBus _eventBus;
        private readonly IFaithService _faithService;
        private readonly MiracleCatalogConfig _catalog;
        private readonly IEffectiveStatsService _stats;

        private bool _isCasting;
        private MiracleType _type;
        private MiracleConfig _config;
        private Vector3 _point;
        private float _holdTime;
        private float _pumpedTime;
        private float _radius;

        // Базовый радиус сессии УЖЕ с учётом MiracleRadiusMultiplier — зафиксирован на BeginCast,
        // чтобы ReleaseCast делил на тот же масштаб, каким считался рост радиуса при накачке
        // (апгрейд не может поменяться посреди одной сессии каста — покупка доступна только в Temple).
        private float _effectiveBaseRadius;

        public MiracleService(IEventBus eventBus, IFaithService faithService, MiracleCatalogConfig catalog, IEffectiveStatsService stats)
        {
            _eventBus = eventBus;
            _faithService = faithService;
            _catalog = catalog;
            _stats = stats;
        }

        public bool IsCasting => _isCasting;
        public float CurrentRadius => _isCasting ? _radius : 0f;

        public bool CanCast(MiracleType type)
        {
            if (!_catalog.TryGet(type, out var config)) return false;
            return _faithService.CanAfford(config.BaseFaithCost);
        }

        public bool BeginCast(MiracleType type, Vector3 point)
        {
            if (_isCasting) return false; // одна активная сессия одновременно
            if (!_catalog.TryGet(type, out var config)) return false;

            if (!_faithService.TrySpend(config.BaseFaithCost))
            {
                // Публикуем ТОЛЬКО при отказе нехваткой Faith (TrySpend возвращает false ровно в
                // этом случае — amount<=0 всегда true, см. FaithService.TrySpend) — шаг 10, волна 2,
                // решение владельца: rewarded-окно должно реагировать на явный тап игрока, не только
                // на автоматический soft-fail (LevelStalledEvent).
                _eventBus.Publish(new MiracleDeniedEvent(type, (int)config.BaseFaithCost, _faithService.Current));
                return false;
            }

            _isCasting = true;
            _type = type;
            _config = config;
            _point = point;
            _holdTime = 0f;
            _pumpedTime = 0f;
            _effectiveBaseRadius = config.BaseRadius * _stats.MiracleRadiusMultiplier;
            _radius = _effectiveBaseRadius;

            return true;
        }

        public void UpdateCast(float deltaTime, Vector3 point)
        {
            if (!_isCasting) return;

            _point = point;

            // Время сессии копится всегда — не зависит от того, хватило ли Faith на накачку.
            _holdTime += deltaTime;

            // Накачиваемое время — часть holdTime сверх льготного окна (MiracleConfig.HoldGraceSeconds):
            // первые HoldGraceSeconds удержания — «ещё тап», прирост накачиваемого времени нулевой.
            var pumpableTime = Mathf.Max(0f, _holdTime - _config.HoldGraceSeconds);
            var pumpDelta = Mathf.Max(0f, pumpableTime - _pumpedTime);

            var holdCost = _config.HoldCostPerSecond * pumpDelta;
            if (_faithService.TrySpend(holdCost))
            {
                _pumpedTime = pumpableTime;
                var t = Mathf.Clamp01(_pumpedTime / _config.HoldSecondsToMaxRadius);
                var effectiveMaxRadius = _config.MaxRadius * _stats.MiracleRadiusMultiplier;
                _radius = Mathf.Lerp(_effectiveBaseRadius, effectiveMaxRadius, t);
            }
            // Иначе — Faith не хватило на этот тик накачки: накачиваемое время и радиус замирают
            // на текущих значениях, сессия остаётся легальной (BaseFaithCost уже оплачен при BeginCast).
        }

        public void ReleaseCast()
        {
            if (!_isCasting) return;

            var power = _config.EffectPower * _stats.MiraclePowerMultiplier * (_radius / _effectiveBaseRadius);
            _eventBus.Publish(new MiracleCastEvent(_type, _point, power, _radius));

            CloseSession();
        }

        public void CancelCast()
        {
            if (!_isCasting) return;

            CloseSession(); // без события, без возврата Faith
        }

        private void CloseSession()
        {
            _isCasting = false;
            _config = null;
            _holdTime = 0f;
            _pumpedTime = 0f;
            _radius = 0f;
            _effectiveBaseRadius = 0f;
        }
    }
}
