using System.IO;
using System.Linq;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Installers;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Собирает boot-сцену проекта (шаг 1 "Каркас", game-design.md §19.1): Main Camera с
    /// AudioListener и персистентный Composition Root верхнего уровня [RootScope] (RootLifetimeScope,
    /// держит LevelCatalogConfig — мета-прогресс кампании).
    /// Шаг 7 (ревью, ВАЖНО 2) — Bootstrap чисто служебная сцена и сценовый scope ([Systems] с
    /// GameLifetimeScope) ей больше не нужен: порядок Awake двух корневых объектов ([Systems] и
    /// [RootScope]) не определён, и если [Systems] отрабатывает первым, RootLifetimeScope.Instance
    /// ещё null — недетерминированные фоллбэк-регистрации. Сам переход из Bootstrap в MainMenu
    /// делает BootstrapFlowEntryPoint из [RootScope]. Префаб [Systems]/SystemsRoot.prefab при этом
    /// по-прежнему обеспечивается этим скриптом (см. <see cref="EnsureSystemsPrefabAsset"/>) — его
    /// инстансы нужны сценам MainMenu/Village_Base (см. TestHandSceneSetupEditor.EnsureSystemsInstance),
    /// просто больше не размещаются в самой Bootstrap-сцене.
    /// Идемпотентен: повторный запуск дособирает недостающее в уже существующую сцену, ничего не
    /// дублирует; при открытии сцены, собранной старой версией скрипта (со сценовым [Systems]),
    /// мигрирует — удаляет его (см. <see cref="MigrateRemoveSceneSystemsScope"/>).
    /// </summary>
    public static class BootstrapSceneSetupEditor
    {
        private const string ScenesFolder = "Assets/Development/Scenes";
        private const string ScenePath = ScenesFolder + "/Bootstrap.unity";
        private const string PrefabsFolder = "Assets/Development/Prefabs/Systems";
        private const string PrefabPath = PrefabsFolder + "/SystemsRoot.prefab";
        private const string SystemsRootName = "[Systems]";
        private const string RootScopeName = "[RootScope]";
        private const string LevelCatalogPath = "Assets/Development/Data/Configs/Levels/LevelCatalog.asset";

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

            // Шаг 7 (ревью, ВАЖНО 2) — Bootstrap больше не держит сценовый [Systems]: если сцена
            // собрана старой версией скрипта — мигрируем (удаляем), затем гарантируем только
            // AudioListener/камеру и сам файл префаба (нужен другим сценам, в Bootstrap не ставится).
            MigrateRemoveSceneSystemsScope();
            EnsureCameraAndAudioListener();
            EnsureSystemsPrefabAsset();

            // Шаг 7 — [RootScope] (RootLifetimeScope) — персистентный Composition Root верхнего
            // уровня, отдельный от per-level [Systems]/GameLifetimeScope.
            var rootScope = EnsureRootScope();
            WireRootScope(rootScope.GetComponent<RootLifetimeScope>());

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

            // Шаг 7 — единственный источник порядка Build Settings (Bootstrap/MainMenu/Village_Base),
            // общий для всех трёх сборщиков сцен кампании (см. BuildSettingsEditor).
            BuildSettingsEditor.EnsureCampaignScenesInBuildSettings();

            Debug.Log("[Editor] Bootstrap-сцена собрана: Main Camera (AudioListener), [RootScope] (RootLifetimeScope) + " +
                      "LevelCatalog; сценового [Systems]/GameLifetimeScope в Bootstrap больше нет (ревью шага 7), " +
                      "префаб для других сцен обеспечен по пути " + PrefabPath + ".");
        }

        /// <summary>
        /// Шаг 7 — гарантирует наличие объекта [RootScope] с компонентом RootLifetimeScope
        /// (Composition Root верхнего уровня, персистентный, отдельный от per-level [Systems]).
        /// Тот же паттерн поиска по компоненту (включая выключенные объекты), что и у остальных
        /// Ensure*-методов этого файла — GameObject.Find не видит неактивные объекты.
        /// </summary>
        private static GameObject EnsureRootScope()
        {
            var scopes = Object.FindObjectsByType<RootLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (scopes.Length > 1)
            {
                var paths = scopes.Select(s => GetHierarchyPath(s.gameObject));
                Debug.LogWarning("[Editor] В сцене найдено несколько RootLifetimeScope: " +
                                  string.Join(", ", paths) + ". Используется первый, остальные не тронуты.");
            }

            GameObject go;
            if (scopes.Length > 0)
            {
                go = scopes[0].gameObject;
            }
            else
            {
                go = new GameObject(RootScopeName);
                Undo.RegisterCreatedObjectUndo(go, "Create [RootScope]");
                Undo.AddComponent<RootLifetimeScope>(go);
                Debug.Log("[Editor] Создан объект [RootScope] с компонентом RootLifetimeScope.");
                return go;
            }

            if (go.name != RootScopeName)
            {
                Undo.RecordObject(go, "Rename Root Scope");
                Debug.Log($"[Editor] RootLifetimeScope-объект переименован из \"{go.name}\" в \"{RootScopeName}\".");
                go.name = RootScopeName;
            }

            return go;
        }

        /// <summary>
        /// Шаг 7 — прошивает RootLifetimeScope._levelCatalog ссылкой на LevelCatalog.asset. Не
        /// присваивает null, если каталог ещё не создан (см. .claude/rules/editor-files.md).
        /// </summary>
        private static void WireRootScope(RootLifetimeScope scope)
        {
            if (scope == null)
            {
                Debug.LogWarning("[Editor] RootLifetimeScope не найден на [RootScope] — прошивка ссылок пропущена.");
                return;
            }

            var so = new SerializedObject(scope);
            var prop = so.FindProperty("_levelCatalog");
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле \"_levelCatalog\" не найдено на RootLifetimeScope — прошивка пропущена " +
                                  "(проверьте, что code-writer добавил это SerializeField).");
                return;
            }

            if (prop.objectReferenceValue != null)
            {
                Debug.Log("[Editor] RootLifetimeScope._levelCatalog уже прошит — не перезаписываем.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalogConfig>(LevelCatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("[Editor] LevelCatalog.asset не найден по пути " + LevelCatalogPath +
                                  " — сначала прогоните \"BlackWhiteLike/Configs/Create Empty SO Set\".");
                return;
            }

            prop.objectReferenceValue = catalog;
            so.ApplyModifiedProperties();
            Debug.Log("[Editor] RootLifetimeScope._levelCatalog прошит ссылкой на LevelCatalog.asset.");
        }

        /// <summary>
        /// Шаг 7 (ревью, ВАЖНО 2) — миграция сцен, собранных старой версией скрипта: Bootstrap
        /// больше не держит сценовый [Systems]/GameLifetimeScope (гонка Awake с [RootScope], см.
        /// class-doc) — любой найденный в сцене GameLifetimeScope удаляется. Поиск по компоненту
        /// (включая выключенные объекты), а не по имени — GameObject.Find не видит неактивные объекты.
        /// </summary>
        private static void MigrateRemoveSceneSystemsScope()
        {
            var scopes = Object.FindObjectsByType<GameLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var scope in scopes)
            {
                var go = scope.gameObject;
                Debug.Log("[Editor] Bootstrap: удалён сценовый [Systems] (\"" + GetHierarchyPath(go) +
                          "\") — Bootstrap больше не держит сценовый Composition Root (ревью шага 7, гонка Awake с [RootScope]).");
                Undo.DestroyObjectImmediate(go);
            }
        }

        /// <summary>
        /// Шаг 7 (ревью, ВАЖНО 2) — гарантирует наличие файла префаба [Systems]/SystemsRoot.prefab
        /// (нужен другим сценам — MainMenu/Village_Base инстанцируют его через
        /// <c>TestHandSceneSetupEditor.EnsureSystemsInstance</c>), но БЕЗ размещения инстанса в
        /// самой Bootstrap-сцене: временный объект создаётся, сохраняется как ассет и сразу
        /// уничтожается — Undo на нём не регистрируется (не остаётся в сцене, нечего отменять).
        /// Существующий файл не пересоздаётся (настройки, подкрученные владельцем, дороже дефолтов).
        /// </summary>
        private static void EnsureSystemsPrefabAsset()
        {
            if (File.Exists(PrefabPath))
            {
                Debug.Log("[Editor] Префаб " + PrefabPath + " уже существует — пересоздание не требуется.");
                return;
            }

            var temp = new GameObject(SystemsRootName);
            try
            {
                var scope = temp.AddComponent<GameLifetimeScope>();
                // Шаг 5 — явный дефолт состояния FSM для холодного старта: на новом [Systems] это
                // становится дефолтным значением поля самого префаба; сцены, где нужен другой
                // стартовый стейт, переопределяют его в своей WireScope.
                WireInitialState(scope, GameState.MainMenu);

                PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
                Debug.Log("[Editor] Сохранён префаб: " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
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

        /// <summary>Шаг 5 — явная прошивка стартового GameState (SerializeField enum) на Composition Root.</summary>
        private static void WireInitialState(GameLifetimeScope scope, GameState state)
        {
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден на [Systems] — _initialState не прошит.");
                return;
            }

            var so = new SerializedObject(scope);
            var prop = so.FindProperty("_initialState");
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле \"_initialState\" не найдено на GameLifetimeScope — прошивка пропущена " +
                                  "(проверьте, что code-writer добавил это SerializeField).");
                return;
            }

            prop.intValue = (int)state;
            so.ApplyModifiedProperties();
            Debug.Log($"[Editor] GameLifetimeScope._initialState прошито значением {state}.");
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
