using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Localization
{
    /// <summary>
    /// Минимальный слой локализации (шаг 10 «Polish»): единственный источник текущей локали
    /// игрока. Потребители (<c>TutorialPresenter</c> и будущие презентеры UI) читают
    /// <see cref="CurrentLocale"/> и резолвят через него <c>LocalizedString.Resolve</c>.
    /// </summary>
    public interface ILocalizationService
    {
        GameLocale CurrentLocale { get; }
    }
}
