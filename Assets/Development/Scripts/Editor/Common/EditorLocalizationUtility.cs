using BlackWhiteLike.Views.UI.Common;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Common
{
    /// <summary>
    /// Локализационные Editor-утилиты, общие для всех Canvas-билдеров (LevelHudSetupEditor,
    /// MainMenuCanvasSetupEditor, TempleCanvasSetupEditor). Дедуп ревью волны 2 шага 10:
    /// <see cref="SetLocalizationKey"/> (мелочь 3) был дословно продублирован в трёх файлах,
    /// <see cref="EnsureLocalizedTextRegistry"/> (задача 2) — новый editor-шаг, закрывающий
    /// рантайм-скан сцены (LocalizedTextRegistry.Views читается вместо
    /// FindObjectsByType&lt;LocalizedTextView&gt; в проде, см. LocalizedTextApplyEntryPoint).
    /// </summary>
    public static class EditorLocalizationUtility
    {
        /// <summary>
        /// Вешает/обновляет LocalizedTextView на статичном TMP-лейбле (LocalizationConfigSetupEditor —
        /// единственный источник самих текстов по ключу). Авторский текст лейбла НЕ трогаем — он
        /// остаётся ru-плейсхолдером до Apply локализации рантаймом.
        /// </summary>
        public static void SetLocalizationKey(TextMeshProUGUI label, string key)
        {
            var view = label.GetComponent<LocalizedTextView>();
            if (view == null) view = label.gameObject.AddComponent<LocalizedTextView>();

            var so = new SerializedObject(view);
            var prop = so.FindProperty("_key");
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"_key\" не найдено на LocalizedTextView ({label.name}) — прошивка пропущена.");
                return;
            }

            if (prop.stringValue != key)
            {
                Debug.Log($"[Editor] {label.name}.LocalizedTextView._key: \"{prop.stringValue}\" -> \"{key}\".");
                prop.stringValue = key;
                so.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Вешает/обновляет <see cref="LocalizedTextRegistry"/> на корне канваса (<paramref name="canvasRoot"/>)
        /// и заполняет её <c>_views</c> всеми <see cref="LocalizedTextView"/> под этим канвасом
        /// (включая неактивные — оверлеи/панели выключены по умолчанию). Идемпотентно: при
        /// неизменном количестве элементов не логирует (перезапись массива на совпадающий состав —
        /// не событие). Вызывается ПЕРЕД <see cref="PrefabUtility.SaveAsPrefabAsset"/> в каждом
        /// Canvas-билдере (LevelHudSetupEditor/MainMenuCanvasSetupEditor/TempleCanvasSetupEditor).
        /// </summary>
        public static LocalizedTextRegistry EnsureLocalizedTextRegistry(GameObject canvasRoot)
        {
            var registry = canvasRoot.GetComponent<LocalizedTextRegistry>();
            if (registry == null) registry = canvasRoot.AddComponent<LocalizedTextRegistry>();

            var views = canvasRoot.GetComponentsInChildren<LocalizedTextView>(true);
            var so = new SerializedObject(registry);
            var viewsProp = so.FindProperty("_views");
            if (viewsProp == null)
            {
                Debug.LogWarning("[Editor] Поле \"_views\" не найдено на LocalizedTextRegistry — прошивка пропущена.");
                return registry;
            }

            if (viewsProp.arraySize != views.Length)
            {
                Debug.Log($"[Editor] {canvasRoot.name}.LocalizedTextRegistry._views: {viewsProp.arraySize} -> {views.Length}.");
            }

            viewsProp.arraySize = views.Length;
            for (int i = 0; i < views.Length; i++)
                viewsProp.GetArrayElementAtIndex(i).objectReferenceValue = views[i];

            so.ApplyModifiedProperties();
            return registry;
        }
    }
}
