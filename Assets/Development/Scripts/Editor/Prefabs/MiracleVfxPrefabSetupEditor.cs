using System.IO;
using BlackWhiteLike.Controllers.Vfx;
using BlackWhiteLike.Data.Configs.Vfx;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Editor.Configs;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Prefabs
{
    /// <summary>
    /// Собирает два VFX-префаба шага 4 "Чудеса + Faith" (game-design.md §19.4) в
    /// <c>Assets/Development/Prefabs/VFX/</c> и прошивает их в <c>VfxConfig._entries</c>:
    /// <list type="bullet">
    /// <item><c>Vfx_FireballExplosion.prefab</c> — вложенный prefab-инстанс ThirdParty CFXR-взрыва
    /// (ссылка, не копия — CLAUDE.md, "Связь Prefab → ThirdParty через ссылки"), с фиксом
    /// <c>clearBehavior</c> (дефолт ThirdParty-компонента уничтожил бы пуловый объект после первого
    /// проигрывания — недопустимо для <c>IPoolManager</c>).</item>
    /// <item><c>Vfx_RainCast.prefab</c> — собственный дождь (решение владельца: не тянуть ThirdParty
    /// под этот эффект), два дочерних ParticleSystem (капли + всплеск), материал
    /// Vfx_RainDrop.mat на URP/Particles/Unlit.</item>
    /// </list>
    /// Идемпотентен: <see cref="PrefabUtility.LoadPrefabContents"/>/<see cref="PrefabUtility.SaveAsPrefabAsset"/>
    /// (см. память агента "prefab-content-editing-for-idempotent-rebuild") — существующий префаб
    /// дособирается, не пересоздаётся с нуля.
    /// </summary>
    public static class MiracleVfxPrefabSetupEditor
    {
        private const string PrefabsFolder = "Assets/Development/Prefabs/VFX";
        private const string FireballPrefabPath = PrefabsFolder + "/Vfx_FireballExplosion.prefab";
        private const string RainPrefabPath = PrefabsFolder + "/Vfx_RainCast.prefab";
        // Шаг 6 — Heal + Lightning (game-design.md §4).
        private const string HealCastPrefabPath = PrefabsFolder + "/Vfx_HealCast.prefab";
        private const string LightningStrikePrefabPath = PrefabsFolder + "/Vfx_LightningStrike.prefab";

        private const string CfxrExplosionSourcePath =
            "Assets/ThirdParty/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR4 Explosion Orange (HDR) + Smoke.prefab";

        // Шаг 6 — Hovl Studio RPG VFX Bundle (только чтение, визуал — правило CLAUDE.md "Из ThirdParty ... только визуал").
        private const string HealCastSourcePath =
            "Assets/ThirdParty/VFX/Hovl Studio/RPG VFX Bundle v6.0.3/RPG VFX Bundle/Random effect prefabs/Buff heal.prefab";
        private const string LightningStrikeSourcePath =
            "Assets/ThirdParty/VFX/Hovl Studio/RPG VFX Bundle v6.0.3/AOE Magic spells Vol.1/Prefabs/Lightning strike.prefab";

        private const string MaterialsFolder = "Assets/Development/Art/Materials";
        private const string RainDropMaterialPath = MaterialsFolder + "/Vfx_RainDrop.mat";

        private const string VfxConfigPath = "Assets/Development/Data/Configs/Vfx/VfxConfig.asset";

        [MenuItem("BlackWhiteLike/Prefabs/Setup Miracle VFX Prefabs")]
        public static void SetupMiracleVfxPrefabs()
        {
            EnsureFolder(PrefabsFolder);
            EnsureFolder(MaterialsFolder);

            var fireballPrefab = SetupFireballExplosionPrefab();
            var rainPrefab = SetupRainCastPrefab();
            // Шаг 6 — Heal + Lightning: обёртки над ThirdParty Hovl-эффектами, тот же приём, что и
            // Vfx_FireballExplosion (CFXR) — InstantiateUnpacked + VfxInstanceController.
            var healPrefab = SetupWrappedThirdPartyVfxPrefab("Vfx_HealCast", HealCastPrefabPath, HealCastSourcePath, "Effect");
            var lightningPrefab = SetupWrappedThirdPartyVfxPrefab("Vfx_LightningStrike", LightningStrikePrefabPath, LightningStrikeSourcePath, "Effect");

            WireVfxConfig(fireballPrefab, rainPrefab, healPrefab, lightningPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Setup Miracle VFX Prefabs завершён.");
        }

        // ------------------------------------------------------------------------------------
        // Vfx_FireballExplosion — вложенный ThirdParty CFXR-инстанс
        // ------------------------------------------------------------------------------------

        private static GameObject SetupFireballExplosionPrefab()
        {
            bool existed = File.Exists(FireballPrefabPath);
            GameObject root = existed
                ? PrefabUtility.LoadPrefabContents(FireballPrefabPath)
                : new GameObject("Vfx_FireballExplosion");

            var controller = root.GetComponent<VfxInstanceController>();
            if (controller == null)
            {
                controller = root.AddComponent<VfxInstanceController>();
                Debug.Log("[Editor] VfxInstanceController добавлен на Vfx_FireballExplosion.");
            }

            var explosionTransform = root.transform.Find("Explosion");
            GameObject explosionGo;
            if (explosionTransform == null)
            {
                var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CfxrExplosionSourcePath);
                if (sourcePrefab == null)
                {
                    Debug.LogError("[Editor] ThirdParty-префаб не найден: " + CfxrExplosionSourcePath +
                                    " — Vfx_FireballExplosion собран без вложенного эффекта.");
                    FinishPrefabEdit(root, FireballPrefabPath, existed);
                    return AssetDatabase.LoadAssetAtPath<GameObject>(FireballPrefabPath);
                }

                // ThirdParty-визуал (CLAUDE.md "Из ThirdParty ... только визуал") — InstantiateUnpacked
                // сразу распаковывает инстанс (Unpack Completely), разрывая prefab-связь на CFXR-пак.
                var instance = EditorPrefabUtility.InstantiateUnpacked(sourcePrefab, root.transform);
                instance.name = "Explosion";
                instance.transform.localPosition = Vector3.zero;
                explosionGo = instance;
                Debug.Log("[Editor] Вложенный визуал CFXR-взрыва создан под Vfx_FireballExplosion (распакован).");
            }
            else
            {
                explosionGo = explosionTransform.gameObject;

                // Миграция (правка после playtest'а шага 4): существующий Vfx_FireballExplosion.prefab
                // был собран ДО правила "только визуал" — "Explosion" мог остаться живым prefab-
                // инстансом на CFXR-пак. Распаковываем на месте, значения компонентов (включая
                // clearBehavior, прошиваемый ниже) сохраняются.
                if (PrefabUtility.IsAnyPrefabInstanceRoot(explosionGo))
                {
                    var explosionSource = PrefabUtility.GetCorrespondingObjectFromSource(explosionGo);
                    var explosionSourcePath = explosionSource != null ? AssetDatabase.GetAssetPath(explosionSource) : null;
                    if (EditorPrefabUtility.IsThirdPartyPath(explosionSourcePath))
                    {
                        PrefabUtility.UnpackPrefabInstance(explosionGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        Debug.Log("[Editor] Vfx_FireballExplosion.prefab мигрирован: \"Explosion\" был ThirdParty prefab-инстансом (" +
                                  explosionSourcePath + ") — распакован (Unpack Completely).");
                    }
                }
            }

            // ThirdParty CFXR_Effect (asmdef "CFXR Runtime", недоступен из BlackWhiteLike.Editor как
            // тип — ищем по имени класса, не по typeof) — clearBehavior по умолчанию Destroy уничтожил
            // бы пуловый объект после первого же проигрывания. required: true — у CFXR-взрыва этот
            // компонент есть всегда (в отличие от CFXR4 Flies Cloud на Villager.prefab).
            EditorThirdPartyComponentUtility.FixCfxrClearBehaviorToNone(explosionGo, "Vfx_FireballExplosion.prefab");

            var particleSystem = explosionGo.GetComponentInChildren<ParticleSystem>(true);
            var controllerSo = new SerializedObject(controller);
            const int expected = 2;
            int wired = 0;
            wired += SetRef(controllerSo, "_particleSystem", particleSystem, "VfxInstanceController(Vfx_FireballExplosion)") ? 1 : 0;
            wired += StampMaxLifetime(controllerSo, particleSystem, "VfxInstanceController(Vfx_FireballExplosion)") ? 1 : 0;
            controllerSo.ApplyModifiedProperties();

            if (wired == expected)
                Debug.Log($"[Editor] VfxInstanceController(Vfx_FireballExplosion): прошито {wired} из {expected}.");
            else
                Debug.LogError($"[Editor] VfxInstanceController(Vfx_FireballExplosion): прошито только {wired} из {expected}.");

            // Правило CLAUDE.md "Из ThirdParty ... только визуал" — проверяем ДО FinishPrefabEdit
            // (Save + Unload/Destroy), пока root ещё живой GameObject.
            EditorPrefabUtility.AssertNoThirdPartyPrefabLinks(root, "Vfx_FireballExplosion.prefab");

            FinishPrefabEdit(root, FireballPrefabPath, existed);
            return AssetDatabase.LoadAssetAtPath<GameObject>(FireballPrefabPath);
        }

        // ------------------------------------------------------------------------------------
        // Vfx_RainCast — собственный эффект (решение владельца, без ThirdParty)
        // ------------------------------------------------------------------------------------

        private static GameObject SetupRainCastPrefab()
        {
            bool existed = File.Exists(RainPrefabPath);
            GameObject root = existed
                ? PrefabUtility.LoadPrefabContents(RainPrefabPath)
                : new GameObject("Vfx_RainCast");

            var controller = root.GetComponent<VfxInstanceController>();
            if (controller == null)
            {
                controller = root.AddComponent<VfxInstanceController>();
                Debug.Log("[Editor] VfxInstanceController добавлен на Vfx_RainCast.");
            }

            var material = EnsureRainDropMaterial();

            var rainGo = EnsureChild(root, "Rain", new Vector3(0f, 6f, 0f), new Vector3(90f, 0f, 0f));
            var rainPs = ConfigureRainDrops(rainGo, material);

            var splashGo = EnsureChild(root, "Splash", new Vector3(0f, 0.05f, 0f), Vector3.zero);
            ConfigureSplash(splashGo, material);

            var controllerSo = new SerializedObject(controller);
            const int expected = 2;
            int wired = 0;
            wired += SetRef(controllerSo, "_particleSystem", rainPs, "VfxInstanceController(Vfx_RainCast)") ? 1 : 0;
            wired += StampMaxLifetime(controllerSo, rainPs, "VfxInstanceController(Vfx_RainCast)") ? 1 : 0;
            controllerSo.ApplyModifiedProperties();

            if (wired == expected)
                Debug.Log($"[Editor] VfxInstanceController(Vfx_RainCast): прошито {wired} из {expected}.");
            else
                Debug.LogError($"[Editor] VfxInstanceController(Vfx_RainCast): прошито только {wired} из {expected}.");

            // Vfx_RainCast — собственный эффект без ThirdParty-визуала (см. class-doc), проверка
            // здесь чисто страховочная (на случай будущей правки, добавившей ThirdParty-контент).
            EditorPrefabUtility.AssertNoThirdPartyPrefabLinks(root, "Vfx_RainCast.prefab");

            FinishPrefabEdit(root, RainPrefabPath, existed);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RainPrefabPath);
        }

        private static GameObject EnsureChild(GameObject parent, string name, Vector3 localPosition, Vector3 localEuler)
        {
            var existing = parent.transform.Find(name);
            if (existing != null) return existing.gameObject;

            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, worldPositionStays: false);
            child.transform.localPosition = localPosition;
            child.transform.localEulerAngles = localEuler;
            Debug.Log("[Editor] Создан дочерний объект \"" + name + "\" под " + parent.name + ".");
            return child;
        }

        /// <summary>
        /// Капли дождя: конус вниз с высоты 6 м, stretched billboard, короткая жизнь. Значения
        /// выставляются через ParticleSystem API при каждом прогоне (derived-конфигурация одного
        /// префаба, не пользовательские правки — перезапись безопасна и идемпотентна по результату).
        /// </summary>
        private static ParticleSystem ConfigureRainDrops(GameObject go, Material material)
        {
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f);
            main.gravityModifier = 0.5f;
            main.startColor = new Color(0.75f, 0.88f, 1f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 2.5f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 300f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 3f;
                renderer.velocityScale = 0f;
                if (material != null) renderer.sharedMaterial = material;
            }

            // Триггерится вызовом Launch()/Play() на рантайм-стороне (VfxInstanceController) —
            // здесь только конфигурация, playOnAwake выключаем, чтобы пуловый объект не проигрывал
            // эффект в момент активации из пула без явного вызова.
            main.playOnAwake = false;

            return ps;
        }

        /// <summary>Всплеск капель у земли — короткоживущие мелкие частицы, тот же материал.</summary>
        private static void ConfigureSplash(GameObject go, Material material)
        {
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.gravityModifier = 0.2f;
            main.startColor = new Color(0.8f, 0.9f, 1f, 0.4f);
            main.playOnAwake = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 60f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                if (material != null) renderer.sharedMaterial = material;
            }
        }

        /// <summary>
        /// Материал капель — URP/Particles/Unlit, Transparent. Если шейдер не найден в проекте
        /// (пакет URP не подключён/переименован) — LogError с инструкцией, без падения скрипта
        /// (.claude/rules/editor-files.md: не молчать, но и не ронять пайплайн).
        /// </summary>
        private static Material EnsureRainDropMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(RainDropMaterialPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[Editor] Шейдер \"Universal Render Pipeline/Particles/Unlit\" не найден — " +
                                "проверьте, что URP установлен и подключён (Graphics Settings), и запустите " +
                                "\"BlackWhiteLike/Prefabs/Setup Miracle VFX Prefabs\" снова. Vfx_RainCast временно без материала.");
                return null;
            }

            var material = new Material(shader) { name = "Vfx_RainDrop" };
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f); // Alpha
            material.SetColor("_BaseColor", new Color(0.75f, 0.88f, 1f, 0.55f));
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(material, RainDropMaterialPath);
            Debug.Log("[Editor] Создан материал: " + RainDropMaterialPath);
            return material;
        }

        // ------------------------------------------------------------------------------------
        // Шаг 6 — Vfx_HealCast / Vfx_LightningStrike — обёртки над ThirdParty Hovl-эффектами
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Общий приём для простого ThirdParty-эффекта без доп. настройки (в отличие от
        /// Vfx_FireballExplosion — там ThirdParty-компонент CFXR_Effect требует правки clearBehavior).
        /// Hovl-эффекты оборачиваются так же: <see cref="EditorPrefabUtility.InstantiateUnpacked"/> +
        /// <see cref="VfxInstanceController"/>, известные Hovl-скрипты обезвреживаются
        /// <see cref="DisableKnownHovlBehaviours"/> (см. её doc — почему).
        /// </summary>
        private static GameObject SetupWrappedThirdPartyVfxPrefab(string prefabName, string prefabPath, string sourcePath, string childName)
        {
            bool existed = File.Exists(prefabPath);
            GameObject root = existed
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(prefabName);

            var controller = root.GetComponent<VfxInstanceController>();
            if (controller == null)
            {
                controller = root.AddComponent<VfxInstanceController>();
                Debug.Log($"[Editor] VfxInstanceController добавлен на {prefabName}.");
            }

            var childTransform = root.transform.Find(childName);
            GameObject effectGo;
            if (childTransform == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (source == null)
                {
                    Debug.LogError($"[Editor] ThirdParty-префаб не найден: {sourcePath} — {prefabName} собран без вложенного эффекта.");
                    FinishPrefabEdit(root, prefabPath, existed);
                    return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                }

                var instance = EditorPrefabUtility.InstantiateUnpacked(source, root.transform);
                instance.name = childName;
                effectGo = instance;
                Debug.Log($"[Editor] Вложенный визуал ({sourcePath}) создан под {prefabName} (распакован).");
            }
            else
            {
                effectGo = childTransform.gameObject;

                // Миграция — на случай, если ассет когда-то был собран без InstantiateUnpacked.
                if (PrefabUtility.IsAnyPrefabInstanceRoot(effectGo))
                {
                    var effectSource = PrefabUtility.GetCorrespondingObjectFromSource(effectGo);
                    var effectSourcePath = effectSource != null ? AssetDatabase.GetAssetPath(effectSource) : null;
                    if (EditorPrefabUtility.IsThirdPartyPath(effectSourcePath))
                    {
                        PrefabUtility.UnpackPrefabInstance(effectGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        Debug.Log($"[Editor] {prefabName}: \"{childName}\" был ThirdParty prefab-инстансом ({effectSourcePath}) — распакован (Unpack Completely).");
                    }
                }
            }

            DisableKnownHovlBehaviours(effectGo, prefabName);
            DisableLoopingAndPlayOnAwake(effectGo, prefabName);

            var particleSystem = effectGo.GetComponentInChildren<ParticleSystem>(true);
            var controllerSo = new SerializedObject(controller);
            const int expected = 2;
            int wired = 0;
            wired += SetRef(controllerSo, "_particleSystem", particleSystem, "VfxInstanceController(" + prefabName + ")") ? 1 : 0;
            wired += StampMaxLifetime(controllerSo, particleSystem, "VfxInstanceController(" + prefabName + ")") ? 1 : 0;
            controllerSo.ApplyModifiedProperties();

            if (wired == expected)
                Debug.Log($"[Editor] VfxInstanceController({prefabName}): прошито {wired} из {expected}.");
            else
                Debug.LogError($"[Editor] VfxInstanceController({prefabName}): прошито только {wired} из {expected}.");

            EditorPrefabUtility.AssertNoThirdPartyPrefabLinks(root, prefabName + ".prefab");

            FinishPrefabEdit(root, prefabPath, existed);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        /// <summary>
        /// Hovl-паки (в отличие от CFXR) не имеют единого clearBehavior-подобного поля — вместо этого
        /// несут вспомогательные MonoBehaviour'ы (найдено на "Lightning strike.prefab"): <c>HS_EffectSound</c>
        /// (Hovl namespace, InvokeRepeating зацикленный звук — нежелателен на пуловом объекте без
        /// контроля нашей аудио-системы) и <c>HS_ParticleCollisionInstance</c> (спавнит доп. эффекты по
        /// столкновению частиц через собственный статический пул/GameObject.Find — избыточная логика для
        /// нашего сценария, где VfxInstanceController уже управляет жизненным циклом). Оба ищутся по имени
        /// типа (asmdef Hovl-скриптов не подключён к BlackWhiteLike.Editor — тот же приём, что
        /// EditorThirdPartyComponentUtility.FindComponentByTypeName для CFXR_Effect) и отключаются
        /// (enabled=false), не удаляются —
        /// удаление ломало бы ссылки внутри самого ThirdParty-префаба (EffectsOnCollision и т.п.).
        /// </summary>
        private static void DisableKnownHovlBehaviours(GameObject go, string ownerLabel)
        {
            string[] problematicTypeNames = { "HS_EffectSound", "HS_ParticleCollisionInstance" };
            var allComponents = go.GetComponentsInChildren<Component>(true);
            int disabledCount = 0;

            foreach (var component in allComponents)
            {
                if (component == null) continue;
                var typeName = component.GetType().Name;

                bool isProblematic = false;
                foreach (var candidate in problematicTypeNames)
                {
                    if (typeName == candidate) { isProblematic = true; break; }
                }
                if (!isProblematic) continue;

                if (component is Behaviour behaviour && behaviour.enabled)
                {
                    behaviour.enabled = false;
                    disabledCount++;
                    Debug.Log($"[Editor] {ownerLabel}: компонент {typeName} (ThirdParty Hovl-скрипт) отключён (enabled=false) — " +
                              "пуловый VFX не должен самостоятельно управлять доп.эффектами/звуком.");
                }
            }

            if (disabledCount == 0)
                Debug.Log($"[Editor] {ownerLabel}: известных Hovl-скриптов (HS_EffectSound/HS_ParticleCollisionInstance) не найдено или уже отключены.");
        }

        /// <summary>
        /// БЛОКЕР (ревью шага 6): исходные Hovl-системы ("Buff heal" — 8/8 ParticleSystem
        /// loop:1, "Lightning strike" — 12/12) все с playOnAwake:1 — зацикленная система никогда не
        /// удовлетворяет <c>VfxInstanceController.IsAlive(withChildren:true)==false</c>, и пуловый
        /// объект висит до <c>_maxLifetime</c> со спамом warning на каждый каст. Рукописные сборщики
        /// в этом файле (ConfigureRainDrops/ConfigureSplash) ставят <c>main.loop = false</c> явно —
        /// generic-путь обёртки ThirdParty-эффекта эту строку потерял. Идемпотентен: уже выключенные
        /// системы — no-op, лог только при реальном изменении.
        /// </summary>
        private static void DisableLoopingAndPlayOnAwake(GameObject go, string ownerLabel)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            int fixedCount = 0;

            foreach (var system in systems)
            {
                var main = system.main;
                bool needsFix = main.loop || main.playOnAwake;
                if (!needsFix) continue;

                main.loop = false;
                main.playOnAwake = false;
                fixedCount++;
            }

            if (fixedCount > 0)
                Debug.Log($"[Editor] {ownerLabel}: {fixedCount} из {systems.Length} ParticleSystem — loop/playOnAwake сброшены " +
                          "(зацикленный ThirdParty-эффект никогда не завершился бы в пуле).");
            else
                Debug.Log($"[Editor] {ownerLabel}: все {systems.Length} ParticleSystem уже без loop/playOnAwake.");
        }

        // ------------------------------------------------------------------------------------
        // Общее
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Штампует производное значение <c>_maxLifetime</c> из фактических параметров ParticleSystem
        /// (.claude/rules/editor-files.md, "Derived-значения между ассетами штампуются сборкой") —
        /// не перезаписывает совпадающее значение молча, логирует изменение "старое → новое".
        /// </summary>
        private static bool StampMaxLifetime(SerializedObject controllerSo, ParticleSystem ps, string ownerLabel)
        {
            var prop = controllerSo.FindProperty("_maxLifetime");
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"_maxLifetime\" не найдено на {ownerLabel} — не прошито.");
                return false;
            }

            if (ps == null)
            {
                Debug.LogWarning($"[Editor] {ownerLabel}: ParticleSystem не найден — \"_maxLifetime\" не прошит.");
                return false;
            }

            // Проверка мостом после первого прогона: у CFXR-взрыва корневой ParticleSystem живёт
            // ~0.8 с, а дочерний дым — заметно дольше; штамп только по корню обрезал бы эффект и
            // спамил warning'ом VfxInstanceController на каждый каст. _maxLifetime — СТРАХОВКА
            // (штатное завершение — IsAlive(withChildren: true)), поэтому берём максимум по всем
            // дочерним системам и удваиваем: страховка обязана превышать реальную длительность.
            float longest = 0f;
            foreach (var system in ps.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                float startLifetimeMax = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                    ? main.startLifetime.constantMax
                    : main.startLifetime.constant;
                longest = Mathf.Max(longest, main.duration + startLifetimeMax);
            }

            float newValue = longest * MaxLifetimeSafetyMultiplier;

            float oldValue = prop.floatValue;
            if (!Mathf.Approximately(oldValue, newValue))
            {
                prop.floatValue = newValue;
                Debug.Log($"[Editor] {ownerLabel}._maxLifetime: {oldValue:0.###} → {newValue:0.###} (max(duration + startLifetime) по всем дочерним ParticleSystem × {MaxLifetimeSafetyMultiplier}).");
            }

            return true;
        }

        /// <summary>Запас страховки поверх фактической длительности эффекта (см. StampMaxLifetime).</summary>
        private const float MaxLifetimeSafetyMultiplier = 2f;

        private static void FinishPrefabEdit(GameObject root, string path, bool existed)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            if (existed)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);
        }

        private static bool SetRef(SerializedObject so, string propertyName, Object value, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — прошивка пропущена.");
                return false;
            }

            if (value == null)
            {
                Debug.LogWarning($"[Editor] Значение для \"{propertyName}\" на {ownerLabel} не найдено — оставлено как было.");
                return false;
            }

            prop.objectReferenceValue = value;
            return true;
        }

        // ------------------------------------------------------------------------------------
        // Прошивка VfxConfig._entries
        // ------------------------------------------------------------------------------------

        private static void WireVfxConfig(GameObject fireballPrefab, GameObject rainPrefab, GameObject healPrefab, GameObject lightningPrefab)
        {
            var vfxConfig = AssetDatabase.LoadAssetAtPath<VfxConfig>(VfxConfigPath);
            if (vfxConfig == null)
            {
                Debug.LogWarning("[Editor] VfxConfig.asset не найден — вызываю ConfigAssetCreatorEditor.CreateAll() для его создания.");
                ConfigAssetCreatorEditor.CreateAll();
                vfxConfig = AssetDatabase.LoadAssetAtPath<VfxConfig>(VfxConfigPath);
            }

            if (vfxConfig == null)
            {
                Debug.LogError("[Editor] VfxConfig.asset всё ещё не найден после CreateAll() — _entries не прошиты. " +
                                "Проверьте путь Assets/Development/Data/Configs/Vfx/VfxConfig.asset.");
                return;
            }

            var so = new SerializedObject(vfxConfig);
            var entries = so.FindProperty("_entries");

            UpsertEntry(entries, VfxEffectType.RainCast, rainPrefab, poolSize: 4);
            UpsertEntry(entries, VfxEffectType.FireballExplosion, fireballPrefab, poolSize: 4);
            UpsertEntry(entries, VfxEffectType.HealCast, healPrefab, poolSize: 4);
            UpsertEntry(entries, VfxEffectType.LightningStrike, lightningPrefab, poolSize: 4);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(vfxConfig);

            Debug.Log($"[Editor] VfxConfig._entries: {entries.arraySize} запись(ей) (RainCast, FireballExplosion, HealCast, LightningStrike).");
        }

        /// <summary>
        /// Ищет запись по <c>_type</c> и обновляет <c>_prefab</c> при несовпадении ссылки — не
        /// дублирует записи при повторном прогоне (.claude/rules/editor-files.md, "миграция по
        /// несовпадению ссылки, а не безусловная").
        /// </summary>
        private static void UpsertEntry(SerializedProperty entries, VfxEffectType type, GameObject prefab, int poolSize)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                var element = entries.GetArrayElementAtIndex(i);
                var typeProp = element.FindPropertyRelative("_type");
                if (typeProp != null && typeProp.enumValueIndex == (int)type)
                {
                    var prefabProp = element.FindPropertyRelative("_prefab");
                    if (prefabProp.objectReferenceValue != prefab)
                    {
                        if (prefab == null)
                        {
                            Debug.LogWarning($"[Editor] VfxConfig._entries[{type}]: новый префаб не найден — ссылка оставлена как была.");
                            return;
                        }

                        prefabProp.objectReferenceValue = prefab;
                        Debug.Log($"[Editor] VfxConfig._entries[{type}]: _prefab обновлён.");
                    }

                    var poolSizeProp = element.FindPropertyRelative("_poolSize");
                    if (poolSizeProp != null && poolSizeProp.intValue == 0)
                        poolSizeProp.intValue = poolSize;

                    return;
                }
            }

            if (prefab == null)
            {
                Debug.LogWarning($"[Editor] VfxConfig._entries: запись для {type} не создана — префаб не найден.");
                return;
            }

            int newIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(newIndex);
            var newElement = entries.GetArrayElementAtIndex(newIndex);
            newElement.FindPropertyRelative("_type").enumValueIndex = (int)type;
            newElement.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
            newElement.FindPropertyRelative("_poolSize").intValue = poolSize;
            Debug.Log($"[Editor] VfxConfig._entries: добавлена запись {type} (poolSize={poolSize}).");
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
