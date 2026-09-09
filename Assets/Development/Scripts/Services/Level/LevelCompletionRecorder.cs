using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Services.Meta;
using BlackWhiteLike.Services.Worship;
using VContainer.Unity;

namespace BlackWhiteLike.Services.Level
{
    /// <summary>
    /// Мост между гейм-логикой уровня и прогрессом кампании (game-design §7-§9, шаг 7; §8 — награда
    /// и персист alignment, шаг 8): подписывается на <see cref="LevelCompletedEvent"/> сценового
    /// <c>EventBus</c>, отмечает уровень пройденным, начисляет награду через <see cref="IEconomyService"/>
    /// и персистит итоговый <see cref="IAlignmentService.Score"/> в профиль. Регистрируется в
    /// <c>GameLifetimeScope</c> ТОЛЬКО когда <see cref="ILevelProgressService"/> — настоящий
    /// root-сервис (root доступен); в тестовых сценах без root прогресс не пишется вовсе (см. remarks
    /// <c>GameLifetimeScope</c>).
    /// </summary>
    public sealed class LevelCompletionRecorder : IStartable, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly ILevelProgressService _progress;
        private readonly IEconomyService _economy;
        private readonly LevelConfig _levelConfig;
        private readonly IAlignmentService _alignment;
        private readonly IMetaProgressService _metaProgress;

        private IDisposable _levelCompletedSubscription;

        public LevelCompletionRecorder(
            IEventBus eventBus,
            ILevelProgressService progress,
            IEconomyService economy,
            LevelConfig levelConfig,
            IAlignmentService alignment,
            IMetaProgressService metaProgress)
        {
            _eventBus = eventBus;
            _progress = progress;
            _economy = economy;
            _levelConfig = levelConfig;
            _alignment = alignment;
            _metaProgress = metaProgress;
        }

        public void Start()
        {
            _levelCompletedSubscription = _eventBus.Subscribe<LevelCompletedEvent>(OnLevelCompleted);
        }

        public void Dispose()
        {
            _levelCompletedSubscription?.Dispose();
        }

        private void OnLevelCompleted(LevelCompletedEvent evt)
        {
            // firstClear читается ДО MarkCompleted — иначе IsCompleted уже вернёт true после отметки
            // (game-design §8, шаг 8).
            var firstClear = !_progress.IsCompleted(evt.Id);

            _progress.MarkCompleted(evt.Id);
            _economy.GrantLevelReward(evt.Id, _levelConfig.CoinsReward, firstClear);
            _metaProgress.SetAlignmentScore(_alignment.Score);
        }
    }
}
