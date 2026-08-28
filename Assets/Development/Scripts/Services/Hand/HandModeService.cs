using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Hand
{
    /// <summary>Реализация <see cref="IHandModeService"/>. Pure C#, без обращений к UnityEngine.</summary>
    public sealed class HandModeService : IHandModeService
    {
        private readonly IEventBus _eventBus;

        private HandMode _mode = HandMode.Grab;
        private MiracleType _selectedMiracle;
        private bool _hasSelection;

        public HandModeService(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public HandMode Mode => _mode;

        public bool TryGetSelectedMiracle(out MiracleType type)
        {
            type = _selectedMiracle;
            return _hasSelection;
        }

        public void SelectMiracle(MiracleType type)
        {
            if (_hasSelection && _selectedMiracle == type)
            {
                // Повторный тап по уже выбранной иконке — отмена (game-design §3).
                ClearSelection();
                return;
            }

            if (_hasSelection)
            {
                // Переключение на другое чудо — сначала гасим старую подсветку, потом зажигаем новую.
                _eventBus.Publish(new MiracleSelectedEvent(_selectedMiracle, false));
            }

            _selectedMiracle = type;
            _hasSelection = true;
            _mode = HandMode.Miracle;

            _eventBus.Publish(new MiracleSelectedEvent(type, true));
        }

        public void ClearSelection()
        {
            if (!_hasSelection) return; // уже в Grab — не публикуем событие впустую

            var previous = _selectedMiracle;
            _hasSelection = false;
            _mode = HandMode.Grab;

            _eventBus.Publish(new MiracleSelectedEvent(previous, false));
        }
    }
}
