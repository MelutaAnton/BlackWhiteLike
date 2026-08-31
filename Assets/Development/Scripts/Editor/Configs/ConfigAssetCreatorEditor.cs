using System;
using System.IO;
using BlackWhiteLike.Data.Configs.Alignment;
using BlackWhiteLike.Data.Configs.Camera;
using BlackWhiteLike.Data.Configs.Faith;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Data.Configs.Throwable;
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
        };

        /// <summary>Дождь (game-design.md §19.4): исцеляет Fire/Hunger, без удержания-накопления вреда.</summary>
        private static void InitRainMiracle(SerializedObject so)
        {
            so.FindProperty("_type").enumValueIndex = (int)MiracleType.Rain;
            so.FindProperty("_baseFaithCost").floatValue = 20f;
            so.FindProperty("_holdCostPerSecond").floatValue = 10f;
            so.FindProperty("_baseRadius").floatValue = 2f;
            so.FindProperty("_maxRadius").floatValue = 5f;
            so.FindProperty("_holdSecondsToMaxRadius").floatValue = 1.5f;
            so.FindProperty("_effectPower").floatValue = 0f;
            so.FindProperty("_alignmentShift").floatValue = 10f;
            // Шаг 5 (ревью): поле _worshipYield удалено из MiracleConfig — Worship больше не даётся
            // за факт применения чуда, начисляется WorshipService по Awe/Fear (см. WorshipConfig).
            so.FindProperty("_castVfx").enumValueIndex = (int)VfxEffectType.RainCast;
            so.FindProperty("_icon").objectReferenceValue = EditorSpriteUtility.EnsureSpriteAtPath(RainIconPath);

            var resolvesNeeds = so.FindProperty("_resolvesNeeds");
            resolvesNeeds.arraySize = 2;
            resolvesNeeds.GetArrayElementAtIndex(0).enumValueIndex = (int)NeedType.Fire;
            resolvesNeeds.GetArrayElementAtIndex(1).enumValueIndex = (int)NeedType.Hunger;

            so.FindProperty("_createsNeed").boolValue = false;
            so.FindProperty("_blastsVillagers").boolValue = false;
        }

        /// <summary>Фаербол (game-design.md §19.4): наносит урон, создаёт Fire-нужду, разбрасывает жителей.</summary>
        private static void InitFireballMiracle(SerializedObject so)
        {
            so.FindProperty("_type").enumValueIndex = (int)MiracleType.Fireball;
            so.FindProperty("_baseFaithCost").floatValue = 30f;
            so.FindProperty("_holdCostPerSecond").floatValue = 15f;
            so.FindProperty("_baseRadius").floatValue = 1.5f;
            so.FindProperty("_maxRadius").floatValue = 3.5f;
            so.FindProperty("_holdSecondsToMaxRadius").floatValue = 1.5f;
            so.FindProperty("_effectPower").floatValue = 8f;
            so.FindProperty("_alignmentShift").floatValue = -10f;
            // Шаг 5 (ревью): поле _worshipYield удалено из MiracleConfig — см. комментарий в InitRainMiracle.
            so.FindProperty("_castVfx").enumValueIndex = (int)VfxEffectType.FireballExplosion;
            so.FindProperty("_icon").objectReferenceValue = EditorSpriteUtility.EnsureSpriteAtPath(FireballIconPath);

            so.FindProperty("_resolvesNeeds").arraySize = 0;

            so.FindProperty("_createsNeed").boolValue = true;
            so.FindProperty("_createdNeed").enumValueIndex = (int)NeedType.Fire;
            so.FindProperty("_createdNeedDuration").floatValue = 15f;
            so.FindProperty("_blastsVillagers").boolValue = true;
        }

        /// <summary>Исцеление (game-design.md §4, шаг 6): разрешает Disease, без урона/разброса — зеркалит стоимость/радиусы Rain.</summary>
        private static void InitHealMiracle(SerializedObject so)
        {
            so.FindProperty("_type").enumValueIndex = (int)MiracleType.Heal;
            so.FindProperty("_baseFaithCost").floatValue = 20f; // зеркало Miracle_Rain
            so.FindProperty("_holdCostPerSecond").floatValue = 10f;
            so.FindProperty("_baseRadius").floatValue = 2f;
            so.FindProperty("_maxRadius").floatValue = 5f;
            so.FindProperty("_holdSecondsToMaxRadius").floatValue = 1.5f;
            so.FindProperty("_effectPower").floatValue = 0f;
            so.FindProperty("_alignmentShift").floatValue = 10f; // положительный, как у Rain — доброе чудо
            so.FindProperty("_castVfx").enumValueIndex = (int)VfxEffectType.HealCast;
            so.FindProperty("_icon").objectReferenceValue = EditorSpriteUtility.EnsureSpriteAtPath(HealIconPath);

            var resolvesNeeds = so.FindProperty("_resolvesNeeds");
            resolvesNeeds.arraySize = 1;
            resolvesNeeds.GetArrayElementAtIndex(0).enumValueIndex = (int)NeedType.Disease;

            so.FindProperty("_createsNeed").boolValue = false;
            so.FindProperty("_blastsVillagers").boolValue = false;

            SetBoolIfPresent(so, "_strikesVillagers", false, "Miracle_Heal");
        }

        /// <summary>Молния (game-design.md §4, шаг 6): точечный удар, поджигает дом (как Fireball) — стоимость/сила зеркалят Fireball.</summary>
        private static void InitLightningMiracle(SerializedObject so)
        {
            so.FindProperty("_type").enumValueIndex = (int)MiracleType.Lightning;
            so.FindProperty("_baseFaithCost").floatValue = 30f; // как у Fireball
            so.FindProperty("_holdCostPerSecond").floatValue = 15f; // как у Fireball
            so.FindProperty("_baseRadius").floatValue = 1.5f;
            so.FindProperty("_maxRadius").floatValue = 3f; // точечный удар (game-design §4) — меньше Fireball
            so.FindProperty("_holdSecondsToMaxRadius").floatValue = 1.5f;
            so.FindProperty("_effectPower").floatValue = 8f; // как у Fireball
            so.FindProperty("_alignmentShift").floatValue = -10f;
            so.FindProperty("_castVfx").enumValueIndex = (int)VfxEffectType.LightningStrike;
            so.FindProperty("_icon").objectReferenceValue = EditorSpriteUtility.EnsureSpriteAtPath(LightningIconPath);

            so.FindProperty("_resolvesNeeds").arraySize = 0;

            // Решение владельца 2026-09-01: молния поджигает здание, как Fireball.
            so.FindProperty("_createsNeed").boolValue = true;
            so.FindProperty("_createdNeed").enumValueIndex = (int)NeedType.Fire;
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
                if (element.FindPropertyRelative("_act").enumValueIndex == (int)TerrorAct.VillagerStruckByLightning)
                    return; // уже есть — не дублируем.
            }

            int idx = fearByAct.arraySize;
            fearByAct.InsertArrayElementAtIndex(idx);
            var newElement = fearByAct.GetArrayElementAtIndex(idx);
            newElement.FindPropertyRelative("_act").enumValueIndex = (int)TerrorAct.VillagerStruckByLightning;
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
                element.FindPropertyRelative("_type").enumValueIndex = (int)aweEntries[i].Type;
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
                element.FindPropertyRelative("_act").enumValueIndex = (int)fearEntries[i].Act;
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
