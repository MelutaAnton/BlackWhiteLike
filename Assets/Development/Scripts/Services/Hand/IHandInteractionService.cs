using BlackWhiteLike.Core.Interaction;
using UnityEngine;

namespace BlackWhiteLike.Services.Hand
{
    /// <summary>
    /// Правила захвата и физика жеста руки бога (game-design §4, §12) — pure C#. Время всегда
    /// приходит аргументом (не читается из UnityEngine.Time внутри) — тестируется без Unity.
    /// </summary>
    public interface IHandInteractionService
    {
        /// <summary>Можно ли взять объект: не null, <see cref="IGrabbable.CanBeGrabbed"/> и масса ≤ MaxGrabMass.</summary>
        bool CanGrab(IGrabbable grabbable);

        /// <summary>
        /// Держит ли рука сейчас объект. Источник истины для камеры (ревью шага 2, Б1+Б3):
        /// <c>IsometricPanZoomBehaviour</c> не должен начинать/продолжать pan, пока рука что-то несёт —
        /// иначе камера "уезжает" вместе с захваченным объектом.
        /// </summary>
        bool IsHolding { get; }

        /// <summary>Начинает удержание: очищает историю позиций и пишет первый семпл (вызывается при захвате).</summary>
        void BeginHold(Vector3 worldPosition, float unscaledTime);

        /// <summary>Добавляет семпл позиции в историю (вызывается каждый кадр удержания).</summary>
        void RecordSample(Vector3 worldPosition, float unscaledTime);

        /// <summary>
        /// Импульс броска по истории позиций за окно <c>HandConfig.ThrowVelocitySampleWindow</c>:
        /// семплы старше окна (относительно <paramref name="unscaledTime"/>) игнорируются, скорость —
        /// смещение между самым старым валидным и самым новым семплом делить на время между ними,
        /// умноженное на ThrowForceScale и клампнутое по модулю на MaxThrowForce.
        /// Меньше 2 валидных семплов — Vector3.zero (мягкая постановка, не бросок).
        /// </summary>
        Vector3 CalculateThrowVelocity(float unscaledTime);

        /// <summary>Velocity-follow к целевой точке: (target - current) * FollowStiffness, кламп FollowMaxSpeed.</summary>
        Vector3 CalculateFollowVelocity(Vector3 currentPosition, Vector3 targetPosition);

        /// <summary>
        /// Считается ли скорость отпускания "мягкой постановкой" (не броском) — модуль ≤
        /// <c>HandConfig.SoftPlacementSpeedThreshold</c>. Решение вынесено сюда (не magic-сравнение
        /// с нулём в контроллере, см. ревью шага 2, В9).
        /// </summary>
        bool IsSoftPlacement(Vector3 velocity);

        /// <summary>Завершает удержание: очищает историю позиций и сбрасывает <see cref="IsHolding"/> (вызывается после отпускания).</summary>
        void EndHold();
    }
}
