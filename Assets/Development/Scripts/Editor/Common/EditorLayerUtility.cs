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

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
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
