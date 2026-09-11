using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Services.Miracles
{
    /// <summary>
    /// Каст чуда — stateful-сессия (game-design §4, решение владельца 2026-08-28): <c>CanCast</c> →
    /// <c>BeginCast</c> → <c>UpdateCast</c> (накачка по времени) → <c>ReleaseCast</c>/<c>CancelCast</c>,
    /// симметрично <c>HandInteractionService.BeginHold/EndHold</c>. Паттерн Command не применяется —
    /// обёртка CanExecute/Execute не несла бы состояния и смысла (undo/очередь/множественные
    /// инвокеры касту не нужны). Держит ровно одну активную сессию одновременно (тот же контракт,
    /// что у HandInteractionService — второй BeginCast во время активной сессии отклоняется).
    /// Эффекты чуда применяются ТОЛЬКО через <c>MiracleCastEvent</c> — сервис не держит ссылок на
    /// объекты сцены.
    /// </summary>
    public interface IMiracleService
    {
        /// <summary>Идёт ли сейчас сессия каста.</summary>
        bool IsCasting { get; }

        /// <summary>Радиус активной сессии (растёт при накачке); вне сессии — 0.</summary>
        float CurrentRadius { get; }

        /// <summary>Есть ли конфиг для этого чуда в каталоге и хватает ли Faith на BaseFaithCost.</summary>
        bool CanCast(MiracleType type);

        /// <summary>
        /// Начинает сессию: списывает BaseFaithCost, фиксирует точку и стартовый радиус (BaseRadius).
        /// Возвращает false, если уже идёт другая сессия или <see cref="CanCast"/> == false — в этом
        /// случае Faith не тратится и сессия не открывается. Отказ ИМЕННО нехваткой Faith (шаг 10,
        /// волна 2) публикует <c>MiracleDeniedEvent</c> — единственная причина отказа, дающая событие.
        /// </summary>
        bool BeginCast(MiracleType type, Vector3 point);

        /// <summary>
        /// Обновляет точку активной сессии и накачку: списывает HoldCostPerSecond × deltaTime; при
        /// успехе растит радиус к MaxRadius по HoldSecondsToMaxRadius, при нехватке Faith радиус
        /// замирает (сессия остаётся легальной — база уже оплачена). Вне сессии — no-op.
        /// </summary>
        void UpdateCast(float deltaTime, Vector3 point);

        /// <summary>
        /// Завершает сессию публикацией <c>MiracleCastEvent</c> (power = EffectPower × radius/BaseRadius)
        /// и закрывает её. Вне сессии — no-op, событие не публикуется.
        /// </summary>
        void ReleaseCast();

        /// <summary>Отменяет сессию без события и без возврата уже потраченного Faith. Вне сессии — no-op.</summary>
        void CancelCast();
    }
}
