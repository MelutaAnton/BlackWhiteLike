using System;
using System.Collections.Generic;
using BlackWhiteLike.Core.Events;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Общий фейковый <see cref="IEventBus"/> для тестов (game-design §19) — собирает
    /// опубликованные события в список без реальной рассылки подписчикам. Раньше жил как
    /// приватный nested class внутри <c>GameFlowServiceTests</c> (шаг 1); вынесен на шаге 3, чтобы
    /// <c>VillagerStateServiceTests</c> и <c>LevelEventServiceTests</c> не плодили второй дубль
    /// (CLAUDE.md — единственный источник, здесь тот же принцип применён к тестовой инфраструктуре).
    /// </summary>
    internal sealed class FakeEventBus : IEventBus
    {
        public readonly List<object> PublishedEvents = new List<object>();

        public void Publish<T>(in T evt) where T : struct => PublishedEvents.Add(evt);

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct => new NoOpDisposable();

        private sealed class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
