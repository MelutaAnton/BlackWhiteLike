using BlackWhiteLike.Data.Configs.Levels;

namespace BlackWhiteLike.Services.Level
{
    /// <summary>
    /// Реализация <see cref="ILevelSelectionService"/> (game-design §7, шаг 7). Pure C#, без
    /// сохранения на диск — выбор актуален только для текущей игровой сессии (что игрок выбрал в
    /// LevelSelect только что), а не между запусками игры.
    /// </summary>
    public sealed class LevelSelectionService : ILevelSelectionService
    {
        private LevelConfig _selected;
        private bool _returnRequested;

        public void Select(LevelConfig level) => _selected = level;

        public bool TryGetSelected(out LevelConfig level)
        {
            level = _selected;
            return level != null;
        }

        public void RequestReturnToLevelSelect() => _returnRequested = true;

        public bool ConsumeReturnToLevelSelect()
        {
            if (!_returnRequested) return false;

            _returnRequested = false;
            return true;
        }
    }
}
