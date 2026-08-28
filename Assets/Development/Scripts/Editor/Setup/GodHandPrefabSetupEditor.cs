using System.IO;
using System.Linq;
using BlackWhiteLike.Controllers.Hand;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Собирает продуктовую руку бога из ThirdParty-ассетов пака "God Hand" (game-design.md §17,
    /// шаг 2 "Камера + рука"): URP-материал, AnimatorController (Generic) и сам префаб.
    /// Идемпотентен: повторный запуск пересобирает содержимое (материал не перезаписывается,
    /// AnimatorController и визуал-иерархия внутри префаба пересобираются с нуля), но сам
    /// .prefab-ассет не дублируется, а уже подкрученный пользователем масштаб корня — не трогается.
    /// </summary>
    public static class GodHandPrefabSetupEditor
    {
        // --- ThirdParty-источники (только чтение, НЕ редактировать и НЕ копировать целиком) ---
        private const string HandMeshFbxPath = "Assets/ThirdParty/God Hand/FirstPersonHands/MaleHands/MaleHand.FBX";
        private const string AnimClipsFbxPath = "Assets/ThirdParty/God Hand/FirstPersonHands/MaleHands/firstPersonHand.FBX";
        private const string TexturesFolder = "Assets/ThirdParty/God Hand/FirstPersonHands/MaleHands/firstPersonHand_textures";
        private const string AlbedoTexturePath = TexturesFolder + "/HandArmDiff.png";
        private const string NormalTexturePath = TexturesFolder + "/HandArmNorm.png";
        private const string SpecGlossTexturePath = TexturesFolder + "/HandArmSpecColGloss.tga";

        // --- Наши целевые пути ---
        private const string MaterialsFolder = "Assets/Development/Art/Materials/Hand";
        private const string MaterialPath = MaterialsFolder + "/Hand_Neutral.mat";
        private const string AnimationsFolder = "Assets/Development/Art/Animations/Hand";
        private const string ControllerPath = AnimationsFolder + "/GodHand.controller";
        private const string PrefabsFolder = "Assets/Development/Prefabs/Hand";
        private const string PrefabPath = PrefabsFolder + "/GodHand.prefab";

        private const string VisualChildName = "Visual";
        private const string GrabAnchorName = "GrabAnchor";
        private const string VfxAnchorName = "VfxAnchor";
        private const string BoneHandName = "BoneHand";

        // Заметная разница масштаба с жителем — подгонка финального значения за пользователем на playtest.
        private const float DefaultRootScale = 3f;

        // Смещение GrabAnchor от кости BoneHand к условному центру ладони — подгонка по playtest.
        private static readonly Vector3 GrabAnchorLocalOffset = new Vector3(0f, 0.02f, 0.05f);

        /// <summary>
        /// Параметры AnimatorController'а. Имена — из AnimatorHashes.*Name (единственный источник,
        /// см. CLAUDE.md — "Запрещённые практики": дублирование идентификатора запрещено). Здесь
        /// перечислены только типы параметров + порядок создания.
        /// </summary>
        private static readonly (string Name, AnimatorControllerParameterType Type)[] Parameters =
        {
            (AnimatorHashes.GrabName, AnimatorControllerParameterType.Trigger),
            (AnimatorHashes.ReleaseName, AnimatorControllerParameterType.Trigger),
            (AnimatorHashes.PlaceName, AnimatorControllerParameterType.Trigger),
            (AnimatorHashes.IsCarryingName, AnimatorControllerParameterType.Bool),
        };

        /// <summary>
        /// Состояния AnimatorController'а: имя состояния ← имя клипа в firstPersonHand.FBX.
        /// Цикличность (Idle, Carry) зависит от Loop Time исходного клипа — это ThirdParty-ассет,
        /// его импорт-настройки не редактируем; если клип не зациклен на источнике, ставим
        /// предупреждение, а не молчим.
        /// </summary>
        private static readonly (string State, string ClipName, bool ExpectedLoop)[] States =
        {
            ("Idle", "hand_pose_cupped", true),
            ("Grab", "hand_claw", false),
            ("Carry", "hand_unspread", true),
            ("Throw", "hand_cast_overhand", false),
            ("Place", "hand_spread", false),
        };

        [MenuItem("BlackWhiteLike/Setup/Setup God Hand Prefab")]
        public static void SetupGodHandPrefab()
        {
            EnsureFolder(MaterialsFolder);
            EnsureFolder(AnimationsFolder);
            EnsureFolder(PrefabsFolder);

            // Слои Ground/Grabbable — общий источник с TestHandSceneSetupEditor (EditorLayerUtility).
            // Создаём здесь тоже: маски принадлежат префабу и не должны зависеть от порядка запуска
            // относительно "Build Test Hand Scene".
            int groundLayer = EditorLayerUtility.EnsureLayer(EditorLayerUtility.GroundLayerName);
            int grabbableLayer = EditorLayerUtility.EnsureLayer(EditorLayerUtility.GrabbableLayerName);

            var material = EnsureMaterial();
            var controller = BuildAnimatorController();
            BuildPrefab(material, controller, groundLayer, grabbableLayer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Setup God Hand Prefab завершён: " + PrefabPath);
        }

        // ------------------------------------------------------------------------------------
        // Материал
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// URP/Lit материал в specular workflow с текстурами пака. Существующий материал
        /// НЕ перезаписывается — пользовательская подстройка (цвет, gloss) дороже дефолта.
        /// </summary>
        private static Material EnsureMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                Debug.Log("[Editor] Материал уже существует, не перезаписан: " + MaterialPath);
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Editor] Шейдер \"Universal Render Pipeline/Lit\" не найден — материал не создан. " +
                                  "Проверьте, что URP установлен в проект.");
                return null;
            }

            var material = new Material(shader) { name = "Hand_Neutral" };

            // Specular workflow: _WorkflowMode = 0 (см. LitGUI.WorkflowMode.Specular в исходнике URP).
            material.SetFloat("_WorkflowMode", 0f);

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoTexturePath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalTexturePath);
            var specGloss = AssetDatabase.LoadAssetAtPath<Texture2D>(SpecGlossTexturePath);

            if (albedo != null) material.SetTexture("_BaseMap", albedo);
            else Debug.LogWarning("[Editor] Albedo-текстура не найдена: " + AlbedoTexturePath);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            else Debug.LogWarning("[Editor] Normal-текстура не найдена: " + NormalTexturePath);

            if (specGloss != null)
            {
                material.SetTexture("_SpecGlossMap", specGloss);
                // Белый specular color — тон/интенсивность полностью берутся из карты.
                material.SetColor("_SpecColor", Color.white);
                material.EnableKeyword("_SPECULAR_SETUP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else Debug.LogWarning("[Editor] Specular/Gloss-текстура не найдена: " + SpecGlossTexturePath);

            AssetDatabase.CreateAsset(material, MaterialPath);
            Debug.Log("[Editor] Создан материал: " + MaterialPath);
            return material;
        }

        // ------------------------------------------------------------------------------------
        // AnimatorController
        // ------------------------------------------------------------------------------------

        private static AnimatorController BuildAnimatorController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            bool existed = controller != null;

            if (!existed)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                Debug.Log("[Editor] Создан AnimatorController: " + ControllerPath);
            }

            var clipsByName = AssetDatabase.LoadAllAssetsAtPath(AnimClipsFbxPath)
                .OfType<AnimationClip>()
                .ToDictionary(c => c.name, c => c);

            var rootStateMachine = controller.layers[0].stateMachine;

            // Пересобираем содержимое с нуля, чтобы повторный запуск не плодил дубли состояний/переходов.
            foreach (var existingState in rootStateMachine.states.ToArray())
                rootStateMachine.RemoveState(existingState.state);
            controller.parameters = System.Array.Empty<AnimatorControllerParameter>();

            foreach (var (name, type) in Parameters)
                controller.AddParameter(name, type);

            var createdStates = new System.Collections.Generic.Dictionary<string, AnimatorState>();
            foreach (var (stateName, clipName, expectedLoop) in States)
            {
                var state = rootStateMachine.AddState(stateName);
                createdStates[stateName] = state;

                if (clipsByName.TryGetValue(clipName, out var clip))
                {
                    state.motion = clip;
                    if (expectedLoop && !clip.isLooping)
                    {
                        Debug.LogWarning($"[Editor] Клип \"{clipName}\" для состояния \"{stateName}\" ожидался зацикленным " +
                                          "(Loop Time), но в исходном ассете это не так. Импорт-настройки ThirdParty-FBX " +
                                          "не редактируем — при необходимости подправить Loop Time в аниматоре пака вручную.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Editor] Клип \"{clipName}\" не найден в {AnimClipsFbxPath} — " +
                                      $"состояние \"{stateName}\" оставлено без Motion.");
                }
            }

            rootStateMachine.defaultState = createdStates["Idle"];

            AddTransition(createdStates["Idle"], createdStates["Grab"], AnimatorHashes.GrabName, hasExitTime: false);
            // hasExitTime: true — GodHandController ставит триггер Grab и IsCarrying=true в один
            // кадр, поэтому Grab->Carry с hasExitTime:false проскакивало бы состояние Grab целиком
            // (условие уже истинно на входе, виден только 0.1с кроссфейд, hand_claw не проигрывается).
            // Exit-time в конце клипа (0.9) даёт схвату реально доиграть перед переходом в Carry.
            AddTransition(createdStates["Grab"], createdStates["Carry"], AnimatorHashes.IsCarryingName, conditionValue: true, hasExitTime: true);
            AddTransition(createdStates["Grab"], createdStates["Idle"], AnimatorHashes.IsCarryingName, conditionValue: false, hasExitTime: true);
            AddTransition(createdStates["Carry"], createdStates["Throw"], AnimatorHashes.ReleaseName, hasExitTime: false);
            AddTransition(createdStates["Carry"], createdStates["Place"], AnimatorHashes.PlaceName, hasExitTime: false);
            AddTransition(createdStates["Throw"], createdStates["Idle"], null, hasExitTime: true);
            AddTransition(createdStates["Place"], createdStates["Idle"], null, hasExitTime: true);

            EditorUtility.SetDirty(controller);
            Debug.Log("[Editor] AnimatorController пересобран: " + ControllerPath);
            return controller;
        }

        /// <summary>
        /// Добавляет переход между состояниями. paramName == null → безусловный exit-time переход.
        /// paramName задан без conditionValue → трактуется как Trigger (If).
        /// paramName задан с conditionValue → трактуется как Bool (If/IfNot).
        /// </summary>
        private static void AddTransition(AnimatorState from, AnimatorState to, string paramName,
            bool hasExitTime, bool? conditionValue = null)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = hasExitTime;
            if (hasExitTime) transition.exitTime = 0.9f;
            transition.duration = 0.1f;

            if (paramName == null) return;

            if (conditionValue.HasValue)
            {
                transition.AddCondition(conditionValue.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    0f, paramName);
            }
            else
            {
                transition.AddCondition(AnimatorConditionMode.If, 0f, paramName);
            }
        }

        // ------------------------------------------------------------------------------------
        // Префаб
        // ------------------------------------------------------------------------------------

        private static void BuildPrefab(Material material, AnimatorController controller, int groundLayer, int grabbableLayer)
        {
            bool existed = File.Exists(PrefabPath);
            GameObject root = existed
                ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject("GodHand");

            if (root.GetComponent<GodHandController>() == null)
                root.AddComponent<GodHandController>();

            // GrabAnchor обычно живёт под BoneHand ВНУТРИ Visual — если сейчас его уничтожить вместе
            // с Visual без сохранения localPosition, смещение ладони (playtest-параметр, как и
            // масштаб корня) молча слетает к дефолту при каждом прогоне. Захватываем ДО уничтожения.
            Vector3? savedGrabAnchorLocalPosition = null;
            var previousGrabAnchor = FindDeepChild(root.transform, GrabAnchorName);
            if (previousGrabAnchor != null)
                savedGrabAnchorLocalPosition = previousGrabAnchor.localPosition;

            // Visual пересобирается с нуля при каждом запуске — производная данные, не подкручиваются руками.
            var existingVisual = root.transform.Find(VisualChildName);
            if (existingVisual != null)
                Object.DestroyImmediate(existingVisual.gameObject);

            var handFbx = AssetDatabase.LoadAssetAtPath<GameObject>(HandMeshFbxPath);
            Transform visualTransform;
            Animator animator = null;
            Transform boneHand = null;

            if (handFbx == null)
            {
                Debug.LogWarning("[Editor] Не найден меш кисти: " + HandMeshFbxPath + " — Visual не создан.");
                visualTransform = null;
            }
            else
            {
                // ThirdParty-визуал (CLAUDE.md "Из ThirdParty ... только визуал") — InstantiateUnpacked
                // сразу распаковывает инстанс (Unpack Completely), разрывая prefab-связь на MaleHand.FBX;
                // Visual пересобирается с нуля при КАЖДОМ прогоне (см. выше — existingVisual уничтожается
                // безусловно), поэтому миграция уже собранного GodHand.prefab происходит автоматически
                // при первом же запуске этого пункта меню после правки, без отдельной ветки.
                var visualInstance = EditorPrefabUtility.InstantiateUnpacked(handFbx, root.transform);
                visualInstance.name = VisualChildName;
                visualTransform = visualInstance.transform;

                animator = visualInstance.GetComponent<Animator>();
                if (animator == null) animator = visualInstance.AddComponent<Animator>();

                var avatar = AssetDatabase.LoadAllAssetsAtPath(HandMeshFbxPath).OfType<Avatar>().FirstOrDefault();
                if (avatar != null) animator.avatar = avatar;
                else Debug.LogWarning("[Editor] Avatar не найден среди сабассетов " + HandMeshFbxPath + ".");

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                var smr = visualInstance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null)
                {
                    if (material != null) smr.sharedMaterial = material;
                    // Ragdoll/skinning-нюанс из CLAUDE.md: SMR не пересчитывает bounds по вершинам —
                    // без этого флага рука может пропадать из фрустума при экстремальных позах.
                    smr.updateWhenOffscreen = true;
                }
                else
                {
                    Debug.LogWarning("[Editor] SkinnedMeshRenderer не найден в " + HandMeshFbxPath + ".");
                }

                boneHand = FindDeepChild(visualTransform, BoneHandName);
                if (boneHand == null)
                    Debug.LogWarning($"[Editor] Кость \"{BoneHandName}\" не найдена в иерархии {HandMeshFbxPath} — GrabAnchor будет под корнем.");
            }

            // GrabAnchor — родитель по возможности кость BoneHand, иначе корень (не молчим о деградации).
            // Старый GrabAnchor либо уже уничтожен вместе с Visual (если жил под BoneHand — обычный
            // случай), либо пережил уничтожение (жил fallback'ом прямо под корнем) — в этом случае
            // явно убираем его перед пересозданием, чтобы не плодить дубль под новым родителем.
            var grabAnchorParent = boneHand != null ? boneHand : root.transform;
            var leftoverGrabAnchor = FindDeepChild(root.transform, GrabAnchorName);
            if (leftoverGrabAnchor != null)
                Object.DestroyImmediate(leftoverGrabAnchor.gameObject);

            var grabAnchorGo = new GameObject(GrabAnchorName);
            var grabAnchor = grabAnchorGo.transform;
            grabAnchor.SetParent(grabAnchorParent, worldPositionStays: false);
            // Восстанавливаем подкрученное на playtest смещение, если оно было; иначе — дефолт.
            grabAnchor.localPosition = savedGrabAnchorLocalPosition ?? GrabAnchorLocalOffset;
            grabAnchor.localRotation = Quaternion.identity;

            // VfxAnchor — под alignment-ореол (шаг 6 game-design), в корне.
            var vfxAnchor = root.transform.Find(VfxAnchorName);
            if (vfxAnchor == null)
            {
                var go = new GameObject(VfxAnchorName);
                vfxAnchor = go.transform;
                vfxAnchor.SetParent(root.transform, worldPositionStays: false);
                vfxAnchor.localPosition = Vector3.zero;
            }

            if (!existed)
                root.transform.localScale = Vector3.one * DefaultRootScale;
            // Иначе масштаб корня, уже подкрученный пользователем, не трогаем.

            WireController(root, visualTransform, grabAnchor, animator, groundLayer, grabbableLayer);

            // Правило CLAUDE.md "Из ThirdParty ... только визуал" — проверяем ДО SaveAsPrefabAsset.
            EditorPrefabUtility.AssertNoThirdPartyPrefabLinks(root, "GodHand.prefab");

            if (existed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                Debug.Log("[Editor] Префаб пересобран: " + PrefabPath +
                          " (масштаб корня и смещение GrabAnchor, если были подкручены пользователем, сохранены).");
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Object.DestroyImmediate(root);
                Debug.Log("[Editor] Создан префаб: " + PrefabPath + " (масштаб корня по умолчанию: " + DefaultRootScale + " — подгонка на playtest).");
            }
        }

        /// <summary>
        /// Прошивает SerializeField-ссылки на GodHandController. Имена полей сверены с контрактом
        /// (_visualRoot, _grabAnchor, _animator); если в фактическом классе они называются иначе —
        /// предупреждаем, а не молчим и не переименовываем чужой класс.
        /// </summary>
        private static void WireController(GameObject root, Transform visual, Transform grabAnchor, Animator animator,
            int groundLayer, int grabbableLayer)
        {
            var controllerComponent = root.GetComponent<GodHandController>();
            if (controllerComponent == null) return;

            var so = new SerializedObject(controllerComponent);
            // Поля — Transform (см. фактический GodHandController), не GameObject.
            SetRef(so, "_visualRoot", visual);
            SetRef(so, "_grabAnchor", grabAnchor);
            SetRef(so, "_animator", animator);
            EditorLayerUtility.SetLayerMask(so, "_groundMask", groundLayer, "GodHandController");
            EditorLayerUtility.SetLayerMask(so, "_grabbableMask", grabbableLayer, "GodHandController");
            so.ApplyModifiedProperties();
        }

        private static void SetRef(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на GodHandController — " +
                                  "проверьте, что имя совпадает с фактическим классом.");
                return;
            }

            if (value == null)
            {
                Debug.LogWarning($"[Editor] Не удалось найти значение для поля \"{propertyName}\" — оставлено как было.");
                return;
            }

            prop.objectReferenceValue = value;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null) return null;
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindDeepChild(child, name);
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
