using System;
using System.IO;
using BlackWhiteLike.Data.Configs.Camera;
using BlackWhiteLike.Data.Configs.Hand;
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Configs
{
    /// <summary>
    /// Создаёт пустые .asset-инстансы ключевых ScriptableObject-конфигов в правильной
    /// структуре папок (game-design.md §17, шаг 2 "Камера + рука").
    /// Дефолтные значения полей задаёт сам класс SO (code-writer) — здесь ничего не прошивается.
    /// Идемпотентен: существующий ассет НЕ перезаписывается (настройки, подкрученные
    /// пользователем в Inspector, дороже дефолтов) — только логируется пропуск.
    ///
    /// Расширение списком: чтобы добавить новый SO в §17-набор, достаточно дописать одну
    /// строку в <see cref="Descriptors"/> — метод <see cref="CreateAll"/> трогать не нужно.
    /// </summary>
    public static class ConfigAssetCreatorEditor
    {
        private const string ConfigsRoot = "Assets/Development/Data/Configs";

        /// <summary>
        /// Единственное место, где перечислены создаваемые SO. Новый конфиг = новая строка.
        /// </summary>
        private static readonly ConfigAssetDescriptor[] Descriptors =
        {
            new ConfigAssetDescriptor(typeof(HandConfig), ConfigsRoot + "/Hand/HandConfig.asset"),
            new ConfigAssetDescriptor(typeof(IsometricCameraConfig), ConfigsRoot + "/Camera/IsometricCameraConfig.asset"),
        };

        [MenuItem("BlackWhiteLike/Configs/Create Empty SO Set")]
        public static void CreateAll()
        {
            int created = 0;
            int skipped = 0;

            foreach (var descriptor in Descriptors)
            {
                if (CreateIfMissing(descriptor))
                    created++;
                else
                    skipped++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Editor] Create Empty SO Set завершён: создано {created}, пропущено (уже существует) {skipped}.");
        }

        /// <summary>
        /// Создаёт один .asset, если его ещё нет. Возвращает true, если ассет был создан.
        /// </summary>
        private static bool CreateIfMissing(ConfigAssetDescriptor descriptor)
        {
            if (File.Exists(descriptor.AssetPath))
            {
                Debug.Log($"[Editor] {descriptor.AssetPath} уже существует, пропущен.");
                return false;
            }

            EnsureFolder(Path.GetDirectoryName(descriptor.AssetPath).Replace('\\', '/'));

            var instance = ScriptableObject.CreateInstance(descriptor.SoType);
            AssetDatabase.CreateAsset(instance, descriptor.AssetPath);
            // Undo на ассетах не регистрируем — см. .claude/rules/editor-files.md.

            Debug.Log($"[Editor] Создан конфиг: {descriptor.AssetPath}");
            return true;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// Описание одного создаваемого SO-ассета: тип класса + целевой путь.
        /// </summary>
        private readonly struct ConfigAssetDescriptor
        {
            public readonly Type SoType;
            public readonly string AssetPath;

            public ConfigAssetDescriptor(Type soType, string assetPath)
            {
                SoType = soType;
                AssetPath = assetPath;
            }
        }
    }
}
