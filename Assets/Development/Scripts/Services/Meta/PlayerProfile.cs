using System;
using System.Collections.Generic;
using System.Globalization;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Реализация <see cref="IPlayerProfile"/> поверх <see cref="IProfileStorage"/> (game-design §8,
    /// §15, шаг 8). Единственное место в проекте, читающее/пишущее ключи <see cref="ProfileKeys"/> —
    /// весь остальной код (<c>MetaProgressService</c>) работает через этот интерфейс. Загрузка —
    /// целиком в конструкторе; каждый мутатор пишет ТОЛЬКО свой ключ (точечный Save, без полной
    /// пересериализации профиля при каждом изменении).
    /// </summary>
    public sealed class PlayerProfile : IPlayerProfile
    {
        private const char CompletedLevelsSeparator = ';';

        // "O" — round-trip ISO 8601, культурно-независимый формат (тот же принцип, что запрет на
        // локаль-зависимый ToString() у чисел — иначе смена системной локали ломает парсинг).
        private const string DateTimeFormat = "O";

        private readonly IProfileStorage _storage;

        private int _coins;
        private readonly HashSet<LevelId> _completedLevels;
        private readonly Dictionary<UpgradeCategory, int> _upgradeLevels = new Dictionary<UpgradeCategory, int>();
        private float _alignmentScore;
        private DateTime _lastSeenUtc;

        public PlayerProfile(IProfileStorage storage)
        {
            _storage = storage;

            _coins = _storage.GetInt(ProfileKeys.Coins, 0);
            _completedLevels = LoadCompletedLevels(_storage);
            _alignmentScore = _storage.GetFloat(ProfileKeys.AlignmentScore, 0f);
            _lastSeenUtc = LoadLastSeenUtc(_storage);
        }

        public int Coins => _coins;

        public void AddCoins(int amount)
        {
            _coins += amount;
            _storage.SetInt(ProfileKeys.Coins, _coins);
            _storage.Flush();
        }

        public void SpendCoins(int amount)
        {
            _coins -= amount;
            _storage.SetInt(ProfileKeys.Coins, _coins);
            _storage.Flush();
        }

        public ISet<LevelId> CompletedLevels => _completedLevels;

        public void MarkLevelCompleted(LevelId id)
        {
            if (!_completedLevels.Add(id)) return; // уже отмечен — повторно на диск не пишем

            _storage.SetString(ProfileKeys.CompletedLevels, string.Join(CompletedLevelsSeparator.ToString(), _completedLevels));
            _storage.Flush();
        }

        public int GetUpgradeLevel(UpgradeCategory category) =>
            _upgradeLevels.TryGetValue(category, out var level) ? level : LoadUpgradeLevel(category);

        public void SetUpgradeLevel(UpgradeCategory category, int level)
        {
            _upgradeLevels[category] = level;
            _storage.SetInt(UpgradeKey(category), level);
            _storage.Flush();
        }

        public float AlignmentScore => _alignmentScore;

        public void SetAlignmentScore(float score)
        {
            _alignmentScore = score;
            _storage.SetFloat(ProfileKeys.AlignmentScore, score);
            _storage.Flush();
        }

        public DateTime LastSeenUtc => _lastSeenUtc;

        public void SetLastSeenUtc(DateTime value)
        {
            _lastSeenUtc = value;
            _storage.SetString(ProfileKeys.LastSeenUtc, value.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
            _storage.Flush();
        }

        private int LoadUpgradeLevel(UpgradeCategory category)
        {
            var level = _storage.GetInt(UpgradeKey(category), 0);
            _upgradeLevels[category] = level;
            return level;
        }

        private static string UpgradeKey(UpgradeCategory category) => ProfileKeys.UpgradeLevelPrefix + category;

        private static HashSet<LevelId> LoadCompletedLevels(IProfileStorage storage)
        {
            var result = new HashSet<LevelId>();
            var raw = storage.GetString(ProfileKeys.CompletedLevels, string.Empty);
            if (string.IsNullOrEmpty(raw)) return result;

            foreach (var token in raw.Split(CompletedLevelsSeparator))
            {
                // Мусор из повреждённого профиля не должен превращаться в несуществующий LevelId
                // (тот же фикс, что был у LevelProgressService.LoadCompleted, шаг 7 — Enum.TryParse
                // принимает числовые строки типа "42", Enum.IsDefined отсеивает их).
                if (Enum.TryParse<LevelId>(token, out var id) && Enum.IsDefined(typeof(LevelId), id))
                {
                    result.Add(id);
                }
            }

            return result;
        }

        private static DateTime LoadLastSeenUtc(IProfileStorage storage)
        {
            var raw = storage.GetString(ProfileKeys.LastSeenUtc, string.Empty);
            if (string.IsNullOrEmpty(raw)) return default;

            return DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed)
                ? parsed
                : default;
        }
    }
}
