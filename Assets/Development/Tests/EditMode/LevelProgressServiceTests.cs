using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Meta;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="LevelProgressService"/> (game-design §7-§8, шаг 7).</summary>
    public class LevelProgressServiceTests
    {
        private sealed class FakeProfileStorage : IProfileStorage
        {
            private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();

            public bool HasKey(string key) => _strings.ContainsKey(key);
            public int GetInt(string key, int def = 0) => def;
            public void SetInt(string key, int value) { }
            public float GetFloat(string key, float def = 0f) => def;
            public void SetFloat(string key, float value) { }
            public string GetString(string key, string def = "") => _strings.TryGetValue(key, out var v) ? v : def;
            public void SetString(string key, string value) => _strings[key] = value;
            public void DeleteKey(string key) => _strings.Remove(key);
            public void DeleteAll() => _strings.Clear();
            public void Flush() { }
        }

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

        private static LevelCatalogConfig CreateCatalog(params LevelId[] ids)
        {
            var catalog = ScriptableObject.CreateInstance<LevelCatalogConfig>();
            var entries = new LevelCatalogEntry[ids.Length];
            for (var i = 0; i < ids.Length; i++) entries[i] = CreateEntry(CreateLevel(ids[i]));
            typeof(LevelCatalogConfig).GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(catalog, entries);
            return catalog;
        }

        // --- MarkCompleted / IsCompleted ---

        [Test]
        public void IsCompleted_NotMarked_ReturnsFalse()
        {
            var service = new LevelProgressService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            Assert.IsFalse(service.IsCompleted(LevelId.Level01));
        }

        [Test]
        public void MarkCompleted_ThenIsCompleted_ReturnsTrue()
        {
            var service = new LevelProgressService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            service.MarkCompleted(LevelId.Level01);

            Assert.IsTrue(service.IsCompleted(LevelId.Level01));
        }

        [Test]
        public void MarkCompleted_Persists_AcrossNewServiceInstance_SameStorage()
        {
            var storage = new FakeProfileStorage();
            var catalog = CreateCatalog(LevelId.Level01, LevelId.Level02);
            var service = new LevelProgressService(storage, catalog);
            service.MarkCompleted(LevelId.Level01);

            var reloaded = new LevelProgressService(storage, catalog);

            Assert.IsTrue(reloaded.IsCompleted(LevelId.Level01));
            Assert.IsFalse(reloaded.IsCompleted(LevelId.Level02));
        }

        [Test]
        public void MarkCompleted_TwiceSameLevel_StaysCompleted_DoesNotThrow()
        {
            var service = new LevelProgressService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            service.MarkCompleted(LevelId.Level01);
            Assert.DoesNotThrow(() => service.MarkCompleted(LevelId.Level01));

            Assert.IsTrue(service.IsCompleted(LevelId.Level01));
        }

        // --- IsUnlocked (делегирует LevelUnlockRules — интеграционная проверка через сервис) ---

        [Test]
        public void IsUnlocked_FirstLevel_AlwaysTrue()
        {
            var service = new LevelProgressService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01, LevelId.Level02));

            Assert.IsTrue(service.IsUnlocked(LevelId.Level01));
        }

        [Test]
        public void IsUnlocked_SecondLevel_BeforeCompletion_ReturnsFalse()
        {
            var service = new LevelProgressService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01, LevelId.Level02));

            Assert.IsFalse(service.IsUnlocked(LevelId.Level02));
        }

        [Test]
        public void IsUnlocked_SecondLevel_AfterFirstCompleted_ReturnsTrue()
        {
            var service = new LevelProgressService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01, LevelId.Level02));

            service.MarkCompleted(LevelId.Level01);

            Assert.IsTrue(service.IsUnlocked(LevelId.Level02));
        }

        [Test]
        public void IsUnlocked_FarLevel_NotCompletedChain_ReturnsFalse()
        {
            var service = new LevelProgressService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01, LevelId.Level02, LevelId.Level03));

            service.MarkCompleted(LevelId.Level01);

            Assert.IsFalse(service.IsUnlocked(LevelId.Level03));
        }

        // --- LoadCompleted из мусора (ревью шага 7, ВАЖНО 3) ---

        [Test]
        public void LoadCompleted_EmptyString_NothingCompleted()
        {
            var storage = new FakeProfileStorage();
            storage.SetString(ProfileKeys.CompletedLevels, string.Empty);

            var service = new LevelProgressService(storage, CreateCatalog(LevelId.Level01));

            Assert.IsFalse(service.IsCompleted(LevelId.Level01));
        }

        [Test]
        public void LoadCompleted_OnlySeparators_NothingCompleted_DoesNotThrow()
        {
            var storage = new FakeProfileStorage();
            storage.SetString(ProfileKeys.CompletedLevels, ";;;");

            LevelProgressService service = null;
            Assert.DoesNotThrow(() => service = new LevelProgressService(storage, CreateCatalog(LevelId.Level01, LevelId.Level02)));

            Assert.IsFalse(service.IsCompleted(LevelId.Level01));
            Assert.IsFalse(service.IsCompleted(LevelId.Level02));
        }

        [Test]
        public void LoadCompleted_MixOfValidAndGarbageTokens_OnlyValidRead()
        {
            var storage = new FakeProfileStorage();
            storage.SetString(ProfileKeys.CompletedLevels, "Level01;garbage;Level02");

            var service = new LevelProgressService(storage, CreateCatalog(LevelId.Level01, LevelId.Level02, LevelId.Level03));

            Assert.IsTrue(service.IsCompleted(LevelId.Level01));
            Assert.IsTrue(service.IsCompleted(LevelId.Level02));
            Assert.IsFalse(service.IsCompleted(LevelId.Level03));
        }

        [Test]
        public void LoadCompleted_NumericGarbageToken_Discarded()
        {
            // Enum.TryParse принимает числовые строки ("42" -> (LevelId)42) — без
            // Enum.IsDefined мусор из повреждённого профиля молча превращался бы в значение
            // за пределами объявленных LevelId (см. фикс LevelProgressService.LoadCompleted).
            var storage = new FakeProfileStorage();
            storage.SetString(ProfileKeys.CompletedLevels, "42");

            var service = new LevelProgressService(storage, CreateCatalog(LevelId.Level01));

            Assert.IsFalse(service.IsCompleted(LevelId.Level01));
        }
    }
}
