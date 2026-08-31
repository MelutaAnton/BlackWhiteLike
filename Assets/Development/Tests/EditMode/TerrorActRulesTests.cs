using NUnit.Framework;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Worship;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="TerrorActRules"/> (game-design §7, §19, шаг 5).</summary>
    public class TerrorActRulesTests
    {
        [Test]
        public void TryFromMiracle_Fireball_ReturnsFireballOnStructure()
        {
            var result = TerrorActRules.TryFromMiracle(MiracleType.Fireball, out var act);

            Assert.IsTrue(result);
            Assert.AreEqual(TerrorAct.FireballOnStructure, act);
        }

        [Test]
        public void TryFromMiracle_Lightning_ReturnsLightningStrike()
        {
            var result = TerrorActRules.TryFromMiracle(MiracleType.Lightning, out var act);

            Assert.IsTrue(result);
            Assert.AreEqual(TerrorAct.LightningStrike, act);
        }

        [Test]
        public void TryFromMiracle_Rain_ReturnsFalse()
        {
            Assert.IsFalse(TerrorActRules.TryFromMiracle(MiracleType.Rain, out _));
        }

        [Test]
        public void TryFromMiracle_Heal_ReturnsFalse()
        {
            Assert.IsFalse(TerrorActRules.TryFromMiracle(MiracleType.Heal, out _));
        }
    }
}
