using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="VillagerMovementRules.ComputeUprightRotation"/> (пятый playtest шага 3 —
    /// житель после приземления в Tossed мог остаться лежать на боку/вниз головой, т.к.
    /// FreezeRotation замораживает тело в любой ориентации касания). Чистая арифметика над
    /// Quaternion/Vector3 — без Unity-сцены.
    /// </summary>
    public class VillagerMovementRulesRotationTests
    {
        [Test]
        public void ComputeUprightRotation_PreservesYaw_WhenTiltedByRoll()
        {
            // Крен строго вокруг собственного forward не меняет сам forward — удобный способ
            // проверить, что горизонтальное направление (yaw) переживает произвольный наклон.
            var yawOnly = Quaternion.Euler(0f, 40f, 0f);
            var rollAxis = yawOnly * Vector3.forward;
            var tilted = Quaternion.AngleAxis(70f, rollAxis) * yawOnly;

            var result = VillagerMovementRules.ComputeUprightRotation(tilted);

            var expectedForward = yawOnly * Vector3.forward;
            var resultForward = result * Vector3.forward;
            Assert.Less(Vector3.Angle(resultForward, expectedForward), 0.5f);
            Assert.Less(Vector3.Angle(result * Vector3.up, Vector3.up), 0.5f);
        }

        [Test]
        public void ComputeUprightRotation_ForwardIsVertical_FallsBackToValidHorizontalYaw()
        {
            // Житель лежит "мордой вниз" — локальный forward смотрит строго вниз, обычной
            // горизонтальной проекции forward для yaw нет (вырожденный случай).
            var current = Quaternion.LookRotation(Vector3.down, Vector3.right);

            var result = VillagerMovementRules.ComputeUprightRotation(current);

            Assert.Less(Vector3.Angle(result * Vector3.up, Vector3.up), 0.5f);

            var resultForward = result * Vector3.forward;
            Assert.AreEqual(0f, resultForward.y, 0.001f);
            Assert.Greater(resultForward.sqrMagnitude, 0.5f); // не вырожден в ноль
            Assert.IsFalse(float.IsNaN(resultForward.x));
            Assert.IsFalse(float.IsNaN(resultForward.z));
        }

        [Test]
        public void ComputeUprightRotation_ForwardIsVertical_UpsideDown_FallsBackToValidHorizontalYaw()
        {
            // Симметричный вырожденный случай — forward смотрит строго вверх (лежит на спине).
            var current = Quaternion.LookRotation(Vector3.up, Vector3.forward);

            var result = VillagerMovementRules.ComputeUprightRotation(current);

            Assert.Less(Vector3.Angle(result * Vector3.up, Vector3.up), 0.5f);

            var resultForward = result * Vector3.forward;
            Assert.Greater(resultForward.sqrMagnitude, 0.5f);
            Assert.IsFalse(float.IsNaN(resultForward.x) || float.IsNaN(resultForward.y) || float.IsNaN(resultForward.z));
        }

        [Test]
        public void ComputeUprightRotation_AlreadyUpright_ReturnsEquivalentRotation()
        {
            var current = Quaternion.Euler(0f, 123f, 0f);

            var result = VillagerMovementRules.ComputeUprightRotation(current);

            Assert.Less(Quaternion.Angle(current, result), 0.5f);
        }
    }
}
