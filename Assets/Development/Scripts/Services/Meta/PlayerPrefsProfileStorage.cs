using UnityEngine;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Реализация <see cref="IProfileStorage"/> поверх <see cref="PlayerPrefs"/> (game-design §8, §15).
    /// Внутри уровня сохранений нет — профиль пишется только в мета-слое.
    /// </summary>
    public sealed class PlayerPrefsProfileStorage : IProfileStorage
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public int GetInt(string key, int def = 0) => PlayerPrefs.GetInt(key, def);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public float GetFloat(string key, float def = 0f) => PlayerPrefs.GetFloat(key, def);

        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);

        public string GetString(string key, string def = "") => PlayerPrefs.GetString(key, def);

        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);

        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);

        public void DeleteAll() => PlayerPrefs.DeleteAll();

        public void Flush() => PlayerPrefs.Save();
    }
}
