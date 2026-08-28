using System;
using System.IO;
using BlackWhiteLike.Data.Configs.Camera;
using BlackWhiteLike.Data.Configs.Faith;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Data.Configs.Throwable;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Data.Configs.Vfx;
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
        private const string MiracleCatalogPath = ConfigsRoot + "/Miracles/MiracleCatalog.asset";

        // Иконки — единственный источник путей: BlackWhiteLike.Editor.Common.EditorSpriteUtility.MiracleIconPaths
        // (правка после ревью: строки дублировались здесь и в Editor/UI/LevelHudSetupEditor.cs).
        private const string RainIconPath = EditorSpriteUtility.MiracleIconPaths.Rain;
        private const string FireballIconPath = EditorSpriteUtility.MiracleIconPaths.Fireball;

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
            new ConfigAssetDescriptor(typeof(MiracleCatalogConfig), MiracleCatalogPath, InitMiracleCatalog),
            new ConfigAssetDescriptor(typeof(FaithConfig), ConfigsRoot + "/Faith/FaithConfig.asset"),
            // _entries прошивает Editor/Prefabs/MiracleVfxPrefabSetupEditor после сборки VFX-префабов.
            new ConfigAssetDescriptor(typeof(VfxConfig), ConfigsRoot + "/Vfx/VfxConfig.asset"),
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
            so.FindProperty("_worshipYield").intValue = 10;
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
            so.FindProperty("_worshipYield").intValue = 15;
            so.FindProperty("_castVfx").enumValueIndex = (int)VfxEffectType.FireballExplosion;
            so.FindProperty("_icon").objectReferenceValue = EditorSpriteUtility.EnsureSpriteAtPath(FireballIconPath);

            so.FindProperty("_resolvesNeeds").arraySize = 0;

            so.FindProperty("_createsNeed").boolValue = true;
            so.FindProperty("_createdNeed").enumValueIndex = (int)NeedType.Fire;
            so.FindProperty("_createdNeedDuration").floatValue = 15f;
            so.FindProperty("_blastsVillagers").boolValue = true;
        }

        /// <summary>Каталог — ссылки грузятся по пути: оба MiracleConfig уже созданы предыдущими строками Descriptors.</summary>
        private static void InitMiracleCatalog(SerializedObject so)
        {
            var rain = AssetDatabase.LoadAssetAtPath<MiracleConfig>(MiracleRainPath);
            var fireball = AssetDatabase.LoadAssetAtPath<MiracleConfig>(MiracleFireballPath);

            var miracles = so.FindProperty("_miracles");
            miracles.arraySize = 2;
            miracles.GetArrayElementAtIndex(0).objectReferenceValue = rain;
            miracles.GetArrayElementAtIndex(1).objectReferenceValue = fireball;

            if (rain == null || fireball == null)
                Debug.LogWarning("[Editor] MiracleCatalog: одна из ссылок (Miracle_Rain/Miracle_Fireball) не найдена при создании.");
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
