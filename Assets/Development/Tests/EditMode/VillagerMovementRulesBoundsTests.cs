using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты чистых статических хелперов <see cref="VillagerMovementRules"/>, отвечающих за границы
    /// уровня (критический дефект третьего playtest'а шага 3 — жители доходили до края земли и
    /// падали в бездну). Оба хелпера — арифметика над Vector3 без обращений к Transform/Rigidbody,
    /// поэтому тестируются без Unity-сцены.
    /// </summary>
    public class VillagerMovementRulesBoundsTests
    {
        private const float HalfExtent = 9f;

        // --- ClampToVillageBounds (цель блуждания) ---

        [Test]
        public void ClampToVillageBounds_PointInsideBounds_IsUnchanged()
        {
            var point = new Vector3(3f, 0f, -4f);

            var result = VillagerMovementRules.ClampToVillageBounds(point, HalfExtent);

            Assert.AreEqual(point, result);
        }

        [Test]
        public void ClampToVillageBounds_PointOutsideOnPositiveX_IsClampedToBoundary()
        {
            // Ровно сценарий дефекта: точка спавна + WanderRadius уводит на метр дальше края (x=11 > half=9).
            var point = new Vector3(11f, 0f, 2f);

            var result = VillagerMovementRules.ClampToVillageBounds(point, HalfExtent);

            Assert.AreEqual(HalfExtent, result.x);
            Assert.AreEqual(2f, result.z); // ось, где не было выхода за границу, не трогается
        }

        [Test]
        public void ClampToVillageBounds_PointOutsideOnNegativeZ_IsClampedToBoundary()
        {
            var point = new Vector3(1f, 0f, -20f);

            var result = VillagerMovementRules.ClampToVillageBounds(point, HalfExtent);

            Assert.AreEqual(1f, result.x);
            Assert.AreEqual(-HalfExtent, result.z);
        }

        [Test]
        public void ClampToVillageBounds_DoesNotTouchY()
        {
            var point = new Vector3(0f, 5f, 0f);

            var result = VillagerMovementRules.ClampToVillageBounds(point, HalfExtent);

            Assert.AreEqual(5f, result.y);
        }

        // --- ClampFleeDirectionToBounds (направление бегства в Panic) ---

        [Test]
        public void ClampFleeDirectionToBounds_FarFromBoundary_DirectionUnchanged()
        {
            var position = Vector3.zero;
            var direction = new Vector3(1f, 0f, 0f);

            var result = VillagerMovementRules.ClampFleeDirectionToBounds(position, direction, HalfExtent);

            Assert.AreEqual(direction, result);
        }

        [Test]
        public void ClampFleeDirectionToBounds_AtPositiveXBoundary_FleeingFurtherOut_SlidesAlongEdge()
        {
            // У обрыва "прочь от источника" толкает ещё дальше по X — составляющая X гасится,
            // остаётся скольжение вдоль края по Z (не остановка).
            var position = new Vector3(HalfExtent, 0f, 0f);
            var direction = new Vector3(1f, 0f, 1f).normalized;

            var result = VillagerMovementRules.ClampFleeDirectionToBounds(position, direction, HalfExtent);

            Assert.AreEqual(0f, result.x, 0.0001f);
            Assert.Greater(result.z, 0f);
        }

        [Test]
        public void ClampFleeDirectionToBounds_AtNegativeZBoundary_FleeingFurtherOut_SlidesAlongEdge()
        {
            var position = new Vector3(0f, 0f, -HalfExtent);
            var direction = new Vector3(1f, 0f, -1f).normalized;

            var result = VillagerMovementRules.ClampFleeDirectionToBounds(position, direction, HalfExtent);

            Assert.AreEqual(0f, result.z, 0.0001f);
            Assert.Greater(result.x, 0f);
        }

        [Test]
        public void ClampFleeDirectionToBounds_AtBoundary_FleeingInward_DirectionUnchanged()
        {
            // На границе, но направление ведёт ОБРАТНО внутрь уровня — клампить нечего.
            var position = new Vector3(HalfExtent, 0f, 0f);
            var direction = new Vector3(-1f, 0f, 0f);

            var result = VillagerMovementRules.ClampFleeDirectionToBounds(position, direction, HalfExtent);

            Assert.AreEqual(direction, result);
        }

        [Test]
        public void ClampFleeDirectionToBounds_AtCorner_BothAxesBlocked_NeverReturnsZero()
        {
            // Уткнулся ровно в угол по обеим осям — результат не должен выродиться в ноль
            // (житель не должен "замереть" у обрыва).
            var position = new Vector3(HalfExtent, 0f, HalfExtent);
            var direction = new Vector3(1f, 0f, 1f).normalized;

            var result = VillagerMovementRules.ClampFleeDirectionToBounds(position, direction, HalfExtent);

            Assert.Greater(result.magnitude, 0.9f);
        }

        [Test]
        public void ClampFleeDirectionToBounds_AtCorner_RotatedFallbackHasNoOutwardComponent()
        {
            // Вычитка диффа целиком (п.5): поворот на 90° в углу раньше не переклампливался и мог
            // вернуть направление, толкающее наружу по ВТОРОЙ оси — нарушение собственного контракта
            // функции. Ни одна ось не должна получить положительную (наружу) составляющую у границы.
            var position = new Vector3(HalfExtent, 0f, HalfExtent);
            var direction = new Vector3(1f, 0f, 1f).normalized; // строго по диагонали наружу

            var result = VillagerMovementRules.ClampFleeDirectionToBounds(position, direction, HalfExtent);

            Assert.LessOrEqual(result.x, 0f);
            Assert.LessOrEqual(result.z, 0f);
        }

        [Test]
        public void ClampFleeDirectionToBounds_ResultIsAlwaysNormalized()
        {
            var position = new Vector3(HalfExtent, 0f, 3f);
            var direction = new Vector3(2f, 0f, 0f); // намеренно не единичной длины

            var result = VillagerMovementRules.ClampFleeDirectionToBounds(position, direction, HalfExtent);

            Assert.AreEqual(1f, result.magnitude, 0.0001f);
        }

        // --- ClampVelocityToBounds (жёсткий кламп позиции — правка после живого прогона) ---
        // Позиция внутри/снаружи для самого ClampBodyToBounds покрыта тестами ClampToVillageBounds
        // выше (ClampBodyToBounds переиспользует ровно ту же функцию для позиции) — здесь отдельно
        // проверяется только гашение скорости, это новая логика.

        [Test]
        public void ClampVelocityToBounds_FarFromBoundary_VelocityUnchanged()
        {
            var position = Vector3.zero;
            var velocity = new Vector3(5f, -9.8f, 3f);

            var result = VillagerMovementRules.ClampVelocityToBounds(position, velocity, HalfExtent);

            Assert.AreEqual(velocity, result);
        }

        [Test]
        public void ClampVelocityToBounds_AtPositiveXBoundary_OutwardComponentZeroed_TangentialPreserved()
        {
            var position = new Vector3(HalfExtent, 0f, 0f);
            var velocity = new Vector3(4f, -9.8f, 2f); // X наружу, Z вдоль границы, Y — гравитация

            var result = VillagerMovementRules.ClampVelocityToBounds(position, velocity, HalfExtent);

            Assert.AreEqual(0f, result.x);
            Assert.AreEqual(2f, result.z); // тангенциальная составляющая не тронута
            Assert.AreEqual(-9.8f, result.y); // гравитация/вертикаль не тронута никогда
        }

        [Test]
        public void ClampVelocityToBounds_AtBoundary_VelocityPointingInward_IsUnchanged()
        {
            // На границе, но скорость ведёт ОБРАТНО внутрь уровня — гасить нечего.
            var position = new Vector3(HalfExtent, 0f, 0f);
            var velocity = new Vector3(-3f, 0f, 1f);

            var result = VillagerMovementRules.ClampVelocityToBounds(position, velocity, HalfExtent);

            Assert.AreEqual(velocity, result);
        }

        [Test]
        public void ClampVelocityToBounds_AtCorner_BothOutwardComponentsZeroed()
        {
            // Выход за обе оси сразу (угол карты) — обе выталкивающие наружу составляющие гасятся.
            var position = new Vector3(HalfExtent, 0f, -HalfExtent);
            var velocity = new Vector3(2f, 0f, -2f); // X наружу (+), Z наружу (-)

            var result = VillagerMovementRules.ClampVelocityToBounds(position, velocity, HalfExtent);

            Assert.AreEqual(0f, result.x);
            Assert.AreEqual(0f, result.z);
        }

        [Test]
        public void ClampVelocityToBounds_BeyondBoundary_OutwardComponentStillZeroed()
        {
            // Позиция уже ЗА границей (не только на ней) — условие ">=" должно сработать так же.
            var position = new Vector3(HalfExtent + 2f, 0f, 0f);
            var velocity = new Vector3(1f, 0f, 0f);

            var result = VillagerMovementRules.ClampVelocityToBounds(position, velocity, HalfExtent);

            Assert.AreEqual(0f, result.x);
        }
    }
}
