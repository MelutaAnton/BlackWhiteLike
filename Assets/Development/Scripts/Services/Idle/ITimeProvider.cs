using System;

namespace BlackWhiteLike.Services.Idle
{
    /// <summary>
    /// Абстракция текущего времени. Делает idle-расчёты тестируемыми (game-design §15)
    /// и открывает путь к защите от подкрутки часов через серверное время (замена реализации).
    /// </summary>
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }
}
