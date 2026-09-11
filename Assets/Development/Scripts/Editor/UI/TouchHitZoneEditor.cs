using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Editor.UI
{
    /// <summary>
    /// Шаг 9 — Mobile polish (touch targets). Аудит + миграция интерактивных элементов трёх
    /// канвас-префабов: минимальная сторона RectTransform кнопки/тач-элемента должна быть не меньше
    /// <see cref="MinHitZoneSize"/> (88 юнитов в системе координат канваса). Стратегия по контракту
    /// задачи — расширяем сам RectTransform (sizeDelta), не отдельный хитбокс-Image поверх: все
    /// перечисленные кнопки НЕ находятся в layout-группах с childControlHeight/Width = true и НЕ
    /// соседствуют впритык (минимальные зазоры в вёрстке HUD/меню/храма ≥ 16px), поэтому рост
    /// меньшей стороны до 88 не наезжает на соседей и не требует замены на отдельный хитбокс.
    ///
    /// Фактический размер измеряется через <c>RectTransform.rect.size</c>, а не через
    /// <c>sizeDelta</c>: для растянутых RectTransform (anchorMin != anchorMax) sizeDelta — это
    /// offset от родителя, а не размер (доказательство — TemplePanel/NextLevelButton, sizeDelta
    /// 260x72 при фактической высоте 28.8). Добавка к sizeDelta (<c>max(0, 88 − rect.size)</c>)
    /// корректна для обоих случаев: growth напрямую входит в формулу ширины/высоты растянутого
    /// прямоугольника. Узлы с инвертированными якорями (anchorMin > anchorMax по оси) хит-зоной
    /// не чинятся — только предупреждение, проблема в вёрстке узла.
    ///
    /// Идемпотентен: правило "только расти, никогда не уменьшать" — при size уже ≥ 88 повторный
    /// прогон no-op (см. <see cref="EnsureMinHitZone"/>).
    ///
    /// Пути ищутся ОТНОСИТЕЛЬНО корня канваса; сначала пробуется путь напрямую под корнем (канвас
    /// ещё не прошёл Apply Safe Area), затем тот же путь под SafeAreaRoot (канвас уже прошёл —
    /// см. SafeAreaSetupEditor) — скрипт работает в любом порядке относительно Apply Safe Area.
    /// </summary>
    public static class TouchHitZoneEditor
    {
        private const string PrefabsFolder = "Assets/Development/Prefabs/UI";
        private const float MinHitZoneSize = 88f;

        private const string LevelHudPrefabPath = PrefabsFolder + "/LevelHudCanvas.prefab";
        private const string MainMenuPrefabPath = PrefabsFolder + "/MainMenuCanvas.prefab";
        private const string TemplePrefabPath = PrefabsFolder + "/TempleCanvas.prefab";

        // Пути к интерактивным элементам — относительно корня канваса (без учёта SafeAreaRoot,
        // см. ResolveRelative). Единственный источник перечня тач-элементов для этого прогона.
        private static readonly string[] LevelHudTargets =
        {
            "MiraclePanel/MiracleButton_Rain",
            "MiraclePanel/MiracleButton_Heal",
            "MiraclePanel/MiracleButton_Fireball",
            "MiraclePanel/MiracleButton_Lightning",
            "PauseButton",
            "LevelCompleteOverlay/Panel/ContinueButton",
            "LevelStalledOverlay/Panel/RefillButton",
            "LevelStalledOverlay/Panel/DeclineButton",
            "PauseOverlay/Panel/ResumeButton",
        };

        private static readonly string[] MainMenuTargets =
        {
            "MainMenuPanel/PlayButton",
            "MainMenuPanel/TempleButton",
            "LevelSelectPanel/BackButton",
            "LevelSelectPanel/CardsGrid/LevelCard_1",
            "LevelSelectPanel/CardsGrid/LevelCard_2",
            "LevelSelectPanel/CardsGrid/LevelCard_3",
            "LevelSelectPanel/CardsGrid/LevelCard_4",
            "LevelSelectPanel/CardsGrid/LevelCard_5",
            "WelcomeBackOverlay/Panel/CollectButton",
            "WelcomeBackOverlay/Panel/CloseButton",
        };

        private static readonly string[] TempleTargets =
        {
            "TemplePanel/CollectIdleButton",
            "TemplePanel/UpgradesColumn/UpgradeRow_HandPower/BuyButton",
            "TemplePanel/UpgradesColumn/UpgradeRow_FaithCapacity/BuyButton",
            "TemplePanel/UpgradesColumn/UpgradeRow_MiraclePower/BuyButton",
            "TemplePanel/NextLevelButton",
            "WelcomeBackOverlay/Panel/CollectButton",
            "WelcomeBackOverlay/Panel/CloseButton",
        };

        [MenuItem("BlackWhiteLike/UI/Enforce Touch Hit Zones")]
        public static void EnforceTouchHitZones()
        {
            int totalChanged = 0;
            int totalChecked = 0;

            totalChecked += Process(LevelHudPrefabPath, LevelHudTargets, ref totalChanged);
            totalChecked += Process(MainMenuPrefabPath, MainMenuTargets, ref totalChanged);
            totalChecked += Process(TemplePrefabPath, TempleTargets, ref totalChanged);

            Debug.Log($"[Editor] Enforce Touch Hit Zones завершён: проверено {totalChecked} элементов, изменено {totalChanged}.");
        }

        private static int Process(string prefabPath, string[] targets, ref int totalChanged)
        {
            if (!File.Exists(prefabPath))
            {
                Debug.LogError($"[Editor] {prefabPath} не найден — сначала соберите канвас соответствующим \"Build ...\" пунктом меню.");
                return 0;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    Debug.Log($"[Editor] {prefabPath}: CanvasScaler.referenceResolution = {scaler.referenceResolution}, " +
                              $"matchWidthOrHeight = {scaler.matchWidthOrHeight} (88 юнитов проверяется в этой системе координат).");
                }

                var unchanged = new List<string>();
                int changedHere = 0;
                int checkedHere = 0;

                foreach (var relativePath in targets)
                {
                    var rt = ResolveRelative(root.transform, relativePath);
                    if (rt == null)
                    {
                        Debug.LogWarning($"[Editor] {prefabPath}: путь \"{relativePath}\" не найден — пропуск проверки хит-зоны.");
                        continue;
                    }

                    checkedHere++;

                    if (EnsureMinHitZone(rt, relativePath, prefabPath))
                        changedHere++;
                    else
                        unchanged.Add($"{relativePath} ({rt.rect.size.x}x{rt.rect.size.y})");
                }

                if (unchanged.Count > 0)
                    Debug.Log($"[Editor] {prefabPath}: {unchanged.Count} элементов уже ≥{MinHitZoneSize} или пропущены — без изменений: " +
                              string.Join(", ", unchanged));

                totalChanged += changedHere;

                if (changedHere > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                // Фактически проверенные элементы (найденные по пути), а не длина списка целей —
                // ненайденные пути (LogWarning выше) в счётчик "проверено" не входят.
                return checkedHere;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Путь резолвится ПОСЕГМЕНТНО: на каждом уровне сегмент ищется сначала прямым ребёнком,
        /// затем под ребёнком SafeAreaRoot этого же уровня. Это покрывает обе структуры
        /// SafeAreaSetupEditor: перенос детей корня канваса под корневой SafeAreaRoot И вложенный
        /// SafeAreaRoot внутри непрозрачных панелей-фонов (MainMenuPanel/SafeAreaRoot/PlayButton) —
        /// скрипт работает при любом порядке относительно Apply Safe Area.
        /// </summary>
        private static RectTransform ResolveRelative(Transform canvasRoot, string relativePath)
        {
            var current = canvasRoot;
            foreach (var segment in relativePath.Split('/'))
            {
                var next = current.Find(segment);
                if (next == null) next = current.Find("SafeAreaRoot/" + segment);
                if (next == null) return null;
                current = next;
            }

            return current as RectTransform;
        }

        /// <summary>
        /// Растит меньшую сторону ФАКТИЧЕСКОГО прямоугольника (<see cref="RectTransform.rect"/>)
        /// до <see cref="MinHitZoneSize"/>, никогда не уменьшает. Возвращает true, если что-то
        /// изменилось. Для растянутых RectTransform sizeDelta — offset от родителя, а не размер,
        /// поэтому мерить нужно rect.size; добавка к sizeDelta при этом корректна в обоих случаях
        /// (stretch и fixed), т.к. напрямую входит в формулу ширины/высоты растянутого прямоугольника.
        /// </summary>
        private static bool EnsureMinHitZone(RectTransform rt, string label, string prefabPath)
        {
            // Инвертированные якоря (anchorMin > anchorMax по оси) — рост sizeDelta ведёт себя
            // непредсказуемо для такого узла; чинить нужно саму вёрстку, а не хит-зону.
            if (rt.anchorMin.x > rt.anchorMax.x || rt.anchorMin.y > rt.anchorMax.y)
            {
                Debug.LogWarning($"[Editor] {prefabPath}: \"{label}\" имеет инвертированные якоря " +
                                  $"(anchorMin {rt.anchorMin}, anchorMax {rt.anchorMax}) — чинится не хит-зоной, " +
                                  "проверьте вёрстку узла.");
                return false;
            }

            var before = rt.rect.size;

            if (before.x <= 0f || before.y <= 0f)
            {
                Debug.LogWarning($"[Editor] {prefabPath}: \"{label}\" имеет нулевой rect.size ({before.x}x{before.y}) — пропуск.");
                return false;
            }

            var growX = Mathf.Max(0f, MinHitZoneSize - before.x);
            var growY = Mathf.Max(0f, MinHitZoneSize - before.y);

            if (growX <= 0f && growY <= 0f) return false;

            rt.sizeDelta += new Vector2(growX, growY);

            var after = rt.rect.size;
            Debug.Log($"[Editor] {prefabPath}: \"{label}\" {before.x}x{before.y} -> {after.x}x{after.y}.");
            return true;
        }
    }
}
