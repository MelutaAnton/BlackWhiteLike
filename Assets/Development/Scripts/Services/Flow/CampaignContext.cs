namespace BlackWhiteLike.Services.Flow
{
    /// <summary>
    /// Явный флаг "текущая сцена запущена под RootLifetimeScope, т.е. в реальном кампания-флоу"
    /// (game-design §7, §9, шаг 8). Вычисляется один раз в <c>GameLifetimeScope.Configure</c>
    /// (<c>root != null</c>) и передаётся через DI — тот же приём, что <see cref="BootstrapSettings"/>.
    /// </summary>
    /// <remarks>
    /// Закрывает долг шага 7: раньше <c>LevelCompletePresenter</c> выводил "доступен ли реальный
    /// кампания-флоу" косвенно, через <c>ILevelSelectionService.TryGetSelected(out _)</c> — сигнал,
    /// по смыслу означающий "был ли выбран уровень", использованный как proxy для "есть ли root",
    /// без прямой (и оправданно нежелательной для pure C#-presenter'а) зависимости на
    /// <c>Installers.RootLifetimeScope</c>. Этот маркер даёт то же самое явно и по имени.
    /// </remarks>
    public sealed class CampaignContext
    {
        public bool HasRoot { get; }

        public CampaignContext(bool hasRoot)
        {
            HasRoot = hasRoot;
        }
    }
}
