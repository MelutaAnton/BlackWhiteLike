using NUnit.Framework;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="LocalizedString"/> (шаг 10 «Polish»).</summary>
    public class LocalizedStringTests
    {
        [Test]
        public void Resolve_Ru_ReturnsRuText()
        {
            var text = new LocalizedString("Привет", "Hello");

            Assert.AreEqual("Привет", text.Resolve(GameLocale.Ru));
        }

        [Test]
        public void Resolve_En_WithEnText_ReturnsEnText()
        {
            var text = new LocalizedString("Привет", "Hello");

            Assert.AreEqual("Hello", text.Resolve(GameLocale.En));
        }

        [Test]
        public void Resolve_En_EmptyEnText_FallsBackToRu()
        {
            var text = new LocalizedString("Привет", string.Empty);

            Assert.AreEqual("Привет", text.Resolve(GameLocale.En));
        }

        [Test]
        public void Resolve_En_DefaultEnText_FallsBackToRu()
        {
            // default(LocalizedString) — оба поля null (не сериализовано вообще).
            var text = new LocalizedString("Привет", null);

            Assert.AreEqual("Привет", text.Resolve(GameLocale.En));
        }
    }
}
