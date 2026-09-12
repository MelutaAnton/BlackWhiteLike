using System.IO;
using BlackWhiteLike.Controllers.Village;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Prefabs
{
    /// <summary>
    /// Собирает 4 наших префаба структур деревни в <c>Assets/Development/Prefabs/Village/</c> —
    /// правило CLAUDE.md "Из ThirdParty в сцену и в наши префабы попадает только визуал, никогда —
    /// префаб-инстанс" (найдено на playtest'е шага 4: <c>House_01/02/03</c>, <c>Field_Carrots</c> были
    /// голыми инстансами ThirdParty-префабов POLYGON Fantasy Kingdom / Farm Pack прямо в
    /// <c>_TestVillage.unity</c>, с нашим <c>NeedSiteController</c> навешанным поверх).
    ///
    /// Каждый префаб — <see cref="EditorPrefabUtility.InstantiateUnpacked"/> исходного ThirdParty-
    /// префаба (визуал приходит как значения компонентов, prefab-связь на пак сразу разрывается —
    /// см. .claude/rules/editor-files.md, "ThirdParty-префаб — только через unpack completely"),
    /// с прошитым на корень масштабом (см. <see cref="Structures"/> — фактические значения, вручную
    /// подкрученные владельцем в старой сцене, а не дефолт) и, для структур с заданным NeedType,
    /// триггером нужды: kinematic Rigidbody + BoxCollider(isTrigger) по bounds рендереров + NeedSiteController
    /// с прошитыми <c>_needType</c> и тремя визуальными маркерами. <c>_siteIndex</c> и
    /// <c>_villagerMask</c> — НЕ здесь: они завязаны на конкретный инстанс в конкретной сцене
    /// (порядковый номер точки события, маска слоя жителей) и прошиваются
    /// <c>TestVillageSceneSetupEditor</c> на сценном инстансе, а не на общем для всех сцен префабе.
    ///
    /// <see cref="Structures"/> — единственный источник таблицы "имя → ThirdParty-источник →
    /// масштаб → NeedType?" для обоих скриптов (этого и <c>TestVillageSceneSetupEditor</c>).
    /// Идемпотентен: существующий префаб дособирается через LoadPrefabContents/SaveAsPrefabAsset
    /// (масштаб при несовпадении — миграция с логом "старое → новое", не молчаливая перезапись).
    /// </summary>
    public static class VillageStructurePrefabSetupEditor
    {
        public const string PrefabsFolder = "Assets/Development/Prefabs/Village";

        // --- ThirdParty-источники (только чтение) ---
        private const string HouseFolder =
            "Assets/ThirdParty/3D Environment/POLYGON - Fantasy Kingdom Pack/PolygonFantasyKingdom/Prefabs/Buildings/Presets/";
        private const string FarmPlantsFolder = "Assets/ThirdParty/3D Environment/POLYGON - Farm Pack/Prefabs/Plants/";
        private const string TempleSourcePath = HouseFolder + "SM_Bld_Preset_Church_01_A_Optimized.prefab";

        // Решение владельца (вариант "а"): Visual_Active у NeedType.Fire — не маркер-сфера, а
        // зацикленный VFX-огонь. Источник — "(Wide)"-вариант, а не базовый "CFXR4 Burning Fire":
        // сравнение m_Modifications варианта показывает ShapeModule.radius = 4 против 0.3-0.9 у
        // дочерних систем базового префаба — Wide визуально шире и покрывает всю крышу дома, а не
        // точку в её центре (у "(Smaller)"-варианта, для сравнения, radius ещё меньше базового).
        private const string FireVfxSourcePath =
            "Assets/ThirdParty/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/Variants/CFXR4 Burning Fire (Wide).prefab";

        // Целевой МИРОВОЙ масштаб огня (не зависит от масштаба корня дома, HouseScale = 0.3894128) —
        // "на весь дом, но не выше крыши в 2 раза" (ТЗ владельца): 1.0 визуально накрывает крышу
        // House_01 без переливания за карниз.
        private const float FireVfxWorldScale = 1f;

        // Масштаб House_01/02/03 — прочитан из фактических m_LocalScale.x/y/z в PrefabInstance-
        // модификациях _TestVillage.unity (владелец вручную уменьшил дома на playtest'е шага 3;
        // все три оказались одинаковы: 0.3894128 по всем осям). Field_Carrots модификации масштаба
        // не имеет (владелец её не трогал) — дефолт 1.
        private const float HouseScale = 0.3894128f;

        /// <summary>
        /// Единственный источник таблицы "имя → ThirdParty-источник → масштаб → NeedType?" —
        /// читается и этим скриптом, и <c>TestVillageSceneSetupEditor</c> (позиции на сцене и
        /// siteIndex остаются в сценном скрипте — они не свойство префаба).
        /// </summary>
        public static readonly (string Name, string SourcePrefabPath, Vector3 Scale, NeedType? NeedType)[] Structures =
        {
            ("House_01", HouseFolder + "SM_Bld_Preset_House_01_A_Optimized.prefab", new Vector3(HouseScale, HouseScale, HouseScale), NeedType.Fire),
            // Периодические нужды уровней 2-5 (ТЗ владельца): все три дома — Fire-цели, случайная
            // свободная нужда выбирает случайный свободный NeedSite среди них. House_02 ранее (шаг
            // переноса Disease на жителя) был мигрирован в NeedType=null — теперь он снова хостит
            // нужду, BuildStructurePrefab идемпотентно ДОСОБИРАЕТ NeedSite (BuildNeedSite), а не
            // повторно снимает его (MigrateOffNeedSite вызывается только при NeedType == null).
            ("House_02", HouseFolder + "SM_Bld_Preset_House_02_A_Optimized.prefab", new Vector3(HouseScale, HouseScale, HouseScale), NeedType.Fire),
            ("House_03", HouseFolder + "SM_Bld_Preset_House_03_Optimized.prefab", new Vector3(HouseScale, HouseScale, HouseScale), NeedType.Fire),
            ("Field_Carrots", FarmPlantsFolder + "SM_Prop_Carrot_01_Group.prefab", Vector3.one, NeedType.Hunger),
            // Шаг 6 — Alignment-визуал (game-design.md §7, §10): храм без NeedSiteController (нужды
            // ему не назначаются) — масштаб как у домов (владелец: "масштаб как у домов").
            ("Temple", TempleSourcePath, new Vector3(HouseScale, HouseScale, HouseScale), null),
        };

        public static string GetPrefabPath(string structureName) => PrefabsFolder + "/" + structureName + ".prefab";

        [MenuItem("BlackWhiteLike/Prefabs/Setup Village Structure Prefabs")]
        public static void SetupVillageStructurePrefabs()
        {
            EnsureFolder(PrefabsFolder);

            int built = 0;
            foreach (var definition in Structures)
            {
                if (BuildStructurePrefab(definition))
                    built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Editor] Setup Village Structure Prefabs завершён: {built} из {Structures.Length} собрано в {PrefabsFolder}.");
        }

        private static bool BuildStructurePrefab((string Name, string SourcePrefabPath, Vector3 Scale, NeedType? NeedType) definition)
        {
            var prefabPath = GetPrefabPath(definition.Name);
            bool existed = File.Exists(prefabPath);
            GameObject root;
            GameObject tempInstance = null;

            if (existed)
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            else
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(definition.SourcePrefabPath);
                if (source == null)
                {
                    Debug.LogWarning("[Editor] Не найден ThirdParty-префаб " + definition.SourcePrefabPath +
                                      " — \"" + definition.Name + "\".prefab не создан.");
                    return false;
                }

                tempInstance = EditorPrefabUtility.InstantiateUnpacked(source);
                tempInstance.name = definition.Name;
                root = tempInstance;
            }

            if (root.transform.localScale != definition.Scale)
            {
                var oldScale = root.transform.localScale;
                root.transform.localScale = definition.Scale;
                if (existed)
                {
                    Debug.Log($"[Editor] {definition.Name}.prefab: масштаб корня {oldScale} → {definition.Scale} " +
                              "(таблица Structures — источник истины).");
                }
            }

            if (definition.NeedType.HasValue)
                BuildNeedSite(root, definition.NeedType.Value);
            else
                MigrateOffNeedSite(root, definition.Name);

            // Правило "только визуал" проверяется ДО SaveAsPrefabAsset/Unload — root ещё живой
            // GameObject (либо контент, либо сценный tempInstance), проверка работает одинаково в обоих случаях.
            EditorPrefabUtility.AssertNoThirdPartyPrefabLinks(root, definition.Name + ".prefab");

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (existed)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.Log("[Editor] " + definition.Name + ".prefab пересобран: " + prefabPath + ".");
            }
            else
            {
                Undo.DestroyObjectImmediate(tempInstance);
                Debug.Log("[Editor] Создан " + definition.Name + ".prefab: " + prefabPath +
                          " (масштаб " + definition.Scale + ", визуал из " + definition.SourcePrefabPath + ", распакован).");
            }

            return true;
        }

        /// <summary>
        /// Триггер-зона чуда прямо на префабе структуры: kinematic Rigidbody (обязателен для
        /// срабатывания OnTrigger* — CLAUDE.md, "Триггеры требуют Rigidbody"), BoxCollider(isTrigger)
        /// по bounds рендереров (только при первом добавлении — повторный прогон не затирает
        /// подкрученный вручную объём триггера), NeedSiteController с прошитым <c>_needType</c> и
        /// тремя визуальными маркерами над крышей/грядкой.
        /// </summary>
        private static void BuildNeedSite(GameObject go, NeedType needType)
        {
            var rigidbody = go.GetComponent<Rigidbody>();
            if (rigidbody == null) rigidbody = go.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var box = go.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                EditorVisualMarkerUtility.SizeBoxFromRenderers(go, box);
            }
            else if (!box.isTrigger)
            {
                box.isTrigger = true;
            }

            var controller = go.GetComponent<NeedSiteController>();
            if (controller == null)
            {
                controller = go.AddComponent<NeedSiteController>();
                Debug.Log("[Editor] NeedSiteController добавлен на \"" + go.name + "\".");
            }

            float topLocalY = EditorVisualMarkerUtility.ComputeLocalTopY(go);
            var markerLocalPosition = new Vector3(0f, topLocalY + 0.5f, 0f);
            var normalMarker = EditorVisualMarkerUtility.EnsureVisualMarker(go, "Visual_Normal", new Color(0.2f, 0.85f, 0.2f), markerLocalPosition, initiallyActive: true);

            GameObject activeMarker;
            if (needType == NeedType.Fire)
            {
                // По центру XZ bounds (не (0,0), как у маркера-сферы) — огонь должен стоять на
                // крыше, а не в произвольной точке системы координат исходного меша.
                var topCenterLocal = EditorVisualMarkerUtility.ComputeLocalTopCenter(go);
                activeMarker = EnsureFireActiveVisual(go, topCenterLocal);

                // Фоллбэк, если ThirdParty-источник вдруг не найден и легаси-сферы тоже никогда не
                // было — не оставляем NeedSiteController._activeVisual пустым молча.
                if (activeMarker == null)
                {
                    activeMarker = EditorVisualMarkerUtility.EnsureVisualMarker(go, "Visual_Active", new Color(0.95f, 0.3f, 0.05f), markerLocalPosition, initiallyActive: false);
                }
            }
            else
            {
                activeMarker = EditorVisualMarkerUtility.EnsureVisualMarker(go, "Visual_Active", new Color(0.95f, 0.3f, 0.05f), markerLocalPosition, initiallyActive: false);
            }

            var expiredMarker = EditorVisualMarkerUtility.EnsureVisualMarker(go, "Visual_Expired", new Color(0.35f, 0.35f, 0.35f), markerLocalPosition, initiallyActive: false);

            WireNeedSite(controller, needType, normalMarker, activeMarker, expiredMarker);
        }

        /// <summary>
        /// Visual_Active для NeedType.Fire (сейчас — только House_01) — не маркер-сфера, а
        /// зацикленный CFXR-огонь: см. <see cref="FireVfxSourcePath"/> про выбор варианта "(Wide)".
        /// Идемпотентен и мигрирует уже собранный префаб: если под этим именем сейчас лежит легаси
        /// маркер-сфера (собран до этой правки — есть рендерер, нет ParticleSystem в детях) — она
        /// удаляется, а на её месте создаётся VFX ПОД ТЕМ ЖЕ ИМЕНЕМ "Visual_Active" (WireNeedSite
        /// ниже перепрошивает NeedSiteController._activeVisual на актуальный объект при каждом
        /// прогоне — отдельной миграции ссылки не нужно). Повторный прогон без изменений в исходнике
        /// не создаёт и не удаляет ничего — только численно проверяет позицию/масштаб/clearBehavior/
        /// loop/playOnAwake и логирует только фактические расхождения.
        /// Возвращает null, если ThirdParty-источник не найден: легаси-сфера (если была) не трогается,
        /// новый VFX не создаётся — вызывающий код обязан подставить фоллбэк-маркер.
        /// </summary>
        private static GameObject EnsureFireActiveVisual(GameObject houseRoot, Vector3 localPosition)
        {
            var existingTransform = houseRoot.transform.Find("Visual_Active");
            GameObject visual = existingTransform != null ? existingTransform.gameObject : null;
            bool isLegacySphere = visual != null && IsLegacySphereActiveMarker(visual);

            if (visual == null || isLegacySphere)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(FireVfxSourcePath);
                if (source == null)
                {
                    Debug.LogError("[Editor] ThirdParty-префаб не найден: " + FireVfxSourcePath +
                                    " — Visual_Active на \"" + houseRoot.name + "\" не переведён на VFX-огонь" +
                                    (visual != null ? " (легаси маркер-сфера оставлен как есть)." : "."));
                    return visual; // null либо старая сфера — ничего не сломано
                }

                if (isLegacySphere)
                {
                    Debug.Log("[Editor] \"" + houseRoot.name + "\": Visual_Active — легаси маркер-сфера, мигрирую на CFXR-огонь.");
                    Undo.DestroyObjectImmediate(visual);
                }

                // ThirdParty-визуал — только через InstantiateUnpacked (Unpack Completely),
                // вложенных живых prefab-связей на CFXR-пак быть не должно (CLAUDE.md).
                visual = EditorPrefabUtility.InstantiateUnpacked(source, houseRoot.transform);
                visual.name = "Visual_Active";
                Debug.Log("[Editor] \"" + houseRoot.name + "\": Visual_Active — создан CFXR-огонь из " + FireVfxSourcePath + " (Wide, распакован).");
            }

            visual.transform.localPosition = localPosition;

            // Мировой масштаб огня не должен зависеть от масштаба корня дома (0.3894128) —
            // компенсируем родительский lossyScale, чтобы итоговый мировой масштаб был
            // FireVfxWorldScale независимо от того, как отмасштабирован houseRoot.
            var parentLossy = houseRoot.transform.lossyScale;
            var targetLocalScale = new Vector3(
                parentLossy.x != 0f ? FireVfxWorldScale / parentLossy.x : FireVfxWorldScale,
                parentLossy.y != 0f ? FireVfxWorldScale / parentLossy.y : FireVfxWorldScale,
                parentLossy.z != 0f ? FireVfxWorldScale / parentLossy.z : FireVfxWorldScale);

            if (visual.transform.localScale != targetLocalScale)
            {
                var oldScale = visual.transform.localScale;
                visual.transform.localScale = targetLocalScale;
                Debug.Log($"[Editor] \"{houseRoot.name}\"/Visual_Active: localScale {oldScale} → {targetLocalScale} " +
                          $"(компенсация родительского lossyScale {parentLossy}, целевой мировой масштаб {FireVfxWorldScale}).");
            }

            // Численная проверка (.claude/rules/editor-files.md) — фактический мировой масштаб
            // после установки, не "скрипт отработал без ошибок".
            Debug.Log($"[Editor] Проверено: \"{houseRoot.name}\"/Visual_Active lossyScale = {visual.transform.lossyScale}.");

            FixCfxrClearBehavior(visual);
            FixParticleLoopAndPlayOnAwake(visual);

            // Выключен по умолчанию — как и раньше у маркера-сферы; включает NeedSiteController.SetVisualState.
            visual.SetActive(false);

            return visual;
        }

        /// <summary>
        /// Снимает с структуры то, что <see cref="BuildNeedSite"/> добавляет ТОЛЬКО ради триггера
        /// нужды: NeedSiteController, маркеры Visual_Normal/Visual_Active/Visual_Expired и
        /// кинематический триггер-коллайдер + Rigidbody (isKinematic=true, useGravity=false —
        /// сигнатура, по которой BuildNeedSite их узнаёт как "свои", а не Rigidbody/Collider,
        /// добавленные для чего-то ещё). Вызывается только для структур с NeedType == null
        /// (сейчас — Temple; после возврата House_02/House_03 к Fire в периодическом режиме уровней
        /// 2-5 эта ветка их больше не касается). Меш/материалы не трогает. Идемпотентна: если
        /// NeedSiteController уже отсутствует — no-op без логов (второй прогон не шумит).
        /// </summary>
        private static void MigrateOffNeedSite(GameObject go, string structureName)
        {
            var controller = go.GetComponent<NeedSiteController>();
            if (controller == null) return; // уже мигрирован (или никогда не был NeedSite) — no-op.

            Object.DestroyImmediate(controller, true);

            foreach (var markerName in new[] { "Visual_Normal", "Visual_Active", "Visual_Expired" })
            {
                var markerTransform = go.transform.Find(markerName);
                if (markerTransform != null)
                    Object.DestroyImmediate(markerTransform.gameObject, true);
            }

            var box = go.GetComponent<BoxCollider>();
            if (box != null && box.isTrigger)
                Object.DestroyImmediate(box, true);

            var rigidbody = go.GetComponent<Rigidbody>();
            if (rigidbody != null && rigidbody.isKinematic && !rigidbody.useGravity)
                Object.DestroyImmediate(rigidbody, true);

            Debug.Log("[Editor] " + structureName + ".prefab мигрирован: Disease переехал на жителя — " +
                      "NeedSiteController, маркеры Visual_*, триггер-коллайдер и служебный Rigidbody сняты.");
        }

        /// <summary>Легаси-маркер шага 3 — примитив-сфера без ParticleSystem в детях.</summary>
        private static bool IsLegacySphereActiveMarker(GameObject visual) =>
            visual.GetComponentInChildren<ParticleSystem>(true) == null;

        /// <summary>
        /// ThirdParty CFXR_Effect.clearBehavior = 0 (None) — страховка для зацикленного огня нужды:
        /// он не должен сам себя выключать/уничтожать (объектом управляет только
        /// NeedSiteController.SetVisualState через SetActive). required: true — Visual_Active здесь
        /// всегда настоящий CFXR-огонь (см. EnsureFireActiveVisual), компонент есть по построению.
        /// </summary>
        private static void FixCfxrClearBehavior(GameObject visual) =>
            EditorThirdPartyComponentUtility.FixCfxrClearBehaviorToNone(visual, "\"" + visual.name + "\"");

        /// <summary>
        /// Все ParticleSystem под VFX-огнём обязаны быть loop=true, playOnAwake=true — объект
        /// включается через SetActive(true) из NeedSiteController.SetVisualState, без playOnAwake
        /// огонь просто не запустится при активации.
        /// </summary>
        private static void FixParticleLoopAndPlayOnAwake(GameObject visual)
        {
            var systems = visual.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0)
            {
                Debug.LogWarning("[Editor] \"" + visual.name + "\": ParticleSystem не найдены — loop/playOnAwake не проверены.");
                return;
            }

            foreach (var ps in systems)
            {
                var main = ps.main;

                if (!main.loop)
                {
                    main.loop = true;
                    Debug.Log($"[Editor] \"{ps.name}\" (под {visual.name}): main.loop false → true.");
                }

                if (!main.playOnAwake)
                {
                    main.playOnAwake = true;
                    Debug.Log($"[Editor] \"{ps.name}\" (под {visual.name}): main.playOnAwake false → true.");
                }
            }

            bool allOk = true;
            foreach (var ps in systems)
            {
                var main = ps.main;
                if (!main.loop || !main.playOnAwake) allOk = false;
            }

            if (allOk)
                Debug.Log($"[Editor] Проверено: все ParticleSystem под \"{visual.name}\" ({systems.Length} шт.) — loop=true, playOnAwake=true.");
            else
                Debug.LogError($"[Editor] \"{visual.name}\": не все ParticleSystem получили loop/playOnAwake=true.");
        }

        /// <summary>
        /// Прошивает только <c>_needType</c> и три визуальных маркера — <c>_siteIndex</c> и
        /// <c>_villagerMask</c> остаются на дефолте (0), их прошивает сценный скрипт на конкретном
        /// инстансе (см. class-doc).
        /// </summary>
        private static void WireNeedSite(NeedSiteController controller, NeedType needType,
            GameObject normalVisual, GameObject activeVisual, GameObject expiredVisual)
        {
            const int expected = 4;
            var so = new SerializedObject(controller);
            var ownerLabel = "NeedSiteController(\"" + controller.name + "\")";

            int wired = 0;
            wired += SetIntProp(so, "_needType", (int)needType, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_normalVisual", normalVisual, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_activeVisual", activeVisual, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_expiredVisual", expiredVisual, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();

            if (wired == expected)
            {
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected} (needType={needType}; " +
                          "_siteIndex/_villagerMask прошиваются на сценном инстансе, TestVillageSceneSetupEditor).");
            }
            else
            {
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей.");
            }
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
                Debug.LogWarning($"[Editor] Значение для поля \"{propertyName}\" на {ownerLabel} не найдено — оставлено как было.");
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
