using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Localization
{
    /// <summary>
    /// Слой локализации (шаг 10 «Polish», расширен волной 2 — таблица строк): единственный источник
    /// текущей локали игрока и текстов статичных надписей UI. Потребители читают либо
    /// <see cref="CurrentLocale"/> напрямую (свой <c>LocalizedString</c> — туториал, каталог
    /// уровней), либо <see cref="GetText"/> по ключу <c>LocalizationKeys</c> (статичные канвасы).
    /// </summary>
    public interface ILocalizationService
    {
        GameLocale CurrentLocale { get; }

        /// <summary>
        /// Текст по ключу (<c>BlackWhiteLike.Models.LocalizationKeys</c>) для <see cref="CurrentLocale"/>.
        /// Ключ, отсутствующий в таблице, — возвращается сам ключ (не пустая строка — не должен
        /// молча ронять UI-разметку) с предупреждением в лог (один раз за ключ).
        /// </summary>
        string GetText(string key);
    }
}
