using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Idle;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Overlay
{
    /// <summary>MVP-Presenter оверлея "с возвращением" (game-design §9, §13, §15, шаг 8). Pure C#.</summary>
    public sealed class WelcomeBackPresenter : IStartable, IDisposable
    {
        private readonly IWelcomeBackView _view;
        private readonly IEventBus _eventBus;
        private readonly IGameFlowService _gameFlow;
        private readonly IIdleIncomeService _idleIncome;

        private IDisposable _idleIncomeReadySubscription;

        public WelcomeBackPresenter(IWelcomeBackView view, IEventBus eventBus, IGameFlowService gameFlow, IIdleIncomeService idleIncome)
        {
            _view = view;
            _eventBus = eventBus;
            _gameFlow = gameFlow;
            _idleIncome = idleIncome;
        }

        public void Start()
        {
            _view.Hide();
            _view.CollectClicked += OnCollectClicked;
            _view.CloseClicked += OnCloseClicked;
            _idleIncomeReadySubscription = _eventBus.Subscribe<IdleIncomeReadyEvent>(OnIdleIncomeReady);

            // БЛОКЕР 3 (ревью шага 8): pull, а не только push через событие — см. remarks TemplePresenter.
            if (_idleIncome.HasPendingReward)
            {
                _gameFlow.PushState(GameState.WelcomeBack);
                _view.Show(_idleIncome.PendingCoins);
            }
        }

        public void Dispose()
        {
            _view.CollectClicked -= OnCollectClicked;
            _view.CloseClicked -= OnCloseClicked;
            _idleIncomeReadySubscription?.Dispose();
        }

        private void OnIdleIncomeReady(IdleIncomeReadyEvent evt)
        {
            _gameFlow.PushState(GameState.WelcomeBack);
            _view.Show(evt.CoinAmount);
        }

        private void OnCollectClicked()
        {
            _idleIncome.Collect();
            _view.Hide();
            _gameFlow.PopState();
        }

        private void OnCloseClicked()
        {
            _view.Hide();
            _gameFlow.PopState();
        }
    }
}
