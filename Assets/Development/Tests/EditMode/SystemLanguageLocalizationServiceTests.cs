using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using BlackWhiteLike.Data.Configs.Localization;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Localization;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="SystemLanguageLocalizationService"/> (шаг 10 «Polish», волна 2 — GetText).
    /// <see cref="ILocalizationService.CurrentLocale"/> вычисляется из реального
    /// <c>Application.systemLanguage</c> редактора (не мокается — тот же прецедент, что
    /// <c>SystemTimeProvider</c>) — тесты сверяют GetText против <c>LocalizedString.Resolve</c> той
    /// же локали, а не хардкодят ru/en конкретное значение.
    /// </summary>
    public class SystemLanguageLocalizationServiceTests
    {
        private const string KnownKey = "known_key";
        private const string RuText = "Русский текст";
        private const string EnText = "English text";

        private static LocalizationConfig CreateConfig(params (string Key, string Ru, string En)[] entries)
        {
            var config = ScriptableObject.CreateInstance<LocalizationConfig>();
            var so = new SerializedObject(config);
            var entriesProp = so.FindProperty("_entries");
            entriesProp.arraySize = entries.Length;

            for (var i = 0; i < entries.Length; i++)
            {
                var element = entriesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_key").stringValue = entries[i].Key;
                var textProp = element.FindPropertyRelative("_text");
                textProp.FindPropertyRelative("_ru").stringValue = entries[i].Ru;
                textProp.FindPropertyRelative("_en").stringValue = entries[i].En;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        [Test]
        public void GetText_KnownKey_ResolvesForCurrentLocale()
        {
            var config = CreateConfig((KnownKey, RuText, EnText));
            var service = new SystemLanguageLocalizationService(config);

            var expected = new LocalizedString(RuText, EnText).Resolve(service.CurrentLocale);

            Assert.AreEqual(expected, service.GetText(KnownKey));
        }

        [Test]
        public void GetText_UnknownKey_ReturnsKeyItself_AndLogsWarning()
        {
            var service = new SystemLanguageLocalizationService(CreateConfig());

            LogAssert.Expect(LogType.Warning, new Regex("missing_key"));
            var result = service.GetText("missing_key");

            Assert.AreEqual("missing_key", result);
        }

        [Test]
        public void GetText_UnknownKey_CalledTwice_WarnsOnlyOnce()
        {
            var service = new SystemLanguageLocalizationService(CreateConfig());

            LogAssert.Expect(LogType.Warning, new Regex("missing_key"));
            service.GetText("missing_key");
            service.GetText("missing_key"); // повторный вызов того же ключа — без второго LogWarning

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GetText_NullConfig_ReturnsKeyItself_AndLogsWarning()
        {
            // Фоллбэк GameLifetimeScope на "пустой конфиг" собирается через CreateInstance (не null),
            // но сервис обязан пережить и буквальный null (защитный путь).
            var service = new SystemLanguageLocalizationService(null);

            LogAssert.Expect(LogType.Warning, new Regex("some_key"));
            Assert.AreEqual("some_key", service.GetText("some_key"));
        }

        // --- Шаг 10, волна 2: пустой/null ключ — не ошибка данных, молчаливый no-op (см. GetText) ---

        [Test]
        public void GetText_NullKey_ReturnsEmptyString_NoWarning()
        {
            var service = new SystemLanguageLocalizationService(CreateConfig());

            var result = service.GetText(null);

            Assert.AreEqual(string.Empty, result);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GetText_EmptyKey_ReturnsEmptyString_NoWarning()
        {
            var service = new SystemLanguageLocalizationService(CreateConfig());

            var result = service.GetText(string.Empty);

            Assert.AreEqual(string.Empty, result);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CurrentLocale_IsRuOrEn()
        {
            var service = new SystemLanguageLocalizationService(CreateConfig());

            Assert.IsTrue(service.CurrentLocale == GameLocale.Ru || service.CurrentLocale == GameLocale.En);
        }
    }
}
