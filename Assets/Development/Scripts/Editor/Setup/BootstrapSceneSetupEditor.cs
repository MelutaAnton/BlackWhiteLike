using System.IO;
using System.Linq;
using BlackWhiteLike.Installers;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Собирает boot-сцену проекта (шаг 1 "Каркас", game-design.md §19.1):
    /// объект [Systems] с GameLifetimeScope (Composition Root), Main Camera с AudioListener,
    /// [Systems] сохраняется как переиспользуемый префаб.
    /// Идемпотентен: повторный запуск дособирает недостающее в уже существующую сцену,
    /// ничего не дублирует.
    /// </summary>
    public static class BootstrapSceneSetupEditor
    {
        private const string ScenesFolder = "Assets/Development/Scenes";
        private const string ScenePath = ScenesFolder + "/Bootstrap.unity";
        private const string PrefabsFolder = "Assets/Development/Prefabs/Systems";
        private const string PrefabPath = PrefabsFolder + "/SystemsRoot.prefab";
        private const string SystemsRootName = "[Systems]";

        [MenuItem("BlackWhiteLike/Setup/Build Bootstrap Scene")]
        public static void BuildBootstrapScene()
        {
            // Не теряем несохранённые правки текущей открытой сцены молча — но БЕЗ модального диалога
            // (ревью шага 3: EditorUtility.DisplayDialog блокирует главный поток Unity и вешает
            // MCP-мост намертво, пока диалог не закроет человек мышкой; автоматизируемый пункт меню
            // обязан либо решить сам, либо отказаться с понятным Debug.LogError).
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                if (string.IsNullOrEmpty(activeScene.path))
                {
                    Debug.LogError("[Editor] Build Bootstrap Scene прервана: текущая сцена \"" + activeScene.name +
                                    "\" ни разу не сохранялась на диск (нет пути) и содержит изменения — " +
                                    "автосохранение недоступно (вызвало бы диалог \"Save As\"). Сохраните " +
                                    "сцену вручную (File > Save) и запустите пункт меню снова.");
                    return;
                }

                if (!EditorSceneManager.SaveScene(activeScene))
                {
                    Debug.LogError("[Editor] Build Bootstrap Scene прервана: не удалось автоматически сохранить " +
                                    "текущую сцену \"" + activeScene.name + "\".");
                    return;
                }

                Debug.Log("[Editor] Текущая сцена \"" + activeScene.name + "\" содержала несохранённые изменения — " +
                          "сохранена автоматически перед сборкой Bootstrap-сцены.");
            }

            EnsureFolder(ScenesFolder);
            EnsureFolder(PrefabsFolder);

            bool sceneExists = File.Exists(ScenePath);
            Scene scene;
            if (sceneExists)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[Editor] Открыта существующая сцена Bootstrap.unity — дособираем недостающее.");
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Debug.Log("[Editor] Создана новая пустая сцена под Bootstrap.");
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Bootstrap Scene");
            int undoGroup = Undo.GetCurrentGroup();

            var systemsRoot = EnsureSystemsRoot();
            EnsureCameraAndAudioListener();
            EnsureSystemsPrefab(systemsRoot);

            Undo.CollapseUndoOperations(undoGroup);

            if (!sceneExists)
            {
                if (EditorSceneManager.SaveScene(scene, ScenePath))
                    Debug.Log("[Editor] Сцена сохранена: " + ScenePath);
            }
            else
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Editor] Сцена Bootstrap.unity пересохранена.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EnsureBootstrapSceneFirstInBuildSettings();

            Debug.Log("[Editor] Bootstrap-сцена собрана: [Systems] (GameLifetimeScope) + Main Camera (AudioListener), " +
                      "префаб " + PrefabPath + ".");
        }

        /// <summary>
        /// Гарантирует, что Bootstrap.unity зарегистрирована в Build Settings первой сценой
        /// (иначе холодный старт билда невозможен — билд стартует не с той сцены).
        /// Остальные сцены не удаляются и не выключаются, порядок между ними сохраняется.
        /// </summary>
        private static void EnsureBootstrapSceneFirstInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int existingIndex = scenes.FindIndex(s => s.path == ScenePath);

            if (existingIndex == 0 && scenes[0].enabled)
            {
                // Уже первая и включена — ничего делать не нужно.
                return;
            }

            if (existingIndex >= 0)
                scenes.RemoveAt(existingIndex);

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[Editor] Bootstrap.unity добавлена/перемещена в Build Settings на первую позицию.");
        }

        /// <summary>
        /// Гарантирует наличие объекта [Systems] с компонентом GameLifetimeScope.
        /// Ищем Composition Root по компоненту (включая выключенные объекты), а не по имени —
        /// GameObject.Find не видит неактивные объекты, и второй GameLifetimeScope означает
        /// двойную регистрацию зависимостей и два GameBootstrap.
        /// </summary>
        private static GameObject EnsureSystemsRoot()
        {
            var scopes = Object.FindObjectsByType<GameLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (scopes.Length > 1)
            {
                var paths = scopes.Select(s => GetHierarchyPath(s.gameObject));
                Debug.LogWarning("[Editor] В сцене найдено несколько GameLifetimeScope: " +
                                  string.Join(", ", paths) + ". Используется первый, остальные не тронуты.");
            }

            GameObject go;
            if (scopes.Length > 0)
            {
                go = scopes[0].gameObject;
            }
            else
            {
                go = new GameObject(SystemsRootName);
                Undo.RegisterCreatedObjectUndo(go, "Create [Systems]");
                Undo.AddComponent<GameLifetimeScope>(go);
                Debug.Log("[Editor] Создан объект [Systems] с компонентом GameLifetimeScope.");
                return go;
            }

            // Имя префаба (SystemsRoot) отличается от имени сценного объекта ([Systems]) —
            // свежий инстанс, перетащенный вручную, унаследует имя префаба.
            if (go.name != SystemsRootName)
            {
                Undo.RecordObject(go, "Rename Systems Root");
                Debug.Log($"[Editor] Composition Root переименован из \"{go.name}\" в \"{SystemsRootName}\".");
                go.name = SystemsRootName;
            }

            return go;
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var t = go.transform;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        /// <summary>
        /// Гарантирует наличие камеры сцены и единственного AudioListener на ней —
        /// без него Unity не воспроизводит звук вообще.
        /// </summary>
        private static void EnsureCameraAndAudioListener()
        {
            // Ищем камеру по компоненту (включая выключенные объекты), а не по имени —
            // GameObject.Find не видит неактивные объекты.
            var cameras = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            GameObject camGo;
            if (cameras.Length > 0)
            {
                camGo = cameras[0].gameObject;
            }
            else
            {
                camGo = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
                Undo.AddComponent<Camera>(camGo);
                Debug.Log("[Editor] Создан объект Main Camera с компонентом Camera.");
            }

            // Тег выставляется всегда, если отличается — не только при создании компонента,
            // иначе существующая камера с тегом Untagged его не получит.
            if (camGo.tag != Tags.MainCamera)
            {
                Undo.RecordObject(camGo, "Set MainCamera Tag");
                camGo.tag = Tags.MainCamera;
                Debug.Log("[Editor] На " + camGo.name + " выставлен тег " + Tags.MainCamera + ".");
            }

            var existingListeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (existingListeners.Length == 0)
            {
                Undo.AddComponent<AudioListener>(camGo);
                Debug.Log("[Editor] На Main Camera добавлен AudioListener.");
            }
            else if (existingListeners.Length > 1)
            {
                Debug.LogWarning($"[Editor] В сцене найдено {existingListeners.Length} AudioListener — " +
                                  "ожидается ровно один. Лишние не удалены автоматически, проверьте вручную.");
            }
        }

        /// <summary>
        /// Сохраняет [Systems] как переиспользуемый префаб. Если префаб уже существует —
        /// связывает с ним сценный инстанс вместо пересоздания (миграционная идемпотентность).
        /// </summary>
        private static void EnsureSystemsPrefab(GameObject systemsRoot)
        {
            if (File.Exists(PrefabPath))
            {
                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (existingPrefab == null)
                {
                    Debug.LogWarning("[Editor] Файл префаба найден, но не загрузился как GameObject: " + PrefabPath);
                    return;
                }

                bool alreadyConnected =
                    PrefabUtility.GetPrefabInstanceStatus(systemsRoot) == PrefabInstanceStatus.Connected &&
                    PrefabUtility.GetCorrespondingObjectFromSource(systemsRoot) == existingPrefab;

                if (!alreadyConnected)
                {
                    PrefabUtility.SaveAsPrefabAssetAndConnect(systemsRoot, PrefabPath, InteractionMode.AutomatedAction);
                    Debug.Log("[Editor] Сценный [Systems] связан с существующим префабом " + PrefabPath + ".");
                }
                return;
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(systemsRoot, PrefabPath, InteractionMode.AutomatedAction);
            Debug.Log("[Editor] Сохранён префаб: " + PrefabPath);
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
