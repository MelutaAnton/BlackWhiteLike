namespace BlackWhiteLike.Services.Flow
{
    /// <summary>
    /// Загрузка сцен кампании (game-design §7, шаг 7) — единственная абстракция над
    /// <c>SceneManager.LoadScene</c>. Живёт в root-scope (<c>RootLifetimeScope</c>).
    /// </summary>
    public interface ISceneLoaderService
    {
        /// <summary>Имя сцены — только из <see cref="Models.SceneNames"/>, не строковый литерал на вызывающей стороне.</summary>
        void Load(string sceneName);
    }
}
