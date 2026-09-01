using System;
using System.Collections.Generic;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Реализация <see cref="ILevelProgressService"/> (game-design §7-§8, шаг 7). Хранение — через
    /// <see cref="IProfileStorage"/> по ключу <see cref="ProfileKeys.CompletedLevels"/>: список
    /// пройденных <see cref="LevelId"/>, сериализованный строкой через разделитель <c>;</c> — простой
    /// формат вместо JSON, т.к. <see cref="IProfileStorage"/> не предоставляет коллекций, а значений
    /// enum по построению не может быть больше, чем записей в каталоге кампании.
    /// </summary>
    public sealed class LevelProgressService : ILevelProgressService
    {
        private const char Separator = ';';

        private readonly IProfileStorage _storage;
        private readonly LevelCatalogConfig _catalog;
        private readonly HashSet<LevelId> _completed;

        public LevelProgressService(IProfileStorage storage, LevelCatalogConfig catalog)
        {
            _storage = storage;
            _catalog = catalog;
            _completed = LoadCompleted(storage);
        }

        public void MarkCompleted(LevelId id)
        {
            if (!_completed.Add(id)) return; // уже отмечен — повторно на диск не пишем

            _storage.SetString(ProfileKeys.CompletedLevels, string.Join(Separator.ToString(), _completed));
            _storage.Flush();
        }

        public bool IsCompleted(LevelId id) => _completed.Contains(id);

        public bool IsUnlocked(LevelId id) => LevelUnlockRules.IsUnlocked(_catalog.Entries, _completed, id);

        private static HashSet<LevelId> LoadCompleted(IProfileStorage storage)
        {
            var result = new HashSet<LevelId>();
            var raw = storage.GetString(ProfileKeys.CompletedLevels, string.Empty);
            if (string.IsNullOrEmpty(raw)) return result;

            foreach (var token in raw.Split(Separator))
            {
                // Ревью шага 7 (ВАЖНО): Enum.TryParse принимает числовые строки ("42" -> (LevelId)42) —
                // без Enum.IsDefined мусор из повреждённого профиля молча превращался бы в
                // несуществующий LevelId и самовоспроизводился бы при следующем MarkCompleted/сохранении.
                if (Enum.TryParse<LevelId>(token, out var id) && Enum.IsDefined(typeof(LevelId), id))
                {
                    result.Add(id);
                }
            }

            return result;
        }
    }
}
