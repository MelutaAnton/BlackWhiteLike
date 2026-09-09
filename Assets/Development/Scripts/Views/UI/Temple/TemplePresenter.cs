using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Upgrades;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Idle;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Meta;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Temple
{
    /// <summary>MVP-Presenter экрана Temple (game-design §8, §13, шаг 8). Pure C#.</summary>
    public sealed class TemplePresenter : IStartable, IDisposable
    {
        // Порядок строк UI = порядок категорий здесь (game-design §8: 3 фиксированные категории).
        private static readonly UpgradeCategory[] Categories =
        {
            UpgradeCategory.HandPower,
            UpgradeCategory.FaithCapacity,
            UpgradeCategory.MiraclePower
        };

        private readonly ITempleView _view;
        private readonly IEventBus _eventBus;
        private readonly IMetaProgressService _metaProgress;
        private readonly IEconomyService _economy;
        private readonly UpgradeCatalogConfig _upgradeCatalog;
        private readonly IIdleIncomeService _idleIncome;
        private readonly ILevelSelectionService _selection;
        private readonly ISceneLoaderService _sceneLoader;

        private IDisposable _currencyChangedSubscription;
        private IDisposable _idleIncomeReadySubscription;

        public TemplePresenter(
            ITempleView view,
            IEventBus eventBus,
            IMetaProgressService metaProgress,
            IEconomyService economy,
            UpgradeCatalogConfig upgradeCatalog,
            IIdleIncomeService idleIncome,
            ILevelSelectionService selection,
            ISceneLoaderService sceneLoader)
        {
            _view = view;
            _eventBus = eventBus;
            _metaProgress = metaProgress;
            _economy = economy;
            _upgradeCatalog = upgradeCatalog;
            _idleIncome = idleIncome;
            _selection = selection;
            _sceneLoader = sceneLoader;
        }

        public void Start()
        {
            _view.SetVisible(true);
            _view.SetCoins(_metaProgress.Coins);

            // БЛОКЕР 3 (ревью шага 8): pull, а не только push через IdleIncomeReadyEvent — порядок
            // IStartable между IdleIncomeService и этим презентером не гарантирован, событие могло
            // публиковаться ДО подписки ниже и теряться. Состояние читается напрямую из сервиса.
            if (_idleIncome.HasPendingReward)
            {
                _view.ShowIdleReward(_idleIncome.PendingCoins);
            }
            else
            {
                _view.HideIdleReward();
            }

            RefreshRows();

            _view.BuyClicked += OnBuyClicked;
            _view.CollectIdleClicked += OnCollectIdleClicked;
            _view.NextLevelClicked += OnNextLevelClicked;

            _currencyChangedSubscription = _eventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            _idleIncomeReadySubscription = _eventBus.Subscribe<IdleIncomeReadyEvent>(OnIdleIncomeReady);
        }

        public void Dispose()
        {
            _view.BuyClicked -= OnBuyClicked;
            _view.CollectIdleClicked -= OnCollectIdleClicked;
            _view.NextLevelClicked -= OnNextLevelClicked;

            _currencyChangedSubscription?.Dispose();
            _idleIncomeReadySubscription?.Dispose();
        }

        private void OnCurrencyChanged(CurrencyChangedEvent evt) => _view.SetCoins(evt.NewValue);

        private void OnIdleIncomeReady(IdleIncomeReadyEvent evt) => _view.ShowIdleReward(evt.CoinAmount);

        private void OnCollectIdleClicked()
        {
            _idleIncome.Collect();
            _view.HideIdleReward();
        }

        private void OnBuyClicked(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= Categories.Length) return;

            _economy.TryPurchaseUpgrade(Categories[rowIndex]);
            RefreshRows(); // цена/уровень следующей ступени могли поменяться
        }

        private void OnNextLevelClicked()
        {
            _selection.RequestReturnToLevelSelect();
            _sceneLoader.Load(SceneNames.MainMenu);
        }

        private void RefreshRows()
        {
            for (var i = 0; i < Categories.Length && i < _view.RowCount; i++)
            {
                var category = Categories[i];
                var level = _metaProgress.GetUpgradeLevel(category);
                var maxLevel = _upgradeCatalog.MaxLevel(category);

                var hasNext = _upgradeCatalog.TryGetLevelEntry(category, level + 1, out var nextEntry);
                var price = hasNext ? nextEntry.Price : 0;
                var effectText = hasNext ? $"+{nextEntry.EffectValue}" : string.Empty;

                _view.ConfigureRow(i, category.ToString(), level, maxLevel, effectText, price, isMaxed: !hasNext);
            }
        }
    }
}
