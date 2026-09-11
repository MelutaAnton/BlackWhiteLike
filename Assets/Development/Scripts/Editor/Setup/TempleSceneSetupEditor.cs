using System.IO;
using BlackWhiteLike.Data.Configs.Localization;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Editor.Prefabs;
using BlackWhiteLike.Editor.UI;
using BlackWhiteLike.Installers;
using BlackWhiteLike.Models;
using BlackWhiteLike.Views.UI.Common;
using BlackWhiteLike.Views.UI.Overlay;
using BlackWhiteLike.Views.UI.Temple;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Шаг 8 — Meta + idle (game-design.md, шаг 8). Собирает
    /// <c>Assets/Development/Scenes/Temple.unity</c>: 3D-камера + направленный свет + AudioListener,
    /// инстанс НАШЕГО <c>Temple.prefab</c> (обычный connected-инстанс — правило CLAUDE.md об unpack
    /// касается только ThirdParty-источников, наш собственный префаб коннектится как обычно),
    /// подиум под храмом, <c>[AlignmentVisual]</c> (переиспользует
    /// <see cref="AlignmentVisualSetupEditor.SetupAlignmentVisualCore"/> — тот же контроллер/конфиг,
    /// что и в игровых сценах, <c>_handRenderers</c> остаётся пустым — руки в Temple.unity нет),
    /// <c>[Systems]</c> (GameLifetimeScope, <c>_initialState = TempleMeta</c>) и connected-инстанс
    /// <c>TempleCanvas.prefab</c> (<see cref="TempleCanvasSetupEditor"/>) с прошитыми
    /// <c>_templeView</c>/<c>_welcomeBackView</c>.
    /// Идемпотентен: повторный запуск дособирает недостающее, не дублирует объекты (тот же паттерн
    /// поиска по компоненту, что TestHandSceneSetupEditor/MainMenuSceneSetupEditor).
    /// </summary>
    public static class TempleSceneSetupEditor
    {
        private const string ScenesFolder = "Assets/Development/Scenes";
        private const string ScenePath = BuildSettingsEditor.TempleScenePath;
        private const string CanvasPrefabPath = TempleCanvasSetupEditor.PrefabPath;
        private const string LocalizationConfigPath = "Assets/Development/Data/Configs/Localization/LocalizationConfig.asset";

        private const string TempleStructureName = "Temple";
        private const string GroundName = "Ground";

        [MenuItem("BlackWhiteLike/Setup/Build Temple Scene")]
        public static void BuildTempleScene()
        {
            // Канвас и Temple.prefab — вне активной сцены, собираются ДО открытия/создания Temple.unity
            // (тот же порядок, что "конфиги → префабы → HUD → сцена" в остальных сборщиках сцен).
            if (!File.Exists(VillageStructurePrefabSetupEditor.GetPrefabPath(TempleStructureName)))
            {
                Debug.Log("[Editor] Temple.prefab не найден — вызываю VillageStructurePrefabSetupEditor.SetupVillageStructurePrefabs().");
                VillageStructurePrefabSetupEditor.SetupVillageStructurePrefabs();
            }

            if (!File.Exists(CanvasPrefabPath))
            {
                Debug.Log("[Editor] TempleCanvas.prefab не найден — вызываю TempleCanvasSetupEditor.BuildOrUpdateCanvasPrefab().");
                TempleCanvasSetupEditor.BuildOrUpdateCanvasPrefab();
            }

            // БЕЗ модального диалога (.claude/rules/editor-files.md — "Модальные диалоги ломают автоматизацию").
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                if (string.IsNullOrEmpty(activeScene.path))
                {
                    Debug.LogError("[Editor] Build Temple Scene прервана: текущая сцена \"" + activeScene.name +
                                    "\" ни разу не сохранялась на диск (нет пути) и содержит изменения — " +
                                    "автосохранение недоступно (вызвало бы диалог \"Save As\"). Сохраните " +
                                    "сцену вручную (File > Save) и запустите пункт меню снова.");
                    return;
                }

                if (!EditorSceneManager.SaveScene(activeScene))
                {
                    Debug.LogError("[Editor] Build Temple Scene прервана: не удалось автоматически сохранить " +
                                    "текущую сцену \"" + activeScene.name + "\".");
                    return;
                }

                Debug.Log("[Editor] Текущая сцена \"" + activeScene.name + "\" содержала несохранённые изменения — " +
                          "сохранена автоматически перед сборкой Temple.");
            }

            EnsureFolder(ScenesFolder);

            bool sceneExists = File.Exists(ScenePath);
            Scene scene = sceneExists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Debug.Log(sceneExists
                ? "[Editor] Открыта существующая сцена Temple.unity — дособираем недостающее."
                : "[Editor] Создана новая пустая сцена под Temple.");

            // Префабы — грузим ПОСЛЕ открытия/создания сцены (OpenScene выгружает неиспользуемые
            // ассеты, см. .claude/rules/editor-files.md).
            var templePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageStructurePrefabSetupEditor.GetPrefabPath(TempleStructureName));
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);

            if (templePrefab == null)
                Debug.LogError("[Editor] Temple.prefab всё ещё не собран (см. лог выше) — храм не размещён в сцене.");
            if (canvasPrefab == null)
                Debug.LogError("[Editor] TempleCanvas.prefab всё ещё не собран (см. лог выше) — TempleCanvas не размещён в сцене.");

            var localizationConfig = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(LocalizationConfigPath);
            if (localizationConfig == null)
                Debug.LogWarning("[Editor] LocalizationConfig не найден по пути " + LocalizationConfigPath + " — сначала прогоните " +
                                  "\"BlackWhiteLike/Configs/Create Localization Config\".");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Temple Scene");
            int undoGroup = Undo.GetCurrentGroup();

            EnsureCameraLightAndListener();
            EnsureTempleInstance(templePrefab);
            EnsureGround();

            var systemsScope = TestHandSceneSetupEditor.EnsureSystemsInstance();

            // Alignment-визуал — переиспользуем core-логику AlignmentVisualSetupEditor (тот же
            // контроллер/конфиг, что в игровых сценах): находит "Temple" в сцене (уже создан выше),
            // GodHandController не найден (руки в Temple.unity нет) — _handRenderers остаётся пустым
            // с LogWarning, это ожидаемо по контракту задачи.
            AlignmentVisualSetupEditor.SetupAlignmentVisualCore();

            LevelHudSetupEditor.EnsureEventSystem();
            var canvasInstance = EnsureCanvasSceneInstance(canvasPrefab);

            TempleView templeView = null;
            WelcomeBackView welcomeBackView = null;
            if (canvasInstance != null)
            {
                templeView = canvasInstance.GetComponentInChildren<TempleView>(true);
                welcomeBackView = canvasInstance.GetComponentInChildren<WelcomeBackView>(true);
            }

            // Зачистка ревью волны 2 шага 10 (задача 2): LocalizedTextRegistry живёт на корне
            // TempleCanvas (TempleCanvasSetupEditor.EnsureLocalizedTextRegistry) — прошивается
            // массивом из одного элемента.
            var templeLocalizedTextRegistry = canvasInstance != null ? canvasInstance.GetComponent<LocalizedTextRegistry>() : null;
            var localizedTextRegistries = templeLocalizedTextRegistry != null
                ? new[] { templeLocalizedTextRegistry }
                : System.Array.Empty<LocalizedTextRegistry>();

            WireScope(systemsScope, templeView, welcomeBackView, localizationConfig, localizedTextRegistries);

            AssertNoThirdPartyPrefabLinksInScene(scene);

            Undo.CollapseUndoOperations(undoGroup);

            EditorSceneManager.MarkSceneDirty(scene);
            if (sceneExists)
                EditorSceneManager.SaveScene(scene);
            else
                EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildSettingsEditor.EnsureCampaignScenesInBuildSettings();

            Debug.Log("[Editor] Build Temple Scene завершён: " + ScenePath + ".");
        }

        // ------------------------------------------------------------------------------------
        // Камера / свет / AudioListener
        // ------------------------------------------------------------------------------------

        private static void EnsureCameraLightAndListener()
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject camGo;
            if (cameras.Length == 0)
            {
                camGo = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
                Undo.AddComponent<Camera>(camGo);
                Debug.Log("[Editor] Создана Main Camera.");
            }
            else
            {
                camGo = cameras[0].gameObject;
            }

            if (camGo.tag != Tags.MainCamera)
            {
                Undo.RecordObject(camGo, "Set MainCamera Tag");
                camGo.tag = Tags.MainCamera;
            }

            // Камера смотрит на храм (храм размещается в начале координат, см. EnsureTempleInstance) —
            // позиция/поворот трогаются только у новосозданной камеры, не затирая ручную подстройку кадра.
            if (camGo.transform.position == Vector3.zero && camGo.transform.rotation == Quaternion.identity)
            {
                camGo.transform.position = new Vector3(0f, 3.2f, -6.5f);
                camGo.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.6f, 6.5f).normalized, Vector3.up);
            }

            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (listeners.Length == 0)
            {
                Undo.AddComponent<AudioListener>(camGo);
                Debug.Log("[Editor] На Main Camera добавлен AudioListener.");
            }
            else if (listeners.Length > 1)
            {
                Debug.LogWarning($"[Editor] В сцене найдено {listeners.Length} AudioListener — ожидается ровно один. " +
                                  "Лишние не удалены автоматически, проверьте вручную.");
            }

            var lightGo = TestHandSceneSetupEditor.FindInSceneIncludingInactive("Directional Light");
            Light light;
            if (lightGo == null)
            {
                lightGo = new GameObject("Directional Light");
                Undo.RegisterCreatedObjectUndo(lightGo, "Create Directional Light");
                light = Undo.AddComponent<Light>(lightGo);
                light.type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Debug.Log("[Editor] Создан Directional Light.");
            }
            else
            {
                light = lightGo.GetComponent<Light>();
                if (light == null)
                {
                    light = Undo.AddComponent<Light>(lightGo);
                    light.type = LightType.Directional;
                }
            }
        }

        // ------------------------------------------------------------------------------------
        // Temple.prefab — НАШ префаб, обычный connected-инстанс (не unpack — правило про unpack
        // касается только источников из Assets/ThirdParty/).
        // ------------------------------------------------------------------------------------

        private static void EnsureTempleInstance(GameObject templePrefab)
        {
            var existing = TestHandSceneSetupEditor.FindInSceneIncludingInactive(TempleStructureName);
            if (existing != null) return;

            if (templePrefab == null)
            {
                Debug.LogError("[Editor] Temple.prefab не передан — храм не размещён в сцене.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(templePrefab, SceneManager.GetActiveScene());
            instance.name = TempleStructureName;
            instance.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(instance, "Create Temple Instance");
            Debug.Log("[Editor] Инстанс Temple.prefab создан в текущей сцене, в кадре камеры.");
        }

        // ------------------------------------------------------------------------------------
        // Подиум/земля под храмом
        // ------------------------------------------------------------------------------------

        private static void EnsureGround()
        {
            var go = TestHandSceneSetupEditor.FindInSceneIncludingInactive(GroundName);
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = GroundName;
                Undo.RegisterCreatedObjectUndo(go, "Create Ground");
                Debug.Log("[Editor] Создан подиум под храмом: Ground (Cube 6x0.5x6, верх на y=0).");
            }

            go.transform.position = new Vector3(0f, -0.25f, 0f);
            go.transform.localScale = new Vector3(6f, 0.5f, 6f);
            go.isStatic = true;
        }

        // ------------------------------------------------------------------------------------
        // Канвас
        // ------------------------------------------------------------------------------------

        private static GameObject EnsureCanvasSceneInstance(GameObject prefab)
        {
            var existingView = Object.FindAnyObjectByType<TempleView>(FindObjectsInactive.Include);
            if (existingView != null) return existingView.transform.root.gameObject;

            if (prefab == null)
            {
                Debug.LogError("[Editor] TempleCanvas.prefab не передан — инстанс в сцене не создан.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(instance, "Create TempleCanvas Instance");
            Debug.Log("[Editor] Инстанс TempleCanvas.prefab создан в текущей сцене.");
            return instance;
        }

        // ------------------------------------------------------------------------------------
        // Прошивка ссылок на Composition Root
        // ------------------------------------------------------------------------------------

        private static void WireScope(GameLifetimeScope scope, TempleView templeView, WelcomeBackView welcomeBackView,
            LocalizationConfig localizationConfig, LocalizedTextRegistry[] localizedTextRegistries)
        {
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в сцене — прошивка ссылок пропущена.");
                return;
            }

            const int expected = 5;
            var so = new SerializedObject(scope);
            var ownerLabel = "GameLifetimeScope";

            int wired = 0;
            wired += SetEnumIntProp(so, "_initialState", (int)GameState.TempleMeta, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_templeView", templeView, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_welcomeBackView", welcomeBackView, ownerLabel) ? 1 : 0;
            // Локализация — единый ассет LocalizationConfig.asset (Editor/UI/LocalizationConfigSetupEditor.cs).
            wired += SetRef(so, "_localizationConfig", localizationConfig, ownerLabel) ? 1 : 0;
            // Зачистка ревью волны 2 шага 10 (задача 2) — замена рантайм-скану сцены.
            wired += SetRefArray(so, "_localizedTextRegistries", localizedTextRegistries, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();

            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected} полей.");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей — " +
                                "сцена соберётся, но Temple-меню не будет работать в Play Mode.");
        }

        /// <summary>Прошивает массив ссылок — см. TestVillageSceneSetupEditor.SetRefArray (тот же приём).</summary>
        private static bool SetRefArray<T>(SerializedObject so, string propertyName, T[] values, string ownerLabel) where T : Object
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — прошивка пропущена " +
                                  "(проверьте, что code-writer добавил это SerializeField).");
                return false;
            }

            if (values == null || values.Length == 0)
            {
                Debug.LogWarning($"[Editor] Значения для массива \"{propertyName}\" на {ownerLabel} не найдены — оставлено как было (не затираем пустым массивом).");
                return false;
            }

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            return true;
        }

        // enumValueIndex — индекс в отсортированном списке значений enum, а не само значение
        // (тот же нюанс, что MainMenuSceneSetupEditor.SetEnumProp) — используем intValue напрямую.
        private static bool SetEnumIntProp(SerializedObject so, string propertyName, int enumValue, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — прошивка пропущена " +
                                  "(проверьте, что code-writer добавил это SerializeField).");
                return false;
            }

            prop.intValue = enumValue;
            return true;
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
            return true;
        }

        private static void AssertNoThirdPartyPrefabLinksInScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                EditorPrefabUtility.AssertNoThirdPartyPrefabLinks(root, "Temple/\"" + root.name + "\"");
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
