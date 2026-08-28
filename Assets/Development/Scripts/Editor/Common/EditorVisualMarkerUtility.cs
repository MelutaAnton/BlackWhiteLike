using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Common
{
    /// <summary>
    /// Визуальные маркеры-примитивы (сфера-индикатор состояния) и вычисление верхней точки bounds —
    /// вынесено из <c>TestVillageSceneSetupEditor</c> в общий класс, когда сборка NeedSite-маркеров
    /// переехала внутрь префабов структур деревни (<c>VillageStructurePrefabSetupEditor</c>) — сцена
    /// больше не создаёт их сама, а других потребителей у логики не было и нет: единственный источник.
    /// </summary>
    public static class EditorVisualMarkerUtility
    {
        /// <summary>
        /// Создаёт (если ещё нет) простой примитив-маркер — дочерний объект под <paramref name="parent"/>.
        /// Идемпотентен по имени child'а — уже существующий маркер не трогается (цвет/позиция могут
        /// быть подкручены художником вручную). Коллайдер примитива уничтожается — маркер чисто
        /// визуальный и не должен участвовать в физике/триггере родителя.
        /// </summary>
        public static GameObject EnsureVisualMarker(GameObject parent, string name, Color color, Vector3 localPosition, bool initiallyActive)
        {
            var existing = parent.transform.Find(name);
            if (existing != null)
            {
                // Миграция (playtest шага 4, «розовые сферы»): маркеры, собранные старой версией, несли
                // материал, созданный в памяти (new Material) — при сохранении в префаб он не
                // сериализуется, ссылка становится missing и рендерится magenta. Тот же класс
                // проблемы, что атлас TMP-шрифта: всё, на что ссылается ассет, само обязано быть
                // ассетом. Существующему маркеру без материала подшиваем материал-ассет.
                EnsureMarkerMaterial(existing.gameObject, name, color, migrating: true);
                return existing.gameObject;
            }

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            Undo.RegisterCreatedObjectUndo(marker, "Create " + name);
            marker.transform.SetParent(parent.transform, worldPositionStays: false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = Vector3.one * 0.6f;

            var markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
                Undo.DestroyObjectImmediate(markerCollider);

            EnsureMarkerMaterial(marker, name, color, migrating: false);

            marker.SetActive(initiallyActive);
            Debug.Log("[Editor] Создан визуальный маркер \"" + name + "\" под \"" + parent.name + "\".");
            return marker;
        }

        private const string MarkerMaterialsFolder = "Assets/Development/Art/Materials/Markers";

        /// <summary>
        /// Материал маркера — ассет в <see cref="MarkerMaterialsFolder"/>, один на имя состояния
        /// (<c>Visual_Normal</c> → <c>NeedSiteMarker_Normal.mat</c>), общий для всех структур.
        /// Создаётся один раз; существующий не перекрашивается (цвет мог быть подкручен руками).
        /// </summary>
        private static void EnsureMarkerMaterial(GameObject marker, string markerName, Color color, bool migrating)
        {
            var renderer = marker.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            if (migrating && renderer.sharedMaterial != null) return; // материал на месте — no-op

            var material = LoadOrCreateMarkerMaterial(markerName, color);
            if (material == null) return; // причина уже в логе

            renderer.sharedMaterial = material;
            if (migrating)
                Debug.Log("[Editor] Маркер \"" + markerName + "\" под \"" + marker.transform.parent.name + "\": материал был missing → " + AssetDatabase.GetAssetPath(material) + " (миграция).");
        }

        private static Material LoadOrCreateMarkerMaterial(string markerName, Color color)
        {
            var stateSuffix = markerName.StartsWith("Visual_") ? markerName.Substring("Visual_".Length) : markerName;
            var path = MarkerMaterialsFolder + "/NeedSiteMarker_" + stateSuffix + ".mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Editor] Шейдер \"Universal Render Pipeline/Lit\" не найден — материал маркера \"" + markerName + "\" не создан.");
                return null;
            }

            EnsureFolder(MarkerMaterialsFolder);
            var material = new Material(shader) { name = "NeedSiteMarker_" + stateSuffix };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            Debug.Log("[Editor] Создан материал маркера: " + path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// Верх bounds объекта в его локальных координатах (для позиции визуальных маркеров над
        /// крышей/грядкой) — считается по GetComponentsInChildren&lt;Renderer&gt;. Обёртка над
        /// <see cref="ComputeLocalTopCenter"/> — оставлена как есть, у маркеров-сфер X/Z всегда 0
        /// (сферы на House_02/03/Field_Carrots центрированы визуально вручную не были — трогать
        /// их поведение вне задачи VFX-огня не нужно).
        /// </summary>
        public static float ComputeLocalTopY(GameObject go) => ComputeLocalTopCenter(go).y;

        /// <summary>
        /// Центр bounds по X/Z и верх по Y — в локальных координатах <paramref name="go"/>. Нужен
        /// VFX-огню (шаг Fire-VFX): в отличие от маркера-сферы, вложенное пламя должно стоять по
        /// центру крыши, а не в произвольной точке X=0/Z=0 системы координат исходного меша.
        /// </summary>
        public static Vector3 ComputeLocalTopCenter(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Vector3(0f, 1f, 0f);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var topWorld = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            return go.transform.InverseTransformPoint(topWorld);
        }

        /// <summary>Заполняет BoxCollider по bounds всех дочерних рендереров (в локальных координатах go).</summary>
        public static void SizeBoxFromRenderers(GameObject go, BoxCollider box)
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
    }
}
