using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Services.Meta;
using VContainer.Unity;

namespace BlackWhiteLike.Services.Level
{
    /// <summary>
    /// Мост между гейм-логикой уровня и прогрессом кампании (game-design §7-§9, шаг 7):
    /// подписывается на <see cref="LevelCompletedEvent"/> сценового <c>EventBus</c> и отмечает
    /// уровень пройденным в root-сервисе прогресса. Регистрируется в <c>GameLifetimeScope</c>
    /// ТОЛЬКО когда <see cref="ILevelProgressService"/> — настоящий root-сервис (root доступен);
    /// в тестовых сценах без root прогресс не пишется вовсе, а не в одноразовый экземпляр, который
    /// исчезнет вместе со сценой (см. remarks <c>GameLifetimeScope</c>).
    /// </summary>
    public sealed class LevelCompletionRecorder : IStartable, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly ILevelProgressService _progress;

        private IDisposable _levelCompletedSubscription;

        public LevelCompletionRecorder(IEventBus eventBus, ILevelProgressService progress)
        {
            _eventBus = eventBus;
            _progress = progress;
        }

        public void Start()
        {
            _levelCompletedSubscription = _eventBus.Subscribe<LevelCompletedEvent>(OnLevelCompleted);
        }

        public void Dispose()
        {
            _levelCompletedSubscription?.Dispose();
        }

        private void OnLevelCompleted(LevelCompletedEvent evt) => _progress.MarkCompleted(evt.Id);
    }
}
