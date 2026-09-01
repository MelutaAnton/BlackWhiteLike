using BlackWhiteLike.Data.Configs.Levels;

namespace BlackWhiteLike.Services.Level
{
    /// <summary>
    /// Хранит выбранный игроком уровень между сценами MainMenu(LevelSelect) → Village_Base
    /// (game-design §7, шаг 7). Живёт в root-scope (<c>RootLifetimeScope</c>) — переживает загрузку
    /// сцены; в тестовых сценах без root регистрируется как обычный локальный singleton (см.
    /// remarks <c>GameLifetimeScope</c>).
    /// </summary>
    public interface ILevelSelectionService
    {
        /// <summary>Игрок выбрал уровень на экране LevelSelect.</summary>
        void Select(LevelConfig level);

        /// <summary>Уровень, выбранный последним вызовом <see cref="Select"/>. false — если выбора ещё не было.</summary>
        bool TryGetSelected(out LevelConfig level);

        /// <summary>
        /// Одноразовый флаг "после уровня открыть сразу панель выбора" (Continue на LevelComplete,
        /// game-design §7) — выставляется перед загрузкой сцены MainMenu, считывается один раз
        /// презентером MainMenu на старте.
        /// </summary>
        void RequestReturnToLevelSelect();

        /// <summary>Считать и сбросить флаг из <see cref="RequestReturnToLevelSelect"/>. true — ровно один раз после запроса.</summary>
        bool ConsumeReturnToLevelSelect();
    }
}
