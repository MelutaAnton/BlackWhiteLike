using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>Публикуется <c>HandModeService</c> при выборе/отмене выбора чуда в панели (game-design §3, §13).</summary>
    public readonly struct MiracleSelectedEvent
    {
        public readonly MiracleType Type;
        public readonly bool Selected;

        public MiracleSelectedEvent(MiracleType type, bool selected)
        {
            Type = type;
            Selected = selected;
        }
    }

    /// <summary>
    /// Публикуется <c>MiracleService</c> при завершении сессии каста (<c>ReleaseCast</c>) — единственный
    /// канал, через который эффекты чуда применяются к сцене (game-design §4, §13): подписчики
    /// (<c>NeedSiteController</c>, <c>MiracleImpactController</c>, <c>MiracleVfxController</c>) реагируют
    /// на это событие, чудо не держит прямых ссылок на объекты сцены.
    /// </summary>
    public readonly struct MiracleCastEvent
    {
        public readonly MiracleType Type;
        public readonly Vector3 Point;
        public readonly float Power;
        public readonly float Radius;

        public MiracleCastEvent(MiracleType type, Vector3 point, float power, float radius)
        {
            Type = type;
            Point = point;
            Power = power;
            Radius = radius;
        }
    }

    /// <summary>Публикуется <c>FaithService</c> при любом реальном изменении текущего значения Faith (game-design §4, §13).</summary>
    public readonly struct FaithChangedEvent
    {
        public readonly float OldValue;
        public readonly float NewValue;
        public readonly float Capacity;

        public FaithChangedEvent(float oldValue, float newValue, float capacity)
        {
            OldValue = oldValue;
            NewValue = newValue;
            Capacity = capacity;
        }
    }
}
