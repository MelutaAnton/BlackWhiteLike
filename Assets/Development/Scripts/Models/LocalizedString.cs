using UnityEngine;

namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Локализованная строка — минимальный слой локализации шага 10 «Polish» (полная таблица
    /// строк проекта планируется отдельной волной). Хранит пару ru/en прямо в инспекторе, без
    /// внешних таблиц/ключей — этого достаточно для текста туториала.
    /// </summary>
    [System.Serializable]
    public struct LocalizedString
    {
        [SerializeField] private string _ru;
        [SerializeField] private string _en;

        public LocalizedString(string ru, string en)
        {
            _ru = ru;
            _en = en;
        }

        /// <summary>
        /// Текст для локали <paramref name="locale"/>. <see cref="GameLocale.En"/> с пустой
        /// <c>_en</c> — фоллбэк на <c>_ru</c> (неполный перевод не должен показывать пустую строку).
        /// </summary>
        public string Resolve(GameLocale locale)
        {
            if (locale == GameLocale.En && !string.IsNullOrEmpty(_en)) return _en;
            return _ru;
        }
    }
}
