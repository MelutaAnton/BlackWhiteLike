namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Ключи таблицы локализации статичных надписей UI (шаг 10 «Polish», волна 2) — единственный
    /// источник строковых значений (CLAUDE.md, «дублирование строковых ключей»). Сами тексты (ru/en)
    /// живут в <c>LocalizationConfig.asset</c> — единственный источник пар ключ → {ru, en}
    /// (Editor/UI/LocalizationConfigSetupEditor.cs).
    /// </summary>
    public static class LocalizationKeys
    {
        // MainMenuCanvas / LevelSelectCanvas / WelcomeBackOverlay (MainMenuCanvasSetupEditor)
        public const string Play = "play";
        public const string Temple = "temple";
        public const string SelectLevel = "select_level";
        public const string Back = "back";
        public const string WelcomeBack = "welcome_back";
        public const string Collect = "collect";
        public const string Close = "close";

        // TempleCanvas (TempleCanvasSetupEditor)
        public const string HandPower = "hand_power";
        public const string FaithCapacity = "faith_capacity";
        public const string MiraclePower = "miracle_power";
        public const string Buy = "buy";
        public const string CreatureComingSoon = "creature_coming_soon";
        public const string NextLevel = "next_level";

        // LevelHudCanvas (LevelHudSetupEditor)
        public const string Evil = "evil";
        public const string Good = "good";
        public const string LevelComplete = "level_complete";
        public const string Continue = "continue";
        public const string FaithRanOut = "faith_ran_out";
        public const string WatchAd = "watch_ad";
        public const string Later = "later";
        public const string Pause = "pause";
        public const string Resume = "resume";

        // Рантайм-текст презентера (не статичный лейбл канваса — LocalizedTextView на него не
        // вешается, текст выставляется программно через ILocalizationService.GetText), см.
        // LevelStalledPresenter.
        public const string LevelStalledQuestion = "level_stalled_question";

        // Названия апгрейдов храма (playtest волны 2: в строках светились имена enum-значений
        // UpgradeCategory). Рантайм-тексты: заголовок строки пишет TemplePresenter.ConfigureRow
        // через GetText, маппинг категория → ключ — там же.
        public const string UpgradeHandPower = "upgrade_hand_power";
        public const string UpgradeFaithCapacity = "upgrade_faith_capacity";
        public const string UpgradeMiraclePower = "upgrade_miracle_power";
    }
}
