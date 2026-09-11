using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Services.Village;
using BlackWhiteLike.Services.Worship;
using VContainer.Unity;

namespace BlackWhiteLike.Services.Level
{
    /// <summary>
    /// Реализация <see cref="ILevelFlowService"/>. Pure C# — win подписывается на
    /// <c>WorshipChangedEvent</c> (game-design §9, шаг 5) вместо опроса <c>IWorshipService.Current</c>
    /// каждый Tick; stall пересчитывается в <see cref="Tick"/> по решению владельца (game-design,
    /// "Soft-fail"): Faith ≈ 0 ∧ нет блокирующих нужд (<c>ILevelEventService.HasBlockingNeeds</c>,
    /// шаг 10 — только те, что способны разрешиться сами по времени) ∧ Worship ниже порога.
    /// </summary>
    /// <remarks>
    /// Шаг 5 (аудит после первого Play Mode): сервис НЕ зависит от <c>IGameFlowService</c> — раньше
    /// прямой вызов <c>ChangeState(GameState.LevelComplete)</c> отсюда замыкал цикл VContainer
    /// (GameFlowService → биндинги → LevelPlayingState → ILevelFlowService → снова GameFlowService,
    /// Lazy re-entrancy при Build()). Вместо этого сервис только публикует
    /// <see cref="LevelCompletedEvent"/> ровно один раз; саму смену состояния делает
    /// <c>GameFlowService</c> по таблице переходов game-design §9 ("LevelPlaying" + событие →
    /// "LevelComplete"), подписываясь на это событие самостоятельно.
    /// </remarks>
    public sealed class LevelFlowService : ILevelFlowService, ITickable, IDisposable
    {
        private const float StallFaithEpsilon = 0.01f;

        /// <summary>
        /// Шаг 10 (ревью, решение владельца — добивка): после отказа игрока
        /// (<see cref="AcknowledgeStallDeclined"/>) повторный показ <c>LevelStalledEvent</c>
        /// блокируется на это время, даже если условие сталла остаётся истинным каждый кадр — иначе
        /// защёлка сбрасывалась бы и тут же взводилась заново в СЛЕДУЮЩЕМ же Tick (Faith чаще всего
        /// не меняется мгновенно после отказа), окно мигало бы/переоткрывалось мгновенно. Поднято с
        /// 5 до 30 секунд: оверлей закрывает экран dim-блокером, переспрос каждые 5с назойлив — а
        /// игрок в этом окне может продолжать играть БЕЗ веры (например, добивать уровень Fear-путём),
        /// так что частый повторный показ не даёт ему ничего, кроме раздражения.
        /// </summary>
        public const float StallReshowCooldownAfterDecline = 30f;

        private readonly IEventBus _eventBus;
        private readonly IWorshipService _worshipService;
        private readonly IFaithService _faithService;
        private readonly ILevelEventService _levelEvents;
        private readonly LevelConfig _levelConfig;

        private float _elapsed;
        private bool _isRunning;
        private bool _isCompleted;
        private bool _isStalled;

        /// <summary>Шаг 10: остаток кулдауна после <see cref="AcknowledgeStallDeclined"/> (см. <see cref="StallReshowCooldownAfterDecline"/>).</summary>
        private float _stallReshowCooldownRemaining;

        private readonly IDisposable _worshipChangedSubscription;

        public LevelFlowService(
            IEventBus eventBus,
            IWorshipService worshipService,
            IFaithService faithService,
            ILevelEventService levelEvents,
            LevelConfig levelConfig)
        {
            _eventBus = eventBus;
            _worshipService = worshipService;
            _faithService = faithService;
            _levelEvents = levelEvents;
            _levelConfig = levelConfig;

            _worshipChangedSubscription = _eventBus.Subscribe<WorshipChangedEvent>(OnWorshipChanged);
        }

        public float Elapsed => _elapsed;
        public bool IsRunning => _isRunning;
        public bool IsStalled => _isStalled;

        public void StartLevel()
        {
            _elapsed = 0f;
            _isRunning = true;
            _isCompleted = false;
            _isStalled = false;
            _stallReshowCooldownRemaining = 0f;
        }

        private void OnWorshipChanged(WorshipChangedEvent evt)
        {
            if (!_isRunning || _isCompleted) return;
            if (evt.NewValue < evt.Target) return;

            _isCompleted = true;
            _isRunning = false; // аудит п.8: IsRunning по xml-doc — "между StartLevel и достижением Worship.Target"
            _eventBus.Publish(new LevelCompletedEvent(_levelConfig.Id, _worshipService.AweTotal, _worshipService.FearTotal, _elapsed));
        }

        public void Tick(float dt)
        {
            if (!_isRunning || _isCompleted) return;

            _elapsed += dt;

            if (_stallReshowCooldownRemaining > 0f) _stallReshowCooldownRemaining -= dt;

            // Шаг 10 (решение владельца, soft-fail достижим на L01): stall блокируют только нужды,
            // способные разрешиться сами (по времени) — HasBlockingNeeds, не HasActiveNeeds/
            // HasPendingNeeds (вечная нужда/событийные Pending-записи блокировали бы сталл навсегда).
            var isStalledNow = _faithService.Current <= StallFaithEpsilon
                && !_levelEvents.HasBlockingNeeds
                && _worshipService.Current < _worshipService.Target;

            if (isStalledNow && !_isStalled && _stallReshowCooldownRemaining <= 0f)
            {
                _isStalled = true;
                _eventBus.Publish(new LevelStalledEvent());
            }
            else if (!isStalledNow && _faithService.Current > StallFaithEpsilon && _isStalled)
            {
                // Faith снова выше эпсилона (например, "Пополнить веру") — повторный stall возможен.
                _isStalled = false;
            }
        }

        /// <summary>
        /// Шаг 10 (решение владельца — окно rewarded-согласия): игрок отказался пополнить веру
        /// (<c>DeclineClicked</c>) — сбрасывает защёлку показа так, чтобы повторный сталл снова
        /// опубликовал <c>LevelStalledEvent</c>, но не раньше чем через
        /// <see cref="StallReshowCooldownAfterDecline"/> секунд (см. поле). Не в состоянии stall —
        /// безопасный no-op (нечего подтверждать).
        /// </summary>
        public void AcknowledgeStallDeclined()
        {
            if (!_isStalled) return;

            _isStalled = false;
            _stallReshowCooldownRemaining = StallReshowCooldownAfterDecline;
        }

        /// <summary>Мост для VContainer ITickable — единственное место сервиса, читающее UnityEngine.Time.</summary>
        public void Tick() => Tick(UnityEngine.Time.deltaTime);

        public void Dispose()
        {
            _worshipChangedSubscription?.Dispose();
        }
    }
}
