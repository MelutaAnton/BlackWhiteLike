using System;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Levels
{
    /// <summary>
    /// Одна запись каталога уровней кампании — конфиг уровня + превью для карточки LevelSelect
    /// (game-design §7, шаг 7). Плейн-класс (не ScriptableObject) — сериализуется как элемент
    /// массива <see cref="LevelCatalogConfig"/>.
    /// </summary>
    [Serializable]
    public sealed class LevelCatalogEntry
    {
        [SerializeField] private LevelConfig _level;
        [SerializeField] private Sprite _preview;

        public LevelConfig Level => _level;
        public Sprite Preview => _preview;
    }

    /// <summary>
    /// Каталог уровней кампании (game-design §7, шаг 7) — живёт в root-scope
    /// (<c>RootLifetimeScope</c>), переживает загрузку сцен. Порядок массива
    /// <see cref="_entries"/> — единственный источник линейного анлока
    /// (<c>LevelUnlockRules</c> читает позицию в этом массиве, а не значение <c>LevelId</c>,
    /// поэтому уровни можно переставлять/добавлять без правки enum).
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCatalogConfig", menuName = "BlackWhiteLike/Level Catalog Config")]
    public sealed class LevelCatalogConfig : ScriptableObject
    {
        [SerializeField] private LevelCatalogEntry[] _entries = new LevelCatalogEntry[0];

        public LevelCatalogEntry[] Entries => _entries;
    }
}
