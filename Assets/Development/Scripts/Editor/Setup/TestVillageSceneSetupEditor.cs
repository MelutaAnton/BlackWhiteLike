using System.Collections.Generic;
using System.IO;
using BlackWhiteLike.Controllers.Camera;
using BlackWhiteLike.Controllers.Hand;
using BlackWhiteLike.Controllers.Village;
using BlackWhiteLike.Data.Configs.Camera;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Throwable;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Installers;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Собирает временную песочницу _TestVillage.unity (game-design.md §19.3, шаг 3
    /// "Жители + события"): земля, 3 дома + 1 поле из POLYGON Fantasy Kingdom / Farm Pack (3 из
    /// них с NeedSiteController — Fire/Disease/Hunger), рука + камера (переиспользуют код
    /// TestHandSceneSetupEditor), [Systems] с прошитыми _villagerConfig/_levelConfig/_throwableConfig.
    /// Жителей сцена БОЛЬШЕ НЕ расставляет статически (ревью шага 3, контракт с code-writer'ом):
    /// их спавнит рантаймовый <c>VillagerSpawnService</c> из <c>LevelConfig.VillagerCount</c> и
    /// <c>VillageLayoutConfig.VillagerSpawnPositions</c> — эта сцена только наполняет эти два поля
    /// тестовыми данными (если они ещё пустые) и тестовый таймлайн событий-нужд.
    /// Временная сцена (подчёркивание в имени) — в Build Settings не добавляется.
    /// Идемпотентен: повторный запуск дособирает недостающее, не плодит объекты, не перезаписывает
    /// уже заполненные данные (позиции спавна/таймлайн — данные владельца, не производные).
    /// </summary>
    public static class TestVillageSceneSetupEditor
    {
        private const string ScenesFolder = "Assets/Development/Scenes";
        private const string ScenePath = ScenesFolder + "/_TestVillage.unity";

        private const string HandConfigPath = "Assets/Development/Data/Configs/Hand/HandConfig.asset";
        private const string CameraConfigPath = "Assets/Development/Data/Configs/Camera/IsometricCameraConfig.asset";
        private const string VillagerConfigPath = "Assets/Development/Data/Configs/Villagers/VillagerConfig.asset";
        private const string LevelConfigPath = "Assets/Development/Data/Configs/Levels/LevelConfig_01.asset";
        private const string VillageLayoutConfigPath = "Assets/Development/Data/Configs/Levels/VillageLayout_01.asset";
        private const string ThrowableConfigPath = "Assets/Development/Data/Configs/Throwable/ThrowableConfig.asset";

        // --- ThirdParty-источники домов/поля (только чтение) ---
        private const string HouseFolder =
            "Assets/ThirdParty/3D Environment/POLYGON - Fantasy Kingdom Pack/PolygonFantasyKingdom/Prefabs/Buildings/Presets/";
        private const string FarmPlantsFolder = "Assets/ThirdParty/3D Environment/POLYGON - Farm Pack/Prefabs/Plants/";

        /// <summary>
        /// Дома/поле: имя сценного объекта ← путь ThirdParty-префаба ← позиция ← тип нужды
        /// (null = без NeedSiteController). siteIndex назначается последовательно по порядку записей
        /// с ненулевым NeedType (см. EnsureStructures) — House_01=0/Fire, House_02=1/Disease,
        /// Field_Carrots=2/Hunger, House_03 без сайта (ревью шага 3, Б2).
        /// </summary>
        private static readonly (string Name, string PrefabPath, Vector3 Position, NeedType? NeedType)[] Structures =
        {
            ("House_01", HouseFolder + "SM_Bld_Preset_House_01_A_Optimized.prefab", new Vector3(-6f, 0f, -6f), NeedType.Fire),
            ("House_02", HouseFolder + "SM_Bld_Preset_House_02_A_Optimized.prefab", new Vector3(0f, 0f, -6f), NeedType.Disease),
            ("House_03", HouseFolder + "SM_Bld_Preset_House_03_Optimized.prefab", new Vector3(6f, 0f, -6f), null),
            ("Field_Carrots", FarmPlantsFolder + "SM_Prop_Carrot_01_Group.prefab", new Vector3(0f, 0f, 6f), NeedType.Hunger),
        };

        // Позиции спавна жителей (были статичной расстановкой до ревью шага 3) — теперь заполняют
        // VillageLayoutConfig.VillagerSpawnPositions, из которого рантаймовый VillagerSpawnService
        // реально спавнит жителей через IVillagerFactory.
        private static readonly Vector3[] DefaultVillagerSpawnPositions =
        {
            new Vector3(-6f, 0f, -2f), new Vector3(-2f, 0f, -2f), new Vector3(2f, 0f, -2f), new Vector3(6f, 0f, -2f),
            new Vector3(-6f, 0f, 2f), new Vector3(-2f, 0f, 2f), new Vector3(2f, 0f, 2f), new Vector3(6f, 0f, 2f),
        };

        /// <summary>Тестовый таймлайн (Б2-доп, ревью шага 3) — targetSiteIndex сверен с порядком в <see cref="Structures"/>.</summary>
        private static readonly (NeedType Type, float TriggerTime, int SiteIndex, float Duration)[] DefaultEventTimeline =
        {
            (NeedType.Fire, 3f, 0, 15f),
            (NeedType.Disease, 8f, 1, 15f),
            (NeedType.Hunger, 12f, 2, 15f),
        };

        [MenuItem("BlackWhiteLike/Setup/Build Test Village Scene")]
        public static void BuildTestVillageScene()
        {
            // БЕЗ модального диалога (ревью шага 3: EditorUtility.DisplayDialog вешает MCP-мост
            // намертво, пока диалог не закроет человек мышкой — реальный инцидент на этом самом
            // пункте меню) — либо сохраняем сами, либо отказываемся с понятным Debug.LogError.
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                if (string.IsNullOrEmpty(activeScene.path))
                {
                    Debug.LogError("[Editor] Build Test Village Scene прервана: текущая сцена \"" + activeScene.name +
                                    "\" ни разу не сохранялась на диск (нет пути) и содержит изменения — " +
                                    "автосохранение недоступно (вызвало бы диалог \"Save As\"). Сохраните " +
                                    "сцену вручную (File > Save) и запустите пункт меню снова.");
                    return;
                }

                if (!EditorSceneManager.SaveScene(activeScene))
                {
                    Debug.LogError("[Editor] Build Test Village Scene прервана: не удалось автоматически сохранить " +
                                    "текущую сцену \"" + activeScene.name + "\".");
                    return;
                }

                Debug.Log("[Editor] Текущая сцена \"" + activeScene.name + "\" содержала несохранённые изменения — " +
                          "сохранена автоматически перед сборкой _TestVillage.");
            }

            EnsureFolder(ScenesFolder);

            bool sceneExists = File.Exists(ScenePath);
            Scene scene = sceneExists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Debug.Log(sceneExists
                ? "[Editor] Открыта существующая сцена _TestVillage.unity — дособираем недостающее."
                : "[Editor] Создана новая пустая сцена под _TestVillage.");

            // Конфиги — грузим ПОСЛЕ открытия/создания сцены (OpenScene выгружает неиспользуемые
            // ассеты, см. .claude/rules/editor-files.md).
            var handConfig = AssetDatabase.LoadAssetAtPath<HandConfig>(HandConfigPath);
            var cameraConfig = AssetDatabase.LoadAssetAtPath<IsometricCameraConfig>(CameraConfigPath);
            var villagerConfig = AssetDatabase.LoadAssetAtPath<VillagerConfig>(VillagerConfigPath);
            var levelConfig = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelConfigPath);
            var villageLayoutConfig = AssetDatabase.LoadAssetAtPath<VillageLayoutConfig>(VillageLayoutConfigPath);
            var throwableConfig = AssetDatabase.LoadAssetAtPath<ThrowableConfig>(ThrowableConfigPath);
            WarnIfMissing(handConfig, HandConfigPath);
            WarnIfMissing(cameraConfig, CameraConfigPath);
            WarnIfMissing(villagerConfig, VillagerConfigPath);
            WarnIfMissing(levelConfig, LevelConfigPath);
            WarnIfMissing(villageLayoutConfig, VillageLayoutConfigPath);
            WarnIfMissing(throwableConfig, ThrowableConfigPath);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Build Test Village Scene");
            int undoGroup = Undo.GetCurrentGroup();

            int groundLayer = EditorLayerUtility.EnsureLayer(EditorLayerUtility.GroundLayerName);
            int grabbableLayer = EditorLayerUtility.EnsureLayer(EditorLayerUtility.GrabbableLayerName);

            // Земля — переиспользуем миграционно-идемпотентную логику TestHandSceneSetupEditor
            // (объёмный Cube, не Plane — см. .claude/rules/editor-files.md).
            TestHandSceneSetupEditor.EnsureGround(groundLayer);

            var needSiteObjects = EnsureStructures(grabbableLayer);

            // Данные для VillagerSpawnService (рантайм) — не сценные объекты (ревью шага 3: жителей
            // эта сцена больше не расставляет статически).
            PopulateVillageLayout(levelConfig, villageLayoutConfig);
            PopulateEventTimeline(levelConfig, needSiteObjects.Count);

            // Рука и камера — переиспользуем код TestHandSceneSetupEditor (не дублируем).
            var godHand = TestHandSceneSetupEditor.EnsureGodHand();
            var cameraRig = TestHandSceneSetupEditor.EnsureCameraRig(cameraConfig, groundLayer, grabbableLayer);
            TestHandSceneSetupEditor.EnsureMainCameraAndListener();
            var systemsScope = TestHandSceneSetupEditor.EnsureSystemsInstance();

            WireScope(systemsScope, handConfig, cameraConfig, villagerConfig, levelConfig, throwableConfig, godHand, cameraRig);

            // Только NeedSite-объекты — статических жителей в сцене больше нет (их Construct/Launch
            // вызывает VillagerFactory.Spawn через InjectGameObject, не autoInjectGameObjects).
            TestHandSceneSetupEditor.EnsureAutoInjectGameObjects(systemsScope, needSiteObjects);

            Undo.CollapseUndoOperations(undoGroup);

            EditorSceneManager.MarkSceneDirty(scene);
            if (sceneExists)
                EditorSceneManager.SaveScene(scene);
            else
                EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Build Test Village Scene завершён: " + ScenePath +
                      " (в Build Settings не добавлена — временная песочница).");
        }

        private static void WarnIfMissing(Object asset, string path)
        {
            if (asset == null)
                Debug.LogWarning("[Editor] " + path + " не найден — сначала прогоните " +
                                  "\"BlackWhiteLike/Configs/Create Empty SO Set\".");
        }

        // ------------------------------------------------------------------------------------
        // Дома / поле + NeedSiteController
        // ------------------------------------------------------------------------------------

        private static List<GameObject> EnsureStructures(int grabbableLayer)
        {
            var needSiteObjects = new List<GameObject>();
            int nextSiteIndex = 0;

            foreach (var (name, prefabPath, position, needType) in Structures)
            {
                var go = TestHandSceneSetupEditor.FindInSceneIncludingInactive(name);
                bool isNew = go == null;

                if (isNew)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                    {
                        Debug.LogWarning("[Editor] Не найден ThirdParty-префаб " + prefabPath + " — \"" + name + "\" не создан.");
                        continue;
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.name = name;
                    Undo.RegisterCreatedObjectUndo(instance, "Create " + name);
                    go = instance;
                    go.transform.position = position;
                    Debug.Log("[Editor] Создан объект структуры: " + name + " (" + prefabPath + ").");
                }
                // Позицию существующего объекта не трогаем — расстановка на сцене могла быть
                // подкручена вручную (тот же принцип, что и для бочек в TestHandSceneSetupEditor).

                if (needType.HasValue)
                {
                    EnsureNeedSite(go, nextSiteIndex, needType.Value, grabbableLayer);
                    needSiteObjects.Add(go);
                    nextSiteIndex++;
                }
            }

            Debug.Log($"[Editor] Структуры деревни: {Structures.Length} всего, NeedSiteController на " +
                      $"{needSiteObjects.Count} (индексы 0..{needSiteObjects.Count - 1}).");
            return needSiteObjects;
        }

        /// <summary>
        /// Добавляет триггер-зону чуда на структуру: kinematic Rigidbody (обязателен для срабатывания
        /// OnTrigger* — см. CLAUDE.md, "Триггеры требуют Rigidbody"), BoxCollider(isTrigger) по bounds
        /// рендереров, NeedSiteController с прошитыми _siteIndex/_needType/_villagerMask и тремя
        /// визуальными маркерами (ревью шага 3, Б2 — раньше все три сайта получали дефолт
        /// siteIndex=0/Fire без единого прошитого поля, фича не имела наблюдаемого результата).
        /// _villagerMask (LayerMask, ревью плейтеста шага 3) — маска слоя Grabbable, по которой
        /// NeedSiteController собирает жителей Physics.OverlapSphere при срабатывании нужды; без неё
        /// OverlapSphere не находит никого, и паника не возникает вообще (code-writer'у нужна эта
        /// прошивка, чтобы правка была не бесполезной). Размер BoxCollider выставляется только при
        /// первом добавлении компонента — повторный запуск не затирает подкрученный вручную объём триггера.
        /// </summary>
        private static void EnsureNeedSite(GameObject go, int siteIndex, NeedType needType, int grabbableLayer)
        {
            var rigidbody = go.GetComponent<Rigidbody>();
            if (rigidbody == null) rigidbody = Undo.AddComponent<Rigidbody>(go);
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var box = go.GetComponent<BoxCollider>();
            bool boxIsNew = box == null;
            if (boxIsNew)
            {
                box = Undo.AddComponent<BoxCollider>(go);
                box.isTrigger = true;
                SizeBoxFromRenderers(go, box);
            }
            else if (!box.isTrigger)
            {
                Undo.RecordObject(box, "Mark NeedSite Trigger");
                box.isTrigger = true;
            }

            var controller = go.GetComponent<NeedSiteController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<NeedSiteController>(go);
                Debug.Log("[Editor] NeedSiteController добавлен на \"" + go.name + "\".");
            }

            float topLocalY = ComputeLocalTopY(go);
            var markerLocalPosition = new Vector3(0f, topLocalY + 0.5f, 0f);
            var normalMarker = EnsureVisualMarker(go, "Visual_Normal", new Color(0.2f, 0.85f, 0.2f), markerLocalPosition, initiallyActive: true);
            var activeMarker = EnsureVisualMarker(go, "Visual_Active", new Color(0.95f, 0.3f, 0.05f), markerLocalPosition, initiallyActive: false);
            var expiredMarker = EnsureVisualMarker(go, "Visual_Expired", new Color(0.35f, 0.35f, 0.35f), markerLocalPosition, initiallyActive: false);

            WireNeedSite(controller, siteIndex, needType, normalMarker, activeMarker, expiredMarker, grabbableLayer);
        }

        /// <summary>
        /// Прошивает все 6 обязательных полей NeedSiteController (Н8, ревью шага 3; _villagerMask
        /// добавлен ревью плейтеста шага 3): счётчик "прошито N из M" — LogError, если не все поля
        /// прошиты (частичная прошивка = сцена собралась, но фича молча не работает в Play Mode,
        /// см. .claude/rules/editor-files.md).
        /// </summary>
        private static void WireNeedSite(NeedSiteController controller, int siteIndex, NeedType needType,
            GameObject normalVisual, GameObject activeVisual, GameObject expiredVisual, int grabbableLayer)
        {
            const int expected = 6;
            var so = new SerializedObject(controller);
            var ownerLabel = "NeedSiteController(\"" + controller.name + "\")";

            int wired = 0;
            wired += SetIntProp(so, "_siteIndex", siteIndex, ownerLabel) ? 1 : 0;
            wired += SetIntProp(so, "_needType", (int)needType, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_normalVisual", normalVisual, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_activeVisual", activeVisual, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_expiredVisual", expiredVisual, ownerLabel) ? 1 : 0;
            wired += WireVillagerMask(so, "_villagerMask", grabbableLayer, ownerLabel) ? 1 : 0;

            so.ApplyModifiedProperties();

            if (wired == expected)
            {
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected} (siteIndex={siteIndex}, needType={needType}, " +
                          $"_villagerMask=Grabbable).");
            }
            else
            {
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей — " +
                                "проверьте имена SerializeField в NeedSiteController, иначе фича молча не работает в Play Mode.");
            }
        }

        /// <summary>
        /// Прошивает LayerMask-поле слоем Grabbable через единственный источник — EditorLayerUtility.
        /// SetLayerMask (та же утилита, что и для рук/камеры) — и подтверждает успех численно
        /// (m_Bits == 1 &lt;&lt; layerIndex), а не «поле не пустое»: пустая маска молча делает
        /// Physics.OverlapSphere в NeedSiteController бесполезным (OverlapSphere не находит никого),
        /// поэтому недостаточно просто не упасть на LogWarning — нужно подтвердить конкретное число.
        /// </summary>
        private static bool WireVillagerMask(SerializedObject so, string propertyName, int layerIndex, string ownerLabel)
        {
            EditorLayerUtility.SetLayerMask(so, propertyName, layerIndex, ownerLabel);

            var prop = so.FindProperty(propertyName);
            if (prop == null || layerIndex < 0) return false; // EditorLayerUtility уже залогировал причину.

            return prop.intValue == (1 << layerIndex);
        }

        /// <summary>
        /// Верх bounds структуры в её локальных координатах (для позиции визуальных маркеров над
        /// крышей/грядкой) — считается по GetComponentsInChildren&lt;Renderer&gt;, тот же подход,
        /// что и SizeBoxFromRenderers/SizeCapsuleFromRenderers в соседних скриптах.
        /// </summary>
        private static float ComputeLocalTopY(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 1f;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var topWorld = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            return go.transform.InverseTransformPoint(topWorld).y;
        }

        /// <summary>
        /// Создаёт (если ещё нет) простой примитив-маркер под визуальное состояние NeedSite.
        /// Идемпотентен по имени child'а — уже существующий маркер не трогается (цвет/позиция могут
        /// быть подкручены художником вручную). Коллайдер примитива уничтожается — маркер чисто
        /// визуальный и не должен участвовать в физике/триггере самого NeedSite.
        /// </summary>
        private static GameObject EnsureVisualMarker(GameObject parent, string name, Color color, Vector3 localPosition, bool initiallyActive)
        {
            var existing = parent.transform.Find(name);
            if (existing != null) return existing.gameObject;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            Undo.RegisterCreatedObjectUndo(marker, "Create " + name);
            marker.transform.SetParent(parent.transform, worldPositionStays: false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = Vector3.one * 0.6f;

            var markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
                Undo.DestroyObjectImmediate(markerCollider);

            var renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    var material = new Material(shader) { name = name + "_Mat" };
                    material.SetColor("_BaseColor", color);
                    renderer.sharedMaterial = material;
                }
                else
                {
                    Debug.LogWarning("[Editor] Шейдер \"Universal Render Pipeline/Lit\" не найден — маркер \"" + name + "\" останется с дефолтным материалом.");
                }
            }

            marker.SetActive(initiallyActive);
            Debug.Log("[Editor] Создан визуальный маркер \"" + name + "\" под \"" + parent.name + "\".");
            return marker;
        }

        private static void SizeBoxFromRenderers(GameObject go, BoxCollider box)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[Editor] \"" + go.name + "\": рендереры не найдены — BoxCollider оставлен с дефолтными размерами.");
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            box.center = go.transform.InverseTransformPoint(bounds.center);

            var scale = go.transform.lossyScale;
            box.size = new Vector3(
                scale.x != 0f ? bounds.size.x / scale.x : bounds.size.x,
                scale.y != 0f ? bounds.size.y / scale.y : bounds.size.y,
                scale.z != 0f ? bounds.size.z / scale.z : bounds.size.z);
        }

        // ------------------------------------------------------------------------------------
        // Данные для рантаймового спавна жителей (VillagerSpawnService) — не сценные объекты
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Заполняет VillageLayoutConfig.VillagerSpawnPositions тестовыми позициями и согласует
        /// LevelConfig.VillagerCount с их числом — только если позиции ещё пустые (данные владельца,
        /// не перезаписываем повторным прогоном, тот же принцип, что и PopulateEventTimeline).
        /// Также прошивает LevelConfig._layout ссылкой на VillageLayoutConfig, если ещё не прошито —
        /// обязательная связь, не пользовательская подстройка.
        /// </summary>
        private static void PopulateVillageLayout(LevelConfig levelConfig, VillageLayoutConfig layoutConfig)
        {
            if (layoutConfig == null)
            {
                Debug.LogWarning("[Editor] VillageLayoutConfig не найден — позиции спавна жителей не заполнены. " +
                                  "Сначала прогоните \"BlackWhiteLike/Configs/Create Empty SO Set\".");
            }
            else
            {
                var layoutSo = new SerializedObject(layoutConfig);
                var spawnProp = layoutSo.FindProperty("_villagerSpawnPositions");
                if (spawnProp == null)
                {
                    Debug.LogError("[Editor] Поле \"_villagerSpawnPositions\" не найдено на VillageLayoutConfig — позиции спавна не заполнены.");
                }
                else if (spawnProp.arraySize > 0)
                {
                    Debug.Log($"[Editor] VillageLayoutConfig.VillagerSpawnPositions уже заполнен ({spawnProp.arraySize}) — не перезаписываем.");
                }
                else
                {
                    spawnProp.arraySize = DefaultVillagerSpawnPositions.Length;
                    for (int i = 0; i < DefaultVillagerSpawnPositions.Length; i++)
                        spawnProp.GetArrayElementAtIndex(i).vector3Value = DefaultVillagerSpawnPositions[i];
                    layoutSo.ApplyModifiedProperties();

                    if (levelConfig != null)
                    {
                        var countSo = new SerializedObject(levelConfig);
                        var countProp = countSo.FindProperty("_villagerCount");
                        if (countProp != null)
                        {
                            countProp.intValue = DefaultVillagerSpawnPositions.Length;
                            countSo.ApplyModifiedProperties();
                        }
                        else
                        {
                            Debug.LogWarning("[Editor] Поле \"_villagerCount\" не найдено на LevelConfig — VillagerCount не согласован с числом точек спавна.");
                        }
                    }

                    Debug.Log($"[Editor] VillageLayoutConfig.VillagerSpawnPositions заполнен ({DefaultVillagerSpawnPositions.Length} точек), " +
                              $"LevelConfig.VillagerCount выставлен в {DefaultVillagerSpawnPositions.Length}.");
                }
            }

            if (levelConfig != null && layoutConfig != null)
            {
                var levelSo = new SerializedObject(levelConfig);
                var layoutProp = levelSo.FindProperty("_layout");
                if (layoutProp == null)
                {
                    Debug.LogWarning("[Editor] Поле \"_layout\" не найдено на LevelConfig — VillageLayoutConfig не прошит.");
                }
                else if (layoutProp.objectReferenceValue == null)
                {
                    layoutProp.objectReferenceValue = layoutConfig;
                    levelSo.ApplyModifiedProperties();
                    Debug.Log("[Editor] LevelConfig._layout прошит ссылкой на VillageLayout_01.asset.");
                }
            }
        }

        /// <summary>
        /// Заполняет LevelConfig.EventTimeline тремя тестовыми записями (Б2-доп, ревью шага 3) —
        /// только если таймлайн ещё пустой (данные владельца — крутит их сам, повторный прогон не
        /// должен затирать). targetSiteIndex сверяется с фактическим числом собранных NeedSite —
        /// запись за пределами диапазона всё равно пишется (это тестовые данные черновой сцены), но
        /// логируется предупреждением, чтобы расхождение было видно сразу, а не на плейтесте.
        /// </summary>
        private static void PopulateEventTimeline(LevelConfig levelConfig, int siteCount)
        {
            if (levelConfig == null) return;

            var so = new SerializedObject(levelConfig);
            var timelineProp = so.FindProperty("_eventTimeline");
            if (timelineProp == null)
            {
                Debug.LogError("[Editor] Поле \"_eventTimeline\" не найдено на LevelConfig — тестовый таймлайн не заполнен.");
                return;
            }

            if (timelineProp.arraySize > 0)
            {
                Debug.Log($"[Editor] LevelConfig.EventTimeline уже заполнен ({timelineProp.arraySize} записей) — не перезаписываем.");
                return;
            }

            timelineProp.arraySize = DefaultEventTimeline.Length;
            for (int i = 0; i < DefaultEventTimeline.Length; i++)
            {
                var (type, triggerTime, siteIndex, duration) = DefaultEventTimeline[i];
                if (siteIndex >= siteCount)
                {
                    Debug.LogWarning($"[Editor] LevelConfig.EventTimeline[{i}]: targetSiteIndex={siteIndex} выходит за число " +
                                      $"собранных NeedSite ({siteCount}) — запись записана, но сейчас событию некого обслуживать.");
                }

                var element = timelineProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("type").intValue = (int)type;
                element.FindPropertyRelative("triggerTime").floatValue = triggerTime;
                element.FindPropertyRelative("targetSiteIndex").intValue = siteIndex;
                element.FindPropertyRelative("duration").floatValue = duration;
            }

            so.ApplyModifiedProperties();
            Debug.Log($"[Editor] LevelConfig.EventTimeline заполнен {DefaultEventTimeline.Length} тестовыми записями " +
                      "(Fire@0 t=3 dur=15, Disease@1 t=8 dur=15, Hunger@2 t=12 dur=15).");
        }

        // ------------------------------------------------------------------------------------
        // Прошивка ссылок на Composition Root
        // ------------------------------------------------------------------------------------

        private static void WireScope(GameLifetimeScope scope, HandConfig handConfig, IsometricCameraConfig cameraConfig,
            VillagerConfig villagerConfig, LevelConfig levelConfig, ThrowableConfig throwableConfig,
            GodHandController godHand, CameraRig cameraRig)
        {
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в сцене — прошивка ссылок пропущена.");
                return;
            }

            const int expected = 7;
            var so = new SerializedObject(scope);
            var ownerLabel = "GameLifetimeScope";

            int wired = 0;
            wired += SetRef(so, "_handConfig", handConfig, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_isometricCameraConfig", cameraConfig, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_villagerConfig", villagerConfig, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_levelConfig", levelConfig, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_throwableConfig", throwableConfig, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_godHandController", godHand, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_cameraRig", cameraRig, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();

            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected} полей.");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей — " +
                                "сцена соберётся, но часть систем не заработает в Play Mode.");
        }

        /// <summary>Прошивает object-reference поле. Не присваивает null — оставляет как было (см. .claude/rules/editor-files.md).</summary>
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

        private static bool SetIntProp(SerializedObject so, string propertyName, int value, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — прошивка пропущена.");
                return false;
            }

            prop.intValue = value;
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
