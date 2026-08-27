using NUnit.Framework;
using BlackWhiteLike.Services.Village;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="VillagerPoseRules.ShouldPlayStandUp"/> (шестой playtest шага 3):
    /// осознанное решение владельца-уровня — StandUp играет только из настоящего лежания
    /// (Stunned), не из мягкой постановки (Tossed -> Idle через Landed). Чистая функция —
    /// без Unity-сцены/Animator'а.
    /// </summary>
    public class VillagerPoseRulesTests
    {
        [Test]
        public void ShouldPlayStandUp_FromStunnedToIdle_ReturnsTrue()
        {
            Assert.IsTrue(VillagerPoseRules.ShouldPlayStandUp(VillagerState.Stunned, VillagerState.Idle));
        }

        [Test]
        public void ShouldPlayStandUp_FromTossedToIdle_ReturnsFalse()
        {
            // Шестой playtest шага 3: раньше это было true (общее правило "выход из IsStunned-позы") —
            // владелец развёл позы, мягкая постановка рукой больше не проигрывает StandUp.
            Assert.IsFalse(VillagerPoseRules.ShouldPlayStandUp(VillagerState.Tossed, VillagerState.Idle));
        }

        [Test]
        public void ShouldPlayStandUp_FromPanicToIdle_ReturnsFalse()
        {
            Assert.IsFalse(VillagerPoseRules.ShouldPlayStandUp(VillagerState.Panic, VillagerState.Idle));
        }

        [Test]
        public void ShouldPlayStandUp_FromStunnedToNonIdle_ReturnsFalse()
        {
            Assert.IsFalse(VillagerPoseRules.ShouldPlayStandUp(VillagerState.Stunned, VillagerState.Worship));
        }

        // Ревью аудита после шага 3: ForcesZeroMoveSpeed стал публичным API правил поз — фиксируем
        // контракт тестом. В полёте/в руке (Tossed) и лёжа (Stunned) скорость Rigidbody на MoveSpeed
        // аниматора не влияет; на ногах (Idle/Panic/Worship) — влияет.
        [TestCase(VillagerState.Tossed)]
        [TestCase(VillagerState.Stunned)]
        public void ForcesZeroMoveSpeed_AirborneOrLying_ReturnsTrue(VillagerState state)
        {
            Assert.IsTrue(VillagerPoseRules.ForcesZeroMoveSpeed(state));
        }

        [TestCase(VillagerState.Idle)]
        [TestCase(VillagerState.Panic)]
        [TestCase(VillagerState.Worship)]
        public void ForcesZeroMoveSpeed_OnFeet_ReturnsFalse(VillagerState state)
        {
            Assert.IsFalse(VillagerPoseRules.ForcesZeroMoveSpeed(state));
        }
    }
}
