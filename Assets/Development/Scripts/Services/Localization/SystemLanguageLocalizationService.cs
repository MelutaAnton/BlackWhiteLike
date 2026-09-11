using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Localization
{
    /// <summary>
    /// Реализация <see cref="ILocalizationService"/> поверх системного языка устройства. Локаль
    /// вычисляется РОВНО ОДИН РАЗ в конструкторе — тот же прецедент обращения к статике
    /// UnityEngine из pure C#-сервиса, что <c>SystemTimeProvider</c> (game-design §15):
    /// единственное место, читающее <see cref="UnityEngine.Application.systemLanguage"/>.
    /// </summary>
    public sealed class SystemLanguageLocalizationService : ILocalizationService
    {
        private readonly GameLocale _locale;

        public SystemLanguageLocalizationService()
        {
            _locale = UnityEngine.Application.systemLanguage == UnityEngine.SystemLanguage.Russian
                ? GameLocale.Ru
                : GameLocale.En;
        }

        public GameLocale CurrentLocale => _locale;
    }
}
