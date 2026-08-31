using System.IO;
using BlackWhiteLike.Controllers.Hand;
using BlackWhiteLike.Controllers.Temple;
using BlackWhiteLike.Data.Configs.Alignment;
using BlackWhiteLike.Editor.Configs;
using BlackWhiteLike.Editor.Prefabs;
using BlackWhiteLike.Installers;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Шаг 6 — Alignment-визуал (game-design.md §7, §10). Собирает материалы alignment-стейджей
    /// (Hand_Kind/Hand_Cruel — тинтованные копии Hand_Neutral.mat; Temple_Neutral — копия фактического
    /// материала меша храма, Temple_Kind/Temple_Cruel — тинтованные копии Temple_Neutral), запускает
    /// создание <c>AlignmentVisual.asset</c> (ConfigAssetCreatorEditor — единственное место, где
    /// перечислены создаваемые SO проекта) и прошивает сценный <c>[AlignmentVisual]</c>
    /// (<see cref="AlignmentVisualController"/>: _handRenderers/_templeRenderers) + ссылки на
    /// <see cref="GameLifetimeScope"/>.
    /// Пути материалов ДУБЛИРУЮТ константы Editor/Configs/ConfigAssetCreatorEditor.cs (тот же приём,
    /// что у путей VFX-префабов между MiracleVfxPrefabSetupEditor/TestVillageSceneSetupEditor —
    /// избегаем циклической загрузки одного файла из другого при инициализации статических полей).
    /// Идемпотентен: материалы не пересоздаются (GUID сохраняется), рендереры перепрошиваются на
    /// каждом прогоне (производные данные — состав рендереров может измениться при правке префабов).
    /// </summary>
    public static class AlignmentVisualSetupEditor
    {
        private const string HandMaterialsFolder = "Assets/Development/Art/Materials/Hand";
        private const string TempleMaterialsFolder = "Assets/Development/Art/Materials/Temple";
        private const string HandNeutralMaterialPath = HandMaterialsFolder + "/Hand_Neutral.mat";
        private const string HandKindMaterialPath = HandMaterialsFolder + "/Hand_Kind.mat";
        private const string HandCruelMaterialPath = HandMaterialsFolder + "/Hand_Cruel.mat";
        private const string TempleNeutralMaterialPath = TempleMaterialsFolder + "/Temple_Neutral.mat";
        private const string TempleKindMaterialPath = TempleMaterialsFolder + "/Temple_Kind.mat";
        private const string TempleCruelMaterialPath = TempleMaterialsFolder + "/Temple_Cruel.mat";
        // Ревью шага 6 (ВАЖНО 2): Temple (Synty Church_01_A) собран из ДВУХ MeshRenderer с разными
        // материалами — второй рендерер получает "_B"-набор, параллельный первому (см. xml-doc
        // AlignmentStage в Data/Configs/Alignment/AlignmentVisualConfig.cs).
        private const string TempleNeutralBMaterialPath = TempleMaterialsFolder + "/Temple_Neutral_B.mat";
        private const string TempleKindBMaterialPath = TempleMaterialsFolder + "/Temple_Kind_B.mat";
        private const string TempleCruelBMaterialPath = TempleMaterialsFolder + "/Temple_Cruel_B.mat";

        private const string AlignmentVisualConfigPath = "Assets/Development/Data/Configs/Alignment/AlignmentVisual.asset";

        // Тёплый золотистый (Kind) / тёмно-красный (Cruel) — решение владельца (задача шага 6).
        private static readonly Color KindTint = new Color(1f, 0.85f, 0.5f, 1f);
        private static readonly Color CruelTint = new Color(0.45f, 0.05f, 0.05f, 1f);

        [MenuItem("BlackWhiteLike/Setup/Setup Alignment Visual")]
        public static void SetupAlignmentVisual()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Setup Alignment Visual");
            int undoGroup = Undo.GetCurrentGroup();

            SetupAlignmentVisualCore();

            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Тело Wizard'а без управления Undo-группой (ревью шага 6, п.6) — вызывается напрямую из
        /// <see cref="TestVillageSceneSetupEditor"/>, у которого уже есть собственная внешняя
        /// Undo-группа; вложенный <c>Undo.CollapseUndoOperations</c> внутри неё рвал бы внешнюю
        /// группу пополам. Пункт меню <see cref="SetupAlignmentVisual"/> — единственное место, где
        /// эта группа заводится сама.
        /// </summary>
        internal static void SetupAlignmentVisualCore()
        {
            // Ручные материалы всегда можно собрать/дособрать (ревью шага 6, п.6) — Temple.prefab
            // им не нужен. Материалы храма — отдельным шагом ниже, после гарантии, что Temple.prefab
            // существует (EnsureTempleMaterials сама варнинг вместо LogError, если его всё ещё нет).
            EnsureHandMaterials();

            // Temple.prefab — предпосылка для материалов храма (снимаются с его фактических материалов).
            var templePrefabPath = VillageStructurePrefabSetupEditor.GetPrefabPath("Temple");
            if (!File.Exists(templePrefabPath))
            {
                Debug.Log("[Editor] Temple.prefab не найден — вызываю VillageStructurePrefabSetupEditor.SetupVillageStructurePrefabs().");
                VillageStructurePrefabSetupEditor.SetupVillageStructurePrefabs();
            }

            EnsureTempleMaterials();

            // AlignmentVisual.asset — создаётся ConfigAssetCreatorEditor.CreateAll() (единственное
            // место, где перечислены создаваемые SO проекта), материалы к этому моменту уже на диске.
            // Для уже существовавшего ассета CreateAll() сам вызывает миграцию пустых ссылок.
            ConfigAssetCreatorEditor.CreateAll();
            var config = AssetDatabase.LoadAssetAtPath<AlignmentVisualConfig>(AlignmentVisualConfigPath);
            if (config == null)
                Debug.LogWarning("[Editor] " + AlignmentVisualConfigPath + " не найден после CreateAll() — _alignmentVisualConfig не прошит.");

            var go = TestHandSceneSetupEditor.FindInSceneIncludingInactive("[AlignmentVisual]");
            if (go == null)
            {
                go = new GameObject("[AlignmentVisual]");
                Undo.RegisterCreatedObjectUndo(go, "Create [AlignmentVisual]");
                Debug.Log("[Editor] Создан объект \"[AlignmentVisual]\".");
            }

            var controller = go.GetComponent<AlignmentVisualController>();
            if (controller == null)
                controller = Undo.AddComponent<AlignmentVisualController>(go);

            var godHand = Object.FindAnyObjectByType<GodHandController>(FindObjectsInactive.Include);
            var temple = TestHandSceneSetupEditor.FindInSceneIncludingInactive("Temple");

            if (godHand == null)
                Debug.LogWarning("[Editor] GodHandController не найден в текущей сцене — _handRenderers не прошиты.");
            if (temple == null)
                Debug.LogWarning("[Editor] \"Temple\" не найден в текущей сцене — _templeRenderers не прошиты.");

            var handRenderers = godHand != null ? godHand.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
            var templeRenderers = temple != null ? temple.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();

            var controllerSo = new SerializedObject(controller);
            WireRendererArray(controllerSo, "_handRenderers", handRenderers, "AlignmentVisualController");
            WireRendererArray(controllerSo, "_templeRenderers", templeRenderers, "AlignmentVisualController");
            controllerSo.ApplyModifiedProperties();

            var scope = Object.FindAnyObjectByType<GameLifetimeScope>(FindObjectsInactive.Include);
            if (scope != null)
            {
                var scopeSo = new SerializedObject(scope);
                SetRef(scopeSo, "_alignmentVisualConfig", config, "GameLifetimeScope");
                SetRef(scopeSo, "_alignmentVisualController", controller, "GameLifetimeScope");
                scopeSo.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в текущей сцене — _alignmentVisualConfig/_alignmentVisualController не прошиты.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Setup Alignment Visual завершён.");
        }

        // ------------------------------------------------------------------------------------
        // Материалы (задача 4 — только ассетами, правило "всё, на что ссылается ассет, — ассет")
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Публичный — используется и SetupAlignmentVisual, и (при необходимости) другими Wizard'ами.
        /// Собирает ВСЕ материалы (hand + temple) безусловно — temple-часть сама переживает
        /// отсутствующий Temple.prefab через LogWarning (см. <see cref="EnsureTempleMaterials"/>).
        /// В <see cref="SetupAlignmentVisualCore"/> hand/temple вызываются раздельно, чтобы temple
        /// шёл строго после гарантии существования Temple.prefab (ревью шага 6, п.6).
        /// </summary>
        public static void EnsureMaterials()
        {
            EnsureHandMaterials();
            EnsureTempleMaterials();
        }

        private static void EnsureHandMaterials()
        {
            EnsureFolder(HandMaterialsFolder);
            EnsureTintedCopy(HandNeutralMaterialPath, HandKindMaterialPath, KindTint, "Hand — Kind");
            EnsureTintedCopy(HandNeutralMaterialPath, HandCruelMaterialPath, CruelTint, "Hand — Cruel");
        }

        /// <summary>
        /// Материалы храма требуют существующего Temple.prefab (см. <see cref="EnsureTempleNeutralMaterials"/>).
        /// Ревью шага 6, п.6: раньше это условие проверялось ПОСЛЕ вызова, что гарантировало LogError
        /// на чистом прогоне ("Setup Alignment Visual" до "Setup Village Structure Prefabs") — теперь
        /// явная проверка здесь, LogWarning с понятной инструкцией вместо LogError.
        /// </summary>
        private static void EnsureTempleMaterials()
        {
            EnsureFolder(TempleMaterialsFolder);

            var templePrefabPath = VillageStructurePrefabSetupEditor.GetPrefabPath("Temple");
            if (!File.Exists(templePrefabPath))
            {
                Debug.LogWarning("[Editor] " + templePrefabPath + " не найден — материалы храма не созданы. " +
                                  "Сначала выполните \"BlackWhiteLike/Prefabs/Setup Village Structure Prefabs\", " +
                                  "затем \"BlackWhiteLike/Setup/Setup Alignment Visual\" ещё раз.");
                return;
            }

            EnsureTempleNeutralMaterials();

            if (AssetDatabase.LoadAssetAtPath<Material>(TempleNeutralMaterialPath) != null)
            {
                EnsureTintedCopy(TempleNeutralMaterialPath, TempleKindMaterialPath, KindTint, "Temple — Kind");
                EnsureTintedCopy(TempleNeutralMaterialPath, TempleCruelMaterialPath, CruelTint, "Temple — Cruel");
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(TempleNeutralBMaterialPath) != null)
            {
                EnsureTintedCopy(TempleNeutralBMaterialPath, TempleKindBMaterialPath, KindTint, "Temple B — Kind");
                EnsureTintedCopy(TempleNeutralBMaterialPath, TempleCruelBMaterialPath, CruelTint, "Temple B — Cruel");
            }
        }

        /// <summary>
        /// Temple_Neutral.mat / Temple_Neutral_B.mat — копии ФАКТИЧЕСКИХ материалов ОБОИХ
        /// MeshRenderer храма (ревью шага 6: Synty Church_01_A собран из двух рендереров с разными
        /// материалами — раньше снимался только материал первого, а второй рендерер молча
        /// перекрашивался чужим материалом в ApplyMaterials). Порядок копирования — порядок обхода
        /// GetComponentsInChildren&lt;MeshRenderer&gt;, тот же порядок, в котором SetupAlignmentVisualCore
        /// прошивает _templeRenderers на AlignmentVisualController (см. xml-doc AlignmentStage).
        /// Идемпотентен: существующие ассеты не пересоздаются, каждый — независимо от другого.
        /// </summary>
        private static void EnsureTempleNeutralMaterials()
        {
            var neutralExists = AssetDatabase.LoadAssetAtPath<Material>(TempleNeutralMaterialPath) != null;
            var neutralBExists = AssetDatabase.LoadAssetAtPath<Material>(TempleNeutralBMaterialPath) != null;
            if (neutralExists && neutralBExists) return;

            var templePrefabPath = VillageStructurePrefabSetupEditor.GetPrefabPath("Temple");
            var templePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(templePrefabPath);
            if (templePrefab == null)
            {
                Debug.LogError("[Editor] " + templePrefabPath + " не найден — Temple_Neutral(_B).mat не созданы.");
                return;
            }

            var renderers = templePrefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 2)
            {
                Debug.LogWarning($"[Editor] {templePrefabPath}: найдено {renderers.Length} MeshRenderer(ов), ожидалось 2 " +
                                  "(Synty Church_01_A) — материалы прошиваются по доступным индексам, лишние/недостающие не обрабатываются.");
            }

            string[] destPaths = { TempleNeutralMaterialPath, TempleNeutralBMaterialPath };
            for (int i = 0; i < renderers.Length && i < destPaths.Length; i++)
                CopyRendererMaterial(renderers[i], destPaths[i]);
        }

        /// <summary>Копия sharedMaterial одного рендерера в новый .mat-ассет. Идемпотентен — существующий ассет не пересоздаётся.</summary>
        private static void CopyRendererMaterial(MeshRenderer renderer, string destPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(destPath) != null) return;

            if (renderer == null || renderer.sharedMaterial == null)
            {
                Debug.LogError("[Editor] MeshRenderer без материала — " + destPath + " не создан.");
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(renderer.sharedMaterial);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("[Editor] У материала (" + renderer.sharedMaterial.name + ") не найден путь ассета — " + destPath + " не создан.");
                return;
            }

            if (!AssetDatabase.CopyAsset(sourcePath, destPath))
            {
                Debug.LogError("[Editor] Не удалось скопировать " + sourcePath + " -> " + destPath + ".");
                return;
            }

            Debug.Log("[Editor] Создан " + destPath + " — копия фактического материала храма (" + sourcePath + ").");
        }

        /// <summary>Тинтованная копия существующего материала. Существующий ассет не перезаписывается (GUID сохраняется).</summary>
        private static Material EnsureTintedCopy(string sourcePath, string destPath, Color tint, string label)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(destPath);
            if (existing != null) return existing;

            if (AssetDatabase.LoadAssetAtPath<Material>(sourcePath) == null)
            {
                Debug.LogError("[Editor] Исходный материал не найден: " + sourcePath + " — " + destPath + " не создан.");
                return null;
            }

            if (!AssetDatabase.CopyAsset(sourcePath, destPath))
            {
                Debug.LogError("[Editor] Не удалось скопировать " + sourcePath + " -> " + destPath + ".");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(destPath);
            if (material == null)
            {
                Debug.LogError("[Editor] " + destPath + " не загрузился после копирования.");
                return null;
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", tint);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", tint);
            else
                Debug.LogWarning("[Editor] " + destPath + ": ни _BaseColor, ни _Color не найдены на шейдере — тинт не применён.");

            EditorUtility.SetDirty(material);
            Debug.Log("[Editor] Создан " + destPath + " — тинтованная копия (" + label + ").");
            return material;
        }

        // ------------------------------------------------------------------------------------
        // Утилиты
        // ------------------------------------------------------------------------------------

        private static void WireRendererArray(SerializedObject so, string propertyName, Renderer[] renderers, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — прошивка пропущена.");
                return;
            }

            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning($"[Editor] {ownerLabel}.{propertyName}: рендереры не найдены — оставлено как было.");
                return;
            }

            prop.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];

            Debug.Log($"[Editor] {ownerLabel}.{propertyName}: прошито {renderers.Length} рендерер(ов).");
        }

        private static bool SetRef(SerializedObject so, string propertyName, Object value, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — прошивка пропущена " +
                                  "(проверьте, что code-writer добавил это SerializeField).");
                return false;
            }

            if (value == null)
            {
                Debug.LogWarning($"[Editor] Значение для поля \"{propertyName}\" на {ownerLabel} не найдено — оставлено как было (не присваиваем null).");
                return false;
            }

            prop.objectReferenceValue = value;
            Debug.Log($"[Editor] {ownerLabel}.{propertyName} прошито.");
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
    }
}
