using System;
using UnityEngine;

namespace BlackWhiteLike.Services.Flow
{
    /// <summary>Единственное место проекта, читающее/пишущее UnityEngine.Time.timeScale (game-design §15, шаг 5).</summary>
    /// <remarks>
    /// Шаг 5 (аудит после первого Play Mode, чек-лист п.10): <c>Time.timeScale</c> — глобальный
    /// static-стейт, переживающий загрузку сцены (per-scene VContainer scope на него не
    /// распространяется). Если сцена выгружается прямо во время паузы (LevelComplete/выход в меню
    /// без явного PopState), <c>PausedState.Exit</c> не вызовется и timeScale навсегда останется 0
    /// для следующей сцены. Страховка — <see cref="IDisposable"/>: контейнер вызывает
    /// <see cref="Dispose"/> у любого singleton при закрытии scope, здесь он принудительно
    /// восстанавливает 1.
    /// </remarks>
    public sealed class UnityTimeScaleProvider : ITimeScaleProvider, IDisposable
    {
        public float TimeScale
        {
            get => Time.timeScale;
            set => Time.timeScale = value;
        }

        public void Dispose()
        {
            Time.timeScale = 1f;
        }
    }
}
