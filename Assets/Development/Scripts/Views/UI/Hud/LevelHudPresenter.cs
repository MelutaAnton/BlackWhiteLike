using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Worship;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>MVP-Presenter HUD уровня (game-design §10, §13, шаг 5). Pure C#.</summary>
    public sealed class LevelHudPresenter : IStartable, IDisposable
    {
        private readonly ILevelHudView _view;
        private readonly IWorshipService _worshipService;
        private readonly IAlignmentService _alignmentService;
        private readonly IGameFlowService _gameFlow;
        private readonly IEventBus _eventBus;

        private IDisposable _worshipChangedSubscription;
        private IDisposable _alignmentChangedSubscription;

        public LevelHudPresenter(
            ILevelHudView view,
            IWorshipService worshipService,
            IAlignmentService alignmentService,
            IGameFlowService gameFlow,
            IEventBus eventBus)
        {
            _view = view;
            _worshipService = worshipService;
            _alignmentService = alignmentService;
            _gameFlow = gameFlow;
            _eventBus = eventBus;
        }

        public void Start()
        {
            _view.SetWorship(_worshipService.Current, _worshipService.Target);
            PushAlignment(_alignmentService.Score);

            _view.PauseClicked += OnPauseClicked;
            _worshipChangedSubscription = _eventBus.Subscribe<WorshipChangedEvent>(OnWorshipChanged);
            _alignmentChangedSubscription = _eventBus.Subscribe<AlignmentChangedEvent>(OnAlignmentChanged);
        }

        public void Dispose()
        {
            _view.PauseClicked -= OnPauseClicked;
            _worshipChangedSubscription?.Dispose();
            _alignmentChangedSubscription?.Dispose();
        }

        private void OnWorshipChanged(WorshipChangedEvent evt) => _view.SetWorship(evt.NewValue, evt.Target);

        private void OnAlignmentChanged(AlignmentChangedEvent evt) => PushAlignment(evt.NewScore);

        /// <summary>
        /// Нормализует Score в −1..+1 через <see cref="IAlignmentService.MinScore"/>/<see cref="IAlignmentService.MaxScore"/>
        /// (шаг 5, аудит п.12) — единственное место, знающее диапазон Score; view его не хардкодит.
        /// Клампится на случай MinScore не равного −MaxScore (симметрия не гарантирована контрактом).
        /// </summary>
        private void PushAlignment(float score)
        {
            var range = _alignmentService.MaxScore - _alignmentService.MinScore;
            var normalized = range > 0f
                ? (score - _alignmentService.MinScore) / range * 2f - 1f
                : 0f;

            if (normalized > 1f) normalized = 1f;
            else if (normalized < -1f) normalized = -1f;

            _view.SetAlignment(normalized);
        }

        private void OnPauseClicked()
        {
            if (!_gameFlow.IsPaused) _gameFlow.PushState(GameState.Paused);
        }
    }
}
