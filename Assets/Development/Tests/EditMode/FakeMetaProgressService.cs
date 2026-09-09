using System;
using System.Collections.Generic;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Meta;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Общий фейковый <see cref="IMetaProgressService"/> для тестов (game-design §8, шаг 8) — полный
    /// контроль над Coins/AlignmentScore/апгрейдами в памяти, без реального профиля/хранилища. Тот
    /// же принцип, что <c>FakeEventBus</c> (единственный источник — не плодить дубли по тестовым файлам).
    /// </summary>
    internal sealed class FakeMetaProgressService : IMetaProgressService
    {
        private readonly Dictionary<UpgradeCategory, int> _upgradeLevels = new Dictionary<UpgradeCategory, int>();

        public int Coins { get; set; }
        public float AlignmentScore { get; set; }
        public DateTime LastSeenUtc { get; set; }

        public bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return true;
            if (Coins < amount) return false;

            Coins -= amount;
            return true;
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
        }

        public int GetUpgradeLevel(UpgradeCategory category) =>
            _upgradeLevels.TryGetValue(category, out var level) ? level : 0;

        public void IncrementUpgradeLevel(UpgradeCategory category) =>
            _upgradeLevels[category] = GetUpgradeLevel(category) + 1;

        public void SetAlignmentScore(float score) => AlignmentScore = score;

        public void StampLastSeen(DateTime value) => LastSeenUtc = value;
    }
}
