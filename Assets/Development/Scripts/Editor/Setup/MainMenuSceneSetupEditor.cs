using System.IO;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Editor.UI;
using BlackWhiteLike.Installers;
using BlackWhiteLike.Models;
using BlackWhiteLike.Views.UI.LevelSelect;
using BlackWhiteLike.Views.UI.MainMenu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Шаг 7 — Кампания из 5 уровней (game-design.md, шаг 7). Собирает
    /// <c>Assets/Development/Scenes/MainMenu.unity</c>: UI-камера + AudioListener, [Systems]
    /// (GameLifetimeScope, <c>_initialState = MainMenu</c>) и connected-инстанс
    /// <c>MainMenuCanvas.prefab</c> (<see cref="MainMenuCanvasSetupEditor"/>) с прошитыми
    /// <c>GameLifetimeScope._mainMenuView</c>/<c>._levelSelectView</c>.
    /// Идемпотентен: повторный запуск дособирает недостающее, не дублирует объекты (тот же паттерн
    /// поиска по компоненту, что TestHandSceneSetupEditor/BootstrapSceneSetupEditor).
    /// </summary>
    public static class MainMenuSceneSetupEditor
    {
        private const string ScenesFolder = "Assets/Development/Scenes";
        private const string ScenePath = BuildSettingsEditor.MainMenuScenePath;
        private const string CanvasPrefabPath = "Assets/Development/Prefabs/UI/MainMenuCanvas.prefab";

        [MenuItem("BlackWhiteLike/Setup/Build Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            // Канвас — вне активной сцены, собирается ДО открытия/создания MainMenu.unity (тот же
            // порядок, что "конфиги → VFX-префабы → HUD → сцена" в TestVillageSceneSetupEditor).
            if (!File.Exists(CanvasPrefabPath))
            {
                Debug.Log("[Editor] MainMenuCanvas.prefab не найден — вызываю MainMenuCanvasSetupEditor.BuildOrUpdateCanvasPrefab().");
                MainMenuCanvasSetupEditor.BuildOrUpdateCanvasPrefab();
            }

            // БЕЗ модального диалога (.claude/rules/editor-files.md — "Модальные диалоги ломают автоматизацию").
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                if (string.IsNullOrEmpty(activeScene.path))
                {
                    Debug.LogError("[Editor] Build Main Menu Scene прервана: текущая сцена \"" + activeScene.name +
                                    "\" ни разу не сохранялась на диск (нет пути) и содержит изменения — " +
                                    "автосохранение недоступно (вызвало бы диалог \"Save As\"). Сохраните " +
                                    "сцену вручную (File > Save) и запустите пункт меню снова.");
                    return;
                }

                if (!EditorSceneManager.SaveScene(activeScene))
                {
                    Debug.LogError("[Editor] Build Main Menu Scene прервана: не удалось автоматически сохранить " +
                                    "текущую сцену \"" + activeScene.name + "\".");
                    return;
                }

                Debug.Log("[Editor] Текущая сцена \"" + activeScene.name + "\" содержала несохранённые изменения — " +
                          "сохранена автоматически перед сборкой MainMenu.");
            }

            EnsureFolder(ScenesFolder);

            bool sceneExists = File.Exists(ScenePath);
            Scene scene = sceneExists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Debug.Log(sceneExists
                ? "[Editor] Открыта существующая сцена MainMenu.unity — дособираем недостающее."
                : "[Editor] Создана новая пустая сцена под MainMenu.");

            // Канвас-префаб — грузим ПОСЛЕ открытия/создания сцены (OpenScene выгружает неиспользуемые
            // ассеты, см. .claude/rules/editor-files.md).
            var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            if (canvasPrefab == null)
            {
                Debug.LogError("[Editor] " + CanvasPrefabPath + " всё ещё не собран (см. лог выше) — MainMenuCanvas не размещён в сцене.");
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Main Menu Scene");
            int undoGroup = Undo.GetCurrentGroup();

            TestHandSceneSetupEditor.EnsureMainCameraAndListener();
            var systemsScope = TestHandSceneSetupEditor.EnsureSystemsInstance();

            LevelHudSetupEditor.EnsureEventSystem();
            var canvasInstance = EnsureCanvasSceneInstance(canvasPrefab);

            MainMenuView mainMenuView = null;
            LevelSelectView levelSelectView = null;
            if (canvasInstance != null)
            {
                mainMenuView = canvasInstance.GetComponentInChildren<MainMenuView>(true);
                levelSelectView = canvasInstance.GetComponentInChildren<LevelSelectView>(true);
            }

            WireScope(systemsScope, mainMenuView, levelSelectView);

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

            Debug.Log("[Editor] Build Main Menu Scene завершён: " + ScenePath + ".");
        }

        private static GameObject EnsureCanvasSceneInstance(GameObject prefab)
        {
            var existingView = Object.FindAnyObjectByType<MainMenuView>(FindObjectsInactive.Include);
            if (existingView != null) return existingView.transform.root.gameObject;

            if (prefab == null)
            {
                Debug.LogError("[Editor] MainMenuCanvas.prefab не передан — инстанс в сцене не создан.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(instance, "Create MainMenuCanvas Instance");
            Debug.Log("[Editor] Инстанс MainMenuCanvas.prefab создан в текущей сцене.");
            return instance;
        }

        private static void AssertNoThirdPartyPrefabLinksInScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                EditorPrefabUtility.AssertNoThirdPartyPrefabLinks(root, "MainMenu/\"" + root.name + "\"");
        }

        private static void WireScope(GameLifetimeScope scope, MainMenuView mainMenuView, LevelSelectView levelSelectView)
        {
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в сцене — прошивка ссылок пропущена.");
                return;
            }

            const int expected = 3;
            var so = new SerializedObject(scope);
            var ownerLabel = "GameLifetimeScope";

            int wired = 0;
            wired += SetEnumProp(so, "_initialState", (int)GameState.MainMenu, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_mainMenuView", mainMenuView, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_levelSelectView", levelSelectView, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();

            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected} полей.");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей — " +
                                "сцена соберётся, но главное меню не будет работать в Play Mode.");
        }

        // Ревью шага 7 (косметика): enumValueIndex -> intValue — enumValueIndex это индекс в
        // отсортированном списке значений enum, а не само значение; для enum с непоследовательными
        // явными значениями это разъезжается. Вызывающий код уже передаёт (int)EnumValue.
        private static bool SetEnumProp(SerializedObject so, string propertyName, int enumValue, string ownerLabel)
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
