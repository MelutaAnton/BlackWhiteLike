using System.Collections.Generic;
using BlackWhiteLike.Data.Configs.Localization;
using BlackWhiteLike.Models;
using UnityEngine;

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
        private readonly LocalizationConfig _config;

        // "Один раз на ключ" (контракт GetText) — не спамим предупреждением на каждый Tick/повторный вызов.
        private readonly HashSet<string> _warnedKeys = new HashSet<string>();

        public SystemLanguageLocalizationService(LocalizationConfig config)
        {
            _config = config;
            _locale = UnityEngine.Application.systemLanguage == UnityEngine.SystemLanguage.Russian
                ? GameLocale.Ru
                : GameLocale.En;
        }

        public GameLocale CurrentLocale => _locale;

        public string GetText(string key)
        {
            // Пустой/null ключ — не ошибка данных (например, необязательное поле LocalizedTextView.Key
            // у декоративного лейбла), молча возвращаем как есть, без записи в _warnedKeys и без лога.
            if (string.IsNullOrEmpty(key)) return key ?? string.Empty;

            if (_config != null && _config.TryGetText(key, out var text)) return text.Resolve(_locale);

            if (_warnedKeys.Add(key))
            {
                Debug.LogWarning($"[SystemLanguageLocalizationService] Ключ локализации \"{key}\" не найден в LocalizationConfig — используется сам ключ.");
            }

            return key;
        }
    }
}
