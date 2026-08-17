namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Key-value абстракция над сохранением профиля (game-design §8, §15).
    /// Реализация по умолчанию — <see cref="PlayerPrefsProfileStorage"/>.
    /// Cloud save (game-design §14.2) — новая реализация + смена регистрации в GameLifetimeScope,
    /// без правок кода, который зависит от этого интерфейса.
    /// </summary>
    public interface IProfileStorage
    {
        bool HasKey(string key);
        int GetInt(string key, int def = 0);
        void SetInt(string key, int value);
        float GetFloat(string key, float def = 0f);
        void SetFloat(string key, float value);
        string GetString(string key, string def = "");
        void SetString(string key, string value);

        /// <summary>Удалить одно значение по ключу. Идемпотентно — отсутствующий ключ не ошибка.</summary>
        void DeleteKey(string key);

        /// <summary>Сбросить весь профиль целиком (нужно QA). Необратимо.</summary>
        void DeleteAll();

        /// <summary>Принудительно сбросить накопленные изменения на диск/в облако.</summary>
        void Flush();
    }
}
