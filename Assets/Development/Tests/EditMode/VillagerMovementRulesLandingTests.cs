using NUnit.Framework;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="VillagerMovementRules.ResolveLandingStimulus"/> — playtest-фикс 2026-09-09
    /// (решение владельца, механика soft-release): мягкая постановка жителя рукой раньше была
    /// невозможна, потому что падение с высоты переноски (CarryHeight ≈ 1.5м) всегда превышало
    /// HardLandingSpeedThreshold, независимо от жеста игрока. Чистая арифметика — тестируется без
    /// Unity-сцены, отдельно от VillagerMovementRulesBlastTests/BoundsTests/RotationTests (другая тема).
    /// </summary>
    public class VillagerMovementRulesLandingTests
    {
        private const float HardLandingSpeedThreshold = 4f;

        [Test]
        public void ResolveLandingStimulus_SoftPlaced_HighImpactSpeed_ReturnsLanded()
        {
            // Ядро фикса: мягко отпущенный житель падает с высоты переноски (импакт легко выше
            // порога), но это не швырок — исход всё равно Landed, без Stunned/Fear.
            var stimulus = VillagerMovementRules.ResolveLandingStimulus(
                isSoftPlaced: true, impactSpeed: 10f, hardLandingSpeedThreshold: HardLandingSpeedThreshold);

            Assert.AreEqual(VillagerStimulus.Landed, stimulus);
        }

        [Test]
        public void ResolveLandingStimulus_SoftPlaced_LowImpactSpeed_ReturnsLanded()
        {
            var stimulus = VillagerMovementRules.ResolveLandingStimulus(
                isSoftPlaced: true, impactSpeed: 0.5f, hardLandingSpeedThreshold: HardLandingSpeedThreshold);

            Assert.AreEqual(VillagerStimulus.Landed, stimulus);
        }

        [Test]
        public void ResolveLandingStimulus_NotSoftPlaced_ImpactAboveThreshold_ReturnsHardLanded()
        {
            // Обычный (не мягкий) релиз — порог работает так же, как до фикса.
            var stimulus = VillagerMovementRules.ResolveLandingStimulus(
                isSoftPlaced: false, impactSpeed: 5f, hardLandingSpeedThreshold: HardLandingSpeedThreshold);

            Assert.AreEqual(VillagerStimulus.HardLanded, stimulus);
        }

        [Test]
        public void ResolveLandingStimulus_NotSoftPlaced_ImpactBelowThreshold_ReturnsLanded()
        {
            var stimulus = VillagerMovementRules.ResolveLandingStimulus(
                isSoftPlaced: false, impactSpeed: 1f, hardLandingSpeedThreshold: HardLandingSpeedThreshold);

            Assert.AreEqual(VillagerStimulus.Landed, stimulus);
        }

        [Test]
        public void ResolveLandingStimulus_NotSoftPlaced_ImpactExactlyAtThreshold_ReturnsHardLanded()
        {
            // Граница ">=" сохранена такой же, как в исходной проверке OnCollisionEnter.
            var stimulus = VillagerMovementRules.ResolveLandingStimulus(
                isSoftPlaced: false, impactSpeed: HardLandingSpeedThreshold, hardLandingSpeedThreshold: HardLandingSpeedThreshold);

            Assert.AreEqual(VillagerStimulus.HardLanded, stimulus);
        }
    }
}
