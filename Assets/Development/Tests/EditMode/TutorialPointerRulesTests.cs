using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Core.UI;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="TutorialPointerRules"/> (playtest-фикс шага 10, 2026-09-11) — off-screen-
    /// индикатор мировой цели туториала. Чистая арифметика над Vector2/Vector3/float — тестируется
    /// без Unity-сцены.
    /// </summary>
    public class TutorialPointerRulesTests
    {
        private const float MarginX = 0.1f;
        private const float MarginY = 0.1f;

        [Test]
        public void TargetInCenter_OnScreen_NotClamped_PositionUnchanged()
        {
            TutorialPointerRules.ComputeIndicator(new Vector3(0.5f, 0.5f, 1f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsFalse(isClamped);
            Assert.AreEqual(0.5f, clamped.x, 0.0001f);
            Assert.AreEqual(0.5f, clamped.y, 0.0001f);
            Assert.AreEqual(0f, angle, 0.0001f);
        }

        [Test]
        public void TargetInsideMargins_OnScreen_NotClamped_PositionUnchanged()
        {
            // Цель видима и не касается отступов — позиция передаётся как есть, без клампа.
            TutorialPointerRules.ComputeIndicator(new Vector3(0.7f, 0.6f, 1f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsFalse(isClamped);
            Assert.AreEqual(0.7f, clamped.x, 0.0001f);
            Assert.AreEqual(0.6f, clamped.y, 0.0001f);
            Assert.AreEqual(0f, angle, 0.0001f);
        }

        [Test]
        public void TargetBeyondRightEdge_ClampsToRightMargin_AngleZero()
        {
            TutorialPointerRules.ComputeIndicator(new Vector3(1.5f, 0.5f, 1f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsTrue(isClamped);
            Assert.AreEqual(0.9f, clamped.x, 0.0001f); // 1 - MarginX
            Assert.AreEqual(0.5f, clamped.y, 0.0001f);
            Assert.AreEqual(0f, angle, 0.0001f);
        }

        [Test]
        public void TargetBeyondLeftEdge_ClampsToLeftMargin_Angle180()
        {
            TutorialPointerRules.ComputeIndicator(new Vector3(-0.5f, 0.5f, 1f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsTrue(isClamped);
            Assert.AreEqual(0.1f, clamped.x, 0.0001f); // MarginX
            Assert.AreEqual(0.5f, clamped.y, 0.0001f);
            Assert.AreEqual(180f, Mathf.Abs(angle), 0.0001f);
        }

        [Test]
        public void TargetBeyondTopEdge_ClampsToTopMargin_Angle90()
        {
            TutorialPointerRules.ComputeIndicator(new Vector3(0.5f, 1.5f, 1f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsTrue(isClamped);
            Assert.AreEqual(0.5f, clamped.x, 0.0001f);
            Assert.AreEqual(0.9f, clamped.y, 0.0001f); // 1 - MarginY
            Assert.AreEqual(90f, angle, 0.0001f);
        }

        [Test]
        public void TargetBeyondBottomEdge_ClampsToBottomMargin_AngleMinus90()
        {
            TutorialPointerRules.ComputeIndicator(new Vector3(0.5f, -0.5f, 1f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsTrue(isClamped);
            Assert.AreEqual(0.5f, clamped.x, 0.0001f);
            Assert.AreEqual(0.1f, clamped.y, 0.0001f); // MarginY
            Assert.AreEqual(-90f, angle, 0.0001f);
        }

        [Test]
        public void TargetInCorner_ClampsToCorner_Angle45()
        {
            TutorialPointerRules.ComputeIndicator(new Vector3(1.5f, 1.5f, 1f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsTrue(isClamped);
            Assert.AreEqual(0.9f, clamped.x, 0.0001f);
            Assert.AreEqual(0.9f, clamped.y, 0.0001f);
            Assert.AreEqual(45f, angle, 0.0001f);
        }

        [Test]
        public void TargetBehindCamera_MirrorsDirection_ClampsToOppositeEdge_NotRawViewportSide()
        {
            // Цель за спиной камеры: WorldToViewportPoint отдаёт x=0.9 (по сырым координатам —
            // "справа"), но при z<0 это зеркальное отражение — реальная цель слева. Индикатор
            // обязан оказаться у ЛЕВОГО края (не у правого, как дал бы наивный расчёт по x=0.9).
            TutorialPointerRules.ComputeIndicator(new Vector3(0.9f, 0.5f, -1f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsTrue(isClamped);
            Assert.AreEqual(0.1f, clamped.x, 0.0001f); // MarginX — левый край, не правый
            Assert.AreEqual(0.5f, clamped.y, 0.0001f);
            Assert.AreEqual(180f, Mathf.Abs(angle), 0.0001f);
        }

        [Test]
        public void TargetExactlyBehindCamera_AtViewportCenter_DoesNotProduceNaN()
        {
            // Вырожденный случай: цель ровно позади камеры по центру — зеркалирование возвращает
            // координаты обратно в центр, направления нет. Не должно быть NaN/Infinity ни в позиции, ни в угле.
            TutorialPointerRules.ComputeIndicator(new Vector3(0.5f, 0.5f, -3f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsTrue(isClamped);
            Assert.IsFalse(float.IsNaN(clamped.x));
            Assert.IsFalse(float.IsNaN(clamped.y));
            Assert.IsFalse(float.IsNaN(angle));
            Assert.IsFalse(float.IsInfinity(clamped.x));
            Assert.IsFalse(float.IsInfinity(clamped.y));
        }

        [Test]
        public void TargetOnRightMarginBoundary_IsTreatedAsOnScreen_NotClamped()
        {
            // Граница включительно: x == 1 - MarginX считается ещё видимой (>=, <=), не clamped.
            TutorialPointerRules.ComputeIndicator(new Vector3(0.9f, 0.5f, 1f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsFalse(isClamped);
            Assert.AreEqual(0.9f, clamped.x, 0.0001f);
        }

        // --- Композиция угла и поправки спрайта (ревью: блокер "стрелка в противоположную
        // сторону" жил именно здесь, в связке angleDeg + offset, а не в ComputeIndicator) ---

        // Прошитый спрайт 03_Arrow_bot при нулевом повороте смотрит ВНИЗ, т.е. его собственный
        // вектор = -90° в математической системе; поправка +90° доворачивает его до angleDeg.
        private const float DownSpriteOffsetDeg = 90f;

        [TestCase(0f, 90f, TestName = "SpriteRotation_TargetRight_DownArrowRotated90")]
        [TestCase(90f, 180f, TestName = "SpriteRotation_TargetUp_DownArrowRotated180")]
        [TestCase(180f, 270f, TestName = "SpriteRotation_TargetLeft_DownArrowRotated270")]
        [TestCase(-90f, 0f, TestName = "SpriteRotation_TargetDown_DownArrowUnrotated")]
        public void ToSpriteRotation_DownArrow_PointsTowardTarget(float angleDeg, float expectedRotation)
        {
            // Проверка направления: поворот вектора спрайта (0,-1) на итоговый Z-угол должен дать
            // единичный вектор направления angleDeg — стрелка фактически смотрит на цель.
            var rotation = TutorialPointerRules.ToSpriteRotation(angleDeg, DownSpriteOffsetDeg);
            Assert.AreEqual(expectedRotation, Mathf.Repeat(rotation, 360f), 0.0001f);

            var spriteVector = Quaternion.Euler(0f, 0f, rotation) * Vector3.down;
            var targetVector = new Vector3(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad), 0f);
            Assert.AreEqual(targetVector.x, spriteVector.x, 0.0001f);
            Assert.AreEqual(targetVector.y, spriteVector.y, 0.0001f);
        }

        [Test]
        public void NonFiniteViewport_FallsBackToDegenerateBranch_NoNaN()
        {
            // Цель ровно в плоскости камеры: WorldToViewportPoint отдаёт ±Infinity — без гарда
            // Inf * 0 = NaN уехал бы в anchoredPosition/eulerAngles (ревью, важное 3).
            TutorialPointerRules.ComputeIndicator(
                new Vector3(float.PositiveInfinity, float.NegativeInfinity, 0f), MarginX, MarginY,
                out var clamped, out var angle, out var isClamped);

            Assert.IsTrue(isClamped);
            Assert.IsFalse(float.IsNaN(clamped.x) || float.IsNaN(clamped.y));
            Assert.IsFalse(float.IsNaN(angle));
            Assert.AreEqual(0.5f, clamped.x, 0.0001f);
            Assert.AreEqual(1f - MarginY, clamped.y, 0.0001f);
        }
    }
}
