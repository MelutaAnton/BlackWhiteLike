using System.Collections.Generic;
using System.Linq;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Editor.Prefabs;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackWhiteLike.Editor.Validation
{
    /// <summary>
    /// Шаг 7 — Кампания из 5 уровней (game-design.md, шаг 7). Только чтение (никаких правок ассетов/
    /// сцены, без модальных диалогов — безопасно вызывать автоматизацией, тот же принцип, что
    /// <see cref="ThirdPartyPrefabInstanceValidator"/>): по каждому LevelConfig каталога проверяет
    /// target/coinsReward &gt; 0, прошитость <c>_layout</c> и targetSiteIndex в пределах числа фактических
    /// NeedSite-сайтов деревни (единственный источник — <see cref="VillageStructurePrefabSetupEditor.Structures"/>,
    /// та же таблица, из которой строится Village_Base.unity — не нужно открывать сцену). Каталог —
    /// без null-записей и без дублей LevelId. Читает через SerializedObject (не публичные свойства)
    /// — устойчиво к тому, что рантайм-класс может не выставлять наружу все поля.
    /// <c>NeedTriggerMode</c> (добавлен параллельно этому скрипту, code-writer): <c>duration &lt;= 0</c>
    /// легален для событийных режимов (AfterVillagerTossed/AfterPreviousResolved — бесконечная нужда,
    /// истекает только по ручному разрешению игроком), но остаётся ошибкой для <c>AtTime</c> —
    /// на таймерном уровне нужда без duration никогда не истечёт, почти наверняка опечатка (ревью шага 10).
    /// Сортировка <c>triggerTime</c> проверяется ТОЛЬКО для записей с <c>_triggerMode == AtTime</c> — у
    /// AfterVillagerTossed/AfterPreviousResolved triggerTime не задаёт момент срабатывания (событийный
    /// триггер). Для AfterPreviousResolved, оказавшейся ПЕРВОЙ записью таймлайна (нет предыдущей нужды),
    /// выдаётся предупреждение, не ошибка. Больше одной записи AfterVillagerTossed в одном таймлайне
    /// (или единственная такая запись не первая) — тоже предупреждение (ревью шага 10): все они взводятся
    /// ОДНОВРЕМЕННО первым броском, обычно нужна ровно одна и первой.
    /// </summary>
    public static class LevelConfigValidationEditor
    {
        private const string LevelCatalogPath = "Assets/Development/Data/Configs/Levels/LevelCatalog.asset";

        [MenuItem("BlackWhiteLike/Validation/Validate Level Configs")]
        public static void ValidateLevelConfigs()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalogConfig>(LevelCatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Editor] Validate Level Configs: " + LevelCatalogPath + " не найден — сначала прогоните " +
                                "\"BlackWhiteLike/Configs/Create Empty SO Set\".");
                return;
            }

            int siteCount = VillageStructurePrefabSetupEditor.Structures.Count(d => d.NeedType.HasValue);

            var catalogSo = new SerializedObject(catalog);
            var entriesProp = catalogSo.FindProperty("_entries");
            if (entriesProp == null)
            {
                Debug.LogError("[Editor] Validate Level Configs: поле \"_entries\" не найдено на LevelCatalogConfig.");
                return;
            }

            int errorCount = 0;
            int levelsChecked = 0;
            var seenIds = new HashSet<LevelId>();
            var seenLevelConfigs = new HashSet<Object>();

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entry = entriesProp.GetArrayElementAtIndex(i);
                var levelProp = entry.FindPropertyRelative("_level");
                var level = levelProp != null ? levelProp.objectReferenceValue as LevelConfig : null;

                if (level == null)
                {
                    Debug.LogError($"[Editor] LevelCatalog._entries[{i}]: _level не прошит (null-запись).");
                    errorCount++;
                    continue;
                }

                if (!seenLevelConfigs.Add(level))
                {
                    Debug.LogError($"[Editor] LevelCatalog._entries[{i}]: LevelConfig \"{level.name}\" уже встречался в каталоге ранее (дубль ссылки).");
                    errorCount++;
                }

                errorCount += ValidateLevelConfig(level, siteCount, seenIds);
                levelsChecked++;
            }

            if (levelsChecked == 0)
            {
                Debug.LogError("[Editor] Validate Level Configs: каталог пуст (_entries.arraySize == 0).");
                errorCount++;
            }

            if (errorCount == 0)
            {
                Debug.Log($"[Editor] Validate Level Configs: нарушений нет — {levelsChecked} LevelConfig " +
                          $"проверено (siteCount={siteCount}).");
            }
            else
            {
                Debug.LogError($"[Editor] Validate Level Configs: найдено {errorCount} нарушени(й) из {levelsChecked} " +
                                $"проверенных LevelConfig (см. LogError выше).");
            }
        }

        /// <summary>Возвращает число найденных нарушений на одном LevelConfig (0 — конфиг валиден).</summary>
        private static int ValidateLevelConfig(LevelConfig level, int siteCount, HashSet<LevelId> seenIds)
        {
            int errors = 0;
            var so = new SerializedObject(level);
            string label = level.name;

            var idProp = so.FindProperty("_id");
            if (idProp != null)
            {
                // Ревью шага 7 (косметика): intValue, не enumValueIndex — последний это индекс в
                // отсортированном списке значений enum, а не хранимое значение поля (разъедется
                // при первом LevelId с явным непоследовательным значением).
                var id = (LevelId)idProp.intValue;
                if (!seenIds.Add(id))
                {
                    Debug.LogError($"[Editor] {label}: LevelId \"{id}\" уже встречался в каталоге ранее (дубль LevelId).");
                    errors++;
                }
            }
            else
            {
                Debug.LogError($"[Editor] {label}: поле \"_id\" не найдено — LevelId не проверен.");
                errors++;
            }

            var targetProp = so.FindProperty("_worshipTarget");
            if (targetProp == null || targetProp.intValue <= 0)
            {
                Debug.LogError($"[Editor] {label}: WorshipTarget должен быть > 0 (сейчас {(targetProp != null ? targetProp.intValue.ToString() : "поле не найдено")}).");
                errors++;
            }

            var coinsProp = so.FindProperty("_coinsReward");
            if (coinsProp == null || coinsProp.intValue <= 0)
            {
                Debug.LogError($"[Editor] {label}: CoinsReward должен быть > 0 (сейчас {(coinsProp != null ? coinsProp.intValue.ToString() : "поле не найдено")}).");
                errors++;
            }

            var layoutProp = so.FindProperty("_layout");
            if (layoutProp == null || layoutProp.objectReferenceValue == null)
            {
                Debug.LogError($"[Editor] {label}: _layout не прошит (VillageLayoutConfig отсутствует).");
                errors++;
            }

            var timelineProp = so.FindProperty("_eventTimeline");
            if (timelineProp == null)
            {
                Debug.LogError($"[Editor] {label}: поле \"_eventTimeline\" не найдено.");
                errors++;
                return errors;
            }

            float previousTime = float.NegativeInfinity;
            int afterVillagerTossedCount = 0;
            int afterVillagerTossedFirstIndex = -1;
            for (int i = 0; i < timelineProp.arraySize; i++)
            {
                var element = timelineProp.GetArrayElementAtIndex(i);
                float triggerTime = element.FindPropertyRelative("triggerTime").floatValue;

                var triggerModeProp = element.FindPropertyRelative("_triggerMode");
                if (triggerModeProp == null)
                {
                    // Ревью шага 10: null-guard — раньше падало NRE, если поле переименуют в будущем
                    // (сейчас _triggerMode на диске всегда есть, включая немигрированные LevelConfig_02..05
                    // со значением дефолта enum'а AtTime=0, но полагаться на это без проверки нельзя).
                    Debug.LogError($"[Editor] {label}: EventTimeline[{i}]: поле \"_triggerMode\" не найдено на NeedEventEntry — проверьте актуальность имени поля.");
                    errors++;
                    continue;
                }

                var triggerMode = (NeedTriggerMode)triggerModeProp.intValue;
                float duration = element.FindPropertyRelative("duration").floatValue;
                int targetSiteIndex = element.FindPropertyRelative("targetSiteIndex").intValue;

                // Сортировка по triggerTime и duration > 0 проверяются ТОЛЬКО для AtTime-записей — у
                // AfterVillagerTossed/AfterPreviousResolved triggerTime не задаёт момент срабатывания
                // (событийный триггер), а duration <= 0 (бесконечная нужда) для них легален (ревью шага 10).
                if (triggerMode == NeedTriggerMode.AtTime)
                {
                    if (triggerTime < previousTime)
                    {
                        Debug.LogError($"[Editor] {label}: EventTimeline[{i}] triggerTime={triggerTime} меньше предыдущего " +
                                        $"({previousTime}) — таймлайн не отсортирован по времени.");
                        errors++;
                    }
                    previousTime = triggerTime;

                    if (duration <= 0f)
                    {
                        Debug.LogError($"[Editor] {label}: EventTimeline[{i}] (_triggerMode=AtTime) duration={duration} <= 0 — " +
                                        "нужда никогда не истечёт по таймеру, вероятная опечатка.");
                        errors++;
                    }
                }
                else if (triggerMode == NeedTriggerMode.AfterPreviousResolved && i == 0)
                {
                    Debug.LogWarning($"[Editor] {label}: EventTimeline[0] имеет _triggerMode=AfterPreviousResolved, но это первая " +
                                      "запись таймлайна — предыдущей нужды не существует, срабатывание неопределено.");
                }
                else if (triggerMode == NeedTriggerMode.AfterVillagerTossed)
                {
                    afterVillagerTossedCount++;
                    if (afterVillagerTossedFirstIndex < 0) afterVillagerTossedFirstIndex = i;
                }

                if (targetSiteIndex < 0 || targetSiteIndex >= siteCount)
                {
                    Debug.LogError($"[Editor] {label}: EventTimeline[{i}] targetSiteIndex={targetSiteIndex} вне диапазона " +
                                    $"[0..{siteCount - 1}] фактических NeedSite-сайтов деревни.");
                    errors++;
                }
            }

            // Ревью шага 10: предупреждение (не ошибка) — все записи AfterVillagerTossed взводятся
            // ОДНОВРЕМЕННО первым броском жителя рукой, обычно дизайн подразумевает ровно одну и первой.
            if (afterVillagerTossedCount > 1 || (afterVillagerTossedCount == 1 && afterVillagerTossedFirstIndex != 0))
            {
                Debug.LogWarning($"[Editor] {label}: найдено {afterVillagerTossedCount} запис(ей) с _triggerMode=AfterVillagerTossed " +
                                  "— все записи AfterVillagerTossed взводятся ОДНОВРЕМЕННО на первом броске жителя рукой, обычно " +
                                  "нужна ровно одна, и первой в таймлайне.");
            }

            return errors;
        }
    }
}
