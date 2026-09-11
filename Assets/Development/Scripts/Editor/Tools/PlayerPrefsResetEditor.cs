using System;
using BlackWhiteLike.Models;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Tools
{
    /// <summary>
    /// Сброс сохранённого профиля кампании для тестов (game-design §8: PlayerPrefs через
    /// IProfileStorage). Два режима: точечный сброс ТОЛЬКО ключей профиля (ProfileKeys —
    /// единственный источник имён, ключи Unity/Editor не трогаются) и полный DeleteAll на
    /// крайний случай. Без модальных диалогов (правило editor-files: пункт меню может быть
    /// вызван автоматизацией) — действие необратимое, но это и есть его смысл; каждый удалённый
    /// ключ логируется.
    /// </summary>
    /// <remarks>
    /// В Play Mode уже созданный <c>PlayerProfile</c> держит значения в памяти и при следующей
    /// записи вернёт их на диск — сбрасывать имеет смысл ВНЕ Play Mode (или перезапустить его
    /// после сброса). Пункты меню поэтому отключены в Play Mode через валидатор.
    /// </remarks>
    public static class PlayerPrefsResetEditor
    {
        private const string ResetProfileMenu = "BlackWhiteLike/Tools/Reset Campaign Profile";
        private const string DeleteAllMenu = "BlackWhiteLike/Tools/Delete All PlayerPrefs";

        [MenuItem(ResetProfileMenu)]
        public static void ResetCampaignProfile()
        {
            var deleted = 0;

            deleted += DeleteIfExists(ProfileKeys.CompletedLevels);
            deleted += DeleteIfExists(ProfileKeys.Coins);
            deleted += DeleteIfExists(ProfileKeys.AlignmentScore);
            deleted += DeleteIfExists(ProfileKeys.LastSeenUtc);
            deleted += DeleteIfExists(ProfileKeys.TutorialCompleted);

            // Ключи апгрейдов — по всем значениям UpgradeCategory (тот же способ сборки ключа,
            // что PlayerProfile.GetUpgradeLevel: префикс + имя enum-значения).
            foreach (UpgradeCategory category in Enum.GetValues(typeof(UpgradeCategory)))
            {
                deleted += DeleteIfExists(ProfileKeys.UpgradeLevelPrefix + category);
            }

            PlayerPrefs.Save();
            Debug.Log($"[Editor] Reset Campaign Profile: удалено ключей — {deleted} (отсутствовавшие пропущены).");
        }

        /// <summary>
        /// Полная очистка PlayerPrefs — сносит и НЕ-профильные ключи (настройки Unity/пакетов).
        /// Использовать, только когда точечного сброса недостаточно.
        /// </summary>
        [MenuItem(DeleteAllMenu)]
        public static void DeleteAllPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Editor] Delete All PlayerPrefs: очищены ВСЕ ключи PlayerPrefs (включая не-профильные).");
        }

        // В Play Mode сброс бесполезен: живой PlayerProfile перезапишет диск из памяти (см. remarks).
        [MenuItem(ResetProfileMenu, isValidateFunction: true)]
        [MenuItem(DeleteAllMenu, isValidateFunction: true)]
        private static bool ValidateNotPlaying() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static int DeleteIfExists(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return 0;

            PlayerPrefs.DeleteKey(key);
            Debug.Log($"[Editor] Удалён ключ профиля: \"{key}\".");
            return 1;
        }
    }
}
