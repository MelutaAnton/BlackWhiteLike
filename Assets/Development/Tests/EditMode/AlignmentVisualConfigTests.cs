using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using BlackWhiteLike.Data.Configs.Alignment;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="AlignmentVisualConfig.GetStageIndex"/> (шаг 10, волна 2 — UI-тинт по
    /// alignment). Пороги — те же 5, что в AlignmentVisual.asset (Saint 75 / Kind 25 / Neutral -25 /
    /// Cruel -75 / Demon -100), но конфиг здесь собирается локально, не читается с диска.
    /// </summary>
    public class AlignmentVisualConfigTests
    {
        private static AlignmentVisualConfig CreateConfig(params float[] thresholdsDescending)
        {
            var config = ScriptableObject.CreateInstance<AlignmentVisualConfig>();
            var so = new SerializedObject(config);
            var stagesProp = so.FindProperty("_stages");
            stagesProp.arraySize = thresholdsDescending.Length;

            for (var i = 0; i < thresholdsDescending.Length; i++)
            {
                stagesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_scoreThreshold").floatValue = thresholdsDescending[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        [Test]
        public void GetStageIndex_EmptyStages_ReturnsZero()
        {
            var config = CreateConfig();

            Assert.AreEqual(0, config.GetStageIndex(50f));
        }

        [Test]
        public void GetStageIndex_FiveStages_MatchesAlignmentVisualAssetThresholds()
        {
            // 75 (Saint) / 25 (Kind) / -25 (Neutral) / -75 (Cruel) / -100 (Demon).
            var config = CreateConfig(75f, 25f, -25f, -75f, -100f);

            Assert.AreEqual(0, config.GetStageIndex(100f));
            Assert.AreEqual(0, config.GetStageIndex(75f));
            Assert.AreEqual(1, config.GetStageIndex(74.9f));
            Assert.AreEqual(1, config.GetStageIndex(25f));
            Assert.AreEqual(2, config.GetStageIndex(0f));
            Assert.AreEqual(3, config.GetStageIndex(-50f));
            Assert.AreEqual(4, config.GetStageIndex(-100f));
        }

        [Test]
        public void GetStageIndex_BelowLowestThreshold_ClampsToLastIndex()
        {
            var config = CreateConfig(75f, 25f, -25f, -75f, -100f);

            Assert.AreEqual(4, config.GetStageIndex(-999f));
        }

        [Test]
        public void GetStageFor_And_GetStageIndex_AgreeOnSameStage()
        {
            var config = CreateConfig(75f, 25f, -25f, -75f, -100f);

            var index = config.GetStageIndex(-50f);
            var stage = config.GetStageFor(-50f);

            Assert.AreEqual(-75f, stage.ScoreThreshold);
            Assert.AreEqual(3, index);
        }

        [Test]
        public void GetStageIndex_UnsortedStages_LogsWarning_AndStillResolvesCorrectIndex()
        {
            // EnsureSorted сортирует лениво при первом обращении — GetStageIndex должен получить тот
            // же отсортированный кэш, что GetStageFor (общая таблица переходов).
            var config = CreateConfig(-25f, 75f, 25f); // намеренно не по убыванию

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("не отсортированы"));
            Assert.AreEqual(0, config.GetStageIndex(100f)); // после сортировки — 75 первый
        }
    }
}
