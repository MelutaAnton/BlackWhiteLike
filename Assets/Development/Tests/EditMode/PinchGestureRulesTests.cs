using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Core.Input;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="PinchGestureRules"/> (шаг 9, Mobile polish) — единственная арифметика ввода,
    /// не покрытая тестами до этого шага (математика броска руки покрыта в
    /// HandInteractionServiceTests). Чистая арифметика над Vector2/float — тестируется без
    /// Unity-сцены.
    /// </summary>
    public class PinchGestureRulesTests
    {
        private const float Sensitivity = 0.01f;

        [Test]
        public void ComputeZoomDelta_PinchOut_FingersSpreadApart_ReturnsPositiveDelta()
        {
            // Обычный pinch-out: дистанция между пальцами выросла — зум "приближает" (положительная дельта).
            var delta = PinchGestureRules.ComputeZoomDelta(currentDistance: 200f, previousDistance: 100f, pinchWasActive: true, Sensitivity);

            Assert.Greater(delta, 0f);
            Assert.AreEqual((200f - 100f) * Sensitivity, delta, 0.0001f);
        }

        [Test]
        public void ComputeZoomDelta_PinchIn_FingersMoveTogether_ReturnsNegativeDelta()
        {
            // Обычный pinch-in: дистанция уменьшилась — зум "отдаляет" (отрицательная дельта).
            var delta = PinchGestureRules.ComputeZoomDelta(currentDistance: 100f, previousDistance: 200f, pinchWasActive: true, Sensitivity);

            Assert.Less(delta, 0f);
            Assert.AreEqual((100f - 200f) * Sensitivity, delta, 0.0001f);
        }

        [Test]
        public void ComputeZoomDelta_FingersStationary_ReturnsZero()
        {
            // Пальцы не двигались между кадрами — дистанция не изменилась, зум-дельта должна быть 0.
            var delta = PinchGestureRules.ComputeZoomDelta(currentDistance: 150f, previousDistance: 150f, pinchWasActive: true, Sensitivity);

            Assert.AreEqual(0f, delta, 0.0001f);
        }

        [Test]
        public void ComputeZoomDelta_FirstFrameOfPinch_PinchWasNotActive_ReturnsZeroRegardlessOfDistances()
        {
            // Первый кадр пинча (опорной дистанции с предыдущего кадра ещё нет) — дельта всегда 0,
            // даже если переданные дистанции сильно различаются (не должно быть скачка зума).
            var delta = PinchGestureRules.ComputeZoomDelta(currentDistance: 500f, previousDistance: 10f, pinchWasActive: false, Sensitivity);

            Assert.AreEqual(0f, delta);
        }

        [Test]
        public void ComputeDistance_SamePointOnBothFingers_ReturnsZero_NotNaN()
        {
            // Вырожденный случай: оба пальца в одной точке (сведены вместе) — дистанция 0, не NaN.
            var point = new Vector2(42f, 17f);

            var distance = PinchGestureRules.ComputeDistance(point, point);

            Assert.AreEqual(0f, distance);
            Assert.IsFalse(float.IsNaN(distance));
        }

        [Test]
        public void ComputeZoomDelta_DegenerateSamePointOnPreviousAndCurrentFrame_ReturnsZero_NotNaN()
        {
            // Вырожденный случай: пальцы сведены в одну точку И на предыдущем, И на текущем кадре —
            // обе дистанции 0, дельта должна остаться 0 без NaN/Infinity.
            var pointA = new Vector2(10f, 10f);
            var pointB = new Vector2(10f, 10f);

            var previousDistance = PinchGestureRules.ComputeDistance(pointA, pointB);
            var currentDistance = PinchGestureRules.ComputeDistance(pointA, pointB);

            var delta = PinchGestureRules.ComputeZoomDelta(currentDistance, previousDistance, pinchWasActive: true, Sensitivity);

            Assert.AreEqual(0f, delta);
            Assert.IsFalse(float.IsNaN(delta));
        }

        [Test]
        public void ComputeZoomDelta_SwappingCurrentAndPrevious_FlipsSign()
        {
            // Симметрия знака: тот же переход дистанций в обратную сторону должен давать
            // противоположную по знаку дельту той же величины.
            const float a = 120f;
            const float b = 260f;

            var deltaOut = PinchGestureRules.ComputeZoomDelta(currentDistance: b, previousDistance: a, pinchWasActive: true, Sensitivity);
            var deltaIn = PinchGestureRules.ComputeZoomDelta(currentDistance: a, previousDistance: b, pinchWasActive: true, Sensitivity);

            Assert.AreEqual(-deltaOut, deltaIn, 0.0001f);
        }
    }
}
