using BlackWhiteLike.Services.Meta;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Нейтральный фейк <see cref="IEffectiveStatsService"/> для тестов, не завязанных на мета-апгрейды
    /// (game-design §8, шаг 8) — множители 1, бонусы 0 по умолчанию (эквивалент отсутствия root/
    /// апгрейдов). Свойства settable — тест, проверяющий влияние конкретного апгрейда, переопределяет
    /// нужное значение точечно.
    /// </summary>
    internal sealed class FakeEffectiveStatsService : IEffectiveStatsService
    {
        public float HandForceMultiplier { get; set; } = 1f;
        public float HandMassMultiplier { get; set; } = 1f;
        public float FaithCapacityBonus { get; set; } = 0f;
        public float FaithRegenMultiplier { get; set; } = 1f;
        public float MiraclePowerMultiplier { get; set; } = 1f;
        public float MiracleRadiusMultiplier { get; set; } = 1f;
    }
}
