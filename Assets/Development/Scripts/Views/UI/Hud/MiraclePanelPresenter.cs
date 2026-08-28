using System;
using System.Collections.Generic;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Services.Hand;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// MVP-Presenter панели чудес (game-design §4, §10). Pure C# — вся бизнес-логика (что доступно,
    /// что можно себе позволить) остаётся в сервисах, презентер только связывает их с View.
    /// </summary>
    public sealed class MiraclePanelPresenter : IStartable, IDisposable
    {
        private readonly IMiraclePanelView _view;
        private readonly IHandModeService _handModeService;
        private readonly IFaithService _faithService;
        private readonly MiracleCatalogConfig _catalog;
        private readonly IEventBus _eventBus;

        private IDisposable _miracleSelectedSubscription;
        private IDisposable _faithChangedSubscription;

        // Важно 7 (ревью шага 4): построенный один раз в Start список доступных конфигов вместо
        // Enum.GetValues в OnFaithChanged — FaithChangedEvent публикуется каждый кадр при регене
        // Faith, а Enum.GetValues(typeof(MiracleType)) аллоцирует массив и боксит enum-значения
        // на каждый вызов.
        private readonly List<MiracleConfig> _availableMiracles = new List<MiracleConfig>();

        public MiraclePanelPresenter(
            IMiraclePanelView view,
            IHandModeService handModeService,
            IFaithService faithService,
            MiracleCatalogConfig catalog,
            IEventBus eventBus)
        {
            _view = view;
            _handModeService = handModeService;
            _faithService = faithService;
            _catalog = catalog;
            _eventBus = eventBus;
        }

        public void Start()
        {
            foreach (MiracleType type in Enum.GetValues(typeof(MiracleType)))
            {
                if (_catalog.TryGet(type, out var config))
                {
                    _availableMiracles.Add(config);
                    _view.SetAvailable(type, true);
                    _view.SetIcon(type, config.Icon);
                    _view.SetCost(type, (int)config.BaseFaithCost);
                    _view.SetAffordable(type, _faithService.CanAfford(config.BaseFaithCost));
                }
                else
                {
                    // Heal/Lightning на шаге 4 (cli-workflow.md, "Шаг 4") — не в каталоге, значит недоступны.
                    _view.SetAvailable(type, false);
                }
            }

            _view.MiracleClicked += OnMiracleClicked;
            _miracleSelectedSubscription = _eventBus.Subscribe<MiracleSelectedEvent>(OnMiracleSelected);
            _faithChangedSubscription = _eventBus.Subscribe<FaithChangedEvent>(OnFaithChanged);
        }

        public void Dispose()
        {
            _view.MiracleClicked -= OnMiracleClicked;
            _miracleSelectedSubscription?.Dispose();
            _faithChangedSubscription?.Dispose();
        }

        private void OnMiracleClicked(MiracleType type) => _handModeService.SelectMiracle(type);

        private void OnMiracleSelected(MiracleSelectedEvent evt) => _view.SetSelected(evt.Type, evt.Selected);

        private void OnFaithChanged(FaithChangedEvent evt)
        {
            for (var i = 0; i < _availableMiracles.Count; i++)
            {
                var config = _availableMiracles[i];
                _view.SetAffordable(config.Type, evt.NewValue >= config.BaseFaithCost);
            }
        }
    }
}
