using UnityEngine;

namespace BlackWhiteLike.Core.Input
{
    /// <summary>
    /// Чистая арифметика pinch-жеста (two-finger zoom) — вынесена из
    /// <see cref="PointerInputProvider.UpdateTouchGestures"/> (шаг 9, Mobile polish; отложенное
    /// решение с шага 2, см. память проекта deferred-architecture-decisions — "единственная
    /// непокрытая тестами математика во вводе"). Static-класс чистых функций, тот же прецедент, что
    /// <c>VillagerMovementRules</c>: без состояния и без Unity API, кроме Vector2/Mathf — без
    /// интерфейса и без регистрации в GameLifetimeScope, тестируется без Unity-сцены. Поведение НЕ
    /// менялось при выносе — 1:1 копия формулы, которая раньше жила прямо в провайдере.
    /// </summary>
    public static class PinchGestureRules
    {
        /// <summary>Дистанция между двумя точками касания — знаменатель пинча (screen-space).</summary>
        public static float ComputeDistance(Vector2 touch0, Vector2 touch1)
        {
            return Vector2.Distance(touch0, touch1);
        }

        /// <summary>
        /// Зум-дельта пинча: разница между текущей и предыдущей дистанцией между пальцами,
        /// умноженная на <paramref name="sensitivity"/>. Если пинч только начался в этом кадре
        /// (<paramref name="pinchWasActive"/> == false — опорной дистанции с предыдущего кадра ещё
        /// нет), дельта — 0: первый кадр пинча не должен давать скачок зума от неопределённой
        /// "предыдущей" дистанции (1:1 с исходным поведением <c>PointerInputProvider</c> — там
        /// <c>_zoom</c> присваивался только внутри <c>if (_pinchActive)</c>, иначе оставался
        /// значением по умолчанию из сброса в начале Tick). Вырожденный случай "пальцы в одной
        /// точке" (дистанция 0 на любом из кадров) не требует отдельной обработки — разность
        /// конечных чисел не даёт NaN/Infinity.
        /// </summary>
        public static float ComputeZoomDelta(float currentDistance, float previousDistance, bool pinchWasActive, float sensitivity)
        {
            if (!pinchWasActive) return 0f;
            return (currentDistance - previousDistance) * sensitivity;
        }
    }
}
