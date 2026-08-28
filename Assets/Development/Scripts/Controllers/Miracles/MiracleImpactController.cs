using System;
using BlackWhiteLike.Controllers.Village;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Miracles
{
    /// <summary>
    /// Физическое воздействие чуда на жителей (game-design §4 — "Fireball разбрасывает жителей
    /// взрывом"). Unity API only: подписывается на <see cref="MiracleCastEvent"/> и делает
    /// <see cref="Physics.OverlapSphere(Vector3, float, int)"/> — единственная связь чуда со сценой
    /// это событие, сам <c>MiracleService</c> не держит ссылок на объекты.
    /// </summary>
    public sealed class MiracleImpactController : MonoBehaviour
    {
        [Tooltip("Слой жителей для Physics.OverlapSphere.")]
        [SerializeField] private LayerMask _villagerMask;

        private IEventBus _eventBus;
        private IVillagerStateService _stateService;
        private VillagerConfig _villagerConfig;
        private MiracleCatalogConfig _catalog;
        private bool _initialized;

        private IDisposable _miracleCastSubscription;

        [Inject]
        public void Construct(
            IEventBus eventBus,
            IVillagerStateService stateService,
            VillagerConfig villagerConfig,
            MiracleCatalogConfig catalog)
        {
            _eventBus = eventBus;
            _stateService = stateService;
            _villagerConfig = villagerConfig;
            _catalog = catalog;

            Subscribe();

            _initialized = true;
        }

        private void OnEnable()
        {
            // CLAUDE.md: OnEnable вызывается до Construct — на самом первом Construct этот путь
            // безопасно пропускается (_initialized ещё false), на повторных enable — единственный
            // источник переподписки (тот же приём, что NeedSiteController/VillagerController).
            if (!_initialized) return;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            Unsubscribe();
            _miracleCastSubscription = _eventBus.Subscribe<MiracleCastEvent>(OnMiracleCast);
        }

        private void Unsubscribe()
        {
            _miracleCastSubscription?.Dispose();
            _miracleCastSubscription = null;
        }

        private void OnMiracleCast(MiracleCastEvent evt)
        {
            if (!_catalog.TryGet(evt.Type, out var config)) return;
            if (!config.BlastsVillagers) return;

            // OverlapSphere не NonAlloc — вызывается на каст, не каждый кадр (тот же аргумент, что
            // в NeedSiteController.ReportStimulusToNearbyVillagers), лишняя аллокация не проблема.
            var blastColliders = Physics.OverlapSphere(evt.Point, evt.Radius, _villagerMask);
            foreach (var col in blastColliders)
            {
                var villager = col.GetComponentInParent<VillagerController>();
                if (villager == null) continue;

                villager.ApplyBlast(evt.Point, evt.Power);
            }

            // Паника у соседей взрыва — шире радиуса самого блaста (PanicRadius уже используется
            // NeedSiteController для того же смысла "насколько далеко распространяется испуг").
            var panicColliders = Physics.OverlapSphere(evt.Point, _villagerConfig.PanicRadius, _villagerMask);
            foreach (var col in panicColliders)
            {
                var villager = col.GetComponentInParent<VillagerController>();
                if (villager == null) continue;

                // Уже разбросанные (Blasted -> Tossed) взрыв игнорирует по таблице переходов
                // VillagerStateService — повторный вызов здесь безопасен (self-loop без события).
                _stateService.ReportStimulus(villager.VillagerId, VillagerStimulus.ExplosionNearby);
            }
        }
    }
}
