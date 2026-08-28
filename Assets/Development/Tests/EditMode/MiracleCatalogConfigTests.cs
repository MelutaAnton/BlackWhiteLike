using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="MiracleCatalogConfig.TryGet"/> (game-design §4, §19, шаг 4).</summary>
    public class MiracleCatalogConfigTests
    {
        private static MiracleConfig CreateMiracleConfig(MiracleType type)
        {
            var config = ScriptableObject.CreateInstance<MiracleConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_type").enumValueIndex = (int)type;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private static MiracleCatalogConfig CreateCatalog(params MiracleConfig[] miracles)
        {
            var catalog = ScriptableObject.CreateInstance<MiracleCatalogConfig>();
            var field = typeof(MiracleCatalogConfig).GetField("_miracles", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(catalog, miracles);
            return catalog;
        }

        [Test]
        public void TryGet_ExistingType_ReturnsTrue_AndConfig()
        {
            var rain = CreateMiracleConfig(MiracleType.Rain);
            var catalog = CreateCatalog(rain, CreateMiracleConfig(MiracleType.Fireball));

            var found = catalog.TryGet(MiracleType.Rain, out var config);

            Assert.IsTrue(found);
            Assert.AreSame(rain, config);
        }

        [Test]
        public void TryGet_MissingType_ReturnsFalse()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain));

            var found = catalog.TryGet(MiracleType.Heal, out var config);

            Assert.IsFalse(found);
            Assert.IsNull(config);
        }

        [Test]
        public void TryGet_EmptyCatalog_ReturnsFalse()
        {
            var catalog = CreateCatalog();

            Assert.IsFalse(catalog.TryGet(MiracleType.Rain, out _));
        }

        [Test]
        public void TryGet_NullElementsInArray_AreSkipped_DoesNotThrow()
        {
            var catalog = CreateCatalog(null, CreateMiracleConfig(MiracleType.Fireball), null);

            bool found = false;
            Assert.DoesNotThrow(() => found = catalog.TryGet(MiracleType.Fireball, out _));
            Assert.IsTrue(found);
        }
    }
}
