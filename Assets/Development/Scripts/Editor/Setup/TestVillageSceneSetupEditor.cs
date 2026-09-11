using System.Collections.Generic;
using System.IO;
using BlackWhiteLike.Controllers.Camera;
using BlackWhiteLike.Controllers.Hand;
using BlackWhiteLike.Controllers.Miracles;
using BlackWhiteLike.Controllers.Vfx;
using BlackWhiteLike.Controllers.Village;
using BlackWhiteLike.Data.Configs.Camera;
using BlackWhiteLike.Data.Configs.Faith;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Data.Configs.Throwable;
using BlackWhiteLike.Data.Configs.Tutorial;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Data.Configs.Vfx;
using BlackWhiteLike.Data.Configs.Worship;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Editor.Configs;
using BlackWhiteLike.Editor.Prefabs;
using BlackWhiteLike.Editor.UI;
using BlackWhiteLike.Installers;
using BlackWhiteLike.Models;
using BlackWhiteLike.Views.UI.Hud;
using BlackWhiteLike.Views.UI.Overlay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Собирает временную песочницу _TestVillage.unity (game-design.md §19.3, шаг 3
    /// "Жители + события"; §19.4, шаг 4 "Чудеса + Faith"): земля, 3 дома + 1 поле — инстансы НАШИХ
    /// префабов структур деревни (Assets/Development/Prefabs/Village/, собираются
    /// <see cref="VillageStructurePrefabSetupEditor"/> из визуала POLYGON Fantasy Kingdom / Farm Pack;
    /// правка после playtest'а шага 4 — CLAUDE.md "Из ThirdParty ... только визуал, никогда —
    /// префаб-инстанс", 3 из них с NeedSiteController — Fire/Disease/Hunger), рука +
    /// камера (переиспользуют код TestHandSceneSetupEditor), [Systems] с прошитыми
    /// _villagerConfig/_levelConfig/_throwableConfig, [Miracles] (прицеливание/удар/VFX чудес) и
    /// HUD (LevelHudCanvas.prefab + EventSystem).
    /// Жителей сцена БОЛЬШЕ НЕ расставляет статически (ревью шага 3, контракт с code-writer'ом):
    /// их спавнит рантаймовый <c>VillagerSpawnService</c> из <c>LevelConfig.VillagerCount</c> и
    /// <c>VillageLayoutConfig.VillagerSpawnPositions</c> — эта сцена только наполняет эти два поля
    /// тестовыми данными (если они ещё пустые) и тестовый таймлайн событий-нужд.
    /// Шаг 4 добавил конвейер зависимостей "конфиги → VFX-префабы → HUD → сцена" — каждый этап
    /// вызывается через публичный метод соответствующего Editor-скрипта, только если его результат
    /// ещё отсутствует на диске (см. EnsureStep4Configs/EnsureStep4VfxPrefabs/EnsureHudInstance).
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

        // Шаг 4 — Чудеса + Faith (game-design.md §19.4).
        private const string MiracleCatalogConfigPath = "Assets/Development/Data/Configs/Miracles/MiracleCatalog.asset";
        private const string FaithConfigPath = "Assets/Development/Data/Configs/Faith/FaithConfig.asset";
        private const string VfxConfigPath = "Assets/Development/Data/Configs/Vfx/VfxConfig.asset";
        private const string FireballVfxPrefabPath = "Assets/Development/Prefabs/VFX/Vfx_FireballExplosion.prefab";
        private const string RainVfxPrefabPath = "Assets/Development/Prefabs/VFX/Vfx_RainCast.prefab";
        private const string HudPrefabPath = "Assets/Development/Prefabs/UI/LevelHudCanvas.prefab";
        private const string MiraclePreviewRingMaterialPath = "Assets/Development/Art/Materials/MiraclePreviewRing.mat";

        // Шаг 5 — Worship + win (game-design.md §19.5).
        private const string WorshipConfigPath = "Assets/Development/Data/Configs/Worship/WorshipConfig.asset";

        // Tutorial — обучение первого уровня (TutorialConfig/TutorialView, Editor/UI/TutorialConfigSetupEditor.cs).
        private const string TutorialConfigPath = "Assets/Development/Data/Configs/Tutorial/TutorialConfig_Level01.asset";

        /// <summary>
        /// Позиции структур на сцене. Имя ← ThirdParty-источник ← масштаб ← NeedType — теперь
        /// единственный источник таблицы <see cref="VillageStructurePrefabSetupEditor.Structures"/>
        /// (правка после playtest'а шага 4, CLAUDE.md "Из ThirdParty ... только визуал"): эта сцена
        /// больше не инстанцирует ThirdParty-префабы напрямую, только позиционирует НАШИ префабы
        /// структур (Assets/Development/Prefabs/Village/&lt;Name&gt;.prefab). siteIndex назначается
        /// последовательно по порядку записей с ненулевым NeedType (см. EnsureStructures) —
        /// House_01=0/Fire, House_02=1/Disease, Field_Carrots=2/Hunger, House_03 без сайта
        /// (ревью шага 3, Б2).
        /// </summary>
        private static readonly (string Name, Vector3 Position)[] StructurePositions =
        {
            ("House_01", new Vector3(-6f, 0f, -6f)),
            ("House_02", new Vector3(0f, 0f, -6f)),
            ("House_03", new Vector3(6f, 0f, -6f)),
            ("Field_Carrots", new Vector3(0f, 0f, 6f)),
            // Шаг 6 — позиция свободна от домов/поля (владелец: (8, 0, 4)).
            ("Temple", new Vector3(8f, 0f, 4f)),
        };

        // Позиции спавна жителей (были статичной расстановкой до ревью шага 3) — теперь заполняют
        // VillageLayoutConfig.VillagerSpawnPositions, из которого рантаймовый VillagerSpawnService
        // реально спавнит жителей через IVillagerFactory.
        private static readonly Vector3[] DefaultVillagerSpawnPositions =
        {
            new Vector3(-6f, 0f, -2f), new Vector3(-2f, 0f, -2f), new Vector3(2f, 0f, -2f), new Vector3(6f, 0f, -2f),
            new Vector3(-6f, 0f, 2f), new Vector3(-2f, 0f, 2f), new Vector3(2f, 0f, 2f), new Vector3(6f, 0f, 2f),
        };

        /// <summary>Тестовый таймлайн (Б2-доп, ревью шага 3) — targetSiteIndex сверен с порядком в <see cref="StructurePositions"/>.</summary>
        private static readonly (NeedType Type, float TriggerTime, int SiteIndex, float Duration)[] DefaultEventTimeline =
        {
            (NeedType.Fire, 3f, 0, 15f),
            (NeedType.Disease, 8f, 1, 15f),
            (NeedType.Hunger, 12f, 2, 15f),
        };

        [MenuItem("BlackWhiteLike/Setup/Build Test Village Scene")]
        public static void BuildTestVillageScene()
        {
            BuildVillageSceneCore(ScenePath, GameState.LevelPlaying, isTemporarySandbox: true);
        }

        /// <summary>
        /// Ядро сборки деревенской сцены (шаг 3/4/5/6 + шаг 7 "Кампания из 5 уровней") — общее для
        /// временной песочницы <c>_TestVillage.unity</c> и боевой <c>Village_Base.unity</c>
        /// (<see cref="VillageLevelSetupEditor"/>, шаг 7): дома/поле/храм, рука, камера, [Systems],
        /// [Miracles], HUD, Alignment-визуал — идентичны для обеих сцен. Единственные различия между
        /// вызывающими — путь сцены и то, добавляется ли она в Build Settings (temporary sandbox — нет).
        /// <paramref name="initialState"/> прошивается в GameLifetimeScope._initialState (обе сцены
        /// сейчас используют LevelPlaying, но параметризовано на случай будущего расхождения).
        /// </summary>
        public static void BuildVillageSceneCore(string scenePath, GameState initialState, bool isTemporarySandbox)
        {
            string sceneLabel = Path.GetFileNameWithoutExtension(scenePath);

            // Шаг 4 — зависимости, не привязанные к активной сцене (конфиги, VFX-префабы):
            // собираются здесь, ДО работы со сценой, чтобы порядок "конфиги → VFX-префабы → HUD →
            // сцена" был гарантирован при первом же прогоне на чистом проекте. Каждый шаг
            // идемпотентен сам по себе — вызывается только если результат ещё отсутствует на диске.
            EnsureStep4Configs();
            EnsureStep4VfxPrefabs();
            // HUD (Editor/UI/LevelHudSetupEditor) собирается ПОСЛЕ открытия/создания сцены —
            // его EnsureSceneInstance кладёт инстанс в АКТИВНУЮ сцену, см. ниже по методу.

            // БЕЗ модального диалога (ревью шага 3: EditorUtility.DisplayDialog вешает MCP-мост
            // намертво, пока диалог не закроет человек мышкой — реальный инцидент на этом самом
            // пункте меню) — либо сохраняем сами, либо отказываемся с понятным Debug.LogError.
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                if (string.IsNullOrEmpty(activeScene.path))
                {
                    Debug.LogError("[Editor] Build " + sceneLabel + " прервана: текущая сцена \"" + activeScene.name +
                                    "\" ни разу не сохранялась на диск (нет пути) и содержит изменения — " +
                                    "автосохранение недоступно (вызвало бы диалог \"Save As\"). Сохраните " +
                                    "сцену вручную (File > Save) и запустите пункт меню снова.");
                    return;
                }

                if (!EditorSceneManager.SaveScene(activeScene))
                {
                    Debug.LogError("[Editor] Build " + sceneLabel + " прервана: не удалось автоматически сохранить " +
                                    "текущую сцену \"" + activeScene.name + "\".");
                    return;
                }

                Debug.Log("[Editor] Текущая сцена \"" + activeScene.name + "\" содержала несохранённые изменения — " +
                          "сохранена автоматически перед сборкой " + sceneLabel + ".");
            }

            EnsureFolder(ScenesFolder);

            bool sceneExists = File.Exists(scenePath);
            Scene scene = sceneExists
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Debug.Log(sceneExists
                ? "[Editor] Открыта существующая сцена " + sceneLabel + ".unity — дособираем недостающее."
                : "[Editor] Создана новая пустая сцена под " + sceneLabel + ".");

            // Конфиги — грузим ПОСЛЕ открытия/создания сцены (OpenScene выгружает неиспользуемые
            // ассеты, см. .claude/rules/editor-files.md).
            var handConfig = AssetDatabase.LoadAssetAtPath<HandConfig>(HandConfigPath);
            var cameraConfig = AssetDatabase.LoadAssetAtPath<IsometricCameraConfig>(CameraConfigPath);
            var villagerConfig = AssetDatabase.LoadAssetAtPath<VillagerConfig>(VillagerConfigPath);
            var levelConfig = AssetDatabase.LoadAssetAtPath<LevelConfig>(LevelConfigPath);
            var villageLayoutConfig = AssetDatabase.LoadAssetAtPath<VillageLayoutConfig>(VillageLayoutConfigPath);
            var throwableConfig = AssetDatabase.LoadAssetAtPath<ThrowableConfig>(ThrowableConfigPath);
            var miracleCatalogConfig = AssetDatabase.LoadAssetAtPath<MiracleCatalogConfig>(MiracleCatalogConfigPath);
            var faithConfig = AssetDatabase.LoadAssetAtPath<FaithConfig>(FaithConfigPath);
            var vfxConfig = AssetDatabase.LoadAssetAtPath<VfxConfig>(VfxConfigPath);
            var worshipConfig = AssetDatabase.LoadAssetAtPath<WorshipConfig>(WorshipConfigPath);
            var tutorialConfig = AssetDatabase.LoadAssetAtPath<TutorialConfig>(TutorialConfigPath);
            WarnIfMissing(handConfig, HandConfigPath);
            WarnIfMissing(cameraConfig, CameraConfigPath);
            WarnIfMissing(villagerConfig, VillagerConfigPath);
            WarnIfMissing(levelConfig, LevelConfigPath);
            WarnIfMissing(villageLayoutConfig, VillageLayoutConfigPath);
            WarnIfMissing(throwableConfig, ThrowableConfigPath);
            WarnIfMissing(miracleCatalogConfig, MiracleCatalogConfigPath);
            WarnIfMissing(faithConfig, FaithConfigPath);
            WarnIfMissing(vfxConfig, VfxConfigPath);
            WarnIfMissing(worshipConfig, WorshipConfigPath);
            WarnIfMissing(tutorialConfig, TutorialConfigPath);

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
            MigrateLevelConfig01TriggerModes(levelConfig);

            // Рука и камера — переиспользуем код TestHandSceneSetupEditor (не дублируем).
            var godHand = TestHandSceneSetupEditor.EnsureGodHand();
            var cameraRig = TestHandSceneSetupEditor.EnsureCameraRig(cameraConfig, groundLayer, grabbableLayer);
            TestHandSceneSetupEditor.EnsureMainCameraAndListener();
            var systemsScope = TestHandSceneSetupEditor.EnsureSystemsInstance();

            // Шаг 4 — [Miracles]: контроллеры прицеливания/удара/VFX чудес.
            var miraclesGo = EnsureMiraclesObject(groundLayer, grabbableLayer);
            var miracleTargeting = miraclesGo.GetComponent<MiracleTargetingController>();
            var miracleImpact = miraclesGo.GetComponent<MiracleImpactController>();
            var miracleVfx = miraclesGo.GetComponent<MiracleVfxController>();

            // Шаг 4 — HUD: EventSystem (Input System New) + инстанс LevelHudCanvas.prefab в АКТИВНОЙ
            // сцене (_TestVillage уже открыта/создана выше).
            LevelHudSetupEditor.EnsureEventSystem();
            EnsureHudInstance();
            var miraclePanelView = Object.FindAnyObjectByType<MiraclePanelView>(FindObjectsInactive.Include);
            var faithBarView = Object.FindAnyObjectByType<FaithBarView>(FindObjectsInactive.Include);

            // Шаг 5 — LevelHudView живёт на корне того же канваса, что и MiraclePanelView; оверлеи —
            // выключенные дети этого же корня (GetComponentInChildren(true) — иначе неактивные не найдутся).
            var levelHudView = Object.FindAnyObjectByType<LevelHudView>(FindObjectsInactive.Include);
            var hudRootTransform = levelHudView != null ? levelHudView.transform : null;
            var levelCompleteView = hudRootTransform != null ? hudRootTransform.GetComponentInChildren<LevelCompleteView>(true) : null;
            var levelStalledView = hudRootTransform != null ? hudRootTransform.GetComponentInChildren<LevelStalledView>(true) : null;
            var pauseOverlayView = hudRootTransform != null ? hudRootTransform.GetComponentInChildren<PauseOverlayView>(true) : null;

            // TutorialView — на корне того же HUD-канваса, что LevelHudView/MiraclePanelView (см. Editor/UI/LevelHudSetupEditor).
            var tutorialView = Object.FindAnyObjectByType<TutorialView>(FindObjectsInactive.Include);

            WireScope(systemsScope, handConfig, cameraConfig, villagerConfig, levelConfig, throwableConfig, godHand, cameraRig,
                miracleCatalogConfig, faithConfig, vfxConfig, miracleTargeting, miracleImpact, miracleVfx, miraclePanelView, faithBarView,
                worshipConfig, levelHudView, levelCompleteView, levelStalledView, pauseOverlayView, initialState,
                tutorialConfig, tutorialView);

            // Шаг 6 — Alignment-визуал (game-design.md §7, §10): материалы/конфиг/[AlignmentVisual] +
            // прошивка _handRenderers/_templeRenderers и ссылок на GameLifetimeScope. Вызывается
            // ПОСЛЕ размещения структур (Temple уже в сцене) и WireScope (scope уже существует).
            // Core-метод, а не MenuItem-обёртка (ревью шага 6, п.6) — эта сцена уже ведёт свою
            // внешнюю Undo-группу (undoGroup выше), вложенная группа рвала бы её пополам.
            AlignmentVisualSetupEditor.SetupAlignmentVisualCore();

            // NeedSite-объекты — MonoBehaviour без RegisterComponent, нужны в autoInjectGameObjects
            // (см. память агента "vcontainer-autoinjectgameobjects"). [Miracles] сюда НЕ входит:
            // его три контроллера прошиты в GameLifetimeScope через RegisterComponent из
            // SerializeField-полей (см. WireScope) — второй путь инъекции через autoInjectGameObjects
            // вызвал бы Construct() дважды (правка после ревью). HUD-View'ы по той же причине тоже
            // не добавляются: RegisterComponent из полей scope, презентеры — pure C#.
            // Миграция структур (см. EnsureStructures) уничтожает старые ThirdParty-объекты и
            // создаёт новые инстансы наших префабов — старые ссылки в autoInjectGameObjects
            // превращаются в missing-элементы (не null для C#, "fake-null" Unity-объекта), которые
            // EnsureAutoInjectGameObjects сам не убирает (только добавляет недостающее). Чистим их
            // здесь ДО добавления новых — иначе список растёт мусором с каждым прогоном на
            // немигрированной сцене.
            RemoveMissingAutoInjectEntries(systemsScope);

            var autoInjectObjects = new List<GameObject>(needSiteObjects);
            TestHandSceneSetupEditor.EnsureAutoInjectGameObjects(systemsScope, autoInjectObjects);
            RemoveFromAutoInject(systemsScope, miraclesGo);

            // Правило CLAUDE.md "Из ThirdParty ... только визуал" — проверяем ВСЮ собранную сцену
            // перед сохранением (обходит корневые объекты активной сцены, включая только что
            // мигрированные структуры).
            AssertNoThirdPartyPrefabLinksInScene(scene, sceneLabel);

            Undo.CollapseUndoOperations(undoGroup);

            EditorSceneManager.MarkSceneDirty(scene);
            if (sceneExists)
                EditorSceneManager.SaveScene(scene);
            else
                EditorSceneManager.SaveScene(scene, scenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (isTemporarySandbox)
            {
                Debug.Log("[Editor] Build " + sceneLabel + " завершён: " + scenePath +
                          " (в Build Settings не добавлена — временная песочница).");
            }
            else
            {
                BuildSettingsEditor.EnsureCampaignScenesInBuildSettings();
                Debug.Log("[Editor] Build " + sceneLabel + " завершён: " + scenePath + ".");
            }
        }

        /// <summary>
        /// Удаляет из <c>autoInjectGameObjects</c> элементы, чья ссылка стала missing (объект
        /// уничтожен — например, миграцией структур в <see cref="EnsureStructures"/>). Такие элементы
        /// НЕ null для C# (Unity-объект living dead, "fake-null" — CLAUDE.md, "??/?. запрещены с
        /// Unity-объектами"), явное сравнение с null через оператор Unity корректно ловит и это, и
        /// настоящий null. Идемпотентно: на уже чистом списке — no-op без лога.
        /// </summary>
        private static void RemoveMissingAutoInjectEntries(GameLifetimeScope scope)
        {
            if (scope == null) return;

            var so = new SerializedObject(scope);
            var listProp = so.FindProperty("autoInjectGameObjects");
            if (listProp == null) return;

            int removed = 0;
            for (int i = listProp.arraySize - 1; i >= 0; i--)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                // Явный "== null" (не ?./??) корректно ловит и missing-ссылку на уничтоженный
                // объект, и настоящую пустую ссылку — оба варианта здесь мусор.
                if (element.objectReferenceValue == null)
                {
                    listProp.DeleteArrayElementAtIndex(i);
                    removed++;
                }
            }

            if (removed == 0) return;

            so.ApplyModifiedProperties();
            Debug.Log($"[Editor] autoInjectGameObjects: удалено {removed} missing/пустых элемент(ов) (объекты уничтожены миграцией), " +
                      $"осталось {listProp.arraySize}.");
        }

        /// <summary>
        /// Правило CLAUDE.md "Из ThirdParty в сцену и в наши префабы попадает только визуал" —
        /// проверка ВСЕХ корневых объектов активной сцены после сборки/миграции структур.
        /// </summary>
        private static void AssertNoThirdPartyPrefabLinksInScene(Scene scene, string sceneLabel)
        {
            foreach (var root in scene.GetRootGameObjects())
                EditorPrefabUtility.AssertNoThirdPartyPrefabLinks(root, sceneLabel + "/\"" + root.name + "\"");
        }

        private static void WarnIfMissing(Object asset, string path)
        {
            if (asset == null)
                Debug.LogWarning("[Editor] " + path + " не найден — сначала прогоните " +
                                  "\"BlackWhiteLike/Configs/Create Empty SO Set\".");
        }

        /// <summary>
        /// Миграция (правка после ревью): в первом прогоне <c>[Miracles]</c> ошибочно попал в
        /// <c>autoInjectGameObjects</c> одновременно с RegisterComponent его контроллеров в
        /// GameLifetimeScope — Construct() вызывался бы дважды. Удаляет объект из списка, если он
        /// там есть; идемпотентно (повторный прогон на уже мигрированной сцене — no-op без лога).
        /// </summary>
        private static void RemoveFromAutoInject(GameLifetimeScope scope, GameObject go)
        {
            if (scope == null || go == null) return;

            var so = new SerializedObject(scope);
            var listProp = so.FindProperty("autoInjectGameObjects");
            if (listProp == null) return;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).objectReferenceValue as GameObject != go)
                    continue;

                listProp.DeleteArrayElementAtIndex(i);
                so.ApplyModifiedProperties();
                Debug.Log($"[Editor] autoInjectGameObjects: \"{go.name}\" удалён (миграция — регистрируется через " +
                          $"RegisterComponent в GameLifetimeScope), всего в списке {listProp.arraySize}.");
                return;
            }
        }

        // ------------------------------------------------------------------------------------
        // Шаг 4 — зависимости конвейера: конфиги → VFX-префабы → HUD (см. вызовы в начале
        // BuildTestVillageScene). Каждый шаг идемпотентен и вызывается только если результата
        // ещё нет на диске — повторный прогон не плодит лишних логов от уже собранных шагов.
        // ------------------------------------------------------------------------------------

        /// <summary>Конфиги шагов 4 и 5 — общий вызов ConfigAssetCreatorEditor.CreateAll() создаёт только недостающее (идемпотентно).</summary>
        private static void EnsureStep4Configs()
        {
            if (File.Exists(MiracleCatalogConfigPath) && File.Exists(FaithConfigPath) && File.Exists(VfxConfigPath) &&
                File.Exists(WorshipConfigPath))
                return;

            Debug.Log("[Editor] Конфиги шагов 4/5 (Miracles/Faith/Vfx/Worship) не найдены — вызываю ConfigAssetCreatorEditor.CreateAll().");
            ConfigAssetCreatorEditor.CreateAll();
        }

        private static void EnsureStep4VfxPrefabs()
        {
            if (File.Exists(FireballVfxPrefabPath) && File.Exists(RainVfxPrefabPath))
                return;

            Debug.Log("[Editor] VFX-префабы шага 4 не найдены — вызываю MiracleVfxPrefabSetupEditor.SetupMiracleVfxPrefabs().");
            MiracleVfxPrefabSetupEditor.SetupMiracleVfxPrefabs();
        }

        /// <summary>
        /// HUD собирается ПОСЛЕ открытия/создания _TestVillage — EnsureSceneInstance кладёт инстанс
        /// в АКТИВНУЮ сцену (SceneManager.GetActiveScene()), которой к моменту вызова уже является
        /// _TestVillage.
        /// </summary>
        private static void EnsureHudInstance()
        {
            var existingView = Object.FindAnyObjectByType<MiraclePanelView>(FindObjectsInactive.Include);
            if (existingView != null) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Editor] " + HudPrefabPath + " не найден — вызываю LevelHudSetupEditor.BuildLevelHud().");
                LevelHudSetupEditor.BuildLevelHud();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            }

            if (prefab == null)
            {
                Debug.LogError("[Editor] " + HudPrefabPath + " всё ещё не собран (см. лог выше — возможно, ждёт " +
                                "асинхронного импорта TMP Essentials) — HUD не размещён в _TestVillage. " +
                                "Прогоните \"BlackWhiteLike/UI/Build Level HUD\" вручную и повторите сборку сцены.");
                return;
            }

            // BuildLevelHud() уже мог сам разместить инстанс в активной сцене — проверяем ещё раз,
            // чтобы не создать дубль.
            if (Object.FindAnyObjectByType<MiraclePanelView>(FindObjectsInactive.Include) != null) return;

            LevelHudSetupEditor.EnsureSceneInstance(prefab);
        }

        // ------------------------------------------------------------------------------------
        // Шаг 4 — [Miracles]: прицеливание/удар/VFX чудес
        // ------------------------------------------------------------------------------------

        private static GameObject EnsureMiraclesObject(int groundLayer, int grabbableLayer)
        {
            var go = TestHandSceneSetupEditor.FindInSceneIncludingInactive("[Miracles]");
            if (go == null)
            {
                go = new GameObject("[Miracles]");
                Undo.RegisterCreatedObjectUndo(go, "Create [Miracles]");
                Debug.Log("[Editor] Создан объект \"[Miracles]\".");
            }

            var targeting = go.GetComponent<MiracleTargetingController>();
            if (targeting == null) targeting = Undo.AddComponent<MiracleTargetingController>(go);

            var impact = go.GetComponent<MiracleImpactController>();
            if (impact == null) impact = Undo.AddComponent<MiracleImpactController>(go);

            if (go.GetComponent<MiracleVfxController>() == null)
                Undo.AddComponent<MiracleVfxController>(go);

            var previewRoot = EnsurePreviewRing(go);

            WireMiracleTargeting(targeting, previewRoot, groundLayer);
            WireMiracleImpact(impact, grabbableLayer);

            return go;
        }

        /// <summary>
        /// Двухуровневая иерархия превью-кольца чуда: <c>PreviewRoot</c> (это <c>_previewRoot</c> —
        /// контроллер масштабирует ЕГО как (d,1,d), см. контракт) → дочерний <c>Ring</c> (примитив
        /// Cylinder с фиксированным localScale (1,0.02,1) — тонкий диск при localScale родителя (1,1,1)
        /// = диаметр 1 м). Коллайдер примитива удалён — кольцо визуальное, не должно участвовать в
        /// физике. Выключен по умолчанию (появляется только во время прицеливания).
        /// </summary>
        private static Transform EnsurePreviewRing(GameObject miraclesGo)
        {
            var previewRootTransform = miraclesGo.transform.Find("PreviewRoot");
            GameObject previewRootGo;
            if (previewRootTransform == null)
            {
                previewRootGo = new GameObject("PreviewRoot");
                Undo.RegisterCreatedObjectUndo(previewRootGo, "Create PreviewRoot");
                previewRootGo.transform.SetParent(miraclesGo.transform, worldPositionStays: false);
                Debug.Log("[Editor] Создан \"PreviewRoot\" под \"[Miracles]\".");
            }
            else
            {
                previewRootGo = previewRootTransform.gameObject;
            }

            previewRootGo.transform.localScale = Vector3.one;

            var ringTransform = previewRootGo.transform.Find("Ring");
            GameObject ringGo;
            if (ringTransform == null)
            {
                ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ringGo.name = "Ring";
                Undo.RegisterCreatedObjectUndo(ringGo, "Create Ring");
                ringGo.transform.SetParent(previewRootGo.transform, worldPositionStays: false);
                ringGo.transform.localPosition = Vector3.zero;

                var ringCollider = ringGo.GetComponent<Collider>();
                if (ringCollider != null) Undo.DestroyObjectImmediate(ringCollider);

                var renderer = ringGo.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = EnsurePreviewRingMaterial();

                Debug.Log("[Editor] Создан \"Ring\" под \"PreviewRoot\".");
            }
            else
            {
                ringGo = ringTransform.gameObject;
            }

            ringGo.transform.localScale = new Vector3(1f, 0.02f, 1f);
            previewRootGo.SetActive(false);

            return previewRootGo.transform;
        }

        private static Material EnsurePreviewRingMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MiraclePreviewRingMaterialPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Editor] Шейдер \"Universal Render Pipeline/Lit\" не найден — MiraclePreviewRing.mat не создан.");
                return null;
            }

            var material = new Material(shader) { name = "MiraclePreviewRing" };
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f); // Alpha
            material.SetColor("_BaseColor", new Color(0.4f, 0.8f, 1f, 0.35f));
            material.renderQueue = (int)RenderQueue.Transparent;

            EnsureFolder("Assets/Development/Art/Materials");
            AssetDatabase.CreateAsset(material, MiraclePreviewRingMaterialPath);
            Debug.Log("[Editor] Создан материал: " + MiraclePreviewRingMaterialPath);
            return material;
        }

        private static void WireMiracleTargeting(MiracleTargetingController controller, Transform previewRoot, int groundLayer)
        {
            const int expected = 2;
            var so = new SerializedObject(controller);
            var ownerLabel = "MiracleTargetingController";

            int wired = 0;
            wired += SetRef(so, "_previewRoot", previewRoot, ownerLabel) ? 1 : 0;
            // Баг с playtest'а шага 4: дома лежат на слое Default с MeshCollider (структуры сцены не
            // переводятся на Ground), а луч таргетинга ходил только по Ground — в изометрии он
            // проходил СКВОЗЬ дом насквозь и клал точку каста на землю ЗА домом, вне радиуса чуда.
            // Маска расширена на Ground|Default — WireLayerMask здесь не подходит (та утилита
            // прошивает ровно один слой), маска считается и проверяется явно.
            wired += WireGroundAndDefaultMask(so, "_groundMask", groundLayer, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();

            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected} (_groundMask=Ground|Default — таргетинг по структурам).");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей.");
        }

        /// <summary>
        /// Прошивает <paramref name="propertyName"/> битовой маской Ground|Default (Default всегда
        /// слой 0). Численная проверка результата (.claude/rules/editor-files.md): m_Bits должен
        /// точно совпасть с ожидаемой комбинацией, а не просто "поле не пустое".
        /// </summary>
        private static bool WireGroundAndDefaultMask(SerializedObject so, string propertyName, int groundLayer, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — маска не прошита.");
                return false;
            }

            if (groundLayer < 0)
            {
                Debug.LogWarning($"[Editor] Слой Ground не создан — \"{propertyName}\" на {ownerLabel} оставлена как была.");
                return false;
            }

            int expectedBits = (1 << groundLayer) | 1; // Ground | Default(0)
            prop.intValue = expectedBits;
            return prop.intValue == expectedBits;
        }

        private static void WireMiracleImpact(MiracleImpactController controller, int grabbableLayer)
        {
            const int expected = 1;
            var so = new SerializedObject(controller);
            var ownerLabel = "MiracleImpactController";

            int wired = WireLayerMask(so, "_villagerMask", grabbableLayer, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();

            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected} (_villagerMask=Grabbable).");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей.");
        }

        // ------------------------------------------------------------------------------------
        // Дома / поле — наши префабы (VillageStructurePrefabSetupEditor), позиционирование +
        // прошивка _siteIndex/_villagerMask на сценном инстансе
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Расставляет структуры деревни как инстансы НАШИХ префабов
        /// (Assets/Development/Prefabs/Village/&lt;Name&gt;.prefab — собираются
        /// <see cref="VillageStructurePrefabSetupEditor"/>, если их ещё нет на диске). Мигрирует уже
        /// существующие на сцене голые ThirdParty-инстансы (старая версия этой сцены) в наши префабы,
        /// сохраняя позицию/поворот — правка после playtest'а шага 4, CLAUDE.md "Из ThirdParty ...
        /// только визуал, никогда — префаб-инстанс".
        /// </summary>
        private static List<GameObject> EnsureStructures(int grabbableLayer)
        {
            bool anyPrefabMissing = false;
            foreach (var definition in VillageStructurePrefabSetupEditor.Structures)
            {
                if (!File.Exists(VillageStructurePrefabSetupEditor.GetPrefabPath(definition.Name)))
                {
                    anyPrefabMissing = true;
                    break;
                }
            }

            if (anyPrefabMissing)
            {
                Debug.Log("[Editor] Не все префабы структур деревни найдены на диске — вызываю " +
                          "VillageStructurePrefabSetupEditor.SetupVillageStructurePrefabs().");
                VillageStructurePrefabSetupEditor.SetupVillageStructurePrefabs();
            }

            var needSiteObjects = new List<GameObject>();
            int nextSiteIndex = 0;
            int migratedCount = 0;

            foreach (var (name, position) in StructurePositions)
            {
                var definition = FindStructureDefinition(name);
                var prefabPath = VillageStructurePrefabSetupEditor.GetPrefabPath(name);
                var ourPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (ourPrefab == null)
                {
                    Debug.LogWarning("[Editor] " + prefabPath + " не найден (см. лог выше) — \"" + name + "\" не размещён в сцене.");
                    continue;
                }

                var go = TestHandSceneSetupEditor.FindInSceneIncludingInactive(name);

                if (go == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(ourPrefab);
                    instance.name = name;
                    Undo.RegisterCreatedObjectUndo(instance, "Create " + name);
                    instance.transform.position = position;
                    go = instance;
                    Debug.Log("[Editor] Создан объект структуры (наш префаб): " + name + " (" + prefabPath + ").");
                }
                else
                {
                    var correspondingSource = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    var sourcePath = correspondingSource != null ? AssetDatabase.GetAssetPath(correspondingSource) : null;
                    bool isThirdPartyInstance = correspondingSource != null && EditorPrefabUtility.IsThirdPartyPath(sourcePath);

                    if (isThirdPartyInstance)
                    {
                        // Миграция: старая версия сцены держала голый ThirdParty-инстанс с нашим
                        // NeedSiteController поверх — заменяем на инстанс нашего префаба, сохраняя
                        // позицию/поворот (масштаб теперь берётся из префаба — Structures).
                        var savedPosition = go.transform.position;
                        var savedRotation = go.transform.rotation;
                        Undo.DestroyObjectImmediate(go);

                        var migrated = (GameObject)PrefabUtility.InstantiatePrefab(ourPrefab);
                        migrated.name = name;
                        Undo.RegisterCreatedObjectUndo(migrated, "Migrate " + name);
                        migrated.transform.position = savedPosition;
                        migrated.transform.rotation = savedRotation;
                        go = migrated;
                        migratedCount++;
                        Debug.Log("[Editor] \"" + name + "\" мигрирован: ThirdParty-инстанс (" + sourcePath +
                                  ") → наш префаб " + prefabPath + " (позиция/поворот сохранены).");
                    }
                    // Иначе уже инстанс нашего префаба (или неизвестный объект без source) — transform не трогаем.
                }

                if (definition.NeedType.HasValue)
                {
                    EnsureNeedSiteOnInstance(go, nextSiteIndex, definition.NeedType.Value, grabbableLayer);
                    needSiteObjects.Add(go);
                    nextSiteIndex++;
                }
            }

            if (migratedCount > 0)
                Debug.Log($"[Editor] Структуры деревни: мигрировано {migratedCount} объект(ов) с ThirdParty-инстансов на наши префабы.");

            Debug.Log($"[Editor] Структуры деревни: {StructurePositions.Length} всего, NeedSiteController на " +
                      $"{needSiteObjects.Count} (индексы 0..{needSiteObjects.Count - 1}).");
            return needSiteObjects;
        }

        private static (string Name, string SourcePrefabPath, Vector3 Scale, NeedType? NeedType) FindStructureDefinition(string name)
        {
            foreach (var definition in VillageStructurePrefabSetupEditor.Structures)
            {
                if (definition.Name == name) return definition;
            }

            Debug.LogError("[Editor] \"" + name + "\" не найден в VillageStructurePrefabSetupEditor.Structures — " +
                            "таблицы позиций и структур рассинхронизированы.");
            return (name, null, Vector3.one, null);
        }

        /// <summary>
        /// Прошивает <c>_siteIndex</c>/<c>_villagerMask</c> на КОНКРЕТНОМ сценном инстансе структуры
        /// (правка после playtest'а шага 4: триггер/маркеры/NeedType теперь собраны в самом префабе
        /// VillageStructurePrefabSetupEditor'ом — здесь остаётся только то, что зависит от конкретной
        /// сцены/позиции в таймлайне). _villagerMask (ревью плейтеста шага 3) — маска слоя Grabbable,
        /// по которой NeedSiteController собирает жителей Physics.OverlapSphere при срабатывании нужды.
        /// Сверяет needType в префабе с ожидаемым для этой позиции — расхождение (битая таблица) не
        /// молчит, а предупреждает.
        /// </summary>
        private static void EnsureNeedSiteOnInstance(GameObject go, int siteIndex, NeedType expectedNeedType, int grabbableLayer)
        {
            var controller = go.GetComponent<NeedSiteController>();
            if (controller == null)
            {
                Debug.LogError("[Editor] \"" + go.name + "\": NeedSiteController отсутствует на инстансе нашего префаба — " +
                                "проверьте VillageStructurePrefabSetupEditor (возможно, устаревший префаб на диске).");
                return;
            }

            const int expected = 2;
            var so = new SerializedObject(controller);
            var ownerLabel = "NeedSiteController(\"" + go.name + "\")";

            var needTypeProp = so.FindProperty("_needType");
            if (needTypeProp != null && needTypeProp.intValue != (int)expectedNeedType)
            {
                Debug.LogWarning($"[Editor] {ownerLabel}: _needType в префабе ({(NeedType)needTypeProp.intValue}) не совпадает " +
                                  $"с ожидаемым для этой позиции ({expectedNeedType}) — проверьте VillageStructurePrefabSetupEditor.Structures.");
            }

            int wired = 0;
            wired += SetIntProp(so, "_siteIndex", siteIndex, ownerLabel) ? 1 : 0;
            wired += WireLayerMask(so, "_villagerMask", grabbableLayer, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();

            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected} (siteIndex={siteIndex}, _villagerMask=Grabbable).");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей — " +
                                "проверьте имена SerializeField в NeedSiteController, иначе фича молча не работает в Play Mode.");
        }

        /// <summary>
        /// Прошивает LayerMask-поле через единственный источник — EditorLayerUtility.SetLayerMask
        /// (та же утилита, что и для рук/камеры/чудес) — и подтверждает успех численно
        /// (m_Bits == 1 &lt;&lt; layerIndex), а не «поле не пустое»: пустая маска молча делает
        /// Physics.OverlapSphere/Physics.CheckSphere бесполезным, поэтому недостаточно просто не
        /// упасть на LogWarning — нужно подтвердить конкретное число. Изначально назывался
        /// WireVillagerMask (NeedSite/шаг 3) — обобщён под шаг 4 ([Miracles]._groundMask/_villagerMask).
        /// </summary>
        private static bool WireLayerMask(SerializedObject so, string propertyName, int layerIndex, string ownerLabel)
        {
            EditorLayerUtility.SetLayerMask(so, propertyName, layerIndex, ownerLabel);

            var prop = so.FindProperty(propertyName);
            if (prop == null || layerIndex < 0) return false; // EditorLayerUtility уже залогировал причину.

            return prop.intValue == (1 << layerIndex);
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

        /// <summary>
        /// Правка таймлайна ТОЛЬКО LevelConfig_01 (новая семантика <c>NeedTriggerMode</c>, ТЗ владельца):
        /// запись 0 (Fire, site 0) — AfterVillagerTossed, записи 1 (Disease, site 1) и 2 (Hunger, site 2) —
        /// AfterPreviousResolved, все три с duration=0 (нужда не истекает — <see cref="NeedEventEntry.duration"/>
        /// &lt;= 0 означает бесконечную нужду). Уровни 2-5 НЕ трогаем — у них <c>_triggerMode</c> остаётся
        /// AtTime по дефолту enum'а. Сопоставление записи — по индексу с проверкой ожидаемого NeedType
        /// (порядок задаёт <see cref="DefaultEventTimeline"/>/<see cref="PopulateEventTimeline"/> выше:
        /// Fire@0, Disease@1, Hunger@2) — расхождение логируется и пропускается, а не пишется вслепую.
        /// Идемпотентен: совпадающие значения не перезаписываются и не логируются, изменение — "старое -&gt; новое".
        /// </summary>
        private static readonly (int Index, NeedType ExpectedType, NeedTriggerMode Mode, float Duration)[] LevelConfig01TriggerModeOverrides =
        {
            (0, NeedType.Fire, NeedTriggerMode.AfterVillagerTossed, 0f),
            (1, NeedType.Disease, NeedTriggerMode.AfterPreviousResolved, 0f),
            (2, NeedType.Hunger, NeedTriggerMode.AfterPreviousResolved, 0f),
        };

        private static void MigrateLevelConfig01TriggerModes(LevelConfig levelConfig)
        {
            if (levelConfig == null) return;

            var so = new SerializedObject(levelConfig);
            var timelineProp = so.FindProperty("_eventTimeline");
            if (timelineProp == null)
            {
                Debug.LogError("[Editor] Поле \"_eventTimeline\" не найдено на LevelConfig — триггер-моды LevelConfig_01 не прошиты.");
                return;
            }

            int changed = 0;
            foreach (var (index, expectedType, mode, duration) in LevelConfig01TriggerModeOverrides)
            {
                if (index >= timelineProp.arraySize)
                {
                    Debug.LogWarning($"[Editor] LevelConfig_01.EventTimeline[{index}] отсутствует (arraySize={timelineProp.arraySize}) — триггер-мод не прошит.");
                    continue;
                }

                var element = timelineProp.GetArrayElementAtIndex(index);
                var typeProp = element.FindPropertyRelative("type");
                if ((NeedType)typeProp.intValue != expectedType)
                {
                    Debug.LogWarning($"[Editor] LevelConfig_01.EventTimeline[{index}]: тип {(NeedType)typeProp.intValue} не совпадает " +
                                      $"с ожидаемым {expectedType} — триггер-мод не прошит (проверьте порядок записей таймлайна).");
                    continue;
                }

                var modeProp = element.FindPropertyRelative("_triggerMode");
                if (modeProp == null)
                {
                    Debug.LogError("[Editor] Поле \"_triggerMode\" не найдено на NeedEventEntry — проверьте, что code-writer добавил его.");
                    continue;
                }

                var durationProp = element.FindPropertyRelative("duration");

                var oldMode = (NeedTriggerMode)modeProp.intValue;

                // Ревью шага 10: если _triggerMode уже НЕ AtTime — миграция для этой записи уже была
                // применена (этим же скриптом ранее, или руками владельцем на что-то ещё, не обязательно
                // совпадающее с целевым mode ниже) — пропускаем ВЕСЬ блок (mode И duration), ручные правки
                // владельца не откатываем повторным прогоном.
                if (oldMode != NeedTriggerMode.AtTime) continue;

                if (oldMode != mode)
                {
                    Debug.Log($"[Editor] LevelConfig_01.EventTimeline[{index}]._triggerMode: {oldMode} -> {mode}.");
                    modeProp.intValue = (int)mode;
                    changed++;
                }

                var oldDuration = durationProp.floatValue;
                if (!Mathf.Approximately(oldDuration, duration))
                {
                    Debug.Log($"[Editor] LevelConfig_01.EventTimeline[{index}].duration: {oldDuration} -> {duration}.");
                    durationProp.floatValue = duration;
                    changed++;
                }
            }

            if (changed == 0) return;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(levelConfig);
            Debug.Log($"[Editor] LevelConfig_01.EventTimeline: обновлено {changed} значени(й) триггер-модов/duration.");
        }

        // ------------------------------------------------------------------------------------
        // Прошивка ссылок на Composition Root
        // ------------------------------------------------------------------------------------

        private static void WireScope(GameLifetimeScope scope, HandConfig handConfig, IsometricCameraConfig cameraConfig,
            VillagerConfig villagerConfig, LevelConfig levelConfig, ThrowableConfig throwableConfig,
            GodHandController godHand, CameraRig cameraRig,
            MiracleCatalogConfig miracleCatalogConfig, FaithConfig faithConfig, VfxConfig vfxConfig,
            MiracleTargetingController miracleTargeting, MiracleImpactController miracleImpact, MiracleVfxController miracleVfx,
            MiraclePanelView miraclePanelView, FaithBarView faithBarView,
            WorshipConfig worshipConfig, LevelHudView levelHudView, LevelCompleteView levelCompleteView,
            LevelStalledView levelStalledView, PauseOverlayView pauseOverlayView, GameState initialState,
            TutorialConfig tutorialConfig, TutorialView tutorialView)
        {
            if (scope == null)
            {
                Debug.LogWarning("[Editor] GameLifetimeScope не найден в сцене — прошивка ссылок пропущена.");
                return;
            }

            const int expected = 23;
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
            // Шаг 4 — Чудеса + Faith (game-design.md §19.4).
            wired += SetRef(so, "_miracleCatalogConfig", miracleCatalogConfig, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_faithConfig", faithConfig, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_vfxConfig", vfxConfig, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_miracleTargetingController", miracleTargeting, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_miracleImpactController", miracleImpact, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_miracleVfxController", miracleVfx, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_miraclePanelView", miraclePanelView, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_faithBarView", faithBarView, ownerLabel) ? 1 : 0;
            // Шаг 5 — Worship + win (game-design.md §19.5).
            wired += SetRef(so, "_worshipConfig", worshipConfig, ownerLabel) ? 1 : 0;
            wired += SetEnumProp(so, "_initialState", (int)initialState, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_levelHudView", levelHudView, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_levelCompleteView", levelCompleteView, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_levelStalledView", levelStalledView, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_pauseOverlayView", pauseOverlayView, ownerLabel) ? 1 : 0;
            // Tutorial — обучение первого уровня (TutorialConfig/TutorialView).
            wired += SetRef(so, "_tutorialConfig", tutorialConfig, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_tutorialView", tutorialView, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();

            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected} полей.");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей — " +
                                "сцена соберётся, но часть систем не заработает в Play Mode.");
        }

        /// <summary>Прошивает enum-поле по индексу значения. В отличие от SetRef — не бывает "не найдено значение", только "поле не найдено".</summary>
        private static bool SetEnumProp(SerializedObject so, string propertyName, int enumIndex, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — прошивка пропущена " +
                                  "(проверьте, что code-writer добавил это SerializeField).");
                return false;
            }

            prop.enumValueIndex = enumIndex;
            return true;
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
