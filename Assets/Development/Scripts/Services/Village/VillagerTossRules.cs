using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Правило "житель брошен именно рукой игрока" (game-design §5, §13, шаг 10 «событийная цепочка
    /// вместо таймеров») — единственный источник этого предиката, тот же прецедент, что
    /// <see cref="VillagerPoseRules"/>. Нужен ОДНОВРЕМЕННО <c>TutorialService</c> (Services/Tutorial)
    /// и <c>LevelEventService</c> (Services/Village, этот же неймспейс).
    /// </summary>
    /// <remarks>
    /// ПЕРЕСМОТРЕНО playtest-багом шага 10 (2026-09-11): раньше предикат жил над
    /// <c>VillagerStateChangedEvent</c> (<c>To == Tossed &amp;&amp; Cause == Grabbed</c>) — дом
    /// реагировал уже на ВЗЯТИЕ жителя в руку, а не на бросок. Причина — FSM жителя переходит в
    /// <c>VillagerState.Tossed</c> уже в момент ЗАХВАТА (состояние "в руке/в полёте" общее и для
    /// удержания, и для броска, см. game-design §5), поэтому <c>Cause == Grabbed</c> детектил сам
    /// факт захвата, а не отпускание с импульсом.
    /// <para>
    /// Точный сигнал — пара событий руки (<c>Core/Events/Events.Hand.cs</c>): <c>ObjectGrabbedEvent</c>
    /// (момент захвата, несёт <c>GrabbableKind</c>) и <c>ObjectThrownEvent</c> (момент отпускания,
    /// несёт только <c>Velocity</c> — рука одна и держит один объект одновременно, поэтому вид
    /// объекта в <c>ObjectThrownEvent</c> не дублируется, а латчится подписчиком из предыдущего
    /// <c>ObjectGrabbedEvent</c> по совпадению <c>Id</c>). Этот класс предикат над уже сматченной
    /// парой не знает ни о шине, ни о латче — вызывающий (<c>LevelEventService</c>,
    /// <c>TutorialService</c>) сам сопоставляет <c>ObjectThrownEvent.Id</c> с латчем вида последнего
    /// захвата и передаёт сюда только вид объекта и итоговую скорость отпускания.
    /// </para>
    /// </remarks>
    public static class VillagerTossRules
    {
        /// <summary>
        /// Порог квадрата скорости отпускания, отделяющий БРОСОК от мягкой постановки
        /// (<c>ObjectThrownEvent.Velocity</c> ~ <c>Vector3.zero</c>, см. xml-doc события). Не то же
        /// самое, что <c>HandConfig.SoftPlacementSpeedThreshold</c> (гейм-дизайн-тюнинг анимации/
        /// хаптики в <c>IHandInteractionService.IsSoftPlacement</c>) — здесь достаточно маленькой
        /// "safety"-эпсилон, отсекающей именно нулевую/около-нулевую скорость, без завязки на
        /// геймдизайн-конфиг (этот класс — pure C# без DI, общий для двух сервисов).
        /// </summary>
        private const float ThrowVelocityEpsilonSqr = 0.01f; // 0.1 м/с

        /// <summary>
        /// True — отпущенный объект был жителем (<paramref name="heldKind"/>), и скорость отпускания
        /// (<paramref name="velocity"/>) превышает порог мягкой постановки (см.
        /// <see cref="ThrowVelocityEpsilonSqr"/>). Вызывающий сам латчит вид объекта из
        /// <c>ObjectGrabbedEvent</c> и сопоставляет <c>Id</c> с пришедшим <c>ObjectThrownEvent</c> —
        /// см. remarks класса.
        /// </summary>
        public static bool IsVillagerThrow(GrabbableKind heldKind, Vector3 velocity) =>
            heldKind == GrabbableKind.Villager && velocity.sqrMagnitude > ThrowVelocityEpsilonSqr;
    }
}
