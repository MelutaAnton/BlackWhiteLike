using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Прогресс кампании (game-design §7-§8, шаг 7) — какие уровни пройдены, какие открыты.
    /// Живёт в root-scope (<c>RootLifetimeScope</c>), хранение — через <see cref="IProfileStorage"/>
    /// (переживает не только смену сцены, но и перезапуск игры).
    /// </summary>
    public interface ILevelProgressService
    {
        /// <summary>Отметить уровень пройденным (вызывается <c>LevelCompletionRecorder</c> по <c>LevelCompletedEvent</c>). Идемпотентно.</summary>
        void MarkCompleted(LevelId id);

        bool IsCompleted(LevelId id);

        /// <summary>
        /// Правило анлока — <see cref="LevelUnlockRules"/> (game-design §7): первый уровень каталога
        /// открыт всегда, следующий — если предыдущий пройден.
        /// </summary>
        bool IsUnlocked(LevelId id);
    }
}
