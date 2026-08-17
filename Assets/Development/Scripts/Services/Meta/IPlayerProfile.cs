using System;
using System.Collections.Generic;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Архитектурный предохранитель (game-design §15): интерфейс описывает только данные,
    /// нужные вертикальному срезу (coins, пройденные уровни, апгрейды, alignment, lastSeenUtc).
    /// Реализация — <c>MetaProgressService</c> — появится на шаге 8. Сейчас интерфейс существует,
    /// чтобы будущие потребители (Temple UI, idle-доход) сразу проектировались против него,
    /// а не против конкретного класса.
    /// </summary>
    public interface IPlayerProfile
    {
        int Coins { get; }
        IReadOnlyCollection<LevelId> CompletedLevels { get; }
        int GetUpgradeLevel(UpgradeCategory category);
        float AlignmentScore { get; }
        DateTime LastSeenUtc { get; }
    }
}
