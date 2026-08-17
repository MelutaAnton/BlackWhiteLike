using System;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>
    /// Типизированная событийная шина проекта. Все события — <c>readonly struct</c>
    /// (см. CLAUDE.md — "EventBus: типизированная шина ... struct-события").
    /// <para>
    /// Исключение в одном обработчике не обрывает рассылку остальным подписчикам — оно ловится
    /// и логируется через <see cref="UnityEngine.Debug.LogException"/>, следующий обработчик всё равно вызывается.
    /// </para>
    /// <para>
    /// Подписки, созданные во время Publish (например, изнутри обработчика того же
    /// события), текущее событие не получают — рассылка идёт по снимку списка подписчиков,
    /// зафиксированному на момент начала Publish.
    /// </para>
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Публикует событие всем текущим подписчикам типа <typeparamref name="T"/>.
        /// Если подписчиков нет — не выделяет память.
        /// </summary>
        void Publish<T>(in T evt) where T : struct;

        /// <summary>
        /// Подписывает обработчик на события типа <typeparamref name="T"/>.
        /// Возвращает <see cref="IDisposable"/> для отписки; повторный <c>Dispose()</c> безопасен (идемпотентен).
        /// </summary>
        IDisposable Subscribe<T>(Action<T> handler) where T : struct;
    }
}
