using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>
    /// Реализация <see cref="IEventBus"/>. Подписчики хранятся в типизированных каналах
    /// (<see cref="EventChannel{T}"/>) — без boxing struct-событий на Publish.
    /// Отписка во время Publish безопасна: удаляемый обработчик помечается null
    /// и физически удаляется из списка после завершения текущей итерации (см. EventChannel{T}.Publish).
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        // Ключ — Type события, значение — EventChannel<T>, приведение по факту вызова.
        // Канал создаётся лениво только при первой подписке — Publish без подписчиков не аллоцирует.
        private readonly Dictionary<Type, object> _channels = new Dictionary<Type, object>();

        public void Publish<T>(in T evt) where T : struct
        {
            if (_channels.TryGetValue(typeof(T), out var channel))
            {
                ((EventChannel<T>)channel).Publish(in evt);
            }
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            var channel = GetOrCreateChannel<T>();
            channel.Add(handler);
            return new Subscription<T>(channel, handler);
        }

        private EventChannel<T> GetOrCreateChannel<T>() where T : struct
        {
            var type = typeof(T);
            if (_channels.TryGetValue(type, out var existing))
            {
                return (EventChannel<T>)existing;
            }

            var channel = new EventChannel<T>();
            _channels[type] = channel;
            return channel;
        }

        /// <summary>
        /// Канал подписчиков одного типа события. Поддерживает безопасную отписку во время итерации
        /// (например, обработчик отписывается сам от себя) через отложенную компактацию списка.
        /// </summary>
        private sealed class EventChannel<T> where T : struct
        {
            private readonly List<Action<T>> _handlers = new List<Action<T>>();
            private int _iterationDepth;
            private bool _needsCompaction;

            public void Add(Action<T> handler) => _handlers.Add(handler);

            public void Remove(Action<T> handler)
            {
                var index = _handlers.IndexOf(handler);
                if (index < 0) return; // уже отписан — идемпотентно

                if (_iterationDepth > 0)
                {
                    // Идёт Publish — физическое удаление сломает индексацию по циклу,
                    // поэтому просто гасим слот, компактация — после выхода из Publish.
                    _handlers[index] = null;
                    _needsCompaction = true;
                }
                else
                {
                    _handlers.RemoveAt(index);
                }
            }

            public void Publish(in T evt)
            {
                _iterationDepth++;
                try
                {
                    var count = _handlers.Count;
                    for (var i = 0; i < count; i++)
                    {
                        try
                        {
                            _handlers[i]?.Invoke(evt);
                        }
                        catch (Exception ex)
                        {
                            // Исключение в одном обработчике не должно обрывать рассылку остальным.
                            Debug.LogException(ex);
                        }
                    }
                }
                finally
                {
                    _iterationDepth--;
                    if (_iterationDepth == 0 && _needsCompaction)
                    {
                        _handlers.RemoveAll(h => h == null);
                        _needsCompaction = false;
                    }
                }
            }
        }

        /// <summary>Токен подписки. Dispose() идемпотентен.</summary>
        private sealed class Subscription<T> : IDisposable where T : struct
        {
            private EventChannel<T> _channel;
            private Action<T> _handler;

            public Subscription(EventChannel<T> channel, Action<T> handler)
            {
                _channel = channel;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_handler == null) return; // уже отписан

                _channel.Remove(_handler);
                _handler = null;
                _channel = null;
            }
        }
    }
}
