using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Common
{
    /// <summary>
    /// Гарантирует, что ThirdParty-текстура импортирована как Sprite (иконки UI зачастую лежат
    /// в паках с Texture Type = Default), и отдаёт готовый <see cref="Sprite"/> без редактирования
    /// самого файла (только .meta/importer-настройки — см. CLAUDE.md, "не редактировать ThirdParty").
    /// Используется шагом 4 (иконки чудес — Editor/Configs/ConfigAssetCreatorEditor.cs,
    /// Editor/UI/LevelHudSetupEditor.cs).
    /// </summary>
    public static class EditorSpriteUtility
    {
        // Общая папка плоских синих иконок пака — единственный источник для MiracleIconPaths и
        // HudIconPaths (шаг 5: раньше строка дублировалась бы при добавлении второго набора путей).
        private const string BlueIconsFolder = "Assets/ThirdParty/GUI/ClassicFantasyRPG_UI/ARTWORKS/UIicons/Flat/Blue/";

        /// <summary>
        /// Единственный источник путей к иконкам чудес (правка после ревью: строки дублировались в
        /// Editor/Configs/ConfigAssetCreatorEditor.cs и Editor/UI/LevelHudSetupEditor.cs — нарушение
        /// CLAUDE.md "идентификатор объявляется один раз"). Оба скрипта ссылаются на эти константы.
        /// </summary>
        public static class MiracleIconPaths
        {
            public const string Rain = BlueIconsFolder + "129_A_drop.png";
            public const string Heal = "Assets/ThirdParty/GUI/ClassicFantasyRPG_UI/ARTWORKS/Skill_icons/Heal_potion_nobg.png";
            public const string Fireball = BlueIconsFolder + "240_fire.png";
            public const string Lightning = BlueIconsFolder + "90_lightning.png";
        }

        /// <summary>
        /// Шаг 5 (Worship + win) — иконки служебных кнопок HUD (не связанных с конкретным чудом).
        /// Найден на диске: Assets/ThirdParty/GUI/ClassicFantasyRPG_UI/ARTWORKS/UIicons/Flat/Blue/15_pause.png.
        /// </summary>
        public static class HudIconPaths
        {
            public const string Pause = BlueIconsFolder + "15_pause.png";
            // Шаг 8 (Meta + idle) — иконка монеты для Temple/WelcomeBack (найдено на диске: .../Flat/Blue/101_Coin.png).
            // Playtest шага 10: та же иконка переиспользуется в LevelCompleteOverlay (CoinsLabel) —
            // единственный источник, второй константы не заводим.
            public const string Coin = BlueIconsFolder + "101_Coin.png";
            // Tutorial — указатель на активную цель (TutorialView._pointer). Стрелка "вниз" — естественно
            // читается как "смотри сюда" над целевым элементом; позицию/поворот двигает рантайм.
            public const string TutorialPointer = BlueIconsFolder + "03_Arrow_bot.png";
            // Playtest шага 10: LevelCompleteOverlay — четыре числа без подписей были нечитаемы владельцу.
            // Иконки подписывают Worship-итог/Awe/Fear/Coins (Coins переиспользует HudIconPaths.Coin).
            public const string Worship = BlueIconsFolder + "252_crown.png"; // корона — общий итог поклонения
            public const string Awe = BlueIconsFolder + "85_sun.png";        // солнце — светлое почитание
            public const string Fear = BlueIconsFolder + "82_demon.png";     // демон — тёмное почитание/страх
        }

        // Шаг 7 (Кампания из 5 уровней) — иконки экрана выбора уровня. Colored-иконка (не Blue) для
        // превью карточки уровня — в паке нет отдельных превью на уровень, поэтому одна и та же
        // иконка "домика" используется для всех пяти карточек (допустимо контрактом задачи).
        public static class LevelIconPaths
        {
            private const string ColoredIconsFolder = "Assets/ThirdParty/GUI/ClassicFantasyRPG_UI/ARTWORKS/UIicons/Colored/";
            public const string Preview = ColoredIconsFolder + "97_house.png";
            public const string Locked = BlueIconsFolder + "26_lock.png";
            public const string Completed = BlueIconsFolder + "65_star.png";
        }

        public static Sprite EnsureSpriteAtPath(string texturePath)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Editor] Текстура не найдена или не является TextureImporter: " + texturePath);
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Debug.Log("[Editor] Texture Type переведён в Sprite: " + texturePath);
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (sprite == null)
                Debug.LogWarning("[Editor] Не удалось загрузить Sprite после реимпорта: " + texturePath);

            return sprite;
        }
    }
}
