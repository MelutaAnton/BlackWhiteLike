using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Переимпортирует все Mixamo-fbx в <c>Assets/Content/Animations/**</c> из Generic (как они
    /// приходят с mixamo.com) в Humanoid — жители используют Synty-риг (Humanoid), клипы на
    /// Generic-аватаре на него не сядут (game-design.md §19.3, шаг 3 "Жители + события").
    /// Это НАША папка <c>Content/</c> (не ThirdParty) — правка настроек импорта здесь разрешена
    /// (CLAUDE.md: "Content/ vs Development/Art/" — сырьё, но не сторонний пак).
    /// Идемпотентен: уже Humanoid-файлы пропускаются с логом, счётчик "изменено N из M" в конце.
    /// </summary>
    public static class MixamoHumanoidReimportEditor
    {
        private const string AnimationsFolder = "Assets/Content/Animations";

        [MenuItem("BlackWhiteLike/Setup/Reimport Mixamo As Humanoid")]
        public static void ReimportAllAsHumanoid()
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { AnimationsFolder });
            var fbxPaths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToArray();

            if (fbxPaths.Length == 0)
            {
                Debug.LogWarning("[Editor] В " + AnimationsFolder + " не найдено ни одного .fbx — нечего переимпортировать.");
                return;
            }

            int changed = 0;
            int invalidAvatars = 0;

            foreach (var path in fbxPaths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogWarning("[Editor] " + path + ": не удалось получить ModelImporter — пропущен.");
                    continue;
                }

                if (importer.animationType == ModelImporterAnimationType.Human)
                {
                    Debug.Log("[Editor] " + path + ": уже Humanoid, пропущен.");
                }
                else
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.SaveAndReimport();
                    changed++;
                    Debug.Log("[Editor] " + path + ": переимпортирован как Humanoid (аватар создан из самой модели).");
                }

                // Численная проверка (.claude/rules/editor-files.md — "не скрипт отработал без ошибок",
                // а фактический результат): аватар должен существовать и быть валидным, иначе клипы
                // молча не сядут ни на один Humanoid-риг, а обнаружится это только на сцене жителя.
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar == null)
                {
                    Debug.LogWarning("[Editor] " + path + ": Avatar-сабассет не найден после реимпорта.");
                    invalidAvatars++;
                }
                else if (!avatar.isValid)
                {
                    Debug.LogWarning("[Editor] " + path + ": Avatar есть, но isValid=false — риг/пропорции модели " +
                                      "могут конфликтовать с Humanoid-маппингом (см. skill setup-ragdoll, оговорка про Mixamo).");
                    invalidAvatars++;
                }
                else if (!avatar.isHuman)
                {
                    Debug.LogWarning("[Editor] " + path + ": Avatar валиден, но isHuman=false.");
                    invalidAvatars++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Editor] Reimport Mixamo As Humanoid завершён: изменено {changed} из {fbxPaths.Length}, " +
                      $"невалидных/отсутствующих аватаров: {invalidAvatars}.");
        }
    }
}
