using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Common
{
    /// <summary>
    /// Общие приёмы работы с ThirdParty-компонентами, у которых нет asmdef-ссылки из нашей Editor-
    /// сборки (CFXR/Hovl и т.п. — тип доступен только по имени класса, не через typeof()). Вынесено
    /// сюда, чтобы новые Editor-скрипты (например, Visual_Sick на Villager.prefab) не копипастили
    /// одну и ту же пару "найти компонент по имени типа" + "обнулить CFXR_Effect.clearBehavior" —
    /// образец до выноса: <c>MiracleVfxPrefabSetupEditor</c>/<c>VillageStructurePrefabSetupEditor</c>.
    /// </summary>
    public static class EditorThirdPartyComponentUtility
    {
        /// <summary>Ищет компонент по имени типа среди всех Component в иерархии (включая неактивные).</summary>
        public static Component FindComponentByTypeName(GameObject go, string typeName)
        {
            if (go == null) return null;

            var all = go.GetComponentsInChildren<Component>(true);
            foreach (var component in all)
            {
                if (component != null && component.GetType().Name == typeName)
                    return component;
            }

            return null;
        }

        /// <summary>
        /// Находит на <paramref name="visualRoot"/> ThirdParty-компонент CFXR_Effect и прошивает
        /// <c>clearBehavior = 0 (None)</c> — дефолт (обычно Destroy/Disable) не годится для эффекта,
        /// которым управляет наш код (SetActive/пул), а не сам CFXR-скрипт. Идемпотентно: совпадающее
        /// значение не перезаписывается и не логируется повторно. Численная проверка результата —
        /// не «применилось без ошибок» (.claude/rules/editor-files.md).
        /// <paramref name="required"/>: некоторые ThirdParty-эффекты (например CFXR4 Flies Cloud —
        /// только ParticleSystem, компонента CFXR_Effect у него нет вообще) не обязаны нести
        /// CFXR_Effect — отсутствие компонента там штатно, а не повод шуметь Warning'ом на каждом
        /// прогоне (нарушение "повторный прогон не шумит", .claude/rules/editor-files.md). При
        /// <c>required = false</c> отсутствие компонента — молча, без лога.
        /// </summary>
        public static void FixCfxrClearBehaviorToNone(GameObject visualRoot, string ownerLabel, bool required = true)
        {
            var cfxrEffect = FindComponentByTypeName(visualRoot, "CFXR_Effect");
            if (cfxrEffect == null)
            {
                if (required)
                    Debug.LogWarning($"[Editor] {ownerLabel}: компонент CFXR_Effect не найден — clearBehavior не проверен.");
                return;
            }

            var so = new SerializedObject(cfxrEffect);
            var clearProp = so.FindProperty("clearBehavior");
            if (clearProp == null)
            {
                Debug.LogError($"[Editor] {ownerLabel}: поле \"clearBehavior\" не найдено на CFXR_Effect — версия ThirdParty-пака изменилась?");
                return;
            }

            if (clearProp.enumValueIndex != 0)
            {
                int oldValue = clearProp.enumValueIndex;
                clearProp.enumValueIndex = 0; // None
                so.ApplyModifiedProperties();
                Debug.Log($"[Editor] {ownerLabel}: CFXR_Effect.clearBehavior {oldValue} → 0 (None).");
            }

            var verifySo = new SerializedObject(cfxrEffect);
            int verified = verifySo.FindProperty("clearBehavior").enumValueIndex;
            if (verified == 0)
                Debug.Log($"[Editor] Проверено: {ownerLabel} CFXR_Effect.clearBehavior == 0 (None).");
            else
                Debug.LogError($"[Editor] {ownerLabel}: CFXR_Effect.clearBehavior НЕ равен 0 после прошивки ({verified}).");
        }
    }
}
