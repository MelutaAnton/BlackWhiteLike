using NUnit.Framework;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="NeedHostRules"/> (game-design §5, шаг переноса Disease) — единственный
    /// источник правила "какая нужда живёт на жителе, а не на NeedSite".
    /// </summary>
    public class NeedHostRulesTests
    {
        [Test]
        public void IsVillagerHosted_Disease_ReturnsTrue()
        {
            Assert.IsTrue(NeedHostRules.IsVillagerHosted(NeedType.Disease));
        }

        [TestCase(NeedType.Fire)]
        [TestCase(NeedType.Hunger)]
        public void IsVillagerHosted_FireAndHunger_ReturnsFalse(NeedType type)
        {
            Assert.IsFalse(NeedHostRules.IsVillagerHosted(type));
        }
    }
}
