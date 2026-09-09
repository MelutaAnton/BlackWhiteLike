using System;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Upgrades
{
    /// <summary>Одна ступень апгрейда внутри категории (game-design §8, шаг 8).</summary>
    [Serializable]
    public sealed class UpgradeLevelEntry
    {
        [SerializeField] private int _level;

        [Tooltip("Семантика зафиксирована в EffectiveStatsService (шаг 8): процент прибавки для " +
                 "множителей (10 = +10%), абсолютное значение для FaithCapacityBonus.")]
        [SerializeField] private float _effectValue;

        [SerializeField] private int _price;

        public int Level => _level;
        public float EffectValue => _effectValue;
        public int Price => _price;
    }

    /// <summary>Все ступени одной категории апгрейда (game-design §8, шаг 8).</summary>
    [Serializable]
    public sealed class UpgradeCategoryEntry
    {
        [SerializeField] private UpgradeCategory _category;
        [SerializeField] private UpgradeLevelEntry[] _levels = new UpgradeLevelEntry[0];

        public UpgradeCategory Category => _category;
        public UpgradeLevelEntry[] Levels => _levels;
    }

    /// <summary>
    /// Каталог мета-апгрейдов Temple (game-design §8, шаг 8) — 3 категории (<see cref="UpgradeCategory"/>)
    /// × N уровней каждая. Единственный источник цены/эффекта ступени — см.
    /// <see cref="TryGetLevelEntry"/>/<see cref="MaxLevel"/>. Отсутствие записи — легальное состояние
    /// раннего баланса (тот же принцип, что <c>WorshipConfig</c>), не ошибка конфигурации.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeCatalogConfig", menuName = "BlackWhiteLike/Upgrades/Upgrade Catalog Config")]
    public sealed class UpgradeCatalogConfig : ScriptableObject
    {
        [SerializeField] private UpgradeCategoryEntry[] _categories = new UpgradeCategoryEntry[0];

        public UpgradeCategoryEntry[] Categories => _categories;

        /// <summary>Запись конкретного уровня категории. false — категория/уровень отсутствуют в каталоге.</summary>
        public bool TryGetLevelEntry(UpgradeCategory category, int level, out UpgradeLevelEntry entry)
        {
            if (_categories != null)
            {
                foreach (var categoryEntry in _categories)
                {
                    if (categoryEntry == null || categoryEntry.Category != category) continue;

                    if (categoryEntry.Levels != null)
                    {
                        foreach (var levelEntry in categoryEntry.Levels)
                        {
                            if (levelEntry != null && levelEntry.Level == level)
                            {
                                entry = levelEntry;
                                return true;
                            }
                        }
                    }

                    break; // категория найдена, но нужного уровня в ней нет — дальше искать нечего
                }
            }

            entry = null;
            return false;
        }

        /// <summary>Максимальный объявленный уровень категории. 0 — категория отсутствует/пуста.</summary>
        public int MaxLevel(UpgradeCategory category)
        {
            var max = 0;

            if (_categories != null)
            {
                foreach (var categoryEntry in _categories)
                {
                    if (categoryEntry == null || categoryEntry.Category != category) continue;

                    if (categoryEntry.Levels != null)
                    {
                        foreach (var levelEntry in categoryEntry.Levels)
                        {
                            if (levelEntry != null && levelEntry.Level > max) max = levelEntry.Level;
                        }
                    }

                    break;
                }
            }

            return max;
        }
    }
}
