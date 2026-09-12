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
    /// взрывом", шаг 6 — "Lightning мгновенно поражает жителей в радиусе", шаг переноса Disease —
    /// "Heal разрешает болезнь конкретного больного жителя"). Unity API only: подписывается на
    /// <see cref="MiracleCastEvent"/> и делает <see cref="Physics.OverlapSphere(Vector3, float, int)"/> —
    /// единственная связь чуда со сценой это событие, сам <c>MiracleService</c> не держит ссылок на
    /// объекты. Маппинг "какое чудо лечит" — целиком в <see cref="MiracleConfig.ResolvesNeeds"/>
    /// (данные, не код, §14.2): контроллер не ветвится по <c>MiracleType</c>.
    /// </summary>
    public sealed class MiracleImpactController : MonoBehaviour
    {
        [Tooltip("Слой жителей для Physics.OverlapSphere.")]
        [SerializeField] private LayerMask _villagerMask;

        private IEventBus _eventBus;
        private IVillagerStateService _stateService;
        private VillagerConfig _villagerConfig;
        private MiracleCatalogConfig _catalog;
        private ILevelEventService _levelEventService;
        private IVillagerDiseaseService _diseaseService;
        private bool _initialized;

        private IDisposable _miracleCastSubscription;

        [Inject]
        public void Construct(
            IEventBus eventBus,
            IVillagerStateService stateService,
            VillagerConfig villagerConfig,
            MiracleCatalogConfig catalog,
            ILevelEventService levelEventService,
            IVillagerDiseaseService diseaseService)
        {
            _eventBus = eventBus;
            _stateService = stateService;
            _villagerConfig = villagerConfig;
            _catalog = catalog;
            _levelEventService = levelEventService;
            _diseaseService = diseaseService;

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

        /// <summary>«Лечит ли чудо кого-то из жителей» — выводится из данных (ResolvesNeeds) и правила NeedHostRules, без перечисления типов.</summary>
        private static bool ResolvesAnyVillagerHostedNeed(MiracleConfig config)
        {
            var needs = config.ResolvesNeeds;
            if (needs == null) return false;

            foreach (var need in needs)
            {
                if (NeedHostRules.IsVillagerHosted(need)) return true;
            }

            return false;
        }

        private void OnMiracleCast(MiracleCastEvent evt)
        {
            if (!_catalog.TryGet(evt.Type, out var config)) return;

            // Шаг переноса Disease: "лечит" — чудо разрешает хотя бы одну villager-hosted нужду
            // (NeedHostRules), конкретный тип — только из данных MiracleConfig.ResolvesNeeds (ревью:
            // NeedType.Disease в этом контроллере не упоминается). Отдельный флаг от Blasts/Strikes —
            // ни один из трёх не подразумевает прочие.
            var heals = ResolvesAnyVillagerHostedNeed(config);
            if (!config.BlastsVillagers && !config.StrikesVillagers && !heals) return;

            // OverlapSphere не NonAlloc — вызывается на каст, не каждый кадр (тот же аргумент, что в
            // NeedSiteController.ReportStimulusToNearbyVillagers), лишняя аллокация не проблема. Все
            // три ветки (Blast/Strike/Heal) бьют по одному и тому же radius — ревью шага 6: раньше
            // считали сферу дважды одними и теми же аргументами; выше уже гарантирован хотя бы один из флагов.
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

            if (heals)
            {
                foreach (var col in impactColliders)
                {
                    var villager = col.GetComponentInParent<VillagerController>();
                    if (villager == null) continue;

                    // Не каждый житель в радиусе болен — IVillagerDiseaseService единственный источник,
                    // кто именно (NeedHostRules: Disease не имеет точки в сцене, поэтому здесь нет
                    // аналога MatchesThisSite у NeedSiteController).
                    if (!_diseaseService.TryGetActiveNeed(villager.VillagerId, out var needType, out var siteIndex)) continue;
                    if (!config.ResolvesNeed(needType)) continue;

                    // ResolveNeed синхронно тянет NeedResolvedEvent -> WorshipService/LevelFlow -> возможный
                    // LevelCompletedEvent; вызов внутри foreach по снимку OverlapSphere безопасен, пока
                    // завершение уровня никого не despawn'ит (см. remarks VillagerDiseaseService).
                    _levelEventService.ResolveNeed(needType, siteIndex, byPlayer: true, causeMiracle: evt.Type);

                    // Разрешение рядом -> Worship (game-design §5, тот же стимул, что
                    // NeedSiteController.OnResolved для Fire/Hunger) — репортится всем жителям вокруг
                    // ВЫЛЕЧЕННОГО (не вокруг evt.Point), включая его самого (его же коллайдер попадает
                    // в собственный OverlapSphere). Позиция стимула — точка вылеченного, «источник
                    // благодати», а не каждого соседа (ревью: единая точка якорения, как у сайта).
                    var healedPosition = villager.transform.position;
                    var worshipColliders = Physics.OverlapSphere(healedPosition, _villagerConfig.PanicRadius, _villagerMask);
                    foreach (var worshipCol in worshipColliders)
                    {
                        var nearbyVillager = worshipCol.GetComponentInParent<VillagerController>();
                        if (nearbyVillager == null) continue;

                        _stateService.ReportStimulus(nearbyVillager.VillagerId, VillagerStimulus.NeedResolvedNearby, healedPosition);
                    }
                }
            }

            // Паника у соседей взрыва/удара — шире радиуса самого попадания (PanicRadius уже
            // используется NeedSiteController для того же смысла "насколько далеко распространяется
            // испуг"). ТОЛЬКО для Blast/Strike — лечение соседей не пугает.
            if (config.BlastsVillagers || config.StrikesVillagers)
            {
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
}
