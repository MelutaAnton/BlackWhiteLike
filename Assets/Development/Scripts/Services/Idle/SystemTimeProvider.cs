using System;

namespace BlackWhiteLike.Services.Idle
{
    /// <summary>
    /// Единственное место в проекте, где допустим <see cref="DateTime.UtcNow"/> (game-design §15).
    /// Весь остальной код получает время только через <see cref="ITimeProvider"/>.
    /// </summary>
    public sealed class SystemTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
