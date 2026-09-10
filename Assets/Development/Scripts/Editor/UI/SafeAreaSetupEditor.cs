using System.Collections.Generic;
using System.IO;
using BlackWhiteLike.Views.UI.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Editor.UI
{
    /// <summary>
    /// Шаг 9 — Mobile polish (safe area). Заводит под корнем каждого из трёх канвас-префабов
    /// (<c>LevelHudCanvas</c>, <c>MainMenuCanvas</c>, <c>TempleCanvas</c>) узел <c>SafeAreaRoot</c>
    /// (full-stretch RectTransform + <see cref="SafeAreaFitter"/>) и переносит внутрь него ВСЕ
    /// прежние прямые дети корня канваса — компонент сам применяет <c>Screen.safeArea</c> к своим
    /// якорям, поэтому вся HUD/UI-иерархия автоматически уезжает внутрь безопасной зоны экрана.
    ///
    /// Идемпотентен через <see cref="PrefabUtility.LoadPrefabContents"/>/<see cref="SaveAsPrefabAsset"/>
    /// (см. память агента "prefab-content-editing-for-idempotent-rebuild"): повторный прогон не
    /// создаёт второй SafeAreaRoot и не репарентит повторно (дети, уже лежащие под SafeAreaRoot,
    /// не трогаются).
    ///
    /// ВАЖНО (миграционная ловушка, зафиксировать явно): после этого прогона исходные "Build ..."
    /// скрипты канвасов (LevelHudSetupEditor/MainMenuCanvasSetupEditor/TempleCanvasSetupEditor)
    /// ищут свои узлы (MiraclePanel, TemplePanel и т.п.) через <c>Transform.Find</c> ПРЯМО под
    /// корнем канваса — это прямые дети, а не потомки SafeAreaRoot. Если один из "Build ..."
    /// скриптов будет перезапущен ПОСЛЕ Apply Safe Area, он создаст пустой дубль узла прямо под
    /// корнем (старый, прошитый экземпляр останется жить под SafeAreaRoot). Этот скрипт не решает
    /// эту проблему сам (не в контракте задачи) — при следующем прогоне Apply Safe Area такой
    /// дубль-конфликт имён будет обнаружен и залогирован как LogWarning вместо тихого перезатирания.
    /// Правило для конвейера: Apply Safe Area — ПОСЛЕДНИЙ шаг после любых правок содержимого канваса.
    ///
    /// Также затрагивает Dim-блокеры оверлеев (LevelComplete/LevelStalled/Pause/WelcomeBack): Dim —
    /// ребёнок ПАНЕЛИ-оверлея (не отдельный корневой узел), поэтому оверлей целиком уезжает внутрь
    /// SafeAreaRoot вместе с Dim. Это осознанный компромисс среза: полупрозрачное затемнение
    /// (color.a &lt; 1) перестаёт покрывать вырез/чёлку экрана (там дальше просвечивает предыдущий
    /// кадр), но сам оверлей корректно вписан в безопасную зону.
    ///
    /// НЕПРОЗРАЧНЫЕ full-stretch фоны (anchors 0..1, Image.color.a ≈ 1 — например
    /// MainMenuPanel/LevelSelectPanel/TemplePanel) в SafeAreaRoot НЕ переносятся: перенос оставил бы
    /// некрашеные полосы по краям экрана на вырезе. Вместо этого <see cref="EnsureSafeAreaRoot"/>
    /// применяется рекурсивно ОДНИМ уровнем ниже — прямо внутри такого узла заводится свой
    /// SafeAreaRoot (+SafeAreaFitter), и в него переносятся уже дети панели. Сам непрозрачный фон
    /// красит весь экран, а его контент вписан в безопасную зону.
    /// </summary>
    public static class SafeAreaSetupEditor
    {
        private const string PrefabsFolder = "Assets/Development/Prefabs/UI";
        private const string SafeAreaRootName = "SafeAreaRoot";

        private static readonly string[] CanvasPrefabPaths =
        {
            PrefabsFolder + "/LevelHudCanvas.prefab",
            PrefabsFolder + "/MainMenuCanvas.prefab",
            PrefabsFolder + "/TempleCanvas.prefab",
        };

        [MenuItem("BlackWhiteLike/UI/Apply Safe Area To Canvases")]
        public static void ApplySafeAreaToCanvases()
        {
            int processed = 0;
            foreach (var path in CanvasPrefabPaths)
            {
                if (ApplySafeArea(path))
                    processed++;
            }

            Debug.Log($"[Editor] Apply Safe Area To Canvases завершён: обработано {processed} из {CanvasPrefabPaths.Length} префабов.");
        }

        private static bool ApplySafeArea(string prefabPath)
        {
            if (!File.Exists(prefabPath))
            {
                Debug.LogError($"[Editor] {prefabPath} не найден — сначала соберите канвас соответствующим \"Build ...\" пунктом меню.");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent<Canvas>() == null)
                {
                    Debug.LogError($"[Editor] {prefabPath}: на корне префаба нет компонента Canvas — пропуск (это не канвас-префаб).");
                    return false;
                }

                bool changed = EnsureSafeAreaRoot(root.transform, prefabPath, root.name);

                // Сохраняем только при фактических изменениях — повторный прогон обязан быть
                // настоящим no-op без грязного git diff (см. TouchHitZoneEditor.changedHere).
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                else
                    Debug.Log($"[Editor] {prefabPath}: изменений нет — сохранение пропущено (идемпотентность).");

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Заводит SafeAreaRoot непосредственно под <paramref name="parent"/> (либо переиспользует
        /// существующий) и переносит в него прямые дети <paramref name="parent"/> — кроме самого
        /// SafeAreaRoot и кроме непрозрачных full-stretch фонов, для которых этот же метод
        /// вызывается рекурсивно уровнем ниже (см. <see cref="MoveDirectChildrenInto"/>).
        /// Возвращает true, если что-либо было создано или изменено.
        /// </summary>
        private static bool EnsureSafeAreaRoot(Transform parent, string prefabPath, string contextLabel)
        {
            bool changed = false;

            var safeAreaGo = FindDirectChild(parent, SafeAreaRootName);
            bool createdNew = safeAreaGo == null;
            if (createdNew)
            {
                safeAreaGo = new GameObject(SafeAreaRootName, typeof(RectTransform));
                safeAreaGo.transform.SetParent(parent, worldPositionStays: false);
                Debug.Log($"[Editor] {prefabPath}: {contextLabel} — создан узел {SafeAreaRootName}.");
                changed = true;
            }

            var safeAreaRt = (RectTransform)safeAreaGo.transform;
            SetFullStretch(safeAreaRt);

            // SafeAreaRoot — первый сиблинг: не мешает существующей логике "SetAsLastSibling"
            // ВНУТРИ него (оверлеи после переноса продолжают быть последними среди своих новых
            // соседей — прямых детей SafeAreaRoot).
            if (safeAreaGo.transform.GetSiblingIndex() != 0)
            {
                safeAreaGo.transform.SetAsFirstSibling();
                changed = true;
            }

            if (safeAreaGo.GetComponent<SafeAreaFitter>() == null)
            {
                safeAreaGo.AddComponent<SafeAreaFitter>();
                Debug.Log($"[Editor] {prefabPath}: {contextLabel} — добавлен SafeAreaFitter на {SafeAreaRootName}.");
                changed = true;
            }

            int moved = MoveDirectChildrenInto(parent, safeAreaRt, prefabPath, contextLabel, ref changed);
            if (moved == 0 && !createdNew)
                Debug.Log($"[Editor] {prefabPath}: {contextLabel} — {SafeAreaRootName} уже собран, репарент не потребовался (идемпотентность).");

            return changed;
        }

        /// <summary>
        /// Переносит все прямые дети <paramref name="sourceParent"/> (кроме самого SafeAreaRoot)
        /// внутрь <paramref name="safeAreaRoot"/>. SetParent(worldPositionStays:false) безопасен для
        /// anchoredPosition/sizeDelta детей: SafeAreaRoot — full-stretch (anchors 0..1, offset 0),
        /// то есть его RectTransform в момент переноса совпадает по прямоугольнику с прежним
        /// родителем — те же якоря дают тот же результат.
        ///
        /// Непрозрачный full-stretch фон (см. <see cref="IsOpaqueFullStretchBackground"/>) сюда не
        /// переносится — вместо этого SafeAreaRoot заводится рекурсивно ВНУТРИ него, на уровень ниже.
        /// </summary>
        private static int MoveDirectChildrenInto(Transform sourceParent, RectTransform safeAreaRoot, string prefabPath,
            string contextLabel, ref bool changed)
        {
            var toMove = new List<Transform>();
            for (int i = 0; i < sourceParent.childCount; i++)
            {
                var child = sourceParent.GetChild(i);
                if (child == safeAreaRoot) continue;
                toMove.Add(child);
            }

            int moved = 0;
            foreach (var child in toMove)
            {
                if (IsOpaqueFullStretchBackground(child))
                {
                    // Непрозрачный фон на весь экран: сам узел остаётся на месте (красит весь
                    // экран, включая вырез), а безопасная зона применяется к ЕГО детям.
                    bool nestedChanged = EnsureSafeAreaRoot(child, prefabPath, child.name);
                    changed |= nestedChanged;
                    continue;
                }

                var clash = safeAreaRoot.Find(child.name);
                if (clash != null)
                {
                    Debug.LogWarning($"[Editor] {prefabPath}: узел \"{child.name}\" остался в {contextLabel} — под {SafeAreaRootName} " +
                                      "уже есть объект с таким именем (конфликт после повторного прогона \"Build ...\" поверх Safe Area — " +
                                      "разрешить вручную, см. doc-комментарий класса).");
                    continue;
                }

                child.SetParent(safeAreaRoot, worldPositionStays: false);
                moved++;
                changed = true;
                Debug.Log($"[Editor] {prefabPath}: {contextLabel} — \"{child.name}\" перенесён в {SafeAreaRootName}.");
            }

            return moved;
        }

        /// <summary>
        /// Непрозрачный full-stretch фон — узел, растянутый на весь родительский прямоугольник
        /// (anchors 0..1) и несущий <see cref="Image"/> с color.a ≈ 1. Перенос такого узла в
        /// SafeAreaRoot оставил бы некрашеные полосы по краям экрана на вырезе/чёлке.
        /// </summary>
        private static bool IsOpaqueFullStretchBackground(Transform child)
        {
            if (!(child is RectTransform rt)) return false;
            if (rt.anchorMin != Vector2.zero || rt.anchorMax != Vector2.one) return false;

            var image = child.GetComponent<Image>();
            return image != null && image.color.a >= 0.999f;
        }

        private static GameObject FindDirectChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            return t != null ? t.gameObject : null;
        }

        private static void SetFullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }
    }
}
