using System.Collections.Generic;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Editor.Setup;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackWhiteLike.Editor.Validation
{
    /// <summary>
    /// Шаг 7 — Кампания из 5 уровней (game-design.md, шаг 7). Только чтение (никаких правок ассетов/
    /// сцены, без модальных диалогов — безопасно вызывать автоматизацией, тот же принцип, что
    /// <see cref="ThirdPartyPrefabInstanceValidator"/>): по каждому LevelConfig каталога проверяет
    /// target/coinsReward &gt; 0, прошитость <c>_layout</c> и targetSiteIndex — единственный источник
    /// набора валидных сценовых индексов теперь <see cref="TestVillageSceneSetupEditor.StructureSiteIndices"/>
    /// (та же таблица, из которой строится Village_Base.unity — не нужно открывать сцену). Disease
    /// переехал с NeedSite на жителя (<see cref="NeedHostRules.IsVillagerHosted"/>) — для таких записей
    /// диапазон сценовых NeedSite не проверяется вовсе (виртуальный сайт), но targetSiteIndex ОБЯЗАН
    /// лежать в виртуальном диапазоне (<see cref="NeedHostRules.IsVirtualSiteIndex"/>) и не совпадать
    /// с индексом настоящего сценового NeedSite (иначе NeedSiteController получил бы чужое событие).
    /// Каталог — без null-записей и без дублей LevelId. Читает через SerializedObject (не публичные
    /// свойства) — устойчиво к тому, что рантайм-класс может не выставлять наружу все поля.
    /// Периодический режим нужд (ТЗ владельца, L02..L05): <c>_eventIntervalSeconds</c>/
    /// <c>_periodicNeedDuration</c> не бывают отрицательными (ошибка), непустой EventTimeline при
    /// interval &gt; 0 — предупреждение "два источника событий". Итоговая строка лога перечисляет режим
    /// каждого проверенного LevelConfig (timeline/periodic N с).
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

            var validSiteIndices = new HashSet<int>();
            foreach (var (_, siteIndex) in TestVillageSceneSetupEditor.StructureSiteIndices)
                validSiteIndices.Add(siteIndex);

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
            var modeSummaries = new List<string>();

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

                errorCount += ValidateLevelConfig(level, validSiteIndices, seenIds, modeSummaries);
                levelsChecked++;
            }

            if (levelsChecked == 0)
            {
                Debug.LogError("[Editor] Validate Level Configs: каталог пуст (_entries.arraySize == 0).");
                errorCount++;
            }

            // Итоговая строка — число проверенных конфигов, сценовых сайтов и режим КАЖДОГО
            // (timeline/periodic N с) — логируется всегда, независимо от найденных нарушений.
            string summary = $"{levelsChecked} LevelConfig проверено (сценовых NeedSite-индексов: " +
                              $"{validSiteIndices.Count}). Режимы: {string.Join("; ", modeSummaries)}.";

            if (errorCount == 0)
                Debug.Log($"[Editor] Validate Level Configs: нарушений нет — {summary}");
            else
                Debug.LogError($"[Editor] Validate Level Configs: найдено {errorCount} нарушени(й) — {summary}");
        }

        /// <summary>
        /// Возвращает число найденных нарушений на одном LevelConfig (0 — конфиг валиден). Дописывает в
        /// <paramref name="modeSummaries"/> строку "&lt;label&gt;: timeline (N записей)" либо
        /// "&lt;label&gt;: periodic N с" — источник итоговой строки лога <see cref="ValidateLevelConfigs"/>.
        /// </summary>
        private static int ValidateLevelConfig(LevelConfig level, HashSet<int> validSiteIndices, HashSet<LevelId> seenIds, List<string> modeSummaries)
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

            // Периодический режим нужд (ТЗ владельца, L02..L05): interval/duration отрицательными не
            // бывают, а одновременно непустой EventTimeline + interval > 0 — два источника событий
            // (предупреждение, не ошибка — оба технически могут сосуществовать в рантайме, но это
            // почти наверняка забытая очистка таймлайна при переводе уровня на периодический режим).
            float intervalSeconds = 0f;
            var intervalProp = so.FindProperty("_eventIntervalSeconds");
            var periodicDurationProp = so.FindProperty("_periodicNeedDuration");
            if (intervalProp == null || periodicDurationProp == null)
            {
                Debug.LogError($"[Editor] {label}: поле \"_eventIntervalSeconds\"/\"_periodicNeedDuration\" не найдено на LevelConfig.");
                errors++;
            }
            else
            {
                intervalSeconds = intervalProp.floatValue;
                float periodicDuration = periodicDurationProp.floatValue;

                if (intervalSeconds < 0f)
                {
                    Debug.LogError($"[Editor] {label}: _eventIntervalSeconds={intervalSeconds} < 0.");
                    errors++;
                }
                if (periodicDuration < 0f)
                {
                    Debug.LogError($"[Editor] {label}: _periodicNeedDuration={periodicDuration} < 0.");
                    errors++;
                }
                if (intervalSeconds > 0f && timelineProp.arraySize > 0)
                {
                    Debug.LogWarning($"[Editor] {label}: _eventIntervalSeconds={intervalSeconds} > 0 И EventTimeline " +
                                      $"содержит {timelineProp.arraySize} запис(ей) — два источника событий одновременно " +
                                      "(периодический режим и скриптованный таймлайн), вероятная незавершённая миграция уровня.");
                }
            }

            modeSummaries.Add(intervalSeconds > 0f
                ? $"{label}: periodic {intervalSeconds}с"
                : $"{label}: timeline ({timelineProp.arraySize} записей)");

            float previousTime = float.NegativeInfinity;
            int afterVillagerTossedCount = 0;
            int afterVillagerTossedFirstIndex = -1;
            for (int i = 0; i < timelineProp.arraySize; i++)
            {
                var element = timelineProp.GetArrayElementAtIndex(i);
                float triggerTime = element.FindPropertyRelative("triggerTime").floatValue;
                var needType = (NeedType)element.FindPropertyRelative("type").intValue;

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

                if (NeedHostRules.IsVillagerHosted(needType))
                {
                    // Виртуальный сайт (Disease на жителе) — не сверяется со сценовыми NeedSite, но
                    // ОБЯЗАН лежать в виртуальном диапазоне (NeedHostRules.VirtualSiteIndexBase),
                    // иначе NeedSiteController получит чужое событие в Play Mode.
                    // Коллизия со сценовым индексом отдельно не проверяется: сценовые индексы < 1000 по
                    // построению (StructureSiteIndices), виртуальный диапазон с ними не пересекается.
                    if (!NeedHostRules.IsVirtualSiteIndex(targetSiteIndex))
                    {
                        Debug.LogError($"[Editor] {label}: EventTimeline[{i}] {needType} должен использовать виртуальный индекс " +
                                        $">= {NeedHostRules.VirtualSiteIndexBase} (сейчас targetSiteIndex={targetSiteIndex}).");
                        errors++;
                    }
                }
                else if (targetSiteIndex < 0 || !validSiteIndices.Contains(targetSiteIndex))
                {
                    Debug.LogError($"[Editor] {label}: EventTimeline[{i}] targetSiteIndex={targetSiteIndex} не совпадает ни с " +
                                    "одним фактическим NeedSite-сайтом деревни (StructureSiteIndices).");
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
