using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Meta;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="LevelUnlockRules"/> (game-design §7, шаг 7) — чистая функция, без
    /// Unity-сцены и без IProfileStorage.
    /// </summary>
    public class LevelUnlockRulesTests
    {
        private static LevelConfig CreateLevel(LevelId id)
        {
            var level = ScriptableObject.CreateInstance<LevelConfig>();
            typeof(LevelConfig).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(level, id);
            return level;
        }

        private static LevelCatalogEntry CreateEntry(LevelConfig level)
        {
            var entry = new LevelCatalogEntry();
            typeof(LevelCatalogEntry).GetField("_level", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(entry, level);
            return entry;
        }

        private static LevelCatalogEntry[] CreateEntries(params LevelId[] ids)
        {
            var entries = new LevelCatalogEntry[ids.Length];
            for (var i = 0; i < ids.Length; i++) entries[i] = CreateEntry(CreateLevel(ids[i]));
            return entries;
        }

        [Test]
        public void IsUnlocked_FirstEntry_AlwaysTrue()
        {
            var entries = CreateEntries(LevelId.Level01, LevelId.Level02);

            Assert.IsTrue(LevelUnlockRules.IsUnlocked(entries, new HashSet<LevelId>(), LevelId.Level01));
        }

        [Test]
        public void IsUnlocked_SecondEntry_PreviousNotCompleted_ReturnsFalse()
        {
            var entries = CreateEntries(LevelId.Level01, LevelId.Level02);

            Assert.IsFalse(LevelUnlockRules.IsUnlocked(entries, new HashSet<LevelId>(), LevelId.Level02));
        }

        [Test]
        public void IsUnlocked_SecondEntry_PreviousCompleted_ReturnsTrue()
        {
            var entries = CreateEntries(LevelId.Level01, LevelId.Level02);
            var completed = new HashSet<LevelId> { LevelId.Level01 };

            Assert.IsTrue(LevelUnlockRules.IsUnlocked(entries, completed, LevelId.Level02));
        }

        [Test]
        public void IsUnlocked_ThirdEntry_OnlyFirstCompleted_ReturnsFalse()
        {
            var entries = CreateEntries(LevelId.Level01, LevelId.Level02, LevelId.Level03);
            var completed = new HashSet<LevelId> { LevelId.Level01 };

            Assert.IsFalse(LevelUnlockRules.IsUnlocked(entries, completed, LevelId.Level03));
        }

        [Test]
        public void IsUnlocked_ThirdEntry_FirstAndSecondCompleted_ReturnsTrue()
        {
            var entries = CreateEntries(LevelId.Level01, LevelId.Level02, LevelId.Level03);
            var completed = new HashSet<LevelId> { LevelId.Level01, LevelId.Level02 };

            Assert.IsTrue(LevelUnlockRules.IsUnlocked(entries, completed, LevelId.Level03));
        }

        [Test]
        public void IsUnlocked_IdNotInCatalog_ReturnsFalse()
        {
            var entries = CreateEntries(LevelId.Level01);

            Assert.IsFalse(LevelUnlockRules.IsUnlocked(entries, new HashSet<LevelId>(), LevelId.Level05));
        }

        [Test]
        public void IsUnlocked_EmptyCatalog_ReturnsFalse()
        {
            Assert.IsFalse(LevelUnlockRules.IsUnlocked(new LevelCatalogEntry[0], new HashSet<LevelId>(), LevelId.Level01));
        }

        [Test]
        public void IsUnlocked_NullCatalog_ReturnsFalse()
        {
            Assert.IsFalse(LevelUnlockRules.IsUnlocked(null, new HashSet<LevelId>(), LevelId.Level01));
        }
    }
}
