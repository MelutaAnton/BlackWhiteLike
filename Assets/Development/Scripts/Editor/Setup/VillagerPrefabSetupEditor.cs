using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackWhiteLike.Controllers.Village;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Собирает префаб жителя из POLYGON Fantasy Kingdom + Synty-локомоции/Mixamo-реакций
    /// (game-design.md §19.3, шаг 3 "Жители + события"): AnimatorController <c>Villager.controller</c>
    /// и сам префаб (корень — инстанс SM_Chr_Peasant_Male_01.prefab).
    ///
    /// AnimatorController — 2 слоя (третий playtest шага 3: Scared full-body давал скользящие ноги,
    /// физика несла тело, а full-body клип Look_Scared держал ноги статично):
    /// - Слой 0 "Movement" (вес 1, без маски) — 1D BlendTree по MoveSpeed (Idle/Walk/Run, пороги
    ///   0 / VillagerConfig.MoveSpeed / VillagerConfig.PanicMoveSpeed — читаются из конфига при
    ///   сборке, поэтому Run гарантированно достижим по построению, без ручной синхронизации
    ///   constants с конфигом) + full-body Worship/Stunned/StandUp (Bool/Bool/Trigger).
    /// - Слой 1 "UpperBody" (вес 1, Override, AvatarMask <c>VillagerUpperBody.mask</c> — ноги/Root/IK
    ///   выключены, Body/Head/руки/пальцы включены) — Empty/Scared по триггеру Scared. Паника теперь
    ///   выглядит как "бежит ногами из BlendTree + верх тела играет испуг", а не как full-body клип
    ///   поверх реального движения.
    /// Параметры (AnimatorHashes): MoveSpeed (Float), IsWorshipping/IsStunned (Bool),
    /// Scared/StandUp (Trigger). IsStunned true и в Stunned, и в Tossed; выход из Stunned — только
    /// через StandUp (ревью шага 3).
    ///
    /// Перед запуском требует: (1) переимпортированные Humanoid Mixamo-клипы —
    /// "BlackWhiteLike/Setup/Reimport Mixamo As Humanoid"; (2) слои Ground/Grabbable создаются сами
    /// (EditorLayerUtility), но HandConfig.asset/VillagerConfig.asset для подгонки массы и порогов
    /// BlendTree — из "BlackWhiteLike/Configs/Create Empty SO Set".
    /// Идемпотентен: AnimatorController (оба слоя, включая BlendTree-сабассет) пересобирается с нуля
    /// при каждом запуске (производные данные) — второй слой ищется по имени и переиспользуется, не
    /// дублируется; AvatarMask не пересоздаётся, если уже существует, но состав частей мигрируется
    /// при несовпадении. Префаб дособирает недостающие компоненты, не трогая подкрученные
    /// пользователем масштаб/массу/размеры коллайдера у уже существующего префаба (миграционная
    /// идемпотентность на уровне компонента — см. .claude/rules/editor-files.md).
    /// </summary>
    public static class VillagerPrefabSetupEditor
    {
        // --- ThirdParty-источники (только чтение) ---
        private const string PeasantPrefabPath =
            "Assets/ThirdParty/3D Environment/POLYGON - Fantasy Kingdom Pack/PolygonFantasyKingdom/Prefabs/Characters/SM_Chr_Peasant_Male_01.prefab";

        private const string SyntyIdleClipPath =
            "Assets/ThirdParty/Animations/Synty/AnimationIdles/Animations/Polygon/Masculine/Base/Stances/A_POLY_IDL_Base_Masc.fbx";
        private const string SyntyWalkClipPath =
            "Assets/ThirdParty/Animations/Synty/AnimationBaseLocomotion/Animations/Polygon/Masculine/Locomotion/Walk/A_Walk_FwdStrafeF_Masc.fbx";
        private const string SyntyRunClipPath =
            "Assets/ThirdParty/Animations/Synty/AnimationBaseLocomotion/Animations/Polygon/Masculine/Locomotion/Run/A_Run_FwdStrafeF_Masc.fbx";
        private const string SyntyWorshipClipPath =
            "Assets/ThirdParty/Animations/Synty/AnimationIdles/Animations/Polygon/Masculine/Pray/Stances/A_POLY_IDL_Pray_Kneeling_Loop_Masc.fbx";
        private const string SyntyScaredClipPath =
            "Assets/ThirdParty/Animations/Synty/AnimationIdles/Animations/Polygon/Masculine/Look/Actions/A_POLY_IDL_Look_L_Scared_Masc.fbx";

        // --- Наша копия сырья (Content/, не ThirdParty) — требует "Reimport Mixamo As Humanoid" ---
        private const string MixamoStunnedClipPath = "Assets/Content/Animations/Stunned/Dizzy Idle.fbx";
        // Ревью шага 3, Д2: IsStunned теперь true и в Stunned, и в Tossed (висит в позе Dizzy и в
        // руке/полёте) — явное состояние StandUp нужно, чтобы возврат в Idle не был мгновенным.
        private const string MixamoStandUpClipPath = "Assets/Content/Animations/Stunned/Stand Up.fbx";

        // --- Наши целевые пути ---
        private const string AnimationsFolder = "Assets/Development/Art/Animations/Villager";
        private const string ControllerPath = AnimationsFolder + "/Villager.controller";
        private const string PrefabsFolder = "Assets/Development/Prefabs/Village";
        private const string PrefabPath = PrefabsFolder + "/Villager.prefab";
        private const string HandConfigPath = "Assets/Development/Data/Configs/Hand/HandConfig.asset";
        private const string VillagerConfigPath = "Assets/Development/Data/Configs/Villagers/VillagerConfig.asset";

        // Дефолты, если конфиги ещё не созданы — совпадают с дефолтами самих SO-классов
        // (HandConfig._maxGrabMass=20, VillagerConfig._mass=10, _moveSpeed=2, _panicMoveSpeed=5),
        // чтобы предпросмотр в Editor не расходился со значением, которое рантайм и так использует.
        private const float FallbackMaxGrabMass = 20f;
        private const float FallbackMass = 10f;
        private const float FallbackWalkThreshold = 2f;
        private const float FallbackRunThreshold = 5f;
        private const float FallbackStandUpPlaybackSpeed = 2f;

        /// <summary>
        /// Full-body состояния слоя 0 с loop-клипами: имя состояния ← путь к fbx с единственным
        /// клипом (Synty/Mixamo одноклиповые файлы — берём первый неслужебный AnimationClip-сабассет,
        /// без завязки на внутреннее имя клипа, которое у Mixamo-экспортов не совпадает с именем файла).
        /// Idle/Walk/Run сюда не входят — они дети BlendTree (см. BuildMovementLayer), не отдельные
        /// состояния (третий playtest шага 3: BlendTree заменил цепочку состояний-одиночек).
        /// </summary>
        private static readonly (string State, string ClipPath)[] FullBodyLoopStates =
        {
            ("Worship", SyntyWorshipClipPath),
            ("Stunned", MixamoStunnedClipPath),
        };

        private const string MovementLayerName = "Movement";
        private const string UpperBodyLayerName = "UpperBody";
        private const string UpperBodyMaskPath = AnimationsFolder + "/VillagerUpperBody.mask";

        [MenuItem("BlackWhiteLike/Setup/Setup Villager Prefab")]
        public static void SetupVillagerPrefab()
        {
            EnsureFolder(AnimationsFolder);
            EnsureFolder(PrefabsFolder);

            EditorLayerUtility.EnsureTag(Tags.Villager);
            int grabbableLayer = EditorLayerUtility.EnsureLayer(EditorLayerUtility.GrabbableLayerName);

            var handConfig = AssetDatabase.LoadAssetAtPath<HandConfig>(HandConfigPath);
            if (handConfig == null)
            {
                Debug.LogWarning("[Editor] HandConfig не найден по пути " + HandConfigPath + " — проверка массы " +
                                  "против MaxGrabMass будет посчитана от дефолта " + FallbackMaxGrabMass +
                                  ". Сначала прогоните \"BlackWhiteLike/Configs/Create Empty SO Set\".");
            }

            var villagerConfig = AssetDatabase.LoadAssetAtPath<VillagerConfig>(VillagerConfigPath);
            if (villagerConfig == null)
            {
                Debug.LogWarning("[Editor] VillagerConfig не найден по пути " + VillagerConfigPath + " — масса жителя " +
                                  "в Editor будет дефолтной (" + FallbackMass + "), рантайм всё равно перепрошьёт её " +
                                  "из VillagerConfig.Mass при первом Construct().");
            }

            var controller = BuildAnimatorController(villagerConfig);
            BuildPrefab(controller, handConfig, villagerConfig, grabbableLayer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Н6 (ревью шага 3): прошивка VillagerConfig._prefab перенесена сюда из
            // ConfigAssetCreatorEditor — здесь префаб гарантированно уже существует на диске
            // (SaveAsPrefabAsset выше), поэтому не нужен второй прогон "Create Empty SO Set" после
            // "Setup Villager Prefab" только ради этой ссылки.
            var builtPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            WireVillagerConfigPrefab(villagerConfig, builtPrefab);

            Debug.Log("[Editor] Setup Villager Prefab завершён: " + PrefabPath);
        }

        // ------------------------------------------------------------------------------------
        // AnimatorController
        // ------------------------------------------------------------------------------------

        private static AnimatorController BuildAnimatorController(VillagerConfig villagerConfig)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                Debug.Log("[Editor] Создан AnimatorController: " + ControllerPath);
            }

            // Параметры — общий набор для обоих слоёв, единственный источник (AnimatorHashes).
            controller.parameters = System.Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter(AnimatorHashes.MoveSpeedName, AnimatorControllerParameterType.Float);
            controller.AddParameter(AnimatorHashes.IsWorshippingName, AnimatorControllerParameterType.Bool);
            controller.AddParameter(AnimatorHashes.IsStunnedName, AnimatorControllerParameterType.Bool);
            controller.AddParameter(AnimatorHashes.IsLyingName, AnimatorControllerParameterType.Bool);
            controller.AddParameter(AnimatorHashes.ScaredName, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(AnimatorHashes.StandUpName, AnimatorControllerParameterType.Trigger);

            // Пересобираем BlendTree-сабассеты с нуля вместе со всем остальным — иначе повторный
            // запуск оставляет осиротевшие BlendTree-объекты внутри .controller (RemoveState не
            // уничтожает сам Motion-сабассет, только состояние, которое на него ссылалось).
            foreach (var orphanTree in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>().ToArray())
                Object.DestroyImmediate(orphanTree, true);

            float walkThreshold = villagerConfig != null ? villagerConfig.MoveSpeed : FallbackWalkThreshold;
            float runThreshold = villagerConfig != null ? villagerConfig.PanicMoveSpeed : FallbackRunThreshold;
            if (villagerConfig == null)
            {
                Debug.LogWarning("[Editor] VillagerConfig не найден — пороги BlendTree взяты из дефолтов класса " +
                                  $"(Walk={FallbackWalkThreshold}, Run={FallbackRunThreshold}), не из живого ассета.");
            }

            // Четвёртый playtest шага 3: житель "уезжает" во время вставания — подтверждённая
            // гипотеза — рассинхрон VillagerConfig.StandUpDuration (была на глаз, 1.8) и фактической
            // длины клипа Stand Up (никем не замерялась; реально — 8.267 с на несрезанном клипе).
            // Пятый playtest: владелец обрезал клип в .meta (5.567 с) и руками поставил Speed=2 на
            // состоянии StandUp — оба факта нужно учитывать при штамповке длительности лока, иначе
            // деление "полная длина / 1" снова разойдётся с реальным временем проигрывания на экране.
            // Загружаем клип здесь один раз — используется и для лога длины (проверка гипотезы), и
            // для самого состояния StandUp, и для штамповки конфига ниже.
            var standUpClip = LoadFirstClip(MixamoStandUpClipPath);
            var stunnedClipForLog = LoadFirstClip(MixamoStunnedClipPath);
            LogClipLength("Stand Up", standUpClip);
            LogClipLength("Dizzy Idle", stunnedClipForLog);

            // Скорость проигрывания StandUp — данные (VillagerConfig.StandUpPlaybackSpeed), не только
            // ручная правка Speed на состоянии: пересборка контроллера с нуля стёрла бы её, если бы
            // Speed не был восстановлен из того же источника при каждом прогоне (см. BuildMovementLayer).
            // Защита от ноля/отрицательного значения — реальный playback speed не может быть <= 0.
            float standUpPlaybackSpeed = villagerConfig != null ? villagerConfig.StandUpPlaybackSpeed : FallbackStandUpPlaybackSpeed;
            if (standUpPlaybackSpeed <= 0f)
            {
                Debug.LogWarning($"[Editor] VillagerConfig.StandUpPlaybackSpeed <= 0 ({standUpPlaybackSpeed}) — " +
                                  "используется 1 (без ускорения/замедления) и для Speed состояния, и для расчёта _standUpDuration.");
                standUpPlaybackSpeed = 1f;
            }

            BuildMovementLayer(controller, walkThreshold, runThreshold, standUpClip, standUpPlaybackSpeed);
            BuildUpperBodyLayer(controller);

            // StandUpDuration — derived-значение (четвёртый/пятый playtest шага 3), не дизайнерская
            // ручка: держит лок движения VillagerController ровно на время проигрывания Stand Up.
            // Штампуем clip.length / StandUpPlaybackSpeed (не голую длину клипа — состояние
            // проигрывается быстрее/медленнее реального времени клипа ровно во столько раз), лок чуть
            // длиннее визуала безопасен, короче — это и есть баг "тащит во время анимации".
            // Идемпотентно — см. StampStandUpDuration.
            StampStandUpDuration(villagerConfig, standUpClip, standUpPlaybackSpeed);

            EditorUtility.SetDirty(controller);
            Debug.Log($"[Editor] AnimatorController пересобран: {ControllerPath} (BlendTree-пороги: Walk={walkThreshold}, Run={runThreshold}).");
            return controller;
        }

        private static void LogClipLength(string label, AnimationClip clip)
        {
            if (clip != null)
                Debug.Log($"[Editor] Клип \"{label}\": length={clip.length:0.###} с.");
            else
                Debug.LogWarning($"[Editor] Клип \"{label}\" не найден — длина неизвестна.");
        }

        /// <summary>
        /// Штампует VillagerConfig._standUpDuration фактическим временем проигрывания клипа Stand Up
        /// на экране — clip.length / StandUpPlaybackSpeed, не голую длину клипа (пятый playtest шага 3:
        /// владелец поставил Speed=2 на состоянии StandUp вручную — при вдвое ускоренном
        /// проигрывании лок должен держаться вдвое меньше времени, иначе он снова разойдётся с
        /// анимацией, на этот раз в другую сторону — будет длиннее визуала). Derived-значение, не
        /// дизайнерская ручка — штампуем всегда актуальным расчётом, но идемпотентно: совпадающее
        /// значение не перезаписываем и не логируем повторно (иначе каждый прогон "Setup Villager
        /// Prefab" шумел бы в консоли без реального изменения).
        /// </summary>
        private static void StampStandUpDuration(VillagerConfig villagerConfig, AnimationClip standUpClip, float standUpPlaybackSpeed)
        {
            if (villagerConfig == null)
            {
                Debug.LogWarning("[Editor] VillagerConfig не найден — _standUpDuration не проштампован длиной клипа Stand Up.");
                return;
            }

            if (standUpClip == null)
            {
                Debug.LogWarning("[Editor] Клип Stand Up не найден — _standUpDuration не проштампован.");
                return;
            }

            var so = new SerializedObject(villagerConfig);
            var prop = so.FindProperty("_standUpDuration");
            if (prop == null)
            {
                Debug.LogError("[Editor] Поле \"_standUpDuration\" не найдено на VillagerConfig — проверьте имя SerializeField.");
                return;
            }

            float oldValue = prop.floatValue;
            // standUpPlaybackSpeed уже провалидирован вызывающей стороной (BuildAnimatorController) —
            // <= 0 заменено на 1 там же, здесь всегда безопасно делить.
            float newValue = standUpClip.length / standUpPlaybackSpeed;

            if (Mathf.Approximately(oldValue, newValue))
                return;

            prop.floatValue = newValue;
            so.ApplyModifiedProperties();
            Debug.Log($"[Editor] VillagerConfig._standUpDuration: {oldValue:0.###} → {newValue:0.###} " +
                      $"(clip.length={standUpClip.length:0.###} / StandUpPlaybackSpeed={standUpPlaybackSpeed:0.###}).");
        }

        /// <summary>
        /// Слой 0 "Movement" (третий playtest шага 3): 1D BlendTree по MoveSpeed вместо цепочки
        /// состояний-одиночек Idle/Walk/Run — пороги читаются из VillagerConfig прямо здесь, поэтому
        /// Run гарантированно достижим по построению (раньше это была ручная синхронизация константы
        /// RunThreshold с VillagerConfig.PanicMoveSpeed, зафиксированная только тултипом — см. отчёт).
        /// Full-body Worship/Stunned/StandUp остаются как раньше; Scared сюда не входит — он теперь
        /// оверлей на слое 1 (UpperBody).
        /// </summary>
        private static void BuildMovementLayer(AnimatorController controller, float walkThreshold, float runThreshold,
            AnimationClip standUpClip, float standUpPlaybackSpeed)
        {
            // Слой 0 создаётся Unity под именем "Base Layer" и не переименовывается сам —
            // прошиваем имя/вес явно (третий playtest шага 3: спецификация требует "Movement", вес 1;
            // функционально вес базового слоя рантайм игнорирует и всегда трактует как 1, но явное
            // значение убирает двусмысленность при численной проверке YAML).
            var baseLayers = controller.layers;
            baseLayers[0].name = MovementLayerName;
            baseLayers[0].defaultWeight = 1f;
            controller.layers = baseLayers;

            var rootStateMachine = controller.layers[0].stateMachine;

            // Пересобираем с нуля — производные данные, повторный запуск не должен плодить дубли
            // состояний/переходов (тот же подход, что и GodHandPrefabSetupEditor).
            foreach (var existingState in rootStateMachine.states.ToArray())
                rootStateMachine.RemoveState(existingState.state);
            foreach (var anyTransition in rootStateMachine.anyStateTransitions.ToArray())
                rootStateMachine.RemoveAnyStateTransition(anyTransition);

            var blendTree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D };
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.blendParameter = AnimatorHashes.MoveSpeedName;
            // КРИТИЧНО: BlendTree.useAutomaticThresholds по умолчанию true — Unity молча
            // перезаписывает пороги детей равномерно на [0..1] (0/0.5/1), игнорируя явные значения,
            // переданные в AddChild(motion, threshold). Обнаружено численной проверкой YAML сразу
            // после первого прогона (m_Threshold оказались 0/0.5/1 вместо ожидаемых 0/walk/run) —
            // без этой строки вся идея "пороги из VillagerConfig" была бы тихо неработающей.
            blendTree.useAutomaticThresholds = false;

            AddBlendTreeChild(blendTree, SyntyIdleClipPath, 0f, "Idle");
            AddBlendTreeChild(blendTree, SyntyWalkClipPath, walkThreshold, "Walk");
            AddBlendTreeChild(blendTree, SyntyRunClipPath, runThreshold, "Run");

            var locomotionState = rootStateMachine.AddState("Locomotion");
            locomotionState.motion = blendTree;
            rootStateMachine.defaultState = locomotionState;

            var states = new Dictionary<string, AnimatorState>();
            foreach (var (stateName, clipPath) in FullBodyLoopStates)
            {
                var state = rootStateMachine.AddState(stateName);
                states[stateName] = state;

                var clip = LoadFirstClip(clipPath);
                if (clip != null)
                {
                    state.motion = clip;
                }
                else
                {
                    Debug.LogWarning($"[Editor] Клип для состояния \"{stateName}\" не найден по пути {clipPath} — " +
                                      "состояние оставлено без Motion.");
                }
            }

            // StandUp — нецикличная реакция выхода из Stunned (Д2, ревью шага 3): Mixamo "Stand Up".
            // Клип загружен вызывающей стороной (BuildAnimatorController) — переиспользуем ту же
            // ссылку, чтобы не грузить сабассет дважды и чтобы лог длины клипа и штамповка
            // VillagerConfig._standUpDuration (четвёртый playtest шага 3) точно смотрели на тот же клип.
            var standUpState = rootStateMachine.AddState("StandUp");
            if (standUpClip != null) standUpState.motion = standUpClip;
            else Debug.LogWarning("[Editor] Клип для состояния \"StandUp\" не найден по пути " + MixamoStandUpClipPath + ".");
            // Пятый playtest шага 3: владелец руками поставил Speed=2 на StandUp в контроллере —
            // пересборка с нуля стирала бы эту правку при каждом прогоне. Теперь это данные
            // (VillagerConfig.StandUpPlaybackSpeed), не ручная настройка состояния — переживает
            // пересборку и остаётся синхронизирована с делением в StampStandUpDuration.
            standUpState.speed = standUpPlaybackSpeed;

            // StunnedLying (шестой playtest шага 3): жёсткое приземление должно ЛЕЖАТЬ до вставания —
            // раньше житель стоял в Dizzy (Stunned), а потом StandUp заново укладывал в позу,
            // рассинхрон был виден на экране. Motion — ТОТ ЖЕ обрезанный клип Stand Up, что и у
            // состояния StandUp (не отдельный клип "лежать") — speed=0 замораживает на первом кадре,
            // который и есть исходная лежачая поза, поэтому переход в StandUp синхронен по построению
            // (тот же кадр, откуда стартует анимация вставания), без ручной подгонки.
            var stunnedLyingState = rootStateMachine.AddState("StunnedLying");
            if (standUpClip != null) stunnedLyingState.motion = standUpClip;
            else Debug.LogWarning("[Editor] Клип для состояния \"StunnedLying\" не найден (тот же Stand Up, что и у StandUp).");
            stunnedLyingState.speed = 0f;

            // Worship/Stunned/StunnedLying могут прервать BlendTree из любого состояния — AnyState.
            // AddAnyStateTransition уже ставит canTransitionToSelf=false для всех трёх (не пере-входит
            // в себя каждый кадр, что для StunnedLying дёргало бы позу).
            AddAnyStateTransition(rootStateMachine, states["Worship"], AnimatorHashes.IsWorshippingName,
                AnimatorConditionMode.If);
            AddTransition(states["Worship"], locomotionState, AnimatorHashes.IsWorshippingName,
                AnimatorConditionMode.IfNot, 0f, hasExitTime: false);

            // IsStunned (шестой playtest шага 3 — контракт с рантаймом изменился): теперь означает
            // ТОЛЬКО "в руке/в полёте" (висит в позе Dizzy), а не общее состояние оглушения. Жёсткое
            // приземление на земле — отдельный сигнал IsLying (ниже), не IsStunned.
            AddAnyStateTransition(rootStateMachine, states["Stunned"], AnimatorHashes.IsStunnedName,
                AnimatorConditionMode.If);

            // IsLying — жёсткое приземление (шестой playtest шага 3): лежать в замороженной позе
            // StunnedLying до явного триггера StandUp.
            AddAnyStateTransition(rootStateMachine, stunnedLyingState, AnimatorHashes.IsLyingName,
                AnimatorConditionMode.If);

            // Stunned(Dizzy)->Locomotion — мягкое приземление: рука отпустила (IsStunned=false) И это
            // не жёсткий удар (IsLying=false) — сразу на ноги и идти. Если жёсткий удар (IsLying=true),
            // сработает AnyState->StunnedLying выше, конфликт с этим переходом исключён вторым условием.
            AddTransitionTwoConditions(states["Stunned"], locomotionState,
                AnimatorHashes.IsStunnedName, AnimatorConditionMode.IfNot, 0f,
                AnimatorHashes.IsLyingName, AnimatorConditionMode.IfNot, 0f,
                hasExitTime: false);

            // Триггер StandUp теперь уходит из StunnedLying (не из Stunned — Stunned для жёсткого
            // приземления больше не используется как промежуточное состояние, см. выше).
            AddTransition(stunnedLyingState, standUpState, AnimatorHashes.StandUpName,
                AnimatorConditionMode.If, 0f, hasExitTime: false);

            // StandUp->Locomotion по exit time (клип не зациклен) — успевает доиграть перед возвратом.
            var standUpToLocomotion = standUpState.AddTransition(locomotionState);
            standUpToLocomotion.hasExitTime = true;
            standUpToLocomotion.exitTime = 0.9f;
            standUpToLocomotion.duration = 0.15f;
        }

        private static void AddBlendTreeChild(BlendTree blendTree, string clipPath, float threshold, string childLabel)
        {
            var clip = LoadFirstClip(clipPath);
            if (clip != null)
            {
                blendTree.AddChild(clip, threshold);
            }
            else
            {
                Debug.LogWarning($"[Editor] Клип для ветки BlendTree \"{childLabel}\" не найден по пути {clipPath} — " +
                                  "ветка не добавлена.");
            }
        }

        /// <summary>
        /// Слой 1 "UpperBody" (третий playtest шага 3): оверлей испуга поверх локомоции с слоя 0 —
        /// AvatarMask маскирует ноги/Root/IK, поэтому Scared больше не тянет ноги в статичную позу
        /// клипа, пока физика реально несёт тело. Empty/Scared по триггеру Scared, без AnyState (этот
        /// слой не должен реагировать на Worship/Stunned — full-body состояния уже перекрывают его
        /// визуально через blending Override на замаскированных частях, а на немаскированных верх
        /// тела остаётся тем, что дал слой 0). Слой ищется по имени и переиспользуется на повторных
        /// запусках — не дублируется.
        /// </summary>
        private static void BuildUpperBodyLayer(AnimatorController controller)
        {
            var mask = EnsureUpperBodyMask();

            int layerIndex = FindLayerIndex(controller, UpperBodyLayerName);
            if (layerIndex < 0)
            {
                controller.AddLayer(UpperBodyLayerName);
                layerIndex = controller.layers.Length - 1;
                Debug.Log("[Editor] Создан слой \"" + UpperBodyLayerName + "\".");
            }

            var layers = controller.layers;
            var layer = layers[layerIndex];
            layer.avatarMask = mask;
            layer.defaultWeight = 1f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;

            var upperMachine = layer.stateMachine;
            foreach (var existingState in upperMachine.states.ToArray())
                upperMachine.RemoveState(existingState.state);
            foreach (var anyTransition in upperMachine.anyStateTransitions.ToArray())
                upperMachine.RemoveAnyStateTransition(anyTransition);

            var emptyState = upperMachine.AddState("Empty");
            upperMachine.defaultState = emptyState;

            var scaredState = upperMachine.AddState("Scared");
            var scaredClip = LoadFirstClip(SyntyScaredClipPath);
            if (scaredClip != null) scaredState.motion = scaredClip;
            else Debug.LogWarning("[Editor] Клип для состояния \"Scared\" не найден по пути " + SyntyScaredClipPath + ".");

            AddTransition(emptyState, scaredState, AnimatorHashes.ScaredName, AnimatorConditionMode.If, 0f, hasExitTime: false);

            var scaredToEmpty = scaredState.AddTransition(emptyState);
            scaredToEmpty.hasExitTime = true;
            scaredToEmpty.exitTime = 0.9f;
            scaredToEmpty.duration = 0.15f;
        }

        private static int FindLayerIndex(AnimatorController controller, string layerName)
        {
            var layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
                if (layers[i].name == layerName) return i;
            return -1;
        }

        /// <summary>
        /// Создаёт (если ещё нет) AvatarMask слоя UpperBody и приводит состав частей в соответствие
        /// ожидаемому набору, даже если ассет уже существовал (миграция, не только "уже есть" —
        /// .claude/rules/editor-files.md). Ноги/Root/IK-группы выключены (не участвуют в оверлее
        /// испуга — физика/BlendTree слоя 0 несут их), Body/Head/руки/пальцы включены.
        /// </summary>
        private static AvatarMask EnsureUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            bool isNew = mask == null;
            if (isNew)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
                Debug.Log("[Editor] Создана AvatarMask: " + UpperBodyMaskPath);
            }

            bool changed = false;
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.Root, false);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.Body, true);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.Head, true);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.LeftLeg, false);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.RightLeg, false);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.LeftArm, true);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.RightArm, true);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.LeftFingers, true);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.RightFingers, true);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.LeftFootIK, false);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.RightFootIK, false);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.LeftHandIK, false);
            changed |= SetBodyPartActive(mask, AvatarMaskBodyPart.RightHandIK, false);

            if (changed)
            {
                EditorUtility.SetDirty(mask);
                Debug.Log("[Editor] AvatarMask " + UpperBodyMaskPath + " " +
                          (isNew ? "настроена" : "мигрирована под ожидаемый состав частей") +
                          " (ноги/Root/IK выключены, Body/Head/руки/пальцы включены).");
            }

            return mask;
        }

        /// <summary>Возвращает true, если значение части пришлось изменить (для лога/SetDirty).</summary>
        private static bool SetBodyPartActive(AvatarMask mask, AvatarMaskBodyPart part, bool active)
        {
            if (mask.GetHumanoidBodyPartActive(part) == active) return false;
            mask.SetHumanoidBodyPartActive(part, active);
            return true;
        }

        private static AnimationClip LoadFirstClip(string fbxPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        private static void AddTransition(AnimatorState from, AnimatorState to, string paramName,
            AnimatorConditionMode mode, float threshold, bool hasExitTime)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = hasExitTime;
            transition.duration = 0.15f;
            transition.AddCondition(mode, threshold, paramName);
        }

        /// <summary>
        /// Переход с двумя условиями (AND) — нужен для Stunned(Dizzy)->Locomotion (шестой playtest
        /// шага 3): мягкое приземление отличается от жёсткого именно комбинацией IsStunned==false И
        /// IsLying==false, одного условия недостаточно.
        /// </summary>
        private static void AddTransitionTwoConditions(AnimatorState from, AnimatorState to,
            string paramName1, AnimatorConditionMode mode1, float threshold1,
            string paramName2, AnimatorConditionMode mode2, float threshold2, bool hasExitTime)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = hasExitTime;
            transition.duration = 0.15f;
            transition.AddCondition(mode1, threshold1, paramName1);
            transition.AddCondition(mode2, threshold2, paramName2);
        }

        private static void AddAnyStateTransition(AnimatorStateMachine machine, AnimatorState to, string paramName,
            AnimatorConditionMode mode)
        {
            var transition = machine.AddAnyStateTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(mode, 0f, paramName);
        }

        // ------------------------------------------------------------------------------------
        // Префаб
        // ------------------------------------------------------------------------------------

        private static void BuildPrefab(AnimatorController controller, HandConfig handConfig, VillagerConfig villagerConfig, int grabbableLayer)
        {
            bool existed = File.Exists(PrefabPath);
            GameObject root;
            GameObject tempInstance = null;

            if (existed)
            {
                root = PrefabUtility.LoadPrefabContents(PrefabPath);

                // Миграция (правка после playtest'а шага 4, CLAUDE.md "Из ThirdParty ... только
                // визуал"): старая версия Villager.prefab была Prefab Variant поверх
                // SM_Chr_Peasant_Male_01.prefab (корень контента — сам ThirdParty prefab-инстанс).
                // Распаковываем на месте, сохраняя все прошитые компоненты/ссылки — Unpack Completely
                // переносит значения, а не сбрасывает их.
                if (PrefabUtility.IsAnyPrefabInstanceRoot(root))
                {
                    var rootSource = PrefabUtility.GetCorrespondingObjectFromSource(root);
                    var rootSourcePath = rootSource != null ? AssetDatabase.GetAssetPath(rootSource) : null;
                    if (EditorPrefabUtility.IsThirdPartyPath(rootSourcePath))
                    {
                        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        Debug.Log("[Editor] Villager.prefab мигрирован: корень был ThirdParty prefab-инстансом (" +
                                  rootSourcePath + ") — распакован (Unpack Completely), компоненты/ссылки сохранены.");
                    }
                }
            }
            else
            {
                var peasantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PeasantPrefabPath);
                if (peasantPrefab == null)
                {
                    Debug.LogWarning("[Editor] Peasant-префаб не найден: " + PeasantPrefabPath + " — Villager.prefab не создан.");
                    return;
                }

                // Корень — распакованная копия визуала ThirdParty-префаба (EditorPrefabUtility —
                // CLAUDE.md "Из ThirdParty ... только визуал"): InstantiateUnpacked сразу разрывает
                // prefab-связь (Unpack Completely), поэтому SaveAsPrefabAsset создаёт независимый
                // префаб, а не Prefab Variant поверх пака.
                tempInstance = EditorPrefabUtility.InstantiateUnpacked(peasantPrefab);
                root = tempInstance;
            }

            if (root.tag != Tags.Villager) root.tag = Tags.Villager;
            if (grabbableLayer >= 0) root.layer = grabbableLayer;
            else Debug.LogWarning("[Editor] Слой Grabbable не создан — Villager.prefab останется на исходном слое.");

            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            // Ревью шага 3 (плейтест-дефект №1): корень префаба — инстанс Synty-персонажа, у которого
            // applyRootMotion унаследован от базового префаба как true и раньше НЕ переопределялся
            // (в override-модификациях был только m_Controller). Житель двигается физикой напрямую
            // через Rigidbody.linearVelocity (VillagerController.UpdateWander) — root motion из клипов
            // тянул бы его вторым, конкурирующим источником движения (скольжение, доворот, скорость
            // мимо MoveSpeed). Для руки это уже выключено явно (GodHandPrefabSetupEditor); жителю
            // нужен тот же явный override, иначе унаследованное true молча остаётся в новом префабе.
            animator.applyRootMotion = false;

            var rigidbody = root.GetComponent<Rigidbody>();
            bool rigidbodyIsNew = rigidbody == null;
            if (rigidbodyIsNew) rigidbody = root.AddComponent<Rigidbody>();
            // Житель — динамическое тело (ревью шага 3, Б1): физику ведёт рантайм напрямую через
            // Rigidbody.linearVelocity (VillagerController.UpdateWander) и через реальные столкновения
            // (OnCollisionEnter в Tossed). VillagerController не переключает isKinematic сам нигде —
            // если префаб держит isKinematic=true/useGravity=false, житель физически мёртв: не
            // блуждает, не поднимается рукой, не падает и никогда не получает OnCollisionEnter против
            // статической земли, застревая в Tossed навсегда. Поведенческий контракт, не пользовательская
            // подстройка — прошиваем всегда, а не только на new.
            rigidbody.isKinematic = false;
            rigidbody.useGravity = true;
            // Плейтест-дефект №2 (ревью шага 3): 8 жителей двигаются каждый физтик (50 Гц) при рендере
            // 60+ Гц — без интерполяции виден микро-стуттер сразу на всех. Interpolate — стандарт для
            // управляемых физикой персонажей; на бочках (TestHandSceneSetupEditor.EnsureBarrels)
            // оставляем None намеренно — они летают быстро и редко, а Interpolate там того не стоит.
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            // Плейтест-дефект Д1 (ревью шага 3): в префабе m_Constraints был 0 (без ограничений) —
            // житель крутился на месте ещё ДО первого ApplyPhysicsForState (рантайм замораживает
            // вращение только по факту смены состояния, а не сразу на спавне). Стартовое значение по
            // умолчанию — полная заморозка вращения по всем осям; рантайм снимает её в Tossed и
            // восстанавливает при возврате. Поведенческий контракт — прошиваем всегда, а не только на new.
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            if (rigidbodyIsNew)
            {
                // Editor-предпросмотр массы — VillagerController.Construct() всё равно перепрошьёт
                // rigidbody.mass = VillagerConfig.Mass при первом старте (рантайм — источник истины),
                // здесь только заранее подставляем то же число, чтобы Inspector не вводил в заблуждение.
                float mass = villagerConfig != null ? villagerConfig.Mass : FallbackMass;
                rigidbody.mass = mass;

                float maxGrabMass = handConfig != null ? handConfig.MaxGrabMass : FallbackMaxGrabMass;
                if (mass > maxGrabMass)
                {
                    Debug.LogWarning($"[Editor] Villager.prefab: масса ({mass}) больше HandConfig.MaxGrabMass " +
                                      $"({maxGrabMass}) — рука не сможет схватить жителя. Проверьте VillagerConfig.Mass/HandConfig.MaxGrabMass.");
                }
                else
                {
                    Debug.Log($"[Editor] Villager.prefab: масса {mass} в пределах MaxGrabMass ({maxGrabMass}) — захват рукой доступен.");
                }
            }

            var capsule = root.GetComponent<CapsuleCollider>();
            bool capsuleIsNew = capsule == null;
            if (capsuleIsNew)
            {
                capsule = root.AddComponent<CapsuleCollider>();
                SizeCapsuleFromRenderers(root, capsule);
            }

            var villagerController = root.GetComponent<VillagerController>();
            if (villagerController == null)
                villagerController = root.AddComponent<VillagerController>();

            WireVillagerController(villagerController, animator);

            // Правило CLAUDE.md "Из ThirdParty ... только визуал" — проверяем ДО SaveAsPrefabAsset,
            // пока root ещё живой GameObject (contents или tempInstance — работает одинаково).
            EditorPrefabUtility.AssertNoThirdPartyPrefabLinks(root, "Villager.prefab");

            if (existed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                Debug.Log("[Editor] Villager.prefab пересобран: " + PrefabPath +
                          " (масса/размер коллайдера, если были подкручены пользователем, сохранены).");
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Undo.DestroyObjectImmediate(tempInstance);
                Debug.Log("[Editor] Создан Villager.prefab: " + PrefabPath +
                          " (Prefab Variant поверх " + PeasantPrefabPath + ").");
            }
        }

        /// <summary>
        /// Прошивает SerializeField-ссылку <c>_animator</c> на VillagerController (единственное поле
        /// контроллера — Rigidbody/CapsuleCollider он берёт через GetComponent в Construct(), как
        /// ThrowableObjectController). Имя поля сверено с фактическим классом (шаг 3). Н8 (ревью
        /// шага 3): промах прошивки обязательной ссылки — LogError, а не LogWarning, иначе фича
        /// (Animator у жителя) молча простаивает и обнаруживается только на плейтесте.
        /// </summary>
        private static void WireVillagerController(VillagerController controller, Animator animator)
        {
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("_animator");
            if (prop == null)
            {
                Debug.LogError("[Editor] Поле \"_animator\" не найдено на VillagerController — проверьте, что имя " +
                                "SerializeField не изменилось в классе. Villager.prefab останется без Animator-ссылки.");
                return;
            }

            prop.objectReferenceValue = animator;
            so.ApplyModifiedProperties();
            Debug.Log("[Editor] VillagerController._animator прошито (1 из 1).");
        }

        /// <summary>
        /// Прошивает VillagerConfig._prefab ссылкой на только что собранный Villager.prefab (Н6,
        /// ревью шага 3): раньше это делал ConfigAssetCreatorEditor и требовало прогонять "Setup
        /// Villager Prefab" ДО "Create Empty SO Set" во второй раз — здесь префаб гарантированно
        /// уже существует (только что записан на диск), поэтому шаг работает за один прогон.
        /// Идемпотентен: перезаписывает ссылку тем же значением, если конфиг уже существует —
        /// это обязательная связь (единственный источник для VillagerFactory), не пользовательские
        /// данные, затирать нечего.
        /// </summary>
        private static void WireVillagerConfigPrefab(VillagerConfig villagerConfig, GameObject prefab)
        {
            if (villagerConfig == null)
            {
                Debug.LogWarning("[Editor] VillagerConfig не найден — VillagerConfig._prefab не прошит. Сначала прогоните " +
                                  "\"BlackWhiteLike/Configs/Create Empty SO Set\", затем повторно этот пункт меню.");
                return;
            }

            var so = new SerializedObject(villagerConfig);
            var prop = so.FindProperty("_prefab");
            if (prop == null)
            {
                Debug.LogError("[Editor] Поле \"_prefab\" не найдено на VillagerConfig — проверьте имя SerializeField. " +
                                "VillagerFactory не сможет спавнить жителей.");
                return;
            }

            prop.objectReferenceValue = prefab;
            so.ApplyModifiedProperties();
            Debug.Log("[Editor] VillagerConfig._prefab прошито ссылкой на " + PrefabPath + ".");
        }

        /// <summary>
        /// Подгоняет CapsuleCollider под рост персонажа по bounds рендереров — только для
        /// новосозданного коллайдера (не трогаем подкрученный пользователем размер повторным запуском).
        /// Пивот Synty-персонажей — на земле (y=0), поэтому центр капсулы смещён на полвысоты вверх.
        /// </summary>
        private static void SizeCapsuleFromRenderers(GameObject root, CapsuleCollider capsule)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[Editor] Villager.prefab: рендереры не найдены — CapsuleCollider оставлен с дефолтными размерами.");
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // bounds — в мировых координатах инстанса без родителя (root на позиции identity),
            // совпадают с локальными координатами капсулы на корне.
            //
            // Radius НЕ берём из bounds.size.x: SkinnedMeshRenderer.bounds считается по T-pose
            // (bind pose) исходного FBX, где руки разведены в стороны вдоль локального X — это дало
            // бы капсулу шириной ~2м (размах рук), а не ширину тела (обнаружено численной проверкой
            // после первого прогона: m_Radius=1.02 на реальном Villager.prefab). bounds.size.z
            // (глубина, грудь-спина) T-позой не искажается — берём его как базу для радиуса и
            // подрезаем долей от роста как страховку на случай другой позы исходника.
            float height = Mathf.Max(bounds.size.y, 0.1f);
            float radiusFromDepth = bounds.size.z * 0.5f;
            float radius = Mathf.Clamp(radiusFromDepth, 0.15f, height * 0.25f);

            capsule.height = height;
            capsule.radius = radius;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
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
