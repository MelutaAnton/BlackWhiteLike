using System;
using System.IO;
using System.Linq;
using System.Text;
using BlackWhiteLike.Editor.Setup;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlackWhiteLike.Editor.Build
{
    /// <summary>
    /// Вставка между шагами 2 и 3: первая смоук-сборка на Android — проверить, что игра вообще
    /// запускается на телефоне и что рука хватается пальцем. Это НЕ шаг 9 "Mobile polish"
    /// (safe area, хит-зоны, хаптика, game-design.md §19.9) — только инфраструктура сборки.
    /// Собирает единственную сцену-песочницу _TestHand.unity (Bootstrap.unity в этой сборке
    /// не участвует). Список сцен передаётся напрямую в BuildPlayerOptions — EditorBuildSettings.
    /// scenes проекта не меняется.
    /// </summary>
    public static class AndroidSmokeBuildEditor
    {
        private const string TestHandScenePath = "Assets/Development/Scenes/_TestHand.unity";
        private const string BuildFolder = "Builds/Android";
        private const string BuildFileName = "GodHand_Smoke.apk";
        private const string BuildPath = BuildFolder + "/" + BuildFileName;

        [MenuItem("BlackWhiteLike/Build/Android Smoke Build")]
        public static void BuildAndroidSmoke()
        {
            // Единственный источник настроек PlayerSettings — MobilePlayerSettingsEditor,
            // сборка не копирует их, а переиспользует.
            MobilePlayerSettingsEditor.ApplyMobilePlayerSettings();

            if (!EnsureAndroidActiveTarget())
                return;

            if (!File.Exists(TestHandScenePath))
            {
                Debug.LogError($"[Editor] Сцена сборки не найдена: {TestHandScenePath}. Сборка прервана.");
                return;
            }

            var buildFolderAbsolute = Path.Combine(Directory.GetCurrentDirectory(), BuildFolder);
            Directory.CreateDirectory(buildFolderAbsolute);

            var buildPathAbsolute = Path.Combine(Directory.GetCurrentDirectory(), BuildPath);
            if (File.Exists(buildPathAbsolute))
            {
                // Перезаписываем старый APK, не плодим _1/_2 — идемпотентность сборки.
                File.Delete(buildPathAbsolute);
                Debug.Log("[Editor] Старый APK удалён перед пересборкой: " + BuildPath);
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { TestHandScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.Android,
                targetGroup = UnityEditor.Build.NamedBuildTarget.Android.ToBuildTargetGroup(),
                // Профайлер и внятные стектрейсы на устройстве нужны для первой проверки.
                options = BuildOptions.Development | BuildOptions.ConnectWithProfiler | BuildOptions.AllowDebugging
            };

            Debug.Log("[Editor] Старт Android-смоук-сборки: " + TestHandScenePath + " -> " + BuildPath);

            var startTime = DateTime.UtcNow;
            var report = BuildPipeline.BuildPlayer(options);
            var elapsed = DateTime.UtcNow - startTime;

            ReportResult(report, elapsed);
        }

        /// <summary>
        /// Гарантирует, что активная платформа сборки — Android. Переключение платформы
        /// вызывает долгий переимпорт ассетов (в проекте ~1.7 ГБ паков) — предупреждаем заранее.
        /// Если переключиться не удалось — явная ошибка и выход, без попытки собрать не туда.
        /// </summary>
        private static bool EnsureAndroidActiveTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log("[Editor] Активная платформа уже Android — переключение не требуется.");
                return true;
            }

            Debug.LogWarning("[Editor] Активная платформа — " + EditorUserBuildSettings.activeBuildTarget +
                              ". Переключение на Android запустит долгий переимпорт ассетов " +
                              "(в проекте ~1.7 ГБ паков) — это может занять продолжительное время.");

            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);

            if (!switched || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("[Editor] Не удалось переключить активную платформу на Android. Сборка прервана.");
                return false;
            }

            Debug.Log("[Editor] Активная платформа переключена на Android.");
            return true;
        }

        private static void ReportResult(BuildReport report, TimeSpan elapsed)
        {
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), BuildPath);
                long sizeBytes = File.Exists(absolutePath) ? new FileInfo(absolutePath).Length : (long)summary.totalSize;
                double sizeMb = sizeBytes / (1024.0 * 1024.0);

                Debug.Log($"[Editor] Android-смоук-сборка успешна: {BuildPath} " +
                          $"({sizeMb:F1} МБ, {elapsed.TotalSeconds:F1} с).");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[Editor] Android-смоук-сборка провалена: result={summary.result}, " +
                           $"errors={summary.totalErrors}, warnings={summary.totalWarnings}, " +
                           $"время={elapsed.TotalSeconds:F1} с.");

            var firstErrors = report.steps
                .SelectMany(step => step.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                .Take(10)
                .ToList();

            if (firstErrors.Count > 0)
            {
                sb.AppendLine("Первые ошибки:");
                foreach (var msg in firstErrors)
                    sb.AppendLine(" - " + msg.content);
            }

            Debug.LogError(sb.ToString());
        }
    }
}
