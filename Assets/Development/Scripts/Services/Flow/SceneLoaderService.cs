using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackWhiteLike.Services.Flow
{
    /// <summary>
    /// Реализация <see cref="ISceneLoaderService"/> (game-design §7, шаг 7) — единственное место
    /// проекта, вызывающее <c>SceneManager.LoadScene</c>. Живёт в root-scope, переживает загрузку
    /// сцен (в отличие от <c>UnityTimeScaleProvider</c>, который сценовый).
    /// </summary>
    /// <remarks>
    /// Чек-лист CLAUDE.md п.10 — <c>Time.timeScale</c> глобальный static, переживающий выгрузку
    /// сцены: <c>UnityTimeScaleProvider</c> зарегистрирован только в сценовом
    /// <c>GameLifetimeScope</c> и восстанавливает 1 в своём <c>Dispose</c> при закрытии scope, но
    /// это защищает только штатный путь (PopState/выход в обычном потоке). Если сцена выгружается
    /// прямо во время паузы БЕЗ явного PopState (например, Continue на LevelComplete напрямую в
    /// MainMenu), страховка нужна и на этом уровне — root не имеет своего <c>ITimeScaleProvider</c>
    /// (эта абстракция сценовая), поэтому здесь единственное осознанное исключение из правила
    /// "TimeScale только через ITimeScaleProvider": прямая запись в <see cref="Time.timeScale"/>
    /// перед загрузкой любой сцены.
    /// </remarks>
    public sealed class SceneLoaderService : ISceneLoaderService
    {
        public void Load(string sceneName)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
