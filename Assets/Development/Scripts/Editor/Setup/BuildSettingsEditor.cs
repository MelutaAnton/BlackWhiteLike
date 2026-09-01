using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Шаг 7 — Кампания из 5 уровней (game-design.md, шаг 7). Единственное место, где перечислены
    /// финальные три сцены кампании и их порядок в Build Settings — вызывается из
    /// <see cref="BootstrapSceneSetupEditor"/>, MainMenuSceneSetupEditor (Editor/UI) и
    /// <see cref="VillageLevelSetupEditor"/>, чтобы прогон любого из трёх сборщиков в изоляции
    /// (например, только "Build Main Menu Scene" на чистом проекте) держал Build Settings в
    /// консистентном состоянии, а не полагался на порядок вызова остальных.
    /// Идемпотентно: canonical-тройка всегда встаёт первой в фиксированном порядке (Bootstrap,
    /// MainMenu, Village_Base), остальные НЕ временные (без "_" в начале имени файла) записи —
    /// сохраняются следом в исходном относительном порядке, не теряются и не дублируются.
    /// Временные ("_"-сцены, песочницы шагов 2-6) в результат никогда не попадают — даже если
    /// когда-то были добавлены руками, эта функция их убирает.
    /// </summary>
    public static class BuildSettingsEditor
    {
        public const string BootstrapScenePath = "Assets/Development/Scenes/Bootstrap.unity";
        public const string MainMenuScenePath = "Assets/Development/Scenes/MainMenu.unity";
        public const string VillageBaseScenePath = "Assets/Development/Scenes/Village_Base.unity";

        private static readonly string[] CanonicalOrder =
        {
            BootstrapScenePath,
            MainMenuScenePath,
            VillageBaseScenePath,
        };

        [MenuItem("BlackWhiteLike/Setup/Ensure Campaign Scenes In Build Settings")]
        public static void EnsureCampaignScenesInBuildSettingsMenuItem()
        {
            EnsureCampaignScenesInBuildSettings();
        }

        public static void EnsureCampaignScenesInBuildSettings()
        {
            var existing = EditorBuildSettings.scenes.ToList();

            // Остальные ("чужие") записи — всё, что не входит в canonical-тройку и не временная
            // ("_"-сцена, песочница) — сохраняются следом за canonical-тройкой в исходном порядке.
            var others = new List<EditorBuildSettingsScene>();
            foreach (var entry in existing)
            {
                if (CanonicalOrder.Contains(entry.path)) continue;

                string fileName = System.IO.Path.GetFileNameWithoutExtension(entry.path);
                if (!string.IsNullOrEmpty(fileName) && fileName.StartsWith("_")) continue; // временная сцена — не включаем.

                others.Add(entry);
            }

            // Фикс ревью шага 7 (БЛОКЕР): на чистом проекте ещё не все сцены кампании собраны —
            // безусловное добавление всей CanonicalOrder создавало бы missing-записи (билд падает).
            // Пропускаем пути, которых нет на диске; идемпотентность (SequenceEqual ниже) не страдает,
            // т.к. once-собранная сцена появится на диске и будет добавлена следующим прогоном.
            var result = new List<EditorBuildSettingsScene>(CanonicalOrder.Length + others.Count);
            foreach (var path in CanonicalOrder)
            {
                if (!System.IO.File.Exists(path))
                {
                    Debug.Log($"[Editor] {path} ещё не собрана — в Build Settings не добавлена.");
                    continue;
                }

                result.Add(new EditorBuildSettingsScene(path, true));
            }
            result.AddRange(others);

            if (SequenceEqualByPathAndEnabled(existing, result))
            {
                Debug.Log("[Editor] Build Settings уже в порядке Bootstrap/MainMenu/Village_Base — без изменений.");
                return;
            }

            EditorBuildSettings.scenes = result.ToArray();
            Debug.Log("[Editor] Build Settings обновлены: " + string.Join(" -> ", result.Select(s => s.path)) + ".");
        }

        private static bool SequenceEqualByPathAndEnabled(List<EditorBuildSettingsScene> a, List<EditorBuildSettingsScene> b)
        {
            if (a.Count != b.Count) return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].path != b[i].path || a[i].enabled != b[i].enabled) return false;
            }

            return true;
        }
    }
}
