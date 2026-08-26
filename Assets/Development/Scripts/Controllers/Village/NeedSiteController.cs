using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Village
{
    /// <summary>
    /// Точка события-нужды деревни (game-design §5) — дом/поле/группа жителей. Unity API only:
    /// переключает визуальные маркеры по своему <see cref="_siteIndex"/>/<see cref="_needType"/> и
    /// репортит стимулы соседним жителям (Д3, ревью плейтеста шага 3 — недостающее звено §5:
    /// "рядом пожар/взрыв → Panic; нужда разрешена рядом → Worship"), подписываясь на
    /// <c>NeedTriggeredEvent</c>/<c>NeedResolvedEvent</c>/<c>NeedExpiredEvent</c> от
    /// <c>LevelEventService</c>. Разрешение нужды чудом — шаг 4+ (здесь не реализуется), но
    /// триггер-зона попадания чуда обязана существовать уже сейчас с корректной физикой.
    /// </summary>
    public sealed class NeedSiteController : MonoBehaviour
    {
        private enum VisualState
        {
            Normal,
            Active,
            Expired
        }

        [Tooltip("Индекс точки события — сопоставляется с NeedEventEntry.targetSiteIndex в LevelConfig.")]
        [SerializeField] private int _siteIndex;

        [Tooltip("Тип нужды, обслуживаемый этой точкой (дом=Fire, поле=Hunger, группа жителей=Disease).")]
        [SerializeField] private NeedType _needType;

        [Header("Визуал по стейту (маркеры-GameObject'ы, пустые ссылки игнорируются)")]
        [SerializeField] private GameObject _normalVisual;
        [SerializeField] private GameObject _activeVisual;
        [SerializeField] private GameObject _expiredVisual;

        [Tooltip("Д3 (ревью плейтеста шага 3): слой жителей для Physics.OverlapSphere — кому репортить стимул при срабатывании/разрешении нужды.")]
        [SerializeField] private LayerMask _villagerMask;

        private IEventBus _eventBus;
        private IVillagerStateService _stateService;
        private VillagerConfig _villagerConfig;
        private bool _initialized;

        private IDisposable _triggeredSubscription;
        private IDisposable _resolvedSubscription;
        private IDisposable _expiredSubscription;

        [Inject]
        public void Construct(IEventBus eventBus, IVillagerStateService stateService, VillagerConfig villagerConfig)
        {
            _eventBus = eventBus;
            _stateService = stateService;
            _villagerConfig = villagerConfig;

            // Триггеры требуют Rigidbody (CLAUDE.md, "Известные нюансы Unity Physics") — без него
            // OnTriggerEnter молчит, если ни у одного участника нет Rigidbody. Kinematic + без
            // гравитации: точка события не должна падать/двигаться от физики.
            var body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;

            SetVisualState(VisualState.Normal);

            // В8 (ревью шага 3): подписка — здесь, а не в OnEnable под "if (!_initialized) return"
            // (как было раньше). CLAUDE.md: OnEnable вызывается ДО Construct — прежний вариант
            // держался только на удачном порядке загрузки сцены, а не гарантии. Construct()
            // вызывается VContainer'ом ровно один раз явно (autoInjectGameObjects/RegisterComponent),
            // тот же приём, что VillagerController. Отписка — в OnDisable, переподписка при
            // повторном enable — в OnEnable (В4, см. ниже).
            Subscribe();

            _initialized = true;
        }

        private void OnEnable()
        {
            // В4 (ревью шага 3): любой SetActive(false→true) МИМО Construct (например, ручное
            // выключение/включение точки события) раньше навсегда терял подписку — OnDisable
            // отписывал безусловно, а переподписки не было нигде. На самом первом Construct этот
            // путь безопасно пропускается (_initialized ещё false).
            if (!_initialized) return;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            Unsubscribe(); // защита от дублирования, если Construct и OnEnable оба успели вызваться
            _triggeredSubscription = _eventBus.Subscribe<NeedTriggeredEvent>(OnTriggered);
            _resolvedSubscription = _eventBus.Subscribe<NeedResolvedEvent>(OnResolved);
            _expiredSubscription = _eventBus.Subscribe<NeedExpiredEvent>(OnExpired);
        }

        private void Unsubscribe()
        {
            _triggeredSubscription?.Dispose();
            _resolvedSubscription?.Dispose();
            _expiredSubscription?.Dispose();

            _triggeredSubscription = null;
            _resolvedSubscription = null;
            _expiredSubscription = null;
        }

        private void OnTriggered(NeedTriggeredEvent evt)
        {
            if (!MatchesThisSite(evt.SiteIndex, evt.Type, nameof(NeedTriggeredEvent))) return;
            SetVisualState(VisualState.Active);

            // Д3 (ревью плейтеста шага 3): недостающее звено §5 — таймлайн срабатывал, дом "горел",
            // но до жителей это не доходило вообще (единственные ReportStimulus были Grabbed/Landed/
            // HardLanded из самого VillagerController). Только Fire явно описан в game-design §5 как
            // "жители паникуют" — для Disease/Hunger подходящего VillagerStimulus пока нет, не
            // выдумываем несуществующий (см. TryGetPanicStimulus).
            if (TryGetPanicStimulus(evt.Type, out var stimulus))
            {
                ReportPanicStimulusToNearbyVillagers(stimulus);
            }
        }

        private void OnResolved(NeedResolvedEvent evt)
        {
            if (!MatchesThisSite(evt.SiteIndex, evt.Type, nameof(NeedResolvedEvent))) return;
            SetVisualState(VisualState.Normal);

            // Д3: разрешение нужды рядом → Worship (game-design §5), независимо от типа нужды.
            ReportStimulusToNearbyVillagers(VillagerStimulus.NeedResolvedNearby);
        }

        private void OnExpired(NeedExpiredEvent evt)
        {
            if (!MatchesThisSite(evt.SiteIndex, evt.Type, nameof(NeedExpiredEvent))) return;
            SetVisualState(VisualState.Expired);
        }

        /// <summary>
        /// Вычитка диффа целиком (бонус к п.4-5): совпадение siteIndex при несовпадении _needType
        /// раньше было молчаливым no-op — это не "чужое событие" (тот случай, когда siteIndex вообще
        /// не совпал, ожидаем и не логируется), а битый таймлайн в LevelConfig (тот же индекс, другой
        /// тип нужды), который лучше увидеть сейчас, чем ждать валидатор шага 7.
        /// </summary>
        private bool MatchesThisSite(int siteIndex, NeedType type, string eventName)
        {
            if (siteIndex != _siteIndex) return false;

            if (type != _needType)
            {
                Debug.LogWarning(
                    $"[NeedSiteController] Точка {_siteIndex} (тип {_needType}) получила {eventName} " +
                    $"с совпавшим siteIndex, но другим типом ({type}) — вероятно, битый таймлайн в LevelConfig.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Физический запрос "кто рядом" — законная работа контроллера (Unity API, Physics);
        /// решение о переходе состояния остаётся целиком в IVillagerStateService — сюда мы не
        /// заходим через VillagerController, чтобы не дублировать источник правды о FSM-переходах.
        /// Радиус — VillagerConfig.PanicRadius (Д3 ревью, обоснование решения см. отчёт): он уже
        /// существовал в конфиге, но не использовался нигде, заводить второй радиус с тем же
        /// смыслом ("насколько далеко распространяется паника") было бы дублированием параметра.
        /// OverlapSphere не NonAlloc — вызывается редко (на срабатывание/разрешение нужды, не
        /// каждый кадр), лишняя аллокация здесь не проблема.
        /// </summary>
        private void ReportStimulusToNearbyVillagers(VillagerStimulus stimulus)
        {
            var colliders = Physics.OverlapSphere(transform.position, _villagerConfig.PanicRadius, _villagerMask);
            foreach (var col in colliders)
            {
                var villager = col.GetComponentInParent<VillagerController>();
                if (villager == null) continue;

                // Вычитка диффа целиком (п.4): "id жителя = InstanceId" раньше пересчитывался ещё и
                // здесь — единственный источник теперь VillagerController.VillagerId.
                _stateService.ReportStimulus(villager.VillagerId, stimulus);
            }
        }

        /// <summary>
        /// П2 (второй playtest шага 3): вариант для паника-стимулов — вдобавок сообщает каждому
        /// найденному жителю позицию источника опасности (<c>VillagerController.SetPanicSource</c>),
        /// чтобы бегство в Panic шло прочь от конкретной точки, а не в случайную сторону. Порядок
        /// вызовов важен: позиция источника фиксируется ДО ReportStimulus, чтобы к моменту, когда
        /// FSM реально войдёт в Panic (в этом же кадре, синхронно), направление уже было известно.
        /// </summary>
        private void ReportPanicStimulusToNearbyVillagers(VillagerStimulus stimulus)
        {
            var colliders = Physics.OverlapSphere(transform.position, _villagerConfig.PanicRadius, _villagerMask);
            foreach (var col in colliders)
            {
                var villager = col.GetComponentInParent<VillagerController>();
                if (villager == null) continue;

                villager.SetPanicSource(transform.position);
                _stateService.ReportStimulus(villager.VillagerId, stimulus);
            }
        }

        private static bool TryGetPanicStimulus(NeedType type, out VillagerStimulus stimulus)
        {
            switch (type)
            {
                case NeedType.Fire:
                    stimulus = VillagerStimulus.FireNearby;
                    return true;

                default:
                    // Disease/Hunger (game-design §5) не описаны как источник паники — только
                    // визуал/анимация по месту, без стимула жителям. Не изобретаем стимул,
                    // которого нет в контракте VillagerStimulus.
                    stimulus = default;
                    return false;
            }
        }

        private void SetVisualState(VisualState state)
        {
            if (_normalVisual != null) _normalVisual.SetActive(state == VisualState.Normal);
            if (_activeVisual != null) _activeVisual.SetActive(state == VisualState.Active);
            if (_expiredVisual != null) _expiredVisual.SetActive(state == VisualState.Expired);
        }
    }
}
