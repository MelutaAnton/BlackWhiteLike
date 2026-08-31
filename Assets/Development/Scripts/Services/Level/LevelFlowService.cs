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
    /// "Soft-fail"): Faith ≈ 0 ∧ нет активных ∧ нет ожидающих нужд ∧ Worship ниже порога.
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

        private readonly IEventBus _eventBus;
        private readonly IWorshipService _worshipService;
        private readonly IFaithService _faithService;
        private readonly ILevelEventService _levelEvents;
        private readonly LevelConfig _levelConfig;

        private float _elapsed;
        private bool _isRunning;
        private bool _isCompleted;
        private bool _isStalled;

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

            var isStalledNow = _faithService.Current <= StallFaithEpsilon
                && !_levelEvents.HasActiveNeeds
                && !_levelEvents.HasPendingNeeds
                && _worshipService.Current < _worshipService.Target;

            if (isStalledNow && !_isStalled)
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

        /// <summary>Мост для VContainer ITickable — единственное место сервиса, читающее UnityEngine.Time.</summary>
        public void Tick() => Tick(UnityEngine.Time.deltaTime);

        public void Dispose()
        {
            _worshipChangedSubscription?.Dispose();
        }
    }
}
