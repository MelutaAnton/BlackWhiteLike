using System;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Мета-прогресс кампании поверх профиля игрока (game-design §8, §15, шаг 8) — единая точка
    /// доступа к coins/апгрейдам/alignment для сценовых потребителей (<c>EconomyService</c>,
    /// <c>EffectiveStatsService</c>, Temple UI). Реализация (<c>MetaProgressService</c>) реализует
    /// ТАКЖЕ <see cref="ILevelProgressService"/> на том же инстансе — прогресс кампании (шаг 7)
    /// слился в общий мета-профиль, отдельного <c>LevelProgressService</c> больше нет.
    /// </summary>
    public interface IMetaProgressService
    {
        int Coins { get; }

        /// <summary>Списывает coins, только если хватает (всё или ничего). amount ≤ 0 — no-op, true.</summary>
        bool TrySpendCoins(int amount);

        /// <summary>Начисляет coins. amount ≤ 0 — no-op.</summary>
        void AddCoins(int amount);

        int GetUpgradeLevel(UpgradeCategory category);

        /// <summary>Повышает уровень апгрейда категории на 1 (вызывается после успешного списания цены — см. EconomyService).</summary>
        void IncrementUpgradeLevel(UpgradeCategory category);

        float AlignmentScore { get; }

        /// <summary>Персист итогового AlignmentScore уровня (game-design §8) — вызывается LevelCompletionRecorder.</summary>
        void SetAlignmentScore(float score);

        DateTime LastSeenUtc { get; }

        /// <summary>Штампует момент выхода из игры/входа в мета-сцену (game-design §15) — вызывается IdleIncomeService.</summary>
        void StampLastSeen(DateTime value);
    }
}
