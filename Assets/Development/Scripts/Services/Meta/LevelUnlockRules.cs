using System.Collections.Generic;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Правило линейного анлока кампании (game-design §7, шаг 7) — тот же приём, что
    /// <c>VillagerPoseRules</c> (Services/Village): чистая статическая функция без состояния и без
    /// UnityEngine-API (кроме типов данных, которые сюда не попадают вовсе), тестируется без
    /// Unity-сцены и без <see cref="IProfileStorage"/>.
    /// </summary>
    public static class LevelUnlockRules
    {
        /// <summary>
        /// Первая запись каталога открыта всегда; каждая следующая — если <c>LevelConfig</c>
        /// предыдущей записи пройден. Уровень, отсутствующий в каталоге (или каталог пуст/не
        /// прошит), считается закрытым.
        /// </summary>
        public static bool IsUnlocked(LevelCatalogEntry[] entries, ISet<LevelId> completed, LevelId id)
        {
            if (entries == null) return false;

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Level == null || entry.Level.Id != id) continue;

                if (i == 0) return true;

                var previousEntry = entries[i - 1];
                var previousLevel = previousEntry != null ? previousEntry.Level : null;
                return previousLevel != null && completed.Contains(previousLevel.Id);
            }

            return false;
        }
    }
}
