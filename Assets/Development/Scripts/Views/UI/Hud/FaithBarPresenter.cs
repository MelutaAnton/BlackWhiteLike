using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Services.Faith;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>MVP-Presenter бара Faith (game-design §4, §10). Pure C#.</summary>
    public sealed class FaithBarPresenter : IStartable, IDisposable
    {
        private readonly IFaithBarView _view;
        private readonly IFaithService _faithService;
        private readonly IEventBus _eventBus;

        private IDisposable _faithChangedSubscription;

        public FaithBarPresenter(IFaithBarView view, IFaithService faithService, IEventBus eventBus)
        {
            _view = view;
            _faithService = faithService;
            _eventBus = eventBus;
        }

        public void Start()
        {
            _view.SetFaith(_faithService.Current, _faithService.Capacity);
            _faithChangedSubscription = _eventBus.Subscribe<FaithChangedEvent>(OnFaithChanged);
        }

        public void Dispose()
        {
            _faithChangedSubscription?.Dispose();
        }

        private void OnFaithChanged(FaithChangedEvent evt) => _view.SetFaith(evt.NewValue, evt.Capacity);
    }
}
