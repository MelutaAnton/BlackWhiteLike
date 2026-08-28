using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Common
{
    /// <summary>
    /// Единственный источник правила CLAUDE.md "Из ThirdParty в сцену и в наши префабы попадает
    /// только визуал, никогда — префаб-инстанс" (см. .claude/rules/editor-files.md, "ThirdParty-
    /// префаб — только через unpack completely"). Любой Editor-скрипт, который берёт GameObject из
    /// <c>Assets/ThirdParty/</c> и кладёт его в НАШУ сцену/префаб, обязан провести его через
    /// <see cref="InstantiateUnpacked"/> вместо голого <c>PrefabUtility.InstantiatePrefab</c> —
    /// иначе в нашем ассете навсегда остаётся живая prefab-связь на чужой пак (обновление пака
    /// молча меняет структуру нашего контента, лицензионная граница размывается).
    /// </summary>
    public static class EditorPrefabUtility
    {
        private const string ThirdPartyPrefix = "Assets/ThirdParty/";

        /// <summary>true, если путь ассета лежит под Assets/ThirdParty/ (единственная проверка "чужое/наше").</summary>
        public static bool IsThirdPartyPath(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) && assetPath.StartsWith(ThirdPartyPrefix);

        /// <summary>
        /// Инстанцирует префаб под <paramref name="parent"/> (может быть null — тогда в корень
        /// активной сцены). Если исходный ассет лежит в Assets/ThirdParty/ — инстанс сразу
        /// полностью распаковывается (<see cref="PrefabUnpackMode.Completely"/>): результат —
        /// обычная иерархия GameObject'ов без единой prefab-связи на чужой пак, только визуал
        /// (меши/материалы/кости приходят как значения компонентов, не как ссылка на источник).
        /// Наши собственные префабы (путь вне ThirdParty/) НЕ распаковываются — connected-инстанс
        /// нашего префаба в сцене/другом префабе — это нормальный, ожидаемый паттерн проекта.
        /// </summary>
        public static GameObject InstantiateUnpacked(GameObject prefabAsset, Transform parent = null)
        {
            if (prefabAsset == null) return null;

            var instance = parent != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, parent)
                : (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);

            var assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (IsThirdPartyPath(assetPath))
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Debug.Log($"[Editor] \"{instance.name}\": инстанс ThirdParty-префаба ({assetPath}) распакован " +
                          "(Unpack Completely) — берём только визуал, без живой prefab-связи на чужой пак.");
            }

            return instance;
        }

        /// <summary>
        /// Обходит всю иерархию под <paramref name="root"/> (включая сам root) и находит объекты,
        /// у которых <see cref="PrefabUtility.GetCorrespondingObjectFromSource"/> указывает на ассет
        /// под Assets/ThirdParty/ — то есть живую prefab-связь на чужой пак. Коннекшн на НАШ префаб
        /// (source-путь под Assets/Development/) нарушением не считается — это штатный connected-
        /// инстанс (например, Villager.prefab-инстанс в сцене). Внутри <c>LoadPrefabContents</c> сам
        /// открытый root не связан ни с чем (GetCorrespondingObjectFromSource возвращает null) —
        /// проверяется естественно, без отдельной ветки.
        /// </summary>
        public static int CountThirdPartyPrefabLinks(GameObject root, List<string> report)
        {
            if (root == null) return 0;

            int count = 0;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                var go = t.gameObject;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (source == null) continue;

                var sourcePath = AssetDatabase.GetAssetPath(source);
                if (!IsThirdPartyPath(sourcePath)) continue;

                count++;
                report?.Add(GetHierarchyPath(root.transform, t) + " <- " + sourcePath);
            }

            return count;
        }

        /// <summary>
        /// Обёртка над <see cref="CountThirdPartyPrefabLinks"/> с логированием — вызывается последним
        /// шагом в конце каждого Editor-скрипта, который собирает префаб/сцену из ThirdParty-визуала
        /// (контракт задачи). Возвращает false и пишет LogError с перечнем нарушений, если хоть один
        /// ThirdParty prefab-инстанс остался в собранной иерархии.
        /// </summary>
        public static bool AssertNoThirdPartyPrefabLinks(GameObject root, string ownerLabel)
        {
            var report = new List<string>();
            int count = CountThirdPartyPrefabLinks(root, report);

            if (count == 0)
            {
                Debug.Log($"[Editor] {ownerLabel}: ThirdParty-инстансов нет.");
                return true;
            }

            var sb = new StringBuilder();
            sb.Append($"[Editor] {ownerLabel}: найдено {count} ThirdParty prefab-инстанс(ов) — правило " +
                      "\"из ThirdParty попадает только визуал\" нарушено:");
            foreach (var line in report)
                sb.Append("\n  ").Append(line);

            Debug.LogError(sb.ToString());
            return false;
        }

        private static string GetHierarchyPath(Transform root, Transform target)
        {
            if (target == root) return root.name;

            var stack = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                stack.Add(current.name);
                current = current.parent;
            }

            stack.Add(root.name);
            stack.Reverse();
            return string.Join("/", stack);
        }
    }
}
