using System;
using System.IO;
using BlackWhiteLike.Data.Configs.Alignment;
using BlackWhiteLike.Data.Configs.Camera;
using BlackWhiteLike.Data.Configs.Faith;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Data.Configs.Idle;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Data.Configs.Rewards;
using BlackWhiteLike.Data.Configs.Throwable;
using BlackWhiteLike.Data.Configs.Upgrades;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Data.Configs.Vfx;
using BlackWhiteLike.Data.Configs.Worship;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Configs
{
    /// <summary>
    /// Создаёт пустые .asset-инстансы ключевых ScriptableObject-конфигов в правильной
    /// структуре папок (game-design.md §17, шаг 2 "Камера + рука", шаг 3 "Жители + события").
    /// Дефолтные значения полей задаёт сам класс SO (code-writer) — здесь ничего не прошивается.
    /// Идемпотентен: существующий ассет НЕ перезаписывается (настройки, подкрученные
    /// пользователем в Inspector, дороже дефолтов) — значения уже присутствующих на диске полей
    /// не трогаются никогда.
    ///
    /// Расширение списком: чтобы добавить новый SO в §17-набор, достаточно дописать одну
    /// строку в <see cref="Descriptors"/> — метод <see cref="CreateAll"/> трогать не нужно.
    ///
    /// Н6 (ревью шага 3): прошивка <c>VillagerConfig._prefab</c> раньше жила здесь и требовала
    /// прогонять "Setup Villager Prefab" ДО этого пункта меню повторно, иначе ссылка не
    /// проставлялась (порядок нигде не был зафиксирован, кроме текста warning). Перенесена в
    /// конец <c>VillagerPrefabSetupEditor.SetupVillagerPrefab</c> — там префаб гарантированно уже
    /// существует за один прогон.
    ///
    /// Пересериализация существующих ассетов (playtest №2, ревью шага 3): Unity НЕ переписывает
    /// YAML уже созданного ассета сам по себе, когда в его класс добавляют новый [SerializeField] —
    /// рантайм подставляет C#-инициализатор отсутствующего на диске поля "на лету" при десериализации,
    /// но на диске самого ключа нет. Конкретный инцидент: в VillagerConfig добавили четыре поля
    /// (_turnSpeedDegreesPerSecond/_wanderTargetTimeout/_panicMoveSpeed/_standUpDuration), а
    /// VillagerConfig.asset не пересериализовывался с момента создания — первая же частичная правка
    /// через Inspector закрепила бы там 0/null вместо актуального инициализатора класса
    /// (_panicMoveSpeed:0 означало бы, что житель в панике замирает на месте — выглядело бы как баг
    /// логики паники, а не как дыра в конфиге). Решение — общее, не разовая правка одного ассета: для
    /// КАЖДОГО уже существующего дескриптора в <see cref="Descriptors"/> дополнительно помечаем ассет
    /// грязным (EditorUtility.SetDirty) перед AssetDatabase.SaveAssets() — Unity досериализует
    /// недостающие поля значениями текущих инициализаторов класса, не трогая уже присутствующие на
    /// диске (подкрученные владельцем) значения. Та же проблема неизбежно повторится при добавлении
    /// поля в любой другой SO проекта — этот пункт меню ловит её всегда, а не только когда кто-то
    /// вручную заметил дыру.
    /// </summary>
    public static class ConfigAssetCreatorEditor
    {
        private const string ConfigsRoot = "Assets/Development/Data/Configs";

        private const string MiracleRainPath = ConfigsRoot + "/Miracles/Miracle_Rain.asset";
        private const string MiracleFireballPath = ConfigsRoot + "/Miracles/Miracle_Fireball.asset";
        // Шаг 6 — Heal + Lightning (game-design.md §4).
        private const string MiracleHealPath = ConfigsRoot + "/Miracles/Miracle_Heal.asset";
        private const string MiracleLightningPath = ConfigsRoot + "/Miracles/Miracle_Lightning.asset";
        private const string MiracleCatalogPath = ConfigsRoot + "/Miracles/MiracleCatalog.asset";

        // Шаг 5 — Worship + win (game-design.md §19.5).
        private const string WorshipConfigPath = ConfigsRoot + "/Worship/WorshipConfig.asset";

        // Иконки — единственный источник путей: BlackWhiteLike.Editor.Common.EditorSpriteUtility.MiracleIconPaths
        // (правка после ревью: строки дублировались здесь и в Editor/UI/LevelHudSetupEditor.cs).
        private const string RainIconPath = EditorSpriteUtility.MiracleIconPaths.Rain;
        private const string FireballIconPath = EditorSpriteUtility.MiracleIconPaths.Fireball;
        private const string HealIconPath = EditorSpriteUtility.MiracleIconPaths.Heal;
        private const string LightningIconPath = EditorSpriteUtility.MiracleIconPaths.Lightning;

        // Шаг 6 — Alignment-визуал (game-design.md §7, §10). Пути материалов ДУБЛИРУЮТ константы
        // Editor/Setup/AlignmentVisualSetupEditor.cs (тот же приём, что и у путей VFX-префабов
        // между MiracleVfxPrefabSetupEditor/TestVillageSceneSetupEditor — избегаем циклической
        // зависимости неймспейсов Editor.Configs <-> Editor.Setup). Материалы обязаны существовать
        // на диске ДО вызова этого дескриптора — их создаёт AlignmentVisualSetupEditor.EnsureMaterials(),
        // вызываемый ДО ConfigAssetCreatorEditor.CreateAll() в SetupAlignmentVisual().
        // Шаг 7 — Кампания из 5 уровней (game-design.md, шаг 7). VillageLayout_01 — единственная
        // запечённая раскладка деревни для всех пяти уровней (решение владельца: одна деревня).
        private const string VillageLayout01Path = ConfigsRoot + "/Levels/VillageLayout_01.asset";
        private const string LevelConfig01Path = ConfigsRoot + "/Levels/LevelConfig_01.asset";
        private const string LevelConfig02Path = ConfigsRoot + "/Levels/LevelConfig_02.asset";
        private const string LevelConfig03Path = ConfigsRoot + "/Levels/LevelConfig_03.asset";
        private const string LevelConfig04Path = ConfigsRoot + "/Levels/LevelConfig_04.asset";
        private const string LevelConfig05Path = ConfigsRoot + "/Levels/LevelConfig_05.asset";
        private const string LevelCatalogPath = ConfigsRoot + "/Levels/LevelCatalog.asset";

        private const string AlignmentVisualConfigPath = ConfigsRoot + "/Alignment/AlignmentVisual.asset";
        private const string HandMaterialsFolder = "Assets/Development/Art/Materials/Hand";
        private const string TempleMaterialsFolder = "Assets/Development/Art/Materials/Temple";
        private const string HandNeutralMaterialPath = HandMaterialsFolder + "/Hand_Neutral.mat";
        private const string HandKindMaterialPath = HandMaterialsFolder + "/Hand_Kind.mat";
        private const string HandCruelMaterialPath = HandMaterialsFolder + "/Hand_Cruel.mat";
        private const string TempleNeutralMaterialPath = TempleMaterialsFolder + "/Temple_Neutral.mat";
        private const string TempleKindMaterialPath = TempleMaterialsFolder + "/Temple_Kind.mat";
        private const string TempleCruelMaterialPath = TempleMaterialsFolder + "/Temple_Cruel.mat";
        // Ревью шага 6 (ВАЖНО 2): Temple — два MeshRenderer, второй получает "_B"-набор материалов
        // (те же пути, что в Editor/Setup/AlignmentVisualSetupEditor.cs — тот же приём дублирования
        // констант между двумя файлами, что и у остальных путей материалов выше).
        private const string TempleNeutralBMaterialPath = TempleMaterialsFolder + "/Temple_Neutral_B.mat";
        private const string TempleKindBMaterialPath = TempleMaterialsFolder + "/Temple_Kind_B.mat";
        private const string TempleCruelBMaterialPath = TempleMaterialsFolder + "/Temple_Cruel_B.mat";

        // Шаг 8 — Meta + idle (Temple.unity, кампания магазина прокачки). Три пути — единственный
        // источник для InitUpgradeCatalog/InitRewardConfig/InitIdleIncomeConfig ниже.
        private const string UpgradeCatalogPath = ConfigsRoot + "/Upgrades/UpgradeCatalog.asset";
        private const string RewardConfigPath = ConfigsRoot + "/Rewards/RewardConfig.asset";
        private const string IdleIncomeConfigPath = ConfigsRoot + "/Idle/IdleIncome.asset";

        /// <summary>
        /// Единственное место, где перечислены создаваемые SO. Новый конфиг = новая строка.
        /// </summary>
        private static readonly ConfigAssetDescriptor[] Descriptors =
        {
            new ConfigAssetDescriptor(typeof(HandConfig), ConfigsRoot + "/Hand/HandConfig.asset"),
            new ConfigAssetDescriptor(typeof(IsometricCameraConfig), ConfigsRoot + "/Camera/IsometricCameraConfig.asset"),
            // Шаг 3 — Жители + события (game-design.md §19.3).
            new ConfigAssetDescriptor(typeof(VillagerConfig), ConfigsRoot + "/Villagers/VillagerConfig.asset"),
            new ConfigAssetDescriptor(typeof(LevelConfig), ConfigsRoot + "/Levels/LevelConfig_01.asset"),
            new ConfigAssetDescriptor(typeof(VillageLayoutConfig), ConfigsRoot + "/Levels/VillageLayout_01.asset"),
            new ConfigAssetDescriptor(typeof(ThrowableConfig), ConfigsRoot + "/Throwable/ThrowableConfig.asset"),
            // Шаг 4 — Чудеса + Faith (game-design.md §19.4). Инициализаторы выполняются ТОЛЬКО при
            // создании ассета — пересериализация существующего их не вызывает (см. class-doc).
            // Порядок важен: сначала оба MiracleConfig, потом каталог (ссылается на них по пути).
            new ConfigAssetDescriptor(typeof(MiracleConfig), MiracleRainPath, InitRainMiracle),
            new ConfigAssetDescriptor(typeof(MiracleConfig), MiracleFireballPath, InitFireballMiracle),
            // Шаг 6 — Heal + Lightning (game-design.md §4). Порядок перед каталогом — обязателен
            // (InitMiracleCatalog грузит все 4 по путям).
            new ConfigAssetDescriptor(typeof(MiracleConfig), MiracleHealPath, InitHealMiracle),
            new ConfigAssetDescriptor(typeof(MiracleConfig), MiracleLightningPath, InitLightningMiracle),
            new ConfigAssetDescriptor(typeof(MiracleCatalogConfig), MiracleCatalogPath, InitMiracleCatalog),
            new ConfigAssetDescriptor(typeof(FaithConfig), ConfigsRoot + "/Faith/FaithConfig.asset"),
            // _entries прошивает Editor/Prefabs/MiracleVfxPrefabSetupEditor после сборки VFX-префабов.
            new ConfigAssetDescriptor(typeof(VfxConfig), ConfigsRoot + "/Vfx/VfxConfig.asset"),
            // Шаг 5 — Worship + win (game-design.md §19.5).
            new ConfigAssetDescriptor(typeof(WorshipConfig), WorshipConfigPath, InitWorshipConfig),
            // Шаг 6 — Alignment-визуал (game-design.md §7, §10). Материалы (_handMaterials/_templeMaterials)
            // обязаны существовать на диске к моменту создания — см. class-doc про AlignmentVisualSetupEditor.
            new ConfigAssetDescriptor(typeof(AlignmentVisualConfig), AlignmentVisualConfigPath, InitAlignmentVisualConfig),

            // Шаг 7 — Кампания из 5 уровней (game-design.md, шаг 7). LevelConfig_01 уже создан выше
            // (шаг 3) без инициализатора (черновой, заполнялся вручную) — здесь только 02..05, все с
            // прогрессией (target/villagerCount/coinsReward/таймлайн) и общей VillageLayout_01.
            // Порядок обязателен: VillageLayout_01 (Descriptors выше) → LevelConfig_02..05 → каталог.
            new ConfigAssetDescriptor(typeof(LevelConfig), LevelConfig02Path, so => InitLevelConfig(so,
                LevelId.Level02, worshipTarget: 130, villagerCount: 5, coinsReward: 15, Level02Timeline)),
            new ConfigAssetDescriptor(typeof(LevelConfig), LevelConfig03Path, so => InitLevelConfig(so,
                LevelId.Level03, worshipTarget: 160, villagerCount: 4, coinsReward: 20, Level03Timeline)),
            new ConfigAssetDescriptor(typeof(LevelConfig), LevelConfig04Path, so => InitLevelConfig(so,
                LevelId.Level04, worshipTarget: 200, villagerCount: 4, coinsReward: 25, Level04Timeline)),
            new ConfigAssetDescriptor(typeof(LevelConfig), LevelConfig05Path, so => InitLevelConfig(so,
                LevelId.Level05, worshipTarget: 250, villagerCount: 3, coinsReward: 30, Level05Timeline)),
            new ConfigAssetDescriptor(typeof(LevelCatalogConfig), LevelCatalogPath, InitLevelCatalog),

            // Шаг 8 — Meta + idle (game-design.md, шаг 8). Порядок между собой не важен — три
            // независимых конфига, ссылок друг на друга не имеют.
            new ConfigAssetDescriptor(typeof(UpgradeCatalogConfig), UpgradeCatalogPath, InitUpgradeCatalog),
            new ConfigAssetDescriptor(typeof(RewardConfig), RewardConfigPath, InitRewardConfig),
            new ConfigAssetDescriptor(typeof(IdleIncomeConfig), IdleIncomeConfigPath, InitIdleIncomeConfig),
        };

        /// <summary>
        /// Шаг 8 — таблица прокачки (3 категории × 5 уровней, ТЗ владельца): HandPower/MiraclePower —
        /// проценты прибавки, FaithCapacity — абсолютная прибавка ёмкости. Единственный источник для
        /// <see cref="InitUpgradeCatalog"/> И <see cref="MigrateUpgradeCatalogEntries"/> — миграция
        /// дозаполняет недостающие категории/уровни по ЭТОЙ же таблице, не по копии.
        /// </summary>
        private static readonly (UpgradeCategory Category, (int Level, float EffectValue, int Price)[] Levels)[] UpgradeCatalogDefs =
        {
            (UpgradeCategory.HandPower, new[]
            {
                (1, 10f, 20), (2, 20f, 40), (3, 30f, 70), (4, 45f, 110), (5, 60f, 160),
            }),
            (UpgradeCategory.FaithCapacity, new[]
            {
                (1, 20f, 20), (2, 40f, 40), (3, 60f, 70), (4, 90f, 110), (5, 120f, 160),
            }),
            (UpgradeCategory.MiraclePower, new[]
            {
                (1, 10f, 25), (2, 20f, 50), (3, 30f, 85), (4, 45f, 130), (5, 60f, 180),
            }),
        };

        /// <summary>Только при создании ассета (см. class-doc) — 3 категории по 5 уровней, из <see cref="UpgradeCatalogDefs"/>.</summary>
        private static void InitUpgradeCatalog(SerializedObject so)
        {
            var categories = so.FindProperty("_categories");
            categories.arraySize = UpgradeCatalogDefs.Length;
            for (int i = 0; i < UpgradeCatalogDefs.Length; i++)
            {
                var categoryElement = categories.GetArrayElementAtIndex(i);
                categoryElement.FindPropertyRelative("_category").intValue = (int)UpgradeCatalogDefs[i].Category;
                WriteUpgradeLevels(categoryElement.FindPropertyRelative("_levels"), UpgradeCatalogDefs[i].Levels);
            }
        }

        private static void WriteUpgradeLevels(SerializedProperty levelsProp, (int Level, float EffectValue, int Price)[] levelDefs)
        {
            levelsProp.arraySize = levelDefs.Length;
            for (int j = 0; j < levelDefs.Length; j++)
            {
                var levelElement = levelsProp.GetArrayElementAtIndex(j);
                levelElement.FindPropertyRelative("_level").intValue = levelDefs[j].Level;
                levelElement.FindPropertyRelative("_effectValue").floatValue = levelDefs[j].EffectValue;
                levelElement.FindPropertyRelative("_price").intValue = levelDefs[j].Price;
            }
        }

        /// <summary>
        /// Миграция каталога, уже существовавшего до шага 8 (Initializer выше — только при создании
        /// ассета, см. class-doc): дозаполняет недостающие категории целиком и недостающие уровни
        /// внутри уже присутствующих категорий (сопоставление по <c>_level</c>), не трогая и не
        /// дублируя уже заполненные записи — тот же приём, что MigrateMiracleCatalogEntries/
        /// MigrateLevelCatalogEntries выше в этом файле.
        /// </summary>
        private static void MigrateUpgradeCatalogEntries()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UpgradeCatalogConfig>(UpgradeCatalogPath);
            if (catalog == null) return; // ещё не создан — InitUpgradeCatalog при создании соберёт полный набор.

            var so = new SerializedObject(catalog);
            var categories = so.FindProperty("_categories");

            var existingByCategory = new System.Collections.Generic.Dictionary<UpgradeCategory, int>();
            for (int i = 0; i < categories.arraySize; i++)
            {
                var cat = (UpgradeCategory)categories.GetArrayElementAtIndex(i).FindPropertyRelative("_category").intValue;
                existingByCategory[cat] = i;
            }

            int changed = 0;
            foreach (var def in UpgradeCatalogDefs)
            {
                if (!existingByCategory.TryGetValue(def.Category, out int index))
                {
                    index = categories.arraySize;
                    categories.InsertArrayElementAtIndex(index);
                    var newElement = categories.GetArrayElementAtIndex(index);
                    newElement.FindPropertyRelative("_category").intValue = (int)def.Category;
                    WriteUpgradeLevels(newElement.FindPropertyRelative("_levels"), def.Levels);
                    changed++;
                    continue;
                }

                var levelsProp = categories.GetArrayElementAtIndex(index).FindPropertyRelative("_levels");
                var existingLevels = new System.Collections.Generic.HashSet<int>();
                for (int j = 0; j < levelsProp.arraySize; j++)
                    existingLevels.Add(levelsProp.GetArrayElementAtIndex(j).FindPropertyRelative("_level").intValue);

                foreach (var levelDef in def.Levels)
                {
                    if (existingLevels.Contains(levelDef.Level)) continue;

                    int levelIndex = levelsProp.arraySize;
                    levelsProp.InsertArrayElementAtIndex(levelIndex);
                    var levelElement = levelsProp.GetArrayElementAtIndex(levelIndex);
                    levelElement.FindPropertyRelative("_level").intValue = levelDef.Level;
                    levelElement.FindPropertyRelative("_effectValue").floatValue = levelDef.EffectValue;
                    levelElement.FindPropertyRelative("_price").intValue = levelDef.Price;
                    existingLevels.Add(levelDef.Level);
                    changed++;
                }
            }

            if (changed == 0) return;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
            Debug.Log($"[Editor] UpgradeCatalog: дозаполнено {changed} запис(ей) (категорий/уровней), всего категорий {categories.arraySize}.");
        }

        /// <summary>Только при создании ассета — множитель первого прохождения уровня (ТЗ владельца).</summary>
        private static void InitRewardConfig(SerializedObject so)
        {
            so.FindProperty("_firstClearMultiplier").floatValue = 2f;
        }

        // Ревью шага 8 (мелочь): MigrateRewardConfig() удалён — ветка "если 0" была недостижима.
        // RewardConfig._firstClearMultiplier имеет C#-инициализатор 2f (см. class), и Unity
        // подставляет его "на лету" для отсутствующего в YAML поля ДО срабатывания OnValidate
        // (см. class-doc "Пересериализация существующих ассетов" выше) — значение на диске
        // к моменту вызова миграции уже >= 1 (2f для отсутствующего поля, min 1 от самого
        // OnValidate для явно записанного 0), проверка "== 0" никогда не проходила.

        /// <summary>Только при создании ассета — идле-доход (ТЗ владельца: 60 монет/час, кап 8ч, минимум 5 мин офлайна).</summary>
        private static void InitIdleIncomeConfig(SerializedObject so)
        {
            so.FindProperty("_coinsPerHour").floatValue = 60f;
            so.FindProperty("_capHours").floatValue = 8f;
            so.FindProperty("_minOfflineMinutes").floatValue = 5f;
        }

        /// <summary>Миграция IdleIncomeConfig — тот же приём "0 -> дефолт", что MigrateRewardConfig.</summary>
        private static void MigrateIdleIncomeConfig()
        {
            var idle = AssetDatabase.LoadAssetAtPath<IdleIncomeConfig>(IdleIncomeConfigPath);
            if (idle == null) return;

            var so = new SerializedObject(idle);
            int changed = 0;
            changed += MigrateFloatIfZero(so, "_coinsPerHour", 60f, "IdleIncomeConfig");
            changed += MigrateFloatIfZero(so, "_capHours", 8f, "IdleIncomeConfig");
            changed += MigrateFloatIfZero(so, "_minOfflineMinutes", 5f, "IdleIncomeConfig");

            if (changed == 0) return;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(idle);
        }

        /// <summary>Дозаполняет float-поле дефолтом, только если оно сейчас 0 (незаполненное). Возвращает 1, если изменено.</summary>
        private static int MigrateFloatIfZero(SerializedObject so, string propertyName, float defaultValue, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — миграция пропущена.");
                return 0;
            }

            if (!Mathf.Approximately(prop.floatValue, 0f)) return 0;

            prop.floatValue = defaultValue;
            Debug.Log($"[Editor] {ownerLabel}.{propertyName}: 0 -> {defaultValue} (дозаполнение).");
            return 1;
        }

        // Таймлайны прогрессии (game-design.md, шаг 7, ТЗ владельца): плотнее и с более короткими
        // интервалами с ростом уровня (~25с → ~15с), duration=15с как у LevelConfig_01. targetSiteIndex
        // — по фактической раскладке сцены (VillageStructurePrefabSetupEditor.Structures): House_01=0/Fire,
        // House_02=1/Disease, Field_Carrots=2/Hunger (см. TestVillageSceneSetupEditor.DefaultEventTimeline).
        private static readonly (NeedType Type, float TriggerTime, int SiteIndex, float Duration)[] Level02Timeline =
        {
            (NeedType.Fire, 5f, 0, 15f),
            (NeedType.Disease, 30f, 1, 15f),
            (NeedType.Hunger, 55f, 2, 15f),
            (NeedType.Fire, 80f, 0, 15f),
        };

        private static readonly (NeedType Type, float TriggerTime, int SiteIndex, float Duration)[] Level03Timeline =
        {
            (NeedType.Fire, 5f, 0, 15f),
            (NeedType.Disease, 26f, 1, 15f),
            (NeedType.Hunger, 47f, 2, 15f),
            (NeedType.Fire, 68f, 0, 15f),
            (NeedType.Disease, 89f, 1, 15f),
        };

        private static readonly (NeedType Type, float TriggerTime, int SiteIndex, float Duration)[] Level04Timeline =
        {
            (NeedType.Fire, 5f, 0, 15f),
            (NeedType.Disease, 23f, 1, 15f),
            (NeedType.Hunger, 41f, 2, 15f),
            (NeedType.Fire, 59f, 0, 15f),
            (NeedType.Disease, 77f, 1, 15f),
            (NeedType.Hunger, 95f, 2, 15f),
        };

        private static readonly (NeedType Type, float TriggerTime, int SiteIndex, float Duration)[] Level05Timeline =
        {
            (NeedType.Fire, 5f, 0, 15f),
            (NeedType.Disease, 20f, 1, 15f),
            (NeedType.Hunger, 35f, 2, 15f),
            (NeedType.Fire, 50f, 0, 15f),
            (NeedType.Disease, 65f, 1, 15f),
            (NeedType.Hunger, 80f, 2, 15f),
            (NeedType.Fire, 95f, 0, 15f),
        };

        /// <summary>
        /// Общий инициализатор LevelConfig_02..05 (шаг 7): прогрессия данными, единая
        /// VillageLayout_01.asset (решение владельца — одна запечённая деревня на всю кампанию) и
        /// скриптованный таймлайн по образцу LevelConfig_01 (см. Level0XTimeline выше). Выполняется
        /// ТОЛЬКО при создании ассета (см. class-doc) — существующий файл не трогаем.
        /// </summary>
        private static void InitLevelConfig(SerializedObject so, LevelId id, int worshipTarget, int villagerCount,
            int coinsReward, (NeedType Type, float TriggerTime, int SiteIndex, float Duration)[] timeline)
        {
            so.FindProperty("_id").intValue = (int)id;
            so.FindProperty("_worshipTarget").intValue = worshipTarget;
            so.FindProperty("_villagerCount").intValue = villagerCount;
            so.FindProperty("_coinsReward").intValue = coinsReward;

            var layout = AssetDatabase.LoadAssetAtPath<VillageLayoutConfig>(VillageLayout01Path);
            if (layout == null)
                Debug.LogWarning($"[Editor] {id}: VillageLayout_01.asset не найден по пути {VillageLayout01Path} — _layout не прошит.");
            so.FindProperty("_layout").objectReferenceValue = layout;

            var timelineProp = so.FindProperty("_eventTimeline");
            timelineProp.arraySize = timeline.Length;
            for (int i = 0; i < timeline.Length; i++)
            {
                var element = timelineProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("type").intValue = (int)timeline[i].Type;
                element.FindPropertyRelative("triggerTime").floatValue = timeline[i].TriggerTime;
                element.FindPropertyRelative("targetSiteIndex").intValue = timeline[i].SiteIndex;
                element.FindPropertyRelative("duration").floatValue = timeline[i].Duration;
            }
        }

        /// <summary>
        /// Каталог кампании (шаг 7) — 5 записей LevelConfig_01..05 в порядке прохождения, превью —
        /// единая иконка (EditorSpriteUtility.LevelIconPaths.Preview), т.к. отдельных превью на
        /// уровень в паках не нашлось (допустимо контрактом задачи). Выполняется ТОЛЬКО при создании
        /// ассета — существующий каталог дозаполняет <see cref="MigrateLevelCatalogEntries"/>.
        /// </summary>
        private static void InitLevelCatalog(SerializedObject so)
        {
            var preview = EditorSpriteUtility.EnsureSpriteAtPath(EditorSpriteUtility.LevelIconPaths.Preview);
            var levelPaths = new[] { LevelConfig01Path, LevelConfig02Path, LevelConfig03Path, LevelConfig04Path, LevelConfig05Path };

            var entries = so.FindProperty("_entries");
            entries.arraySize = levelPaths.Length;
            bool anyMissing = false;
            for (int i = 0; i < levelPaths.Length; i++)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(levelPaths[i]);
                if (level == null) anyMissing = true;

                var element = entries.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_level").objectReferenceValue = level;
                element.FindPropertyRelative("_preview").objectReferenceValue = preview;
            }

            if (anyMissing)
                Debug.LogWarning("[Editor] LevelCatalog: одна или несколько ссылок на LevelConfig_01..05 не найдены при создании.");
        }

        /// <summary>
        /// Миграция каталога, уже существовавшего до шага 7 (Initializer выше — только при создании
        /// ассета, см. class-doc): дозаполняет недостающие записи (по числу — до 5) и недостающие
        /// превью в уже присутствующих записях, не трогая и не дублируя уже заполненные. Сопоставление
        /// "какая запись — какой LevelConfig" — по позиции (0..4 = Level01..05), т.к. записи создаются
        /// в фиксированном порядке прохождения.
        /// </summary>
        private static void MigrateLevelCatalogEntries()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalogConfig>(LevelCatalogPath);
            if (catalog == null) return; // ещё не создан — InitLevelCatalog при создании соберёт полный набор.

            var levelPaths = new[] { LevelConfig01Path, LevelConfig02Path, LevelConfig03Path, LevelConfig04Path, LevelConfig05Path };
            var preview = EditorSpriteUtility.EnsureSpriteAtPath(EditorSpriteUtility.LevelIconPaths.Preview);

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("_entries");

            int changed = 0;
            for (int i = 0; i < levelPaths.Length; i++)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(levelPaths[i]);

                if (i >= entries.arraySize)
                {
                    entries.InsertArrayElementAtIndex(entries.arraySize);
                    var newElement = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    newElement.FindPropertyRelative("_level").objectReferenceValue = level;
                    newElement.FindPropertyRelative("_preview").objectReferenceValue = preview;
                    changed++;
                    continue;
                }

                var element = entries.GetArrayElementAtIndex(i);
                var levelProp = element.FindPropertyRelative("_level");
                if (levelProp.objectReferenceValue == null && level != null)
                {
                    levelProp.objectReferenceValue = level;
                    changed++;
                }

                var previewProp = element.FindPropertyRelative("_preview");
                if (previewProp.objectReferenceValue == null && preview != null)
                {
                    previewProp.objectReferenceValue = preview;
                    changed++;
                }
            }

            if (changed == 0) return;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
            Debug.Log($"[Editor] LevelCatalog._entries: дозаполнено {changed} значени(й), всего записей {entries.arraySize}.");
        }

        /// <summary>Дождь (game-design.md §19.4): исцеляет Fire/Hunger, без удержания-накопления вреда.</summary>
        private static void InitRainMiracle(SerializedObject so)
        {
            so.FindProperty("_type").intValue = (int)MiracleType.Rain;
            so.FindProperty("_baseFaithCost").floatValue = 20f;
            so.FindProperty("_holdCostPerSecond").floatValue = 10f;
            so.FindProperty("_baseRadius").floatValue = 2f;
            so.FindProperty("_maxRadius").floatValue = 5f;
            so.FindProperty("_holdSecondsToMaxRadius").floatValue = 1.5f;
            so.FindProperty("_effectPower").floatValue = 0f;
            so.FindProperty("_alignmentShift").floatValue = 10f;
            // Шаг 5 (ревью): поле _worshipYield удалено из MiracleConfig — Worship больше не даётся
            // за факт применения чуда, начисляется WorshipService по Awe/Fear (см. WorshipConfig).
            so.FindProperty("_castVfx").intValue = (int)VfxEffectType.RainCast;
            so.FindProperty("_icon").objectReferenceValue = EditorSpriteUtility.EnsureSpriteAtPath(RainIconPath);

            var resolvesNeeds = so.FindProperty("_resolvesNeeds");
            resolvesNeeds.arraySize = 2;
            resolvesNeeds.GetArrayElementAtIndex(0).intValue = (int)NeedType.Fire;
            resolvesNeeds.GetArrayElementAtIndex(1).intValue = (int)NeedType.Hunger;

            so.FindProperty("_createsNeed").boolValue = false;
            so.FindProperty("_blastsVillagers").boolValue = false;
        }

        /// <summary>Фаербол (game-design.md §19.4): наносит урон, создаёт Fire-нужду, разбрасывает жителей.</summary>
        private static void InitFireballMiracle(SerializedObject so)
        {
            so.FindProperty("_type").intValue = (int)MiracleType.Fireball;
            so.FindProperty("_baseFaithCost").floatValue = 30f;
            so.FindProperty("_holdCostPerSecond").floatValue = 15f;
            so.FindProperty("_baseRadius").floatValue = 1.5f;
            so.FindProperty("_maxRadius").floatValue = 3.5f;
            so.FindProperty("_holdSecondsToMaxRadius").floatValue = 1.5f;
            so.FindProperty("_effectPower").floatValue = 8f;
            so.FindProperty("_alignmentShift").floatValue = -10f;
            // Шаг 5 (ревью): поле _worshipYield удалено из MiracleConfig — см. комментарий в InitRainMiracle.
            so.FindProperty("_castVfx").intValue = (int)VfxEffectType.FireballExplosion;
            so.FindProperty("_icon").objectReferenceValue = EditorSpriteUtility.EnsureSpriteAtPath(FireballIconPath);

            so.FindProperty("_resolvesNeeds").arraySize = 0;

            so.FindProperty("_createsNeed").boolValue = true;
            so.FindProperty("_createdNeed").intValue = (int)NeedType.Fire;
            so.FindProperty("_createdNeedDuration").floatValue = 15f;
            so.FindProperty("_blastsVillagers").boolValue = true;
        }

        /// <summary>Исцеление (game-design.md §4, шаг 6): разрешает Disease, без урона/разброса — зеркалит стоимость/радиусы Rain.</summary>
        private static void InitHealMiracle(SerializedObject so)
        {
            so.FindProperty("_type").intValue = (int)MiracleType.Heal;
            so.FindProperty("_baseFaithCost").floatValue = 20f; // зеркало Miracle_Rain
            so.FindProperty("_holdCostPerSecond").floatValue = 10f;
            so.FindProperty("_baseRadius").floatValue = 2f;
            so.FindProperty("_maxRadius").floatValue = 5f;
            so.FindProperty("_holdSecondsToMaxRadius").floatValue = 1.5f;
            so.FindProperty("_effectPower").floatValue = 0f;
            so.FindProperty("_alignmentShift").floatValue = 10f; // положительный, как у Rain — доброе чудо
            so.FindProperty("_castVfx").intValue = (int)VfxEffectType.HealCast;
            so.FindProperty("_icon").objectReferenceValue = EditorSpriteUtility.EnsureSpriteAtPath(HealIconPath);

            var resolvesNeeds = so.FindProperty("_resolvesNeeds");
            resolvesNeeds.arraySize = 1;
            resolvesNeeds.GetArrayElementAtIndex(0).intValue = (int)NeedType.Disease;

            so.FindProperty("_createsNeed").boolValue = false;
            so.FindProperty("_blastsVillagers").boolValue = false;

            SetBoolIfPresent(so, "_strikesVillagers", false, "Miracle_Heal");
        }

        /// <summary>Молния (game-design.md §4, шаг 6): точечный удар, поджигает дом (как Fireball) — стоимость/сила зеркалят Fireball.</summary>
        private static void InitLightningMiracle(SerializedObject so)
        {
            so.FindProperty("_type").intValue = (int)MiracleType.Lightning;
            so.FindProperty("_baseFaithCost").floatValue = 30f; // как у Fireball
            so.FindProperty("_holdCostPerSecond").floatValue = 15f; // как у Fireball
            so.FindProperty("_baseRadius").floatValue = 1.5f;
            so.FindProperty("_maxRadius").floatValue = 3f; // точечный удар (game-design §4) — меньше Fireball
            so.FindProperty("_holdSecondsToMaxRadius").floatValue = 1.5f;
            so.FindProperty("_effectPower").floatValue = 8f; // как у Fireball
            so.FindProperty("_alignmentShift").floatValue = -10f;
            so.FindProperty("_castVfx").intValue = (int)VfxEffectType.LightningStrike;
            so.FindProperty("_icon").objectReferenceValue = EditorSpriteUtility.EnsureSpriteAtPath(LightningIconPath);

            so.FindProperty("_resolvesNeeds").arraySize = 0;

            // Решение владельца 2026-09-01: молния поджигает здание, как Fireball.
            so.FindProperty("_createsNeed").boolValue = true;
            so.FindProperty("_createdNeed").intValue = (int)NeedType.Fire;
            so.FindProperty("_createdNeedDuration").floatValue = 15f; // как у Fireball
            so.FindProperty("_blastsVillagers").boolValue = false;

            SetBoolIfPresent(so, "_strikesVillagers", true, "Miracle_Lightning");
        }

        /// <summary>
        /// Прошивает bool-поле, если оно уже присутствует в классе на момент вызова; иначе
        /// предупреждает вместо NullReferenceException — контракт задачи вводит новое поле
        /// MiracleConfig._strikesVillagers параллельно с этим скриптом (code-writer).
        /// </summary>
        private static void SetBoolIfPresent(SerializedObject so, string propertyName, bool value, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — проверьте, что code-writer добавил это поле в MiracleConfig.");
                return;
            }

            prop.boolValue = value;
        }

        /// <summary>Каталог — ссылки грузятся по пути: все MiracleConfig уже созданы предыдущими строками Descriptors.</summary>
        private static void InitMiracleCatalog(SerializedObject so)
        {
            var rain = AssetDatabase.LoadAssetAtPath<MiracleConfig>(MiracleRainPath);
            var heal = AssetDatabase.LoadAssetAtPath<MiracleConfig>(MiracleHealPath);
            var fireball = AssetDatabase.LoadAssetAtPath<MiracleConfig>(MiracleFireballPath);
            var lightning = AssetDatabase.LoadAssetAtPath<MiracleConfig>(MiracleLightningPath);
            var all = new[] { rain, heal, fireball, lightning };

            var miracles = so.FindProperty("_miracles");
            miracles.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++)
                miracles.GetArrayElementAtIndex(i).objectReferenceValue = all[i];

            bool anyMissing = false;
            foreach (var m in all)
                if (m == null) anyMissing = true;

            if (anyMissing)
                Debug.LogWarning("[Editor] MiracleCatalog: одна или несколько ссылок (Rain/Heal/Fireball/Lightning) не найдены при создании.");
        }

        /// <summary>
        /// Миграция каталога, уже существовавшего до шага 6 (Initializer выше выполняется ТОЛЬКО
        /// при создании ассета — см. class-doc) — дописывает Heal/Lightning, не трогая и не дублируя
        /// уже присутствующие Rain/Fireball записи.
        /// </summary>
        private static void MigrateMiracleCatalogEntries()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MiracleCatalogConfig>(MiracleCatalogPath);
            if (catalog == null) return; // ещё не создан — InitMiracleCatalog при создании соберёт полный список.

            var rain = AssetDatabase.LoadAssetAtPath<MiracleConfig>(MiracleRainPath);
            var heal = AssetDatabase.LoadAssetAtPath<MiracleConfig>(MiracleHealPath);
            var fireball = AssetDatabase.LoadAssetAtPath<MiracleConfig>(MiracleFireballPath);
            var lightning = AssetDatabase.LoadAssetAtPath<MiracleConfig>(MiracleLightningPath);

            var so = new SerializedObject(catalog);
            var miracles = so.FindProperty("_miracles");

            var existing = new System.Collections.Generic.HashSet<UnityEngine.Object>();
            for (int i = 0; i < miracles.arraySize; i++)
            {
                var v = miracles.GetArrayElementAtIndex(i).objectReferenceValue;
                if (v != null) existing.Add(v);
            }

            int added = 0;
            foreach (var candidate in new MiracleConfig[] { rain, heal, fireball, lightning })
            {
                if (candidate == null || existing.Contains(candidate)) continue;

                int idx = miracles.arraySize;
                miracles.InsertArrayElementAtIndex(idx);
                miracles.GetArrayElementAtIndex(idx).objectReferenceValue = candidate;
                existing.Add(candidate);
                added++;
            }

            if (added == 0) return;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
            Debug.Log($"[Editor] MiracleCatalog._miracles: добавлено {added} новых запис(ей), всего {miracles.arraySize}.");
        }

        /// <summary>
        /// Миграция WorshipConfig, уже существовавшего до шага 6 (InitWorshipConfig выполняется
        /// ТОЛЬКО при создании ассета) — дописывает запись Fear для VillagerStruckByLightning,
        /// не трогая существующие записи (образец — добавление VillagerBlasted на шаге 5).
        /// </summary>
        private static void MigrateWorshipTerrorFearEntries()
        {
            var worship = AssetDatabase.LoadAssetAtPath<WorshipConfig>(WorshipConfigPath);
            if (worship == null) return; // ещё не создан — InitWorshipConfig при создании соберёт полный список.

            var so = new SerializedObject(worship);
            var fearByAct = so.FindProperty("_fearByAct");

            for (int i = 0; i < fearByAct.arraySize; i++)
            {
                var element = fearByAct.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("_act").intValue == (int)TerrorAct.VillagerStruckByLightning)
                    return; // уже есть — не дублируем.
            }

            int idx = fearByAct.arraySize;
            fearByAct.InsertArrayElementAtIndex(idx);
            var newElement = fearByAct.GetArrayElementAtIndex(idx);
            newElement.FindPropertyRelative("_act").intValue = (int)TerrorAct.VillagerStruckByLightning;
            newElement.FindPropertyRelative("_fear").intValue = 8;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(worship);
            Debug.Log("[Editor] WorshipConfig._fearByAct: добавлена запись VillagerStruckByLightning (Fear=8).");
        }

        /// <summary>
        /// Шаг 6 — Alignment-визуал (game-design.md §7, §10). Материалы читаются по фиксированным
        /// путям (см. константы выше) — обязаны существовать на диске к моменту создания ассета
        /// (создаются AlignmentVisualSetupEditor.EnsureMaterials() ДО ConfigAssetCreatorEditor.CreateAll()
        /// в SetupAlignmentVisual()). Контракт задачи: массив _stages отсортирован по _scoreThreshold
        /// УБЫВАЮЩЕ, Saint/Demon — пустые заготовки (решение владельца — визуал этих стейджей появится позже).
        /// </summary>
        private static void InitAlignmentVisualConfig(SerializedObject so)
        {
            var handNeutral = AssetDatabase.LoadAssetAtPath<Material>(HandNeutralMaterialPath);
            var handKind = AssetDatabase.LoadAssetAtPath<Material>(HandKindMaterialPath);
            var handCruel = AssetDatabase.LoadAssetAtPath<Material>(HandCruelMaterialPath);
            var templeNeutral = AssetDatabase.LoadAssetAtPath<Material>(TempleNeutralMaterialPath);
            var templeNeutralB = AssetDatabase.LoadAssetAtPath<Material>(TempleNeutralBMaterialPath);
            var templeKind = AssetDatabase.LoadAssetAtPath<Material>(TempleKindMaterialPath);
            var templeKindB = AssetDatabase.LoadAssetAtPath<Material>(TempleKindBMaterialPath);
            var templeCruel = AssetDatabase.LoadAssetAtPath<Material>(TempleCruelMaterialPath);
            var templeCruelB = AssetDatabase.LoadAssetAtPath<Material>(TempleCruelBMaterialPath);

            // Temple — параллельный массив к _templeRenderers (два MeshRenderer, ревью шага 6, п.2):
            // [0] = основной материал, [1] = "_B". Рука — по одному материалу, как было.
            var stageDefs = new (float Threshold, Material[] Hand, Material[] Temple)[]
            {
                (75f, new Material[0], new Material[0]),                                  // Saint — пустая заготовка
                (25f, new[] { handKind }, new[] { templeKind, templeKindB }),               // Kind
                (-25f, new[] { handNeutral }, new[] { templeNeutral, templeNeutralB }),      // Neutral
                (-75f, new[] { handCruel }, new[] { templeCruel, templeCruelB }),            // Cruel
                (-100f, new Material[0], new Material[0]),                                 // Demon — пустая заготовка
            };

            var stagesProp = so.FindProperty("_stages");
            stagesProp.arraySize = stageDefs.Length;
            for (int i = 0; i < stageDefs.Length; i++)
            {
                var element = stagesProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_scoreThreshold").floatValue = stageDefs[i].Threshold;

                var handProp = element.FindPropertyRelative("_handMaterials");
                handProp.arraySize = stageDefs[i].Hand.Length;
                for (int j = 0; j < stageDefs[i].Hand.Length; j++)
                    handProp.GetArrayElementAtIndex(j).objectReferenceValue = stageDefs[i].Hand[j];

                var templeProp = element.FindPropertyRelative("_templeMaterials");
                templeProp.arraySize = stageDefs[i].Temple.Length;
                for (int j = 0; j < stageDefs[i].Temple.Length; j++)
                    templeProp.GetArrayElementAtIndex(j).objectReferenceValue = stageDefs[i].Temple[j];

                element.FindPropertyRelative("_decalPrefabsToSpawn").arraySize = 0;
                element.FindPropertyRelative("_vfxToActivate").arraySize = 0;
            }

            if (handNeutral == null || handKind == null || handCruel == null ||
                templeNeutral == null || templeKind == null || templeCruel == null ||
                templeNeutralB == null || templeKindB == null || templeCruelB == null)
            {
                Debug.LogWarning("[Editor] AlignmentVisualConfig: один или несколько материалов не найдены при создании — " +
                                  "сначала вызовите \"BlackWhiteLike/Setup/Setup Alignment Visual\" (создаёт материалы ДО конфига).");
            }
        }

        /// <summary>
        /// Шаг 5 — Worship + win (game-design.md §19.5). Массивы вложенных [Serializable]-элементов
        /// (NeedAweEntry/TerrorFearEntry) прошиваются через arraySize + FindPropertyRelative на каждом
        /// элементе — сериализация SerializedObject не поддерживает прямое присваивание C#-массива.
        /// </summary>
        private static void InitWorshipConfig(SerializedObject so)
        {
            var aweByNeed = so.FindProperty("_aweByNeed");
            var aweEntries = new (NeedType Type, int Awe)[]
            {
                (NeedType.Fire, 20),
                (NeedType.Disease, 20),
                (NeedType.Hunger, 15),
            };
            aweByNeed.arraySize = aweEntries.Length;
            for (int i = 0; i < aweEntries.Length; i++)
            {
                var element = aweByNeed.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_type").intValue = (int)aweEntries[i].Type;
                element.FindPropertyRelative("_awe").intValue = aweEntries[i].Awe;
            }

            var fearByAct = so.FindProperty("_fearByAct");
            var fearEntries = new (TerrorAct Act, int Fear)[]
            {
                (TerrorAct.FireballOnStructure, 25),
                (TerrorAct.LightningStrike, 20),
                (TerrorAct.VillagerHardLanding, 8),
                (TerrorAct.ObjectHitStructure, 10),
                // Playtest шага 5 (решение владельца): взрыв чуда — отдельная ставка, не «как рука».
                (TerrorAct.VillagerBlasted, 6),
                // Шаг 6 (ревью, ВАЖНО 5): та же ставка, что MigrateWorshipTerrorFearEntries дописывает
                // существующим ассетам — единственный канонический источник, запись не должна жить
                // только в миграции (CLAUDE.md — "дублирование строковых ключей"/единственный источник).
                (TerrorAct.VillagerStruckByLightning, 8),
            };
            fearByAct.arraySize = fearEntries.Length;
            for (int i = 0; i < fearEntries.Length; i++)
            {
                var element = fearByAct.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_act").intValue = (int)fearEntries[i].Act;
                element.FindPropertyRelative("_fear").intValue = fearEntries[i].Fear;
            }

            so.FindProperty("_worshipDecayPerSecond").floatValue = 0f;
            so.FindProperty("_handTerrorAlignmentShift").floatValue = -3f;
        }

        /// <summary>
        /// Миграция AlignmentVisual.asset, уже существовавшего до появления материалов храма/этого
        /// инициализатора (Initializer выше выполняется ТОЛЬКО при создании ассета, см. class-doc) —
        /// ревью шага 6, ВАЖНО 3. Дозаполняет ПУСТЫЕ массивы материалов по стейджам, сопоставляя по
        /// <c>_scoreThreshold</c> (тот же порог, что задаёт <see cref="InitAlignmentVisualConfig"/>);
        /// уже заполненные массивы не трогает (правки владельца дороже дефолта — тот же принцип, что
        /// у остальных Create/Migrate в этом файле). Вызывается и из <see cref="CreateAll"/>, и из
        /// <c>AlignmentVisualSetupEditor</c> после <c>EnsureMaterials</c>/<c>EnsureTempleMaterials</c> —
        /// поэтому <c>internal</c>, а не <c>private</c>.
        /// </summary>
        internal static void MigrateAlignmentVisualStages()
        {
            var config = AssetDatabase.LoadAssetAtPath<AlignmentVisualConfig>(AlignmentVisualConfigPath);
            if (config == null) return; // ещё не создан — InitAlignmentVisualConfig при создании соберёт полный набор.

            var handNeutral = AssetDatabase.LoadAssetAtPath<Material>(HandNeutralMaterialPath);
            var handKind = AssetDatabase.LoadAssetAtPath<Material>(HandKindMaterialPath);
            var handCruel = AssetDatabase.LoadAssetAtPath<Material>(HandCruelMaterialPath);
            var templeNeutral = AssetDatabase.LoadAssetAtPath<Material>(TempleNeutralMaterialPath);
            var templeNeutralB = AssetDatabase.LoadAssetAtPath<Material>(TempleNeutralBMaterialPath);
            var templeKind = AssetDatabase.LoadAssetAtPath<Material>(TempleKindMaterialPath);
            var templeKindB = AssetDatabase.LoadAssetAtPath<Material>(TempleKindBMaterialPath);
            var templeCruel = AssetDatabase.LoadAssetAtPath<Material>(TempleCruelMaterialPath);
            var templeCruelB = AssetDatabase.LoadAssetAtPath<Material>(TempleCruelBMaterialPath);

            // Канонический набор порогов/материалов — тот же, что InitAlignmentVisualConfig. Saint/Demon
            // сюда не входят: их массивы — намеренно пустые заготовки, а не "не заполненные" данные.
            var stageDefs = new (float Threshold, Material[] Hand, Material[] Temple)[]
            {
                (25f, new[] { handKind }, new[] { templeKind, templeKindB }),
                (-25f, new[] { handNeutral }, new[] { templeNeutral, templeNeutralB }),
                (-75f, new[] { handCruel }, new[] { templeCruel, templeCruelB }),
            };

            var so = new SerializedObject(config);
            var stagesProp = so.FindProperty("_stages");
            int patched = 0;

            for (int i = 0; i < stagesProp.arraySize; i++)
            {
                var element = stagesProp.GetArrayElementAtIndex(i);
                var threshold = element.FindPropertyRelative("_scoreThreshold").floatValue;

                foreach (var def in stageDefs)
                {
                    if (!Mathf.Approximately(threshold, def.Threshold)) continue;

                    patched += PatchMaterialArrayIfEmpty(element.FindPropertyRelative("_handMaterials"), def.Hand, $"стейдж {threshold} (hand)");
                    patched += PatchMaterialArrayIfEmpty(element.FindPropertyRelative("_templeMaterials"), def.Temple, $"стейдж {threshold} (temple)");
                    break;
                }
            }

            if (patched == 0) return;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            Debug.Log($"[Editor] AlignmentVisual.asset: дозаполнено {patched} пустых массив(ов) материалов.");
        }

        /// <summary>
        /// Заполняет массив материалов, только если на диске он пуст (нет элементов либо все
        /// элементы null) — уже заполненный массив не трогается ни при каком раскладе. Источник
        /// с отсутствующим (null) материалом пропускается целиком с warning — частично заполненный
        /// массив хуже, чем оставленный пустым до следующего прогона.
        /// </summary>
        private static int PatchMaterialArrayIfEmpty(SerializedProperty arrayProp, Material[] materials, string label)
        {
            bool isEmpty = true;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue != null) { isEmpty = false; break; }
            }

            if (!isEmpty) return 0;
            if (materials == null || materials.Length == 0) return 0;

            foreach (var material in materials)
            {
                if (material == null)
                {
                    Debug.LogWarning($"[Editor] AlignmentVisual.asset: для {label} не все материалы найдены на диске — миграция пропущена, прогоните \"Setup Alignment Visual\" ещё раз.");
                    return 0;
                }
            }

            arrayProp.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++)
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];

            return 1;
        }

        [MenuItem("BlackWhiteLike/Configs/Create Empty SO Set")]
        public static void CreateAll()
        {
            int created = 0;
            int skipped = 0;
            int resynced = 0;

            foreach (var descriptor in Descriptors)
            {
                if (CreateIfMissing(descriptor, out bool wasResynced))
                {
                    created++;
                }
                else
                {
                    skipped++;
                    if (wasResynced) resynced++;
                }
            }

            // Шаг 6 — миграции уже существующих ассетов (Initializer выше срабатывает только при
            // создании, см. class-doc): Heal/Lightning в каталог, Fear-запись молнии в WorshipConfig,
            // пустые ссылки материалов AlignmentVisual (ревью шага 6, ВАЖНО 3).
            MigrateMiracleCatalogEntries();
            MigrateWorshipTerrorFearEntries();
            MigrateAlignmentVisualStages();
            // Шаг 7 — Кампания из 5 уровней.
            MigrateLevelCatalogEntries();
            // Шаг 8 — Meta + idle. MigrateRewardConfig() удалена (мелочь ревью шага 8) — см.
            // комментарий на месте бывшего метода, недостижимая ветка "если 0".
            MigrateUpgradeCatalogEntries();
            MigrateIdleIncomeConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Editor] Create Empty SO Set завершён: создано {created}, пропущено (уже существует) {skipped} " +
                      $"(из них пересериализовано под новые поля класса: {resynced}).");
        }

        /// <summary>
        /// Создаёт один .asset, если его ещё нет. Возвращает true, если ассет был создан. Если ассет
        /// уже существовал, помечает его грязным (<paramref name="resynced"/> = true) —
        /// AssetDatabase.SaveAssets() в CreateAll допишет в YAML любые поля класса, которых там ещё
        /// нет (см. class-doc — "Пересериализация существующих ассетов"), не трогая значения полей,
        /// уже присутствующих на диске.
        /// </summary>
        private static bool CreateIfMissing(ConfigAssetDescriptor descriptor, out bool resynced)
        {
            resynced = false;

            if (File.Exists(descriptor.AssetPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath(descriptor.AssetPath, descriptor.SoType);
                if (existing != null)
                {
                    EditorUtility.SetDirty(existing);
                    resynced = true;
                    Debug.Log($"[Editor] {descriptor.AssetPath} уже существует, пропущен (создание) — " +
                              "помечен грязным для пересериализации новых полей класса (текущие значения не тронуты).");
                }
                else
                {
                    Debug.LogWarning($"[Editor] {descriptor.AssetPath} существует, но не загрузился как " +
                                      $"{descriptor.SoType.Name} — пересериализация пропущена.");
                }

                return false;
            }

            EnsureFolder(Path.GetDirectoryName(descriptor.AssetPath).Replace('\\', '/'));

            var instance = ScriptableObject.CreateInstance(descriptor.SoType);
            AssetDatabase.CreateAsset(instance, descriptor.AssetPath);
            // Undo на ассетах не регистрируем — см. .claude/rules/editor-files.md.

            if (descriptor.Initializer != null)
            {
                var so = new SerializedObject(instance);
                descriptor.Initializer(so);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(instance);
            }

            Debug.Log($"[Editor] Создан конфиг: {descriptor.AssetPath}" +
                      (descriptor.Initializer != null ? " (значения по умолчанию прошиты инициализатором)." : "."));
            return true;
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

        /// <summary>
        /// Описание одного создаваемого SO-ассета: тип класса + целевой путь + опциональный
        /// инициализатор дефолтных значений, вызываемый ТОЛЬКО в момент создания ассета (шаг 4,
        /// game-design.md §19.4) — при пересериализации существующего ассета не выполняется,
        /// чтобы не затирать значения, подкрученные владельцем в Inspector.
        /// </summary>
        private readonly struct ConfigAssetDescriptor
        {
            public readonly Type SoType;
            public readonly string AssetPath;
            public readonly Action<SerializedObject> Initializer;

            public ConfigAssetDescriptor(Type soType, string assetPath, Action<SerializedObject> initializer = null)
            {
                SoType = soType;
                AssetPath = assetPath;
                Initializer = initializer;
            }
        }
    }
}
