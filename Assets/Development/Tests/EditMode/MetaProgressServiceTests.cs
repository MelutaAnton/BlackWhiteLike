using System;
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
    /// Тесты <see cref="MetaProgressService"/> (game-design §7-§8, §15, шаг 8) — профиль раунд-трип
    /// (coins/апгрейды/alignment/lastSeen через реальный <see cref="PlayerProfile"/>) и регрессия
    /// <see cref="ILevelProgressService"/> (адаптировано из бывшего LevelProgressServiceTests, шаг 7 —
    /// LevelProgressService слился в MetaProgressService, см. remarks на нём).
    /// </summary>
    public class MetaProgressServiceTests
    {
        private sealed class FakeProfileStorage : IProfileStorage
        {
            private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
            private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
            private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();

            public bool HasKey(string key) => _ints.ContainsKey(key) || _floats.ContainsKey(key) || _strings.ContainsKey(key);
            public int GetInt(string key, int def = 0) => _ints.TryGetValue(key, out var v) ? v : def;
            public void SetInt(string key, int value) => _ints[key] = value;
            public float GetFloat(string key, float def = 0f) => _floats.TryGetValue(key, out var v) ? v : def;
            public void SetFloat(string key, float value) => _floats[key] = value;
            public string GetString(string key, string def = "") => _strings.TryGetValue(key, out var v) ? v : def;
            public void SetString(string key, string value) => _strings[key] = value;

            public void DeleteKey(string key)
            {
                _ints.Remove(key);
                _floats.Remove(key);
                _strings.Remove(key);
            }

            public void DeleteAll()
            {
                _ints.Clear();
                _floats.Clear();
                _strings.Clear();
            }

            public void Flush()
            {
            }
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

        private static MetaProgressService CreateService(IProfileStorage storage, LevelCatalogConfig catalog) =>
            new MetaProgressService(new PlayerProfile(storage), catalog);

        // --- ILevelProgressService (регрессия LevelProgressServiceTests, шаг 7) ---

        [Test]
        public void IsCompleted_NotMarked_ReturnsFalse()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            Assert.IsFalse(service.IsCompleted(LevelId.Level01));
        }

        [Test]
        public void MarkCompleted_ThenIsCompleted_ReturnsTrue()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            service.MarkCompleted(LevelId.Level01);

            Assert.IsTrue(service.IsCompleted(LevelId.Level01));
        }

        [Test]
        public void MarkCompleted_Persists_AcrossNewServiceInstance_SameStorage()
        {
            var storage = new FakeProfileStorage();
            var catalog = CreateCatalog(LevelId.Level01, LevelId.Level02);
            var service = CreateService(storage, catalog);
            service.MarkCompleted(LevelId.Level01);

            var reloaded = CreateService(storage, catalog);

            Assert.IsTrue(reloaded.IsCompleted(LevelId.Level01));
            Assert.IsFalse(reloaded.IsCompleted(LevelId.Level02));
        }

        [Test]
        public void MarkCompleted_TwiceSameLevel_StaysCompleted_DoesNotThrow()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            service.MarkCompleted(LevelId.Level01);
            Assert.DoesNotThrow(() => service.MarkCompleted(LevelId.Level01));

            Assert.IsTrue(service.IsCompleted(LevelId.Level01));
        }

        [Test]
        public void IsUnlocked_FirstLevel_AlwaysTrue()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01, LevelId.Level02));

            Assert.IsTrue(service.IsUnlocked(LevelId.Level01));
        }

        [Test]
        public void IsUnlocked_SecondLevel_BeforeCompletion_ReturnsFalse()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01, LevelId.Level02));

            Assert.IsFalse(service.IsUnlocked(LevelId.Level02));
        }

        [Test]
        public void IsUnlocked_SecondLevel_AfterFirstCompleted_ReturnsTrue()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01, LevelId.Level02));

            service.MarkCompleted(LevelId.Level01);

            Assert.IsTrue(service.IsUnlocked(LevelId.Level02));
        }

        [Test]
        public void IsUnlocked_FarLevel_NotCompletedChain_ReturnsFalse()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01, LevelId.Level02, LevelId.Level03));

            service.MarkCompleted(LevelId.Level01);

            Assert.IsFalse(service.IsUnlocked(LevelId.Level03));
        }

        // --- Шаг 8: coins ---

        [Test]
        public void AddCoins_IncreasesCoins()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            service.AddCoins(50);

            Assert.AreEqual(50, service.Coins);
        }

        [Test]
        public void AddCoins_ZeroOrNegative_IsNoOp()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            service.AddCoins(0);
            service.AddCoins(-10);

            Assert.AreEqual(0, service.Coins);
        }

        [Test]
        public void TrySpendCoins_EnoughCoins_SpendsAndReturnsTrue()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));
            service.AddCoins(100);

            var spent = service.TrySpendCoins(40);

            Assert.IsTrue(spent);
            Assert.AreEqual(60, service.Coins);
        }

        [Test]
        public void TrySpendCoins_NotEnoughCoins_ReturnsFalse_AndDoesNotChange()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));
            service.AddCoins(10);

            var spent = service.TrySpendCoins(20);

            Assert.IsFalse(spent);
            Assert.AreEqual(10, service.Coins);
        }

        [Test]
        public void TrySpendCoins_ZeroOrNegative_ReturnsTrue_WithoutChange()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));
            service.AddCoins(10);

            Assert.IsTrue(service.TrySpendCoins(0));
            Assert.IsTrue(service.TrySpendCoins(-5));
            Assert.AreEqual(10, service.Coins);
        }

        [Test]
        public void Coins_Persist_AcrossNewServiceInstance_SameStorage()
        {
            var storage = new FakeProfileStorage();
            var catalog = CreateCatalog(LevelId.Level01);
            var service = CreateService(storage, catalog);
            service.AddCoins(77);

            var reloaded = CreateService(storage, catalog);

            Assert.AreEqual(77, reloaded.Coins);
        }

        // --- Шаг 8: апгрейды ---

        [Test]
        public void GetUpgradeLevel_NeverPurchased_ReturnsZero()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            Assert.AreEqual(0, service.GetUpgradeLevel(UpgradeCategory.HandPower));
        }

        [Test]
        public void IncrementUpgradeLevel_IncreasesByOne()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            service.IncrementUpgradeLevel(UpgradeCategory.HandPower);
            service.IncrementUpgradeLevel(UpgradeCategory.HandPower);

            Assert.AreEqual(2, service.GetUpgradeLevel(UpgradeCategory.HandPower));
        }

        [Test]
        public void IncrementUpgradeLevel_DoesNotAffectOtherCategories()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            service.IncrementUpgradeLevel(UpgradeCategory.HandPower);

            Assert.AreEqual(0, service.GetUpgradeLevel(UpgradeCategory.FaithCapacity));
        }

        [Test]
        public void UpgradeLevel_Persists_AcrossNewServiceInstance_SameStorage()
        {
            var storage = new FakeProfileStorage();
            var catalog = CreateCatalog(LevelId.Level01);
            var service = CreateService(storage, catalog);
            service.IncrementUpgradeLevel(UpgradeCategory.MiraclePower);

            var reloaded = CreateService(storage, catalog);

            Assert.AreEqual(1, reloaded.GetUpgradeLevel(UpgradeCategory.MiraclePower));
        }

        // --- Шаг 8: alignment / lastSeen ---

        [Test]
        public void AlignmentScore_DefaultsToZero()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            Assert.AreEqual(0f, service.AlignmentScore, 0.001f);
        }

        [Test]
        public void SetAlignmentScore_Persists_AcrossNewServiceInstance_SameStorage()
        {
            var storage = new FakeProfileStorage();
            var catalog = CreateCatalog(LevelId.Level01);
            var service = CreateService(storage, catalog);

            service.SetAlignmentScore(37.5f);

            var reloaded = CreateService(storage, catalog);
            Assert.AreEqual(37.5f, reloaded.AlignmentScore, 0.001f);
        }

        [Test]
        public void LastSeenUtc_DefaultsToDefaultDateTime()
        {
            var service = CreateService(new FakeProfileStorage(), CreateCatalog(LevelId.Level01));

            Assert.AreEqual(default(DateTime), service.LastSeenUtc);
        }

        [Test]
        public void StampLastSeen_Persists_AcrossNewServiceInstance_SameStorage()
        {
            var storage = new FakeProfileStorage();
            var catalog = CreateCatalog(LevelId.Level01);
            var service = CreateService(storage, catalog);
            var timestamp = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

            service.StampLastSeen(timestamp);

            var reloaded = CreateService(storage, catalog);
            Assert.AreEqual(timestamp, reloaded.LastSeenUtc);
        }

        [Test]
        public void LastSeenUtc_GarbageStoredString_FallsBackToDefault()
        {
            // PlayerProfile.LoadLastSeenUtc: DateTime.TryParse на мусорную строку возвращает false —
            // фоллбэк на default(DateTime), а не исключение (профиль повреждён/ручная правка PlayerPrefs).
            var storage = new FakeProfileStorage();
            storage.SetString(ProfileKeys.LastSeenUtc, "not-a-date");

            var service = CreateService(storage, CreateCatalog(LevelId.Level01));

            Assert.AreEqual(default(DateTime), service.LastSeenUtc);
        }

        // --- Ревью шага 8: восстановленные при слиянии кейсы — устойчивость парсинга ---
        // CompletedLevels к мусорным строкам профиля (PlayerProfile.LoadCompletedLevels).

        [Test]
        public void CompletedLevels_EmptyStoredString_NoLevelsCompleted()
        {
            var storage = new FakeProfileStorage();
            storage.SetString(ProfileKeys.CompletedLevels, string.Empty);

            var service = CreateService(storage, CreateCatalog(LevelId.Level01, LevelId.Level02));

            Assert.IsFalse(service.IsCompleted(LevelId.Level01));
            Assert.IsFalse(service.IsCompleted(LevelId.Level02));
        }

        [Test]
        public void CompletedLevels_OnlySeparators_NoLevelsCompleted_DoesNotThrow()
        {
            var storage = new FakeProfileStorage();
            storage.SetString(ProfileKeys.CompletedLevels, ";;;");

            MetaProgressService service = null;
            Assert.DoesNotThrow(() => service = CreateService(storage, CreateCatalog(LevelId.Level01)));
            Assert.IsFalse(service.IsCompleted(LevelId.Level01));
        }

        [Test]
        public void CompletedLevels_MixedWithGarbageToken_IgnoresGarbage_KeepsValidLevels()
        {
            var storage = new FakeProfileStorage();
            storage.SetString(ProfileKeys.CompletedLevels, "Level01;garbage;Level02");

            var service = CreateService(storage, CreateCatalog(LevelId.Level01, LevelId.Level02));

            Assert.IsTrue(service.IsCompleted(LevelId.Level01));
            Assert.IsTrue(service.IsCompleted(LevelId.Level02));
        }

        [Test]
        public void CompletedLevels_NumericGarbageToken_IsDiscarded_EnumIsDefinedGuard()
        {
            // Enum.TryParse принимает числовые строки ("42") как валидный каст даже без такого
            // значения в LevelId — Enum.IsDefined обязан отсеять их (PlayerProfile.LoadCompletedLevels).
            var storage = new FakeProfileStorage();
            storage.SetString(ProfileKeys.CompletedLevels, "42");

            var service = CreateService(storage, CreateCatalog(LevelId.Level01));

            Assert.IsFalse(service.IsCompleted(LevelId.Level01));
        }

        [Test]
        public void TrySpendCoins_Persists_AcrossNewServiceInstance_SameStorage()
        {
            var storage = new FakeProfileStorage();
            var catalog = CreateCatalog(LevelId.Level01);
            var service = CreateService(storage, catalog);
            service.AddCoins(100);
            service.TrySpendCoins(40);

            var reloaded = CreateService(storage, catalog);

            Assert.AreEqual(60, reloaded.Coins);
        }
    }
}
