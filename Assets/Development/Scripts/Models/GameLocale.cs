namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Поддерживаемые локали (шаг 10 «Polish» — минимальный слой локализации). Единственный
    /// источник значений — потребители (<c>LocalizedString</c>/<c>ILocalizationService</c>)
    /// ссылаются только на этот enum.
    /// </summary>
    public enum GameLocale
    {
        Ru,
        En
    }
}
