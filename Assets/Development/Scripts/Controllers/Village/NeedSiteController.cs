using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Miracles;
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
    /// <c>LevelEventService</c>. Шаг 4: разрешение/создание нужды попаданием чуда — подписка на
    /// <c>MiracleCastEvent</c>, маппинг "чудо → нужда" читается из <c>MiracleConfig</c> (данные, не
    /// код, game-design §14.2) — сама точка не хранит правило, только применяет его к себе.
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
        private ILevelEventService _levelEventService;
        private MiracleCatalogConfig _miracleCatalog;
        private bool _initialized;

        // Шаг 4: true между Triggered и Resolved/Expired — читается обработчиком MiracleCastEvent,
        // чтобы решить "это попадание разрешает нужду или создаёт новую" (game-design §5).
        // Ограничение "сайт обслуживает одну нужду за раз" — то же самое, что уже подразумевает
        // NeedSiteController._needType (один тип на точку); снято будет на шаге 6/7 вместе с
        // расширением на несколько одновременных нужд на одной точке, если понадобится.
        private bool _isNeedActive;

        private IDisposable _triggeredSubscription;
        private IDisposable _resolvedSubscription;
        private IDisposable _expiredSubscription;
        private IDisposable _miracleCastSubscription;

        [Inject]
        public void Construct(
            IEventBus eventBus,
            IVillagerStateService stateService,
            VillagerConfig villagerConfig,
            ILevelEventService levelEventService,
            MiracleCatalogConfig miracleCatalog)
        {
            _eventBus = eventBus;
            _stateService = stateService;
            _villagerConfig = villagerConfig;
            _levelEventService = levelEventService;
            _miracleCatalog = miracleCatalog;

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

            // Незначительное (ревью шага 4): явный сброс на Construct, симметрично OnDisable ниже —
            // консервативное решение владельца на случай повторного использования объекта (если
            // когда-нибудь появится пул точек события): свежий Construct не должен унаследовать
            // _isNeedActive=true от предыдущей жизни того же компонента.
            _isNeedActive = false;
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

            // Незначительное (ревью шага 4): сайт выключен — дальнейшее состояние нужды неизвестно
            // (пока подписки сняты, LevelEventService мог бы разрешить/просрочить её без участия
            // этого объекта). Решение владельца — консервативный сброс в false: при повторном
            // включении Fireball создаст нужду заново, если её на самом деле уже нет (CreatesNeed);
            // если нужда на самом деле ещё активна — TriggerNeed увидит её в LevelEventService и
            // сделает штатный no-op ("уже активна"), а Resolve сработает по факту события. Риск —
            // разве что лишний кадр визуала Normal до следующего события.
            _isNeedActive = false;
        }

        private void Subscribe()
        {
            Unsubscribe(); // защита от дублирования, если Construct и OnEnable оба успели вызваться
            _triggeredSubscription = _eventBus.Subscribe<NeedTriggeredEvent>(OnTriggered);
            _resolvedSubscription = _eventBus.Subscribe<NeedResolvedEvent>(OnResolved);
            _expiredSubscription = _eventBus.Subscribe<NeedExpiredEvent>(OnExpired);
            _miracleCastSubscription = _eventBus.Subscribe<MiracleCastEvent>(OnMiracleCast);
        }

        private void Unsubscribe()
        {
            _triggeredSubscription?.Dispose();
            _resolvedSubscription?.Dispose();
            _expiredSubscription?.Dispose();
            _miracleCastSubscription?.Dispose();

            _triggeredSubscription = null;
            _resolvedSubscription = null;
            _expiredSubscription = null;
            _miracleCastSubscription = null;
        }

        private void OnTriggered(NeedTriggeredEvent evt)
        {
            if (!MatchesThisSite(evt.SiteIndex, evt.Type, nameof(NeedTriggeredEvent))) return;
            _isNeedActive = true;
            SetVisualState(VisualState.Active);

            // Д3 (ревью плейтеста шага 3): недостающее звено §5 — таймлайн срабатывал, дом "горел",
            // но до жителей это не доходило вообще (единственные ReportStimulus были Grabbed/Landed/
            // HardLanded из самого VillagerController). Только Fire явно описан в game-design §5 как
            // "жители паникуют" — для Disease/Hunger подходящего VillagerStimulus пока нет, не
            // выдумываем несуществующий (см. IVillagerStateService.TryGetPanicStimulus).
            if (_stateService.TryGetPanicStimulus(evt.Type, out var stimulus))
            {
                ReportPanicStimulusToNearbyVillagers(stimulus);
            }
        }

        private void OnResolved(NeedResolvedEvent evt)
        {
            if (!MatchesThisSite(evt.SiteIndex, evt.Type, nameof(NeedResolvedEvent))) return;
            _isNeedActive = false;
            SetVisualState(VisualState.Normal);

            // Д3: разрешение нужды рядом → Worship (game-design §5), независимо от типа нужды.
            ReportStimulusToNearbyVillagers(VillagerStimulus.NeedResolvedNearby);
        }

        private void OnExpired(NeedExpiredEvent evt)
        {
            if (!MatchesThisSite(evt.SiteIndex, evt.Type, nameof(NeedExpiredEvent))) return;
            _isNeedActive = false;
            SetVisualState(VisualState.Expired);
        }

        /// <summary>
        /// Шаг 4: попадание чуда в зону этой точки (game-design §4, §5) — горизонтальная дистанция
        /// до <see cref="MiracleCastEvent.Point"/> в пределах <see cref="MiracleCastEvent.Radius"/>.
        /// Маппинг "какое чудо что делает с какой нуждой" — целиком в MiracleConfig (данные, не код,
        /// §14.2): точка либо разрешает уже активную нужду своего типа, либо создаёт новую, если
        /// нужды сейчас нет и это чудо помечено как создающее её именно этого типа.
        /// </summary>
        private void OnMiracleCast(MiracleCastEvent evt)
        {
            if (HorizontalDistance(transform.position, evt.Point) > evt.Radius) return;
            if (!_miracleCatalog.TryGet(evt.Type, out var config)) return;

            if (_isNeedActive && config.ResolvesNeed(_needType))
            {
                _levelEventService.ResolveNeed(_needType, _siteIndex, byPlayer: true, causeMiracle: evt.Type);
            }
            else if (!_isNeedActive && config.CreatesNeed && config.CreatedNeed == _needType)
            {
                _levelEventService.TriggerNeed(_needType, _siteIndex, config.CreatedNeedDuration, byPlayer: true, causeMiracle: evt.Type);
            }
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
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

        private void SetVisualState(VisualState state)
        {
            if (_normalVisual != null) _normalVisual.SetActive(state == VisualState.Normal);
            if (_activeVisual != null) _activeVisual.SetActive(state == VisualState.Active);
            if (_expiredVisual != null) _expiredVisual.SetActive(state == VisualState.Expired);
        }
    }
}
