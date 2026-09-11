namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Ключи сохранённого профиля (game-design §8, §15) — единственный источник строковых ключей
    /// для <c>IProfileStorage</c>. Первый потребитель был <c>LevelProgressService</c> (шаг 7);
    /// шаг 8 расширяет профиль до полного мета-состояния (coins, апгрейды, alignment, lastSeenUtc) —
    /// см. <c>PlayerProfile</c>.
    /// </summary>
    public static class ProfileKeys
    {
        public const string CompletedLevels = "completed_levels";
        public const string Coins = "coins";
        public const string AlignmentScore = "alignment_score";
        public const string LastSeenUtc = "last_seen_utc";

        /// <summary>Шаг 10 «Polish»: флаг "туториал уровня 1 пройден" (int 0/1) — см. <c>PlayerProfile.TutorialCompleted</c>.</summary>
        public const string TutorialCompleted = "tutorial_completed";

        /// <summary>
        /// Префикс ключа уровня апгрейда категории (game-design §8, шаг 8) — полный ключ строится
        /// конкатенацией с именем значения <see cref="UpgradeCategory"/> (например
        /// "upgrade_level_HandPower"). Единственный источник — см. <c>PlayerProfile.GetUpgradeLevel</c>/
        /// <c>SetUpgradeLevel</c>.
        /// </summary>
        public const string UpgradeLevelPrefix = "upgrade_level_";
    }
}
