using System;
using System.Collections.Generic;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Localization
{
    /// <summary>
    /// Одна запись таблицы локализации: ключ (<see cref="LocalizationKeys"/>) → пара {ru, en}
    /// (шаг 10 «Polish», волна 2). Плейн-класс — сериализуется как элемент массива
    /// <see cref="LocalizationConfig"/>.
    /// </summary>
    [Serializable]
    public sealed class LocalizationEntry
    {
        [SerializeField] private string _key;
        [SerializeField] private LocalizedString _text;

        public string Key => _key;
        public LocalizedString Text => _text;
    }

    /// <summary>
    /// Таблица локализации статичных надписей UI (шаг 10 «Polish», волна 2) — единственный источник
    /// текстов по ключу <see cref="LocalizationKeys"/>, читается <c>ILocalizationService.GetText</c>.
    /// Заполняется Editor/UI/LocalizationConfigSetupEditor.cs (единственный источник самих пар
    /// ключ → {ru, en}, владелец правит текст там, не в Inspector).
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationConfig", menuName = "BlackWhiteLike/Localization/Localization Config")]
    public sealed class LocalizationConfig : ScriptableObject
    {
        [SerializeField] private LocalizationEntry[] _entries = Array.Empty<LocalizationEntry>();

        // Словарь — лениво, при первом обращении после загрузки/правки ассета (тот же приём, что
        // AlignmentVisualConfig._sortedCache/EnsureSorted).
        private Dictionary<string, LocalizedString> _lookup;

        /// <summary>false — ключ не найден в таблице (вызывающая сторона решает фоллбэк).</summary>
        public bool TryGetText(string key, out LocalizedString text)
        {
            EnsureLookup();
            return _lookup.TryGetValue(key, out text);
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;

            _lookup = new Dictionary<string, LocalizedString>(_entries?.Length ?? 0);
            if (_entries == null) return;

            foreach (var entry in _entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key)) continue;

                if (_lookup.ContainsKey(entry.Key))
                {
                    Debug.LogWarning($"[LocalizationConfig] Повторяющийся ключ \"{entry.Key}\" — используется первое вхождение.");
                    continue;
                }

                _lookup[entry.Key] = entry.Text;
            }
        }

        private void OnValidate()
        {
            // Правка в инспекторе — кэш инвалидируется, пересчитается лениво при следующем
            // TryGetText (Editor-only колбэк, тот же приём, что AlignmentVisualConfig.OnValidate).
            _lookup = null;
        }
    }
}
