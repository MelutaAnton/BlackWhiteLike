using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Core.Interaction
{
    /// <summary>
    /// Контракт объекта, который можно взять рукой бога (game-design §4, §7, §12).
    /// Реализации: <c>ThrowableObjectController</c> (шаг 2), <c>VillagerController</c> (шаг 3).
    /// </summary>
    public interface IGrabbable
    {
        /// <summary>Стабильный идентификатор объекта для событий руки.</summary>
        GrabbableId Id { get; }

        /// <summary>Тип объекта — влияет на источник Worship при броске (game-design §7).</summary>
        GrabbableKind Kind { get; }

        /// <summary>Rigidbody объекта — им управляет рука (velocity-follow, импульс броска).</summary>
        Rigidbody Body { get; }

        /// <summary>
        /// Можно ли захватить объект прямо сейчас. Например, житель может быть занят/оглушён,
        /// либо объект ещё не инициализирован через DI (см. реализации).
        /// </summary>
        bool CanBeGrabbed { get; }

        /// <summary>Вызывается рукой в момент захвата.</summary>
        void OnGrabbed();

        /// <summary>
        /// Вызывается рукой в момент отпускания. <paramref name="throwVelocity"/> — Vector3.zero = мягкая постановка.
        /// <paramref name="isSoftPlacement"/> — релиз ниже <c>HandConfig.SoftPlacementSpeedThreshold</c>
        /// (playtest-фикс 2026-09-09, решение владельца): мягко отпущенный объект падает с высоты
        /// переноски (CarryHeight) собственным весом, и этот удар о землю — не швырок, а не жест-бросок.
        /// </summary>
        void OnReleased(Vector3 throwVelocity, bool isSoftPlacement);
    }
}
