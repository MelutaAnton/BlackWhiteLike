namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Ключи сохранённого профиля (game-design §8, §15) — единственный источник строковых ключей
    /// для <c>IProfileStorage</c>. Первый потребитель — <c>LevelProgressService</c> (шаг 7,
    /// прогресс кампании); первое реальное сохранение в проекте вообще (все предыдущие шаги
    /// работали без персиста между сессиями).
    /// </summary>
    public static class ProfileKeys
    {
        public const string CompletedLevels = "completed_levels";
    }
}
