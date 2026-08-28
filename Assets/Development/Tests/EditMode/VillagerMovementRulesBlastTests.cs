using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="VillagerMovementRules.ComputeBlastVelocity"/> (game-design §4, шаг 4 —
    /// разброс жителей взрывом чуда Fireball). Чистая арифметика над Vector3 — тестируется без
    /// Unity-сцены, отдельно от VillagerMovementRulesBoundsTests (другая тема — не границы уровня).
    /// </summary>
    public class VillagerMovementRulesBlastTests
    {
        [Test]
        public void ComputeBlastVelocity_DirectionPointsAwayFromOrigin()
        {
            var origin = Vector3.zero;
            var position = new Vector3(3f, 0f, 0f);

            var velocity = VillagerMovementRules.ComputeBlastVelocity(origin, position, power: 10f, upwardBias: 0f);

            Assert.Greater(velocity.x, 0f);
            Assert.AreEqual(0f, velocity.z, 0.001f);
        }

        [Test]
        public void ComputeBlastVelocity_MagnitudeEqualsPower()
        {
            var origin = Vector3.zero;
            var position = new Vector3(3f, 0f, 4f);

            var velocity = VillagerMovementRules.ComputeBlastVelocity(origin, position, power: 10f, upwardBias: 0.7f);

            Assert.AreEqual(10f, velocity.magnitude, 0.001f);
        }

        [Test]
        public void ComputeBlastVelocity_UpwardBiasAddsVerticalComponent()
        {
            var origin = Vector3.zero;
            var position = new Vector3(3f, 0f, 0f);

            var velocity = VillagerMovementRules.ComputeBlastVelocity(origin, position, power: 10f, upwardBias: 1f);

            Assert.Greater(velocity.y, 0f);
        }

        [Test]
        public void ComputeBlastVelocity_ZeroUpwardBias_IsPurelyHorizontal()
        {
            var origin = Vector3.zero;
            var position = new Vector3(0f, 0f, 5f);

            var velocity = VillagerMovementRules.ComputeBlastVelocity(origin, position, power: 10f, upwardBias: 0f);

            Assert.AreEqual(0f, velocity.y, 0.001f);
        }

        [Test]
        public void ComputeBlastVelocity_VillagerAtEpicenter_FallsBackToForward_DoesNotProduceNaN()
        {
            var origin = new Vector3(5f, 0f, 5f);
            var position = new Vector3(5f, 0f, 5f); // ровно в эпицентре

            var velocity = VillagerMovementRules.ComputeBlastVelocity(origin, position, power: 10f, upwardBias: 0.7f);

            Assert.IsFalse(float.IsNaN(velocity.x));
            Assert.IsFalse(float.IsNaN(velocity.y));
            Assert.IsFalse(float.IsNaN(velocity.z));
            Assert.AreEqual(10f, velocity.magnitude, 0.001f);
        }

        [Test]
        public void ComputeBlastVelocity_IgnoresVerticalDeltaBetweenOriginAndPosition()
        {
            // Каст на земле (y=0), житель на возвышении (y=2) — вертикальная разница не должна
            // влиять на горизонтальное направление (только upwardBias его добавляет).
            var origin = new Vector3(0f, 0f, 0f);
            var positionLow = new Vector3(3f, 0f, 0f);
            var positionHigh = new Vector3(3f, 2f, 0f);

            var velocityLow = VillagerMovementRules.ComputeBlastVelocity(origin, positionLow, power: 10f, upwardBias: 0.5f);
            var velocityHigh = VillagerMovementRules.ComputeBlastVelocity(origin, positionHigh, power: 10f, upwardBias: 0.5f);

            Assert.AreEqual(velocityLow.x, velocityHigh.x, 0.001f);
            Assert.AreEqual(velocityLow.z, velocityHigh.z, 0.001f);
        }
    }
}
