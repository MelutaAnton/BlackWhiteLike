namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Имена сцен проекта (game-design §7, шаг 7) — единственный источник, чтобы
    /// <c>ISceneLoaderService</c>/<c>BootstrapFlowEntryPoint</c> и все остальные потребители не
    /// плодили строковые литералы по сервисам.
    /// </summary>
    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string MainMenu = "MainMenu";
        public const string VillageBase = "Village_Base";
    }
}
