using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using BlackWhiteLike.Data.Configs.Worship;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="WorshipConfig.GetAwe"/>/<see cref="WorshipConfig.GetFear"/> (game-design §7, §19, шаг 5).</summary>
    public class WorshipConfigTests
    {
        /// <summary>
        /// _aweByNeed/_fearByAct — приватные SerializeField-массивы вложенных [Serializable]-элементов
        /// (NeedAweEntry/TerrorFearEntry). Прошиваются через SerializedObject + FindPropertyRelative —
        /// тот же приём, что ConfigAssetCreatorEditor.InitWorshipConfig.
        /// </summary>
        private static WorshipConfig CreateConfig(
            (NeedType Type, int Awe)[] aweEntries = null,
            (TerrorAct Act, int Fear)[] fearEntries = null)
        {
            if (aweEntries == null) aweEntries = new (NeedType, int)[0];
            if (fearEntries == null) fearEntries = new (TerrorAct, int)[0];

            var config = ScriptableObject.CreateInstance<WorshipConfig>();
            var so = new SerializedObject(config);

            var aweProp = so.FindProperty("_aweByNeed");
            aweProp.arraySize = aweEntries.Length;
            for (var i = 0; i < aweEntries.Length; i++)
            {
                var element = aweProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_type").enumValueIndex = (int)aweEntries[i].Type;
                element.FindPropertyRelative("_awe").intValue = aweEntries[i].Awe;
            }

            var fearProp = so.FindProperty("_fearByAct");
            fearProp.arraySize = fearEntries.Length;
            for (var i = 0; i < fearEntries.Length; i++)
            {
                var element = fearProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_act").enumValueIndex = (int)fearEntries[i].Act;
                element.FindPropertyRelative("_fear").intValue = fearEntries[i].Fear;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        [Test]
        public void GetAwe_ExistingEntry_ReturnsValue()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 20) });

            Assert.AreEqual(20, config.GetAwe(NeedType.Fire));
        }

        [Test]
        public void GetAwe_MissingEntry_ReturnsZero_WithWarning()
        {
            var config = CreateConfig();

            LogAssert.Expect(LogType.Warning, new Regex("Awe.*Fire"));
            Assert.AreEqual(0, config.GetAwe(NeedType.Fire));
        }

        [Test]
        public void GetAwe_MissingEntry_WarnsOnlyOnceForSameType()
        {
            var config = CreateConfig();

            LogAssert.Expect(LogType.Warning, new Regex("Awe.*Fire"));
            config.GetAwe(NeedType.Fire);
            config.GetAwe(NeedType.Fire); // повторный запрос того же типа — без нового варнинга

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GetFear_ExistingEntry_ReturnsValue()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.FireballOnStructure, 25) });

            Assert.AreEqual(25, config.GetFear(TerrorAct.FireballOnStructure));
        }

        [Test]
        public void GetFear_MissingEntry_ReturnsZero_WithWarning()
        {
            var config = CreateConfig();

            LogAssert.Expect(LogType.Warning, new Regex("Fear.*FireballOnStructure"));
            Assert.AreEqual(0, config.GetFear(TerrorAct.FireballOnStructure));
        }

        [Test]
        public void GetFear_VillagerBlasted_ReturnsValue()
        {
            // Решение владельца (2026-08-29): отдельная ставка Fear за жителя, отброшенного взрывом чуда.
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.VillagerBlasted, 5) });

            Assert.AreEqual(5, config.GetFear(TerrorAct.VillagerBlasted));
        }

        [Test]
        public void GetAwe_MultipleEntries_PicksMatchingType()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 20), (NeedType.Hunger, 15) });

            Assert.AreEqual(15, config.GetAwe(NeedType.Hunger));
        }
    }
}
