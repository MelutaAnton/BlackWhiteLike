using System;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Реализация <see cref="IMetaProgressService"/> И <see cref="ILevelProgressService"/> (game-design
    /// §7-§8, §15, шаг 8) — root-сервис (живёт в <c>RootLifetimeScope</c>, переживает загрузку сцен).
    /// Единственный владелец бизнес-правил над <see cref="IPlayerProfile"/>: списание с проверкой
    /// баланса, идемпотентность <see cref="MarkCompleted"/> и т.п. Сам профиль (persist через
    /// <c>IProfileStorage</c>) инкапсулирован в <see cref="PlayerProfile"/> — этот сервис работает с
    /// ним только через интерфейс.
    /// </summary>
    /// <remarks>
    /// Слияние с <c>LevelProgressService</c> (решение владельца, шаг 8): раньше прогресс кампании
    /// (LevelId, шаг 7) и заготовленный, но не реализованный <see cref="IPlayerProfile"/> (coins,
    /// апгрейды, alignment, шаг 8) были бы двумя независимыми root-сервисами над одним и тем же
    /// физическим профилем игрока — разделение не оправдано. <c>LevelProgressService.cs</c> удалён;
    /// потребители <see cref="ILevelProgressService"/> (<c>LevelCompletionRecorder</c>,
    /// <c>RootLifetimeScope.LevelProgress</c>, <c>MainMenuPresenter</c>/<c>LevelSelectPresenter</c>
    /// через него) не меняются — интерфейс тот же, реализация другая.
    /// </remarks>
    public sealed class MetaProgressService : IMetaProgressService, ILevelProgressService
    {
        private readonly IPlayerProfile _profile;
        private readonly LevelCatalogConfig _catalog;

        public MetaProgressService(IPlayerProfile profile, LevelCatalogConfig catalog)
        {
            _profile = profile;
            _catalog = catalog;
        }

        // --- IMetaProgressService ---

        public int Coins => _profile.Coins;

        public bool TrySpendCoins(int amount)
        {
            if (amount <= 0) return true; // нечего списывать — легальный no-op (тот же приём, что FaithService.TrySpend)
            if (_profile.Coins < amount) return false;

            _profile.SpendCoins(amount);
            return true;
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            _profile.AddCoins(amount);
        }

        public int GetUpgradeLevel(UpgradeCategory category) => _profile.GetUpgradeLevel(category);

        public void IncrementUpgradeLevel(UpgradeCategory category) =>
            _profile.SetUpgradeLevel(category, _profile.GetUpgradeLevel(category) + 1);

        public float AlignmentScore => _profile.AlignmentScore;

        public void SetAlignmentScore(float score) => _profile.SetAlignmentScore(score);

        public DateTime LastSeenUtc => _profile.LastSeenUtc;

        public void StampLastSeen(DateTime value) => _profile.SetLastSeenUtc(value);

        // --- ILevelProgressService (перенесено из LevelProgressService, шаг 7) ---

        public void MarkCompleted(LevelId id)
        {
            if (_profile.CompletedLevels.Contains(id)) return; // уже отмечен — повторно на диск не пишем
            _profile.MarkLevelCompleted(id);
        }

        public bool IsCompleted(LevelId id) => _profile.CompletedLevels.Contains(id);

        // Ревью шага 8 (мелочь): было new HashSet<LevelId>(_profile.CompletedLevels) — аллокация на
        // КАЖДЫЙ вызов (LevelSelect дёргает IsUnlocked на каждую карточку уровня). _profile.CompletedLevels
        // теперь сам ISet (см. IPlayerProfile) — передаём напрямую.
        public bool IsUnlocked(LevelId id) =>
            LevelUnlockRules.IsUnlocked(_catalog.Entries, _profile.CompletedLevels, id);
    }
}
