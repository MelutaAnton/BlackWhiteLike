using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Common
{
    /// <summary>
    /// Единственный источник имён служебных Physics-слоёв, создаваемых Editor-скриптами шага 2
    /// (game-design §19.2), и утилита их программного создания в TagManager. Используется и
    /// <c>GodHandPrefabSetupEditor</c> (маски на самом префабе — не зависят от того, собрана ли
    /// уже тестовая сцена), и <c>TestHandSceneSetupEditor</c> (объекты сцены + маски CameraRig'а).
    /// </summary>
    public static class EditorLayerUtility
    {
        public const string GroundLayerName = "Ground";
        public const string GrabbableLayerName = "Grabbable";

        /// <summary>
        /// Создаёт слой с данным именем, если его ещё нет (первый свободный user-слой 8..31).
        /// Идемпотентен: повторный вызов с уже существующим именем просто возвращает его индекс.
        /// Возвращает индекс слоя или -1, если свободных слотов не нашлось.
        /// </summary>
        public static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManagerAsset = LoadTagManagerAsset();
            if (tagManagerAsset == null) return -1;

            var tagManager = new SerializedObject(tagManagerAsset);
            var layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var element = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[Editor] Создан слой \"{layerName}\" (индекс {i}).");
                    return i;
                }
            }

            Debug.LogWarning($"[Editor] Не удалось создать слой \"{layerName}\" — все user-слоты (8..31) заняты.");
            return -1;
        }

        /// <summary>
        /// Создаёт тег с данным именем в TagManager, если его ещё нет (.claude/rules/editor-files.md —
        /// "Программное создание тегов"). Идемпотентен: повторный вызов с уже существующим тегом — no-op.
        /// Используется, например, шагом 3 (жители — тег <c>Tags.Villager</c> из Models/Tags.cs) —
        /// объявление константы в Models/Tags.cs не регистрирует тег в проекте само по себе.
        /// </summary>
        public static void EnsureTag(string tagName)
        {
            var tagManagerAsset = LoadTagManagerAsset();
            if (tagManagerAsset == null) return;

            var tagManager = new SerializedObject(tagManagerAsset);
            var tagsProp = tagManager.FindProperty("tags");

            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName)
                    return;
            }

            // Undo.RecordObject, а не RegisterCreatedObjectUndo — модифицируем существующий
            // ProjectSettings-ассет, а не создаём новый объект (.claude/rules/editor-files.md:
            // Undo.RegisterCreatedObjectUndo применим только к созданию, для правки — RecordObject).
            Undo.RecordObject(tagManagerAsset, "Create Tag " + tagName);

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"[Editor] Создан тег \"{tagName}\".");
        }

        /// <summary>
        /// Грузит ProjectSettings/TagManager.asset с защитой от IndexOutOfRange — LoadAllAssetsAtPath
        /// на битом/недоступном пути возвращает пустой массив, а не null (Н7, ревью шага 3).
        /// </summary>
        private static UnityEngine.Object LoadTagManagerAsset()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[Editor] ProjectSettings/TagManager.asset не удалось загрузить — слой/тег не созданы.");
                return null;
            }

            return assets[0];
        }

        /// <summary>
        /// Прошивает LayerMask-поле (SerializedProperty типа LayerMask хранится как int-битмаска)
        /// значением слоя <paramref name="layerIndex"/>. Не присваивает 0/невалидную маску молча —
        /// если слой не создан (-1), оставляет поле как было и предупреждает.
        /// </summary>
        public static void SetLayerMask(SerializedObject so, string propertyName, int layerIndex, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — маска не прошита.");
                return;
            }

            if (layerIndex < 0)
            {
                Debug.LogWarning($"[Editor] Слой для \"{propertyName}\" на {ownerLabel} не создан — маска оставлена как была.");
                return;
            }

            prop.intValue = 1 << layerIndex;
        }
    }
}
