using NUnit.Framework;
using BlackWhiteLike.Controllers.Village;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="VillagerController.ShouldPlayStandUp"/> (шестой playtest шага 3):
    /// осознанное решение владельца-уровня — StandUp играет только из настоящего лежания
    /// (Stunned), не из мягкой постановки (Tossed -> Idle через Landed). Чистая функция —
    /// без Unity-сцены/Animator'а.
    /// </summary>
    public class VillagerControllerAnimationRulesTests
    {
        [Test]
        public void ShouldPlayStandUp_FromStunnedToIdle_ReturnsTrue()
        {
            Assert.IsTrue(VillagerController.ShouldPlayStandUp(VillagerState.Stunned, VillagerState.Idle));
        }

        [Test]
        public void ShouldPlayStandUp_FromTossedToIdle_ReturnsFalse()
        {
            // Шестой playtest шага 3: раньше это было true (общее правило "выход из IsStunned-позы") —
            // владелец развёл позы, мягкая постановка рукой больше не проигрывает StandUp.
            Assert.IsFalse(VillagerController.ShouldPlayStandUp(VillagerState.Tossed, VillagerState.Idle));
        }

        [Test]
        public void ShouldPlayStandUp_FromPanicToIdle_ReturnsFalse()
        {
            Assert.IsFalse(VillagerController.ShouldPlayStandUp(VillagerState.Panic, VillagerState.Idle));
        }

        [Test]
        public void ShouldPlayStandUp_FromStunnedToNonIdle_ReturnsFalse()
        {
            Assert.IsFalse(VillagerController.ShouldPlayStandUp(VillagerState.Stunned, VillagerState.Worship));
        }
    }
}
