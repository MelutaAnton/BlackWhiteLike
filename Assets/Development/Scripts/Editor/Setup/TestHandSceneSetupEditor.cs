using System.Collections.Generic;
using System.IO;
using BlackWhiteLike.Controllers.Camera;
using BlackWhiteLike.Controllers.Hand;
using BlackWhiteLike.Controllers.Village;
using BlackWhiteLike.Data.Configs.Camera;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Data.Configs.Throwable;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Installers;
using BlackWhiteLike.Models;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Собирает временную песочницу _TestHand.unity (game-design.md §19.2, шаг 2 "Камера + рука"):
    /// земля, 8 бросаемых кубов (два — заведомо тяжелее MaxGrabMass, для проверки отказа захвата),
    /// GodHand.prefab, изометрическая камера (Cinemachine 3.1), [Systems] (GameLifetimeScope).
    /// Временная сцена (имя с подчёркиванием) — в Build Settings не добавляется.
    /// Идемпотентен: повторный запуск дособирает недостающее, не плодит объекты.
    /// </summary>
    public static class TestHandSceneSetupEditor
    {
        private const string ScenesFolder = "Assets/Development/Scenes";
        private const string ScenePath = ScenesFolder + "/_TestHand.unity";

        private const string HandConfigPath = "Assets/Development/Data/Configs/Hand/HandConfig.asset";
        private const string CameraConfigPath = "Assets/Development/Data/Configs/Camera/IsometricCameraConfig.asset";
        private const string ThrowableConfigPath = "Assets/Development/Data/Configs/Throwable/ThrowableConfig.asset";
        private const string GodHandPrefabPath = "Assets/Development/Prefabs/Hand/GodHand.prefab";
        private const string SystemsPrefabPath = "Assets/Development/Prefabs/Systems/SystemsRoot.prefab";

        // Имена слоёв — единственный источник EditorLayerUtility (общий с GodHandPrefabSetupEditor).

        private const int BarrelCount = 8;
        private const int HeavyBarrelStartIndex = BarrelCount - 2; // Последние два — заведомо тяжелее MaxGrabMass.
        private const float BarrelGridSpacing = 2f;
        private const float DefaultCameraPitch = 45f;
        private const float DefaultCameraDistance = 10f;

        [MenuItem("BlackWhiteLike/Setup/Build Test Hand Scene")]
        public static void BuildTestHandScene()
        {
            // БЕЗ модального диалога (ревью шага 3: EditorUtility.DisplayDialog вешает MCP-мост
            // намертво, пока диалог не закроет человек мышкой) — либо сохраняем сами, либо
            // отказываемся с понятным Debug.LogError.
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                if (string.IsNullOrEmpty(activeScene.path))
                {
                    Debug.LogError("[Editor] Build Test Hand Scene прервана: текущая сцена \"" + activeScene.name +
                                    "\" ни разу не сохранялась на диск (нет пути) и содержит изменения — " +
                                    "автосохранение недоступно (вызвало бы диалог \"Save As\"). Сохраните " +
                                    "сцену вручную (File > Save) и запустите пункт меню снова.");
                    return;
                }

                if (!EditorSceneManager.SaveScene(activeScene))
                {
                    Debug.LogError("[Editor] Build Test Hand Scene прервана: не удалось автоматически сохранить " +
                                    "текущую сцену \"" + activeScene.name + "\".");
                    return;
                }

                Debug.Log("[Editor] Текущая сцена \"" + activeScene.name + "\" содержала несохранённые изменения — " +
                          "сохранена автоматически перед сборкой _TestHand.");
            }

            EnsureFolder(ScenesFolder);

            bool sceneExists = File.Exists(ScenePath);
            Scene scene = sceneExists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Debug.Log(sceneExists
                ? "[Editor] Открыта существующая сцена _TestHand.unity — дособираем недостающее."
                : "[Editor] Создана новая пустая сцена под _TestHand.");

            // Конфиги грузим здесь, ПОСЛЕ открытия/создания сцены (см. .claude/rules/editor-files.md —
            // OpenScene выгружает неиспользуемые ассеты, ссылки, загруженные до этого, станут fake-null).
            var handConfig = AssetDatabase.LoadAssetAtPath<HandConfig>(HandConfigPath);
            var cameraConfig = AssetDatabase.LoadAssetAtPath<IsometricCameraConfig>(CameraConfigPath);
            var throwableConfig = AssetDatabase.LoadAssetAtPath<ThrowableConfig>(ThrowableConfigPath);
            if (handConfig == null)
                Debug.LogWarning("[Editor] HandConfig не найден по пути " + HandConfigPath + " — сначала прогоните " +
                                  "\"BlackWhiteLike/Configs/Create Empty SO Set\".");
            if (cameraConfig == null)
                Debug.LogWarning("[Editor] IsometricCameraConfig не найден по пути " + CameraConfigPath + " — сначала прогоните " +
                                  "\"BlackWhiteLike/Configs/Create Empty SO Set\".");
            if (throwableConfig == null)
                Debug.LogWarning("[Editor] ThrowableConfig не найден по пути " + ThrowableConfigPath + " — сначала прогоните " +
                                  "\"BlackWhiteLike/Configs/Create Empty SO Set\". Бочки останутся без порога приземления " +
                                  "(ThrowableObjectController._throwableConfig).");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Test Hand Scene");
            int undoGroup = Undo.GetCurrentGroup();

            int groundLayer = EditorLayerUtility.EnsureLayer(EditorLayerUtility.GroundLayerName);
            int grabbableLayer = EditorLayerUtility.EnsureLayer(EditorLayerUtility.GrabbableLayerName);

            EnsureGround(groundLayer);
            var barrels = EnsureBarrels(handConfig, throwableConfig, grabbableLayer);
            var godHand = EnsureGodHand();
            var cameraRig = EnsureCameraRig(cameraConfig, groundLayer, grabbableLayer);
            EnsureMainCameraAndListener();
            var systemsScope = EnsureSystemsInstance();

            WireScope(systemsScope, handConfig, cameraConfig, godHand, cameraRig);
            EnsureAutoInjectGameObjects(systemsScope, barrels);

            Undo.CollapseUndoOperations(undoGroup);

            EditorSceneManager.MarkSceneDirty(scene);
            if (sceneExists)
                EditorSceneManager.SaveScene(scene);
            else
                EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Build Test Hand Scene завершён: " + ScenePath +
                      " (в Build Settings не добавлена — временная песочница).");
        }

        // ------------------------------------------------------------------------------------
        // Земля
        // ------------------------------------------------------------------------------------

        // Толщина объёмного пола (Cube, не Plane — см. В4 ревью): бесконечно тонкий MeshCollider
        // Plane пропускает быстрые объекты (velocity-follow до 15 м/с, бросок до 20 м/с) насквозь.
        private const float GroundThickness = 1f;
        private const float GroundHalfSize = 10f; // 20x20 по X/Z, как раньше.

        /// <summary>
        /// Публичный метод — переиспользуется <c>TestVillageSceneSetupEditor</c> (шаг 3), чтобы не
        /// дублировать миграционную логику земли (см. .claude/rules/editor-files.md, "Смена «чем
        /// объект должен быть» требует миграции").
        /// </summary>
        public static void EnsureGround(int groundLayer)
        {
            var go = FindInSceneIncludingInactive("Ground");
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Ground";
                Undo.RegisterCreatedObjectUndo(go, "Create Ground");
                Debug.Log("[Editor] Создана земля: Ground (объёмный Cube 20x1x20, верх на y=0).");
            }
            else
            {
                // Миграционная идемпотентность (.claude/rules/editor-files.md): объект мог быть собран
                // старой версией скрипта как Plane (нулевая толщина, MeshCollider) — сама по себе
                // правка transform'а этого не чинит, меш/коллайдер остаются от прежней сборки.
                MigrateGroundToCube(go);
            }

            // Верх куба на y=0 (та же плоскость, что раньше давал Plane) — центр смещён вниз на половину толщины.
            // Валидно ТОЛЬКО для куба (полувысота 0.5 при scale.y=1) — для Plane (локальный y=0 — сама
            // плоскость) это давало расхождение в полметра с фоллбэком PointerWorldProjector (y=0).
            go.transform.position = new Vector3(0f, -GroundThickness * 0.5f, 0f);
            go.transform.localScale = new Vector3(GroundHalfSize * 2f, GroundThickness, GroundHalfSize * 2f);
            go.isStatic = true;
            if (groundLayer >= 0) go.layer = groundLayer;
        }

        /// <summary>
        /// Мигрирует уже существующий Ground (например, собранный старой версией скрипта как Plane)
        /// в объёмный куб — без пересоздания GameObject, чтобы не потерять слой и возможные ручные
        /// правки/ссылки на этот instance. Идемпотентна: на уже мигрированном кубе — no-op.
        /// </summary>
        private static void MigrateGroundToCube(GameObject go)
        {
            var cubeMesh = GetPrimitiveMesh(PrimitiveType.Cube);

            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = Undo.AddComponent<MeshFilter>(go);
            if (meshFilter.sharedMesh != cubeMesh)
            {
                Undo.RecordObject(meshFilter, "Migrate Ground Mesh To Cube");
                meshFilter.sharedMesh = cubeMesh;
                Debug.Log("[Editor] Ground: меш заменён на объёмный Cube (был Plane/иной от старой сборки).");
            }

            if (go.GetComponent<MeshRenderer>() == null)
                Undo.AddComponent<MeshRenderer>(go);

            // MeshCollider Plane бесконечно тонкий — быстрые объекты (velocity-follow/бросок) проходят
            // насквозь. Заменяем на объёмный BoxCollider, соответствующий кубическому мешу.
            var meshCollider = go.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                Undo.DestroyObjectImmediate(meshCollider);
                Debug.Log("[Editor] Ground: удалён устаревший MeshCollider (бесконечно тонкий) от старой сборки.");
            }

            if (go.GetComponent<BoxCollider>() == null)
                Undo.AddComponent<BoxCollider>(go);
        }

        /// <summary>
        /// Достаёт встроенный меш примитива Unity (например, стандартный Cube), не оставляя
        /// служебный GameObject в сцене — создаёт временный примитив, забирает sharedMesh, уничтожает.
        /// </summary>
        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        // ------------------------------------------------------------------------------------
        // Кубы-«бочки»
        // ------------------------------------------------------------------------------------

        private static List<GameObject> EnsureBarrels(HandConfig handConfig, ThrowableConfig throwableConfig, int grabbableLayer)
        {
            // MaxGrabMass не найден (конфиг ещё не создан) — используем дефолт, чтобы сцена всё равно собралась.
            float maxGrabMass = handConfig != null ? handConfig.MaxGrabMass : 5f;
            float lightMass = maxGrabMass * 0.5f;
            float heavyMass = maxGrabMass * 2f;

            var heavyNames = new List<string>();
            var barrels = new List<GameObject>(BarrelCount);
            int createdCount = 0;

            int columns = 4;
            for (int i = 0; i < BarrelCount; i++)
            {
                string name = $"Barrel_{i:00}";
                // По компоненту/имени, включая неактивные (GameObject.Find их не видит — НБ8) —
                // выключенный вручную Barrel_03 иначе получил бы дубль при повторном прогоне.
                var go = FindInSceneIncludingInactive(name);
                bool isNewBarrel = go == null;
                if (isNewBarrel)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = name;
                    Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                    createdCount++;
                }

                barrels.Add(go);

                if (grabbableLayer >= 0) go.layer = grabbableLayer;

                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = Undo.AddComponent<Rigidbody>(go);

                bool isHeavy = i >= HeavyBarrelStartIndex;
                if (isHeavy) heavyNames.Add(name);

                // Позицию/массу трогаем ТОЛЬКО у новосозданных — иначе расставленную вручную на
                // сцене сборку (позиции, подкрученная масса) затирает каждый повторный прогон (НБ8).
                if (isNewBarrel)
                {
                    int row = i / columns;
                    int col = i % columns;
                    go.transform.position = new Vector3(
                        (col - (columns - 1) / 2f) * BarrelGridSpacing,
                        0.5f,
                        row * BarrelGridSpacing + 2f);

                    rb.mass = isHeavy ? heavyMass : lightMass;
                }

                var throwableController = go.GetComponent<ThrowableObjectController>();
                if (throwableController == null)
                    throwableController = Undo.AddComponent<ThrowableObjectController>(go);

                // Шаг 3 (ревью code-writer'а): порог приземления переехал из HandConfig в прямой
                // SerializeField на самом префабе/объекте (ThrowableObjectController._throwableConfig,
                // не через DI) — без прошивки ObjectLandedEvent молча не публикуется ни разу.
                WireThrowableConfig(throwableController, throwableConfig, name);
            }

            Debug.Log($"[Editor] Бросаемые кубы: {createdCount} новых, {BarrelCount - createdCount} уже существовали " +
                      $"(позиция/масса не тронуты). Заведомо тяжелее MaxGrabMass ({maxGrabMass}) по схеме: " +
                      $"{string.Join(", ", heavyNames)} (масса {heavyMass} — только для новосозданных). " +
                      $"Остальные (новосозданные) — масса {lightMass}.");

            return barrels;
        }

        /// <summary>
        /// Прошивает ссылку на ThrowableConfig на конкретном бросаемом объекте (поле-имя
        /// <c>_throwableConfig</c>, сверено с фактическим ThrowableObjectController, шаг 3). Прошивается
        /// безусловно на каждом прогоне (не только на новосозданных) — это обязательная зависимость,
        /// а не пользовательская подстройка; не присваивает null, если конфиг ещё не создан.
        /// </summary>
        private static void WireThrowableConfig(ThrowableObjectController controller, ThrowableConfig throwableConfig, string ownerName)
        {
            if (throwableConfig == null) return; // предупреждение уже выдано один раз выше, на уровне сцены.

            var so = new SerializedObject(controller);
            var prop = so.FindProperty("_throwableConfig");
            if (prop == null)
            {
                Debug.LogWarning("[Editor] Поле \"_throwableConfig\" не найдено на ThrowableObjectController (" +
                                  ownerName + ") — проверьте, что имя SerializeField не изменилось в классе.");
                return;
            }

            if (prop.objectReferenceValue == throwableConfig) return; // уже прошито — не логируем шум каждый прогон.

            prop.objectReferenceValue = throwableConfig;
            so.ApplyModifiedProperties();
        }

        // ------------------------------------------------------------------------------------
        // Рука бога
        // ------------------------------------------------------------------------------------

        /// <summary>Публичный — переиспользуется <c>TestVillageSceneSetupEditor</c> (шаг 3).</summary>
        public static GodHandController EnsureGodHand()
        {
            var existing = Object.FindAnyObjectByType<GodHandController>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GodHandPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Editor] GodHand.prefab не найден по пути " + GodHandPrefabPath +
                                  " — сначала прогоните \"BlackWhiteLike/Setup/Setup God Hand Prefab\".");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Create GodHand Instance");
            Debug.Log("[Editor] Инстанс GodHand.prefab добавлен в сцену.");
            return instance.GetComponent<GodHandController>();
        }

        // ------------------------------------------------------------------------------------
        // Камера
        // ------------------------------------------------------------------------------------

        /// <summary>Публичный — переиспользуется <c>TestVillageSceneSetupEditor</c> (шаг 3).</summary>
        public static CameraRig EnsureCameraRig(IsometricCameraConfig cameraConfig, int groundLayer, int grabbableLayer)
        {
            var rig = Object.FindAnyObjectByType<CameraRig>(FindObjectsInactive.Include);
            GameObject rigGo;
            if (rig == null)
            {
                rigGo = new GameObject("CameraRig");
                Undo.RegisterCreatedObjectUndo(rigGo, "Create CameraRig");
                rig = Undo.AddComponent<CameraRig>(rigGo);
            }
            else
            {
                rigGo = rig.gameObject;
            }

            IsometricPanZoomBehaviour panZoom = rigGo.GetComponent<IsometricPanZoomBehaviour>();
            if (panZoom == null)
                panZoom = Undo.AddComponent<IsometricPanZoomBehaviour>(rigGo);

            float pitch = cameraConfig != null ? cameraConfig.Pitch : DefaultCameraPitch;
            float yaw = cameraConfig != null ? cameraConfig.Yaw : 0f;
            float distance = cameraConfig != null ? cameraConfig.DefaultZoomDistance : DefaultCameraDistance;

            var vcamTransform = rigGo.transform.Find("IsoVCam");
            CinemachineCamera vcam;
            if (vcamTransform == null)
            {
                var vcamGo = new GameObject("IsoVCam");
                Undo.RegisterCreatedObjectUndo(vcamGo, "Create IsoVCam");
                vcamGo.transform.SetParent(rigGo.transform, worldPositionStays: false);
                vcam = Undo.AddComponent<CinemachineCamera>(vcamGo);
            }
            else
            {
                vcam = vcamTransform.GetComponent<CinemachineCamera>();
                if (vcam == null) vcam = Undo.AddComponent<CinemachineCamera>(vcamTransform.gameObject);
            }

            // Архитектура рига (см. CameraRig/IsometricPanZoomBehaviour): _pivot = сам CameraRig
            // (его локальный поворот несёт Pitch/Yaw, применяется в CameraRig.Construct в рантайме;
            // здесь — только начальное превью для редактора). Камера — ребёнок pivot'а на dolly-
            // дистанции вдоль локальной -Z (zoom = _zoomTransform.localPosition.z, см. IsometricPanZoomBehaviour).
            rigGo.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            vcam.transform.localPosition = new Vector3(0f, 0f, -distance);
            vcam.transform.localRotation = Quaternion.identity;

            WireCameraRig(rig, panZoom, vcam, rigGo.transform, groundLayer, grabbableLayer);

            Debug.Log($"[Editor] CameraRig собран: pitch={pitch}, yaw={yaw}, дистанция={distance} (из IsometricCameraConfig).");
            return rig;
        }

        /// <summary>
        /// Прошивает SerializeField-ссылки на CameraRig и IsometricPanZoomBehaviour. Без этого шага
        /// компоненты добавлены, но пусты — камера и pan/zoom не заработают в Play Mode (найдено
        /// code-writer'ом после сдачи шага 2). Имена полей — из фактических классов.
        /// </summary>
        private static void WireCameraRig(CameraRig rig, IsometricPanZoomBehaviour panZoom, CinemachineCamera vcam,
            Transform pivot, int groundLayer, int grabbableLayer)
        {
            var rigSo = new SerializedObject(rig);
            SetRef(rigSo, "_cinemachineCamera", vcam);
            SetRef(rigSo, "_pivot", pivot);
            SetRef(rigSo, "_panZoomBehaviour", panZoom);
            rigSo.ApplyModifiedProperties();

            var panZoomSo = new SerializedObject(panZoom);
            SetRef(panZoomSo, "_pivot", pivot);
            SetRef(panZoomSo, "_zoomTransform", vcam.transform);
            EditorLayerUtility.SetLayerMask(panZoomSo, "_groundMask", groundLayer, "IsometricPanZoomBehaviour");
            EditorLayerUtility.SetLayerMask(panZoomSo, "_grabbableMask", grabbableLayer, "IsometricPanZoomBehaviour");
            panZoomSo.ApplyModifiedProperties();
        }

        /// <summary>Публичный — переиспользуется <c>TestVillageSceneSetupEditor</c> (шаг 3).</summary>
        public static void EnsureMainCameraAndListener()
        {
            var cameras = Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject camGo;
            if (cameras.Length == 0)
            {
                camGo = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(camGo, "Create Main Camera");
                Undo.AddComponent<UnityEngine.Camera>(camGo);
                Debug.Log("[Editor] Создана Main Camera.");
            }
            else
            {
                camGo = SelectMainCamera(cameras);
                if (cameras.Length > 1)
                {
                    Debug.Log($"[Editor] В сцене {cameras.Length} камер(ы) — выбрана \"{camGo.name}\" " +
                              "(приоритет: тег MainCamera → уже есть CinemachineBrain → по имени).");
                }
            }

            if (camGo.tag != Tags.MainCamera)
            {
                Undo.RecordObject(camGo, "Set MainCamera Tag");
                camGo.tag = Tags.MainCamera;
            }

            if (camGo.GetComponent<CinemachineBrain>() == null)
            {
                Undo.AddComponent<CinemachineBrain>(camGo);
                Debug.Log("[Editor] На Main Camera добавлен CinemachineBrain.");
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
        }

        /// <summary>
        /// Детерминированный выбор камеры среди нескольких найденных (НБ9 ревью — cameras[0] из
        /// несортированного FindObjectsByType давал случайный результат). Приоритет: тег MainCamera →
        /// уже есть CinemachineBrain → детерминированно по имени (Ordinal), чтобы результат не зависел
        /// от порядка обхода сцены.
        /// </summary>
        private static GameObject SelectMainCamera(UnityEngine.Camera[] cameras)
        {
            foreach (var cam in cameras)
                if (cam.CompareTag(Tags.MainCamera)) return cam.gameObject;

            foreach (var cam in cameras)
                if (cam.GetComponent<CinemachineBrain>() != null) return cam.gameObject;

            var best = cameras[0];
            foreach (var cam in cameras)
                if (string.CompareOrdinal(cam.name, best.name) < 0) best = cam;
            return best.gameObject;
        }

        // ------------------------------------------------------------------------------------
        // [Systems]
        // ------------------------------------------------------------------------------------

        /// <summary>Публичный — переиспользуется <c>TestVillageSceneSetupEditor</c> (шаг 3).</summary>
        public static GameLifetimeScope EnsureSystemsInstance()
        {
            var existing = Object.FindAnyObjectByType<GameLifetimeScope>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SystemsPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Editor] SystemsRoot.prefab не найден по пути " + SystemsPrefabPath +
                                  " — сначала прогоните \"BlackWhiteLike/Setup/Build Bootstrap Scene\" (создаёт префаб).");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Create [Systems] Instance");
            Debug.Log("[Editor] Инстанс [Systems] (SystemsRoot.prefab) добавлен в сцену.");
            return instance.GetComponent<GameLifetimeScope>();
        }

        // ------------------------------------------------------------------------------------
        // Прошивка ссылок на Composition Root
        // ------------------------------------------------------------------------------------

        private static void WireScope(GameLifetimeScope scope, HandConfig handConfig, IsometricCameraConfig cameraConfig,
            GodHandController godHand, CameraRig cameraRig)
        {
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в сцене — прошивка ссылок пропущена.");
                return;
            }

            var so = new SerializedObject(scope);
            SetRef(so, "_handConfig", handConfig);
            SetRef(so, "_isometricCameraConfig", cameraConfig);
            SetRef(so, "_godHandController", godHand);
            SetRef(so, "_cameraRig", cameraRig);
            so.ApplyModifiedProperties();
        }

        private static void SetRef(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на GameLifetimeScope — прошивка пропущена " +
                                  "(проверьте, что code-writer добавил это SerializeField).");
                return;
            }

            if (value == null)
            {
                Debug.LogWarning($"[Editor] Значение для поля \"{propertyName}\" не найдено — оставлено как было (не присваиваем null).");
                return;
            }

            prop.objectReferenceValue = value;
            Debug.Log($"[Editor] GameLifetimeScope.{propertyName} прошито.");
        }

        // ------------------------------------------------------------------------------------
        // VContainer auto-inject (грабля из CLAUDE.md: обычные сценные объекты не получают
        // [Inject] Construct без явной регистрации в autoInjectGameObjects базового LifetimeScope).
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Добавляет объекты в <c>LifetimeScope.autoInjectGameObjects</c> (protected List&lt;GameObject&gt;,
        /// см. Library/PackageCache/jp.hadashikick.vcontainer@.../Runtime/Unity/LifetimeScope.cs) —
        /// без этого VContainer никогда не вызовет им Construct(), инициализация останется false и
        /// поведение молча не сработает. Идемпотентно: не добавляет уже присутствующие записи и не
        /// удаляет чужие. Публичный и с общим именем параметра — переиспользуется
        /// <c>TestVillageSceneSetupEditor</c> (шаг 3) для жителей/NeedSite-объектов, не только для бочек.
        /// </summary>
        public static void EnsureAutoInjectGameObjects(GameLifetimeScope scope, List<GameObject> objectsToInject)
        {
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в сцене — autoInjectGameObjects не прошит.");
                return;
            }

            var so = new SerializedObject(scope);
            var listProp = so.FindProperty("autoInjectGameObjects");
            if (listProp == null)
            {
                Debug.LogWarning("[Editor] Поле \"autoInjectGameObjects\" не найдено на LifetimeScope — " +
                                  "проверьте версию пакета VContainer.");
                return;
            }

            var existing = new HashSet<GameObject>();
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (element != null) existing.Add(element);
            }

            int added = 0;
            foreach (var obj in objectsToInject)
            {
                if (obj == null || existing.Contains(obj)) continue;

                int index = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(index);
                listProp.GetArrayElementAtIndex(index).objectReferenceValue = obj;
                existing.Add(obj);
                added++;
            }

            so.ApplyModifiedProperties();
            Debug.Log($"[Editor] autoInjectGameObjects: добавлено {added} новых объектов, всего в списке {listProp.arraySize}.");
        }

        // ------------------------------------------------------------------------------------
        // Поиск по сцене, включая неактивные объекты (НБ9/НБ8: GameObject.Find их не видит).
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Ищет объект по имени во всей активной сцене, включая выключенные (SetActive(false))
        /// объекты и их выключенных родителей — в отличие от GameObject.Find. Обход дерева
        /// Transform не зависит от активности узлов, поэтому дубли-выключенные объекты находятся.
        /// Публичный — переиспользуется <c>TestVillageSceneSetupEditor</c> (шаг 3).
        /// </summary>
        public static GameObject FindInSceneIncludingInactive(string name)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindChildByName(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindChildByName(Transform current, string name)
        {
            if (current.name == name) return current;

            foreach (Transform child in current)
            {
                var found = FindChildByName(child, name);
                if (found != null) return found;
            }

            return null;
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
