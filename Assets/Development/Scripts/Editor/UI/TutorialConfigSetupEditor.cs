using BlackWhiteLike.Data.Configs.Tutorial;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.UI
{
    /// <summary>
    /// Собирает <c>Assets/Development/Data/Configs/Tutorial/TutorialConfig_Level01.asset</c> —
    /// обучение первого уровня (6 шагов: бросок жителя, выбор Дождя на пожар, применение Дождя,
    /// Disease -&gt; Heal, Hunger -&gt; Rain, набор порога Worship). Идемпотентен: существующий ассет не пересоздаётся, поля
    /// перезаписываются через SerializedObject на каждом прогоне (данные шагов — производные от
    /// контракта задачи, не ручная правка владельца, поэтому безопасно перезаписывать целиком —
    /// в отличие от сценных позиций/таймлайнов, которые считаются данными владельца).
    /// </summary>
    public static class TutorialConfigSetupEditor
    {
        private const string ConfigFolder = "Assets/Development/Data/Configs/Tutorial";
        private const string ConfigPath = ConfigFolder + "/TutorialConfig_Level01.asset";

        private struct TriggerDef
        {
            public TutorialTriggerType Type;
            public NeedType NeedType;
            public MiracleType MiracleType;
            public int WorshipThreshold;
        }

        private struct StepDef
        {
            public TriggerDef Activation;
            public TriggerDef Completion;
            public int TargetCount;
            public string TextRu;
            public string TextEn;
            public TutorialPointerTarget Pointer;
            public bool HighlightMiracleButton;
            public MiracleType HighlightedMiracle;
        }

        private static readonly StepDef[] Steps =
        {
            // 1. Подними жителя и брось его.
            new StepDef
            {
                Activation = default, // TutorialTriggerType.None
                Completion = new TriggerDef { Type = TutorialTriggerType.VillagerTossedByHand },
                TargetCount = 1,
                TextRu = "Подними жителя и брось его",
                TextEn = "Pick up a villager and toss them",
                Pointer = TutorialPointerTarget.None,
            },
            // 2. Дом горит! Выбери чудо Дождь.
            new StepDef
            {
                Activation = new TriggerDef { Type = TutorialTriggerType.NeedTriggered, NeedType = NeedType.Fire },
                Completion = new TriggerDef { Type = TutorialTriggerType.MiracleSelected, MiracleType = MiracleType.Rain },
                TargetCount = 1,
                TextRu = "Дом горит! Выбери чудо Дождь",
                TextEn = "A house is on fire! Select the Rain miracle",
                Pointer = TutorialPointerTarget.MiracleButton,
                HighlightMiracleButton = true,
                HighlightedMiracle = MiracleType.Rain,
            },
            // 3. Пролей Дождь на горящий дом.
            new StepDef
            {
                Activation = default,
                Completion = new TriggerDef { Type = TutorialTriggerType.NeedResolvedByPlayer, NeedType = NeedType.Fire },
                TargetCount = 1,
                TextRu = "Пролей Дождь на горящий дом",
                TextEn = "Cast Rain over the burning house",
                Pointer = TutorialPointerTarget.ActiveNeedSite,
            },
            // 4. Дом заболел! Вылечи его чудом Исцеление.
            new StepDef
            {
                Activation = new TriggerDef { Type = TutorialTriggerType.NeedTriggered, NeedType = NeedType.Disease },
                Completion = new TriggerDef { Type = TutorialTriggerType.NeedResolvedByPlayer, NeedType = NeedType.Disease },
                TargetCount = 1,
                TextRu = "Дом заболел! Вылечи его чудом Исцеление",
                TextEn = "A house fell ill! Cure it with the Heal miracle",
                Pointer = TutorialPointerTarget.ActiveNeedSite,
                HighlightMiracleButton = true,
                HighlightedMiracle = MiracleType.Heal,
            },
            // 5. Поле засохло! Пролей на него Дождь.
            new StepDef
            {
                Activation = new TriggerDef { Type = TutorialTriggerType.NeedTriggered, NeedType = NeedType.Hunger },
                Completion = new TriggerDef { Type = TutorialTriggerType.NeedResolvedByPlayer, NeedType = NeedType.Hunger },
                TargetCount = 1,
                TextRu = "Поле засохло! Пролей на него Дождь",
                TextEn = "The field dried up! Cast Rain over it",
                Pointer = TutorialPointerTarget.ActiveNeedSite,
            },
            // 6. Наполняй шкалу Поклонения.
            new StepDef
            {
                Activation = default,
                Completion = new TriggerDef { Type = TutorialTriggerType.WorshipReached, WorshipThreshold = 30 },
                TargetCount = 1,
                TextRu = "Наполняй шкалу Поклонения: твори чудеса или наводи страх",
                TextEn = "Fill the Worship bar: work miracles or spread fear",
                Pointer = TutorialPointerTarget.WorshipBar,
            },
        };

        [MenuItem("BlackWhiteLike/Configs/Create Tutorial Config")]
        public static void CreateTutorialConfig()
        {
            EnsureFolder(ConfigFolder);

            bool existed = AssetDatabase.LoadAssetAtPath<TutorialConfig>(ConfigPath) != null;
            TutorialConfig config;
            if (existed)
            {
                config = AssetDatabase.LoadAssetAtPath<TutorialConfig>(ConfigPath);
            }
            else
            {
                config = ScriptableObject.CreateInstance<TutorialConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var so = new SerializedObject(config);

            var targetLevelProp = so.FindProperty("_targetLevel");
            if (targetLevelProp == null)
            {
                Debug.LogError("[Editor] Поле \"_targetLevel\" не найдено на TutorialConfig — ассет не заполнен.");
                return;
            }
            targetLevelProp.enumValueIndex = (int)LevelId.Level01;

            var stepsProp = so.FindProperty("_steps");
            if (stepsProp == null)
            {
                Debug.LogError("[Editor] Поле \"_steps\" не найдено на TutorialConfig — ассет не заполнен.");
                return;
            }

            stepsProp.arraySize = Steps.Length;
            for (int i = 0; i < Steps.Length; i++)
            {
                var element = stepsProp.GetArrayElementAtIndex(i);
                var def = Steps[i];

                WriteTrigger(element.FindPropertyRelative("_activationTrigger"), def.Activation);
                WriteTrigger(element.FindPropertyRelative("_completionTrigger"), def.Completion);

                element.FindPropertyRelative("_targetCount").intValue = def.TargetCount;

                var textProp = element.FindPropertyRelative("_text");
                textProp.FindPropertyRelative("_ru").stringValue = def.TextRu;
                textProp.FindPropertyRelative("_en").stringValue = def.TextEn;

                element.FindPropertyRelative("_pointerTarget").enumValueIndex = (int)def.Pointer;
                element.FindPropertyRelative("_highlightMiracleButton").boolValue = def.HighlightMiracleButton;
                element.FindPropertyRelative("_highlightedMiracle").enumValueIndex = (int)def.HighlightedMiracle;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] " + ConfigPath + (existed ? ": поля обновлены (" : ": создан (") +
                      Steps.Length + " шаг(ов)).");
        }

        private static void WriteTrigger(SerializedProperty triggerProp, TriggerDef def)
        {
            if (triggerProp == null)
            {
                Debug.LogError("[Editor] Вложенное поле TutorialTrigger не найдено на TutorialStep — ассет не заполнен.");
                return;
            }

            triggerProp.FindPropertyRelative("_type").enumValueIndex = (int)def.Type;
            triggerProp.FindPropertyRelative("_needType").enumValueIndex = (int)def.NeedType;
            triggerProp.FindPropertyRelative("_miracleType").enumValueIndex = (int)def.MiracleType;
            triggerProp.FindPropertyRelative("_worshipThreshold").intValue = def.WorshipThreshold;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
