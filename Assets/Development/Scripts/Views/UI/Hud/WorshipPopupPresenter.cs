using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Factories.Ui;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>MVP-Presenter попапов Worship (game-design §10, §13, шаг 5). Pure C#.</summary>
    public sealed class WorshipPopupPresenter : IStartable, IDisposable
    {
        private readonly IWorshipPopupFactory _factory;
        private readonly IEventBus _eventBus;

        private IDisposable _worshipChangedSubscription;

        public WorshipPopupPresenter(IWorshipPopupFactory factory, IEventBus eventBus)
        {
            _factory = factory;
            _eventBus = eventBus;
        }

        public void Start()
        {
            _worshipChangedSubscription = _eventBus.Subscribe<WorshipChangedEvent>(OnWorshipChanged);
        }

        public void Dispose()
        {
            _worshipChangedSubscription?.Dispose();
        }

        private void OnWorshipChanged(WorshipChangedEvent evt)
        {
            if (evt.NewValue <= evt.OldValue) return; // затухание/уменьшение — попап не показываем
            _factory.Show(evt.NewValue - evt.OldValue, evt.Source);
        }
    }
}
