using System;
using System.Collections.Generic;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Архитектурный предохранитель (game-design §15): единственный источник персистентных данных
    /// игрока (coins, пройденные уровни, апгрейды, alignment, lastSeenUtc). Реализация —
    /// <see cref="PlayerProfile"/> — единственное место, читающее/пишущее <c>IProfileStorage</c>/
    /// <c>ProfileKeys</c> напрямую. Бизнес-правила (списание с проверкой баланса, идемпотентность
    /// MarkCompleted, first-clear-множитель и т.п.) живут в потребителях
    /// (<c>MetaProgressService</c>/<c>EconomyService</c>), не здесь — этот интерфейс только читает и
    /// пишет данные "точечно" (каждый мутатор персистит ровно свой ключ), без валидации.
    /// </summary>
    public interface IPlayerProfile
    {
        int Coins { get; }
        void AddCoins(int amount);
        void SpendCoins(int amount);

        // ISet, а не IReadOnlyCollection (ревью шага 8, мелочь): MetaProgressService.IsUnlocked
        // передаёт это множество в LevelUnlockRules.IsUnlocked(ISet<LevelId>, ...) напрямую — без
        // ISet здесь потребовался бы new HashSet<LevelId>(...) на КАЖДЫЙ вызов (LevelSelect дёргает
        // IsUnlocked на каждую карточку уровня). Мутация профиля через этот сеттер вовне не
        // предполагается — единственный мутатор коллекции остаётся MarkLevelCompleted.
        ISet<LevelId> CompletedLevels { get; }
        void MarkLevelCompleted(LevelId id);

        int GetUpgradeLevel(UpgradeCategory category);
        void SetUpgradeLevel(UpgradeCategory category, int level);

        float AlignmentScore { get; }
        void SetAlignmentScore(float score);

        /// <summary>default(DateTime) — профиль ещё ни разу не штамповался (первый запуск игры).</summary>
        DateTime LastSeenUtc { get; }
        void SetLastSeenUtc(DateTime value);

        /// <summary>Шаг 10 «Polish»: туториал уровня 1 пройден — <c>ITutorialService</c> не активируется повторно.</summary>
        bool TutorialCompleted { get; }
        void MarkTutorialCompleted();
    }
}
