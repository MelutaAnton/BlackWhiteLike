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

    /// <summary>
    /// Публикуется <c>MiracleService.BeginCast</c> в момент отказа НЕХВАТКОЙ Faith (game-design §4,
    /// §9, §13, шаг 10, волна 2, решение владельца) — ТОЛЬКО по этой причине, не по другим отказам
    /// (уже идёт сессия, тип чуда вне каталога). Явное намерение игрока: тап по чуду, на которое не
    /// хватает веры, показывает rewarded-окно немедленно, а не ждёт автоматического soft-fail
    /// (<c>LevelStalledEvent</c>) — см. <c>LevelStalledPresenter</c>.
    /// </summary>
    public readonly struct MiracleDeniedEvent
    {
        public readonly MiracleType Type;
        public readonly int Cost;
        public readonly float FaithAvailable;

        public MiracleDeniedEvent(MiracleType type, int cost, float faithAvailable)
        {
            Type = type;
            Cost = cost;
            FaithAvailable = faithAvailable;
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
