using System.IO;
using BlackWhiteLike.Data.Configs.Localization;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.UI
{
    /// <summary>
    /// Создаёт/обновляет <c>Assets/Development/Data/Configs/Localization/LocalizationConfig.asset</c> —
    /// таблицу ключ -> {ru, en} для ВСЕХ статичных (авторских) надписей MainMenuCanvas/TempleCanvas/
    /// LevelHudCanvas (см. <see cref="MainMenuCanvasSetupEditor"/>/<see cref="TempleCanvasSetupEditor"/>/
    /// <see cref="LevelHudSetupEditor"/>, которые вешают <c>LocalizedTextView</c> с этими же ключами на
    /// соответствующие TMP-лейблы). Ключи — константы <see cref="LocalizationKeys"/> (единственный
    /// источник строковых значений, CLAUDE.md "дублирование строковых ключей").
    ///
    /// Заголовок игры "BlackWhiteLike" на MainMenuPanel сюда намеренно НЕ входит — это бренд/лого,
    /// не переводимая фраза (решение оркестратора этого шага). Динамические тексты (счётчики, суммы,
    /// названия уровней — они локализуются отдельно через LevelCatalogEntry._displayName,
    /// см. ConfigAssetCreatorEditor.StampLevelCatalogDisplayNames) и рантайм-тексты презентеров
    /// (TutorialObjectivePanel, LevelStalledOverlay/QuestionText) сюда тоже не входят.
    ///
    /// Идемпотентен: существующая запись с совпадающим текстом не трогается; отсутствующая —
    /// добавляется; существующая с ДРУГИМ текстом — перезаписывается с логом "старое -> новое"
    /// (тот же принцип, что ConfigAssetCreatorEditor.StampLevelCatalogDisplayNames — таблица ниже
    /// единственный источник, владелец правит текст здесь, не в Inspector).
    /// </summary>
    public static class LocalizationConfigSetupEditor
    {
        private const string ConfigsFolder = "Assets/Development/Data/Configs/Localization";
        private const string ConfigPath = ConfigsFolder + "/LocalizationConfig.asset";

        // Единственный источник пар ключ -> {ru, en}. Порядок — по канвасам (MainMenu -> Temple ->
        // LevelHud), внутри канваса — сверху вниз по иерархии сборки в соответствующем Build-скрипте.
        private static readonly (string Key, string Ru, string En)[] Entries =
        {
            // MainMenuCanvas (MainMenuCanvasSetupEditor)
            (LocalizationKeys.Play, "Играть", "Play"),
            (LocalizationKeys.Temple, "Храм", "Temple"),
            (LocalizationKeys.SelectLevel, "Выбор уровня", "Select Level"),
            (LocalizationKeys.Back, "Назад", "Back"),
            (LocalizationKeys.WelcomeBack, "С возвращением!", "Welcome back!"),
            (LocalizationKeys.Collect, "Собрать", "Collect"),
            (LocalizationKeys.Close, "Закрыть", "Close"),

            // TempleCanvas (TempleCanvasSetupEditor) — Temple/Collect/Close/WelcomeBack переиспользуют
            // ключи выше (одна и та же надпись на разных канвасах — один ключ).
            (LocalizationKeys.HandPower, "Сила руки", "Hand Power"),
            (LocalizationKeys.FaithCapacity, "Ёмкость веры", "Faith Capacity"),
            (LocalizationKeys.MiraclePower, "Сила чудес", "Miracle Power"),
            (LocalizationKeys.Buy, "Купить", "Buy"),
            (LocalizationKeys.CreatureComingSoon, "Существо — скоро", "Creature — Coming soon"),
            (LocalizationKeys.NextLevel, "Следующий уровень", "Next level"),

            // LevelHudCanvas (LevelHudSetupEditor)
            (LocalizationKeys.Evil, "Зло", "Evil"),
            (LocalizationKeys.Good, "Добро", "Good"),
            (LocalizationKeys.LevelComplete, "Уровень пройден", "Level Complete"),
            (LocalizationKeys.Continue, "Продолжить", "Continue"),
            (LocalizationKeys.FaithRanOut, "Вера иссякла", "Faith Ran Out"),
            (LocalizationKeys.WatchAd, "Смотреть рекламу", "Watch ad"),
            (LocalizationKeys.Later, "Позже", "Later"),
            (LocalizationKeys.Pause, "Пауза", "Pause"),
            (LocalizationKeys.Resume, "Продолжить", "Resume"),

            // Рантайм-текст LevelStalledPresenter (единственный ключ не с канваса: вопрос пишет
            // презентер через GetText, канвас держит placeholder "—").
            (LocalizationKeys.LevelStalledQuestion, "Посмотреть рекламу, чтобы пополнить веру?", "Watch an ad to refill your faith?"),

            // Названия апгрейдов храма — рантайм-тексты TemplePresenter.ConfigureRow (playtest
            // волны 2: строки светили имена enum-значений UpgradeCategory).
            (LocalizationKeys.UpgradeHandPower, "Сила руки", "Hand Power"),
            (LocalizationKeys.UpgradeFaithCapacity, "Запас веры", "Faith Capacity"),
            (LocalizationKeys.UpgradeMiraclePower, "Сила чудес", "Miracle Power"),
        };

        [MenuItem("BlackWhiteLike/Configs/Create Localization Config")]
        public static void CreateOrUpdateConfig()
        {
            EnsureFolder(ConfigsFolder);

            bool existed = File.Exists(ConfigPath);
            LocalizationConfig config;
            if (existed)
            {
                config = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(ConfigPath);
                if (config == null)
                {
                    Debug.LogError("[Editor] " + ConfigPath + " существует, но не загрузился как LocalizationConfig — прошивка прервана.");
                    return;
                }
            }
            else
            {
                config = ScriptableObject.CreateInstance<LocalizationConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                Debug.Log("[Editor] Создан LocalizationConfig: " + ConfigPath);
            }

            var so = new SerializedObject(config);
            var entriesProp = so.FindProperty("_entries");
            if (entriesProp == null)
            {
                Debug.LogError("[Editor] Поле \"_entries\" не найдено на LocalizationConfig — прошивка прервана " +
                                "(проверьте, что code-writer добавил это SerializeField).");
                return;
            }

            int added = 0;
            int updated = 0;
            foreach (var (key, ru, en) in Entries)
            {
                int index = FindEntryIndex(entriesProp, key);
                if (index < 0)
                {
                    index = entriesProp.arraySize;
                    entriesProp.InsertArrayElementAtIndex(index);
                    var keyProp = entriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("_key");
                    if (keyProp == null)
                    {
                        Debug.LogWarning($"[Editor] Поле \"_key\" не найдено на записи LocalizationConfig (\"{key}\") — запись не добавлена.");
                        entriesProp.DeleteArrayElementAtIndex(index);
                        continue;
                    }
                    keyProp.stringValue = key;
                    added++;
                }

                var textProp = entriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("_text");
                if (textProp == null)
                {
                    Debug.LogWarning($"[Editor] Поле \"_text\" не найдено на записи LocalizationConfig (\"{key}\") — прошивка пропущена.");
                    continue;
                }

                updated += StampLocalizedStringIfChanged(textProp, key, ru, en);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Editor] LocalizationConfig: добавлено {added} новых запис(ей), обновлено {updated} значени(й) текста, всего записей {entriesProp.arraySize}.");
        }

        private static int FindEntryIndex(SerializedProperty entries, string key)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                var keyProp = entries.GetArrayElementAtIndex(i).FindPropertyRelative("_key");
                if (keyProp != null && keyProp.stringValue == key) return i;
            }
            return -1;
        }

        /// <summary>Прошивает LocalizedString (_ru/_en), только если значение отличается от текущего на диске.</summary>
        private static int StampLocalizedStringIfChanged(SerializedProperty textProp, string key, string ru, string en)
        {
            var ruProp = textProp.FindPropertyRelative("_ru");
            var enProp = textProp.FindPropertyRelative("_en");
            if (ruProp == null || enProp == null)
            {
                Debug.LogWarning($"[Editor] LocalizedString._ru/_en не найдены на записи \"{key}\" — прошивка пропущена.");
                return 0;
            }

            int changed = 0;
            if (ruProp.stringValue != ru)
            {
                Debug.Log($"[Editor] LocalizationConfig[\"{key}\"].ru: \"{ruProp.stringValue}\" -> \"{ru}\".");
                ruProp.stringValue = ru;
                changed = 1;
            }
            if (enProp.stringValue != en)
            {
                Debug.Log($"[Editor] LocalizationConfig[\"{key}\"].en: \"{enProp.stringValue}\" -> \"{en}\".");
                enProp.stringValue = en;
                changed = 1;
            }
            return changed;
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
