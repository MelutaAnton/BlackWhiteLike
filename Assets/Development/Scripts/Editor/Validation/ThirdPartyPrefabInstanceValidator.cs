using System.Collections.Generic;
using BlackWhiteLike.Editor.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlackWhiteLike.Editor.Validation
{
    /// <summary>
    /// Проверяет правило CLAUDE.md "Из ThirdParty в сцену и в наши префабы попадает только визуал,
    /// никогда — префаб-инстанс": (а) все корневые объекты всех сейчас ОТКРЫТЫХ сцен, (б) все
    /// .prefab-ассеты под Assets/Development/Prefabs (читаются через LoadPrefabContents/
    /// UnloadPrefabContents, без правок). Только чтение, без модальных диалогов и без изменения
    /// сцен/ассетов — безопасно вызывать автоматизацией.
    /// </summary>
    public static class ThirdPartyPrefabInstanceValidator
    {
        private const string PrefabsSearchFolder = "Assets/Development/Prefabs";

        [MenuItem("BlackWhiteLike/Validation/Find ThirdParty Prefab Instances")]
        public static void FindThirdPartyPrefabInstances()
        {
            int sceneObjectsChecked = 0;
            int sceneViolations = 0;
            var sceneReport = new List<string>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                {
                    sceneObjectsChecked++;
                    var report = new List<string>();
                    int count = EditorPrefabUtility.CountThirdPartyPrefabLinks(root, report);
                    if (count == 0) continue;

                    sceneViolations += count;
                    sceneReport.Add($"Сцена \"{scene.name}\" / корневой объект \"{root.name}\":\n  " + string.Join("\n  ", report));
                }
            }

            int prefabsChecked = 0;
            int prefabViolations = 0;
            var prefabReport = new List<string>();

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsSearchFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    prefabsChecked++;
                    var report = new List<string>();
                    int count = EditorPrefabUtility.CountThirdPartyPrefabLinks(contents, report);
                    if (count == 0) continue;

                    prefabViolations += count;
                    prefabReport.Add($"Префаб \"{path}\":\n  " + string.Join("\n  ", report));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            if (sceneViolations == 0 && prefabViolations == 0)
            {
                Debug.Log($"[Editor] Find ThirdParty Prefab Instances: нарушений нет — {sceneObjectsChecked} " +
                          $"корневых объектов открытых сцен, {prefabsChecked} префабов проверено.");
                return;
            }

            foreach (var line in sceneReport) Debug.LogError("[Editor] ThirdParty prefab-инстанс в сцене:\n" + line);
            foreach (var line in prefabReport) Debug.LogError("[Editor] ThirdParty prefab-инстанс в префабе:\n" + line);

            Debug.LogError($"[Editor] Find ThirdParty Prefab Instances: найдено {sceneViolations + prefabViolations} " +
                            $"нарушений (сцены: {sceneViolations}, префабы: {prefabViolations}) из {sceneObjectsChecked} " +
                            $"корневых объектов открытых сцен и {prefabsChecked} проверенных префабов.");
        }
    }
}
