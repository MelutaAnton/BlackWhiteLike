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
    /// взрывом", шаг 6 — "Lightning мгновенно поражает жителей в радиусе"). Unity API only:
    /// подписывается на <see cref="MiracleCastEvent"/> и делает
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
            if (!config.BlastsVillagers && !config.StrikesVillagers) return;

            // OverlapSphere не NonAlloc — вызывается на каст, не каждый кадр (тот же аргумент, что в
            // NeedSiteController.ReportStimulusToNearbyVillagers), лишняя аллокация не проблема. Обе
            // ветки (Blast/Strike) бьют по одному и тому же radius — ревью шага 6: раньше считали
            // сферу дважды одними и теми же аргументами; выше уже гарантирован хотя бы один из флагов.
            var impactColliders = Physics.OverlapSphere(evt.Point, evt.Radius, _villagerMask);

            if (config.BlastsVillagers)
            {
                foreach (var col in impactColliders)
                {
                    var villager = col.GetComponentInParent<VillagerController>();
                    if (villager == null) continue;

                    villager.ApplyBlast(evt.Point, evt.Power);
                }
            }

            if (config.StrikesVillagers)
            {
                // Шаг 6 (решение владельца 2026-09-01): удар молнии — мгновенный Stunned, без
                // физического отброса (в отличие от BlastsVillagers/ApplyBlast выше). Стимул
                // LightningStruck сам решает через VillagerStateService.TryResolveTarget, из каких
                // состояний переход легален (Idle/Panic/Worship -> Stunned, Tossed — игнор).
                foreach (var col in impactColliders)
                {
                    var villager = col.GetComponentInParent<VillagerController>();
                    if (villager == null) continue;

                    // Шаг 10 (волна 2): позиция удара — мировая точка жителя, WorshipService якорит
                    // к ней попап Fear (VillagerStruckByLightning).
                    _stateService.ReportStimulus(villager.VillagerId, VillagerStimulus.LightningStruck, villager.transform.position);
                }
            }

            // Паника у соседей взрыва/удара — шире радиуса самого попадания (PanicRadius уже
            // используется NeedSiteController для того же смысла "насколько далеко распространяется
            // испуг"). Выполняется для обоих видов воздействия — уже поражённые (Tossed/Stunned)
            // взрыв/удар игнорирует по таблице переходов VillagerStateService (self-loop без события).
            var panicColliders = Physics.OverlapSphere(evt.Point, _villagerConfig.PanicRadius, _villagerMask);
            foreach (var col in panicColliders)
            {
                var villager = col.GetComponentInParent<VillagerController>();
                if (villager == null) continue;

                _stateService.ReportStimulus(villager.VillagerId, VillagerStimulus.ExplosionNearby);
            }
        }
    }
}
