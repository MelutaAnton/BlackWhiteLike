using System.Collections.Generic;
using System.IO;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Models;
using BlackWhiteLike.Views.UI.Overlay;
using BlackWhiteLike.Views.UI.Temple;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BlackWhiteLike.Editor.UI
{
    /// <summary>
    /// Шаг 8 — Meta + idle (game-design.md, шаг 8). Собирает ОДИН префаб
    /// <c>Assets/Development/Prefabs/UI/TempleCanvas.prefab</c> (CLAUDE.md §7: канвас, повторяющийся
    /// в сценах, существует одним префабом): <c>TemplePanel</c> (заголовок, монеты, кнопка Collect,
    /// колонка из 3 <see cref="UpgradeRowView"/> — HandPower/FaithCapacity/MiraclePower,
    /// <see cref="CreatureTeaserView"/>-заглушка, кнопка Next level) и <c>WelcomeBackOverlay</c>
    /// (<see cref="WelcomeBackView"/>) — СИБЛИНГ TemplePanel под тем же канвасом, выключен по
    /// умолчанию (CLAUDE.md §7: оверлей — не потомок гасимого узла).
    /// Идемпотентен: <see cref="PrefabUtility.LoadPrefabContents"/>/<see cref="PrefabUtility.SaveAsPrefabAsset"/>
    /// (образец — LevelHudSetupEditor/MainMenuCanvasSetupEditor) — существующий префаб дособирается
    /// по имени child'ов, без дублей.
    /// </summary>
    public static class TempleCanvasSetupEditor
    {
        private const string PrefabsFolder = "Assets/Development/Prefabs/UI";
        public const string PrefabPath = PrefabsFolder + "/TempleCanvas.prefab";
        private const string FontAssetPath = "Assets/Development/Art/Fonts/Nunito-Regular SDF.asset";

        // Порядок категорий — колонка из 3 строк прокачки (ТЗ владельца, шаг 8). Единственный
        // источник заголовков строк для этого файла.
        private static readonly (UpgradeCategory Category, string Title)[] UpgradeRows =
        {
            (UpgradeCategory.HandPower, "Hand Power"),
            (UpgradeCategory.FaithCapacity, "Faith Capacity"),
            (UpgradeCategory.MiraclePower, "Miracle Power"),
        };

        [MenuItem("BlackWhiteLike/UI/Build Temple Canvas")]
        public static void BuildTempleCanvasMenuItem()
        {
            BuildOrUpdateCanvasPrefab();
        }

        /// <summary>Публичный — переиспользуется <c>TempleSceneSetupEditor</c>. Возвращает null, если TMP Font Asset ещё не создан.</summary>
        public static GameObject BuildOrUpdateCanvasPrefab()
        {
            if (!LevelHudSetupEditor.EnsureTmpEssentials())
                return null; // импорт асинхронный — лог уже написан, повторить прогон после его завершения.

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                Debug.LogError("[Editor] TMP Font Asset не найден по пути " + FontAssetPath +
                                " — сначала прогоните \"BlackWhiteLike/UI/Build Level HUD\" (единственный источник шрифта проекта).");
                return null;
            }

            EnsureFolder(PrefabsFolder);

            bool existed = File.Exists(PrefabPath);
            GameObject root = existed
                ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject("TempleCanvas", typeof(RectTransform));

            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (root.GetComponent<GraphicRaycaster>() == null)
                root.AddComponent<GraphicRaycaster>();

            int totalWired = 0;
            int totalExpected = 0;

            BuildTemplePanel(root.transform, fontAsset, ref totalWired, ref totalExpected);
            BuildWelcomeBackOverlay(root.transform, fontAsset, ref totalWired, ref totalExpected);

            Debug.Log($"[Editor] TempleCanvas: прошито {totalWired} из {totalExpected} полей суммарно.");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (existed)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        // ------------------------------------------------------------------------------------
        // TemplePanel
        // ------------------------------------------------------------------------------------

        private static void BuildTemplePanel(Transform canvasRoot, TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var panelGo = FindOrCreateUIChild(canvasRoot, "TemplePanel");
            var panelRt = (RectTransform)panelGo.transform;
            SetAnchor(panelRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var bg = panelGo.GetComponent<Image>();
            if (bg == null) bg = panelGo.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.maskable = false;
            bg.color = new Color(0.08f, 0.08f, 0.12f, 1f);

            var titleGo = FindOrCreateUIChild(panelGo.transform, "Title");
            var titleRt = (RectTransform)titleGo.transform;
            SetAnchor(titleRt, new Vector2(0.5f, 0.94f), new Vector2(0.5f, 0.94f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(500f, 70f));
            var title = titleGo.GetComponent<TextMeshProUGUI>();
            if (title == null) title = titleGo.AddComponent<TextMeshProUGUI>();
            title.raycastTarget = false;
            title.maskable = false;
            title.font = fontAsset;
            title.fontSize = 48f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(title.text)) title.text = "Temple";

            var coinsText = BuildCoinsRow(panelGo.transform, fontAsset);
            var collectButton = BuildCollectIdleButton(panelGo.transform, fontAsset);
            var idleRewardText = BuildIdleRewardText(panelGo.transform, fontAsset);

            var rows = new List<UpgradeRowView>(UpgradeRows.Length);
            var columnGo = FindOrCreateUIChild(panelGo.transform, "UpgradesColumn");
            var columnRt = (RectTransform)columnGo.transform;
            SetAnchor(columnRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f), new Vector2(720f, 3 * 96f + 2 * 16f));

            var columnLayout = columnGo.GetComponent<VerticalLayoutGroup>();
            if (columnLayout == null) columnLayout = columnGo.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 16f;
            columnLayout.childAlignment = TextAnchor.UpperCenter;
            columnLayout.childControlWidth = false;
            columnLayout.childControlHeight = false;
            columnLayout.childForceExpandWidth = false;
            columnLayout.childForceExpandHeight = false;

            foreach (var (category, rowTitle) in UpgradeRows)
                rows.Add(BuildUpgradeRow(columnGo.transform, category, rowTitle, fontAsset, ref totalWired, ref totalExpected));

            var creatureTeaser = BuildCreatureTeaser(panelGo.transform, fontAsset, ref totalWired, ref totalExpected);
            var nextLevelButton = BuildTempleButton(panelGo.transform, "NextLevelButton", fontAsset, "Next level",
                new Vector2(0.98f, 0.04f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(260f, 72f));

            var view = panelGo.GetComponent<TempleView>();
            if (view == null) view = panelGo.AddComponent<TempleView>();

            const int expected = 7; // _root, _coinsText, _collectIdleButton, _idleRewardText, _creatureTeaser, _nextLevelButton + массив _upgradeRows (wired++ ниже)
            totalExpected += expected;
            var so = new SerializedObject(view);
            int wired = 0;
            wired += SetRef(so, "_root", panelGo, "TempleView") ? 1 : 0;
            wired += SetRef(so, "_coinsText", coinsText, "TempleView") ? 1 : 0;
            wired += SetRef(so, "_collectIdleButton", collectButton, "TempleView") ? 1 : 0;
            wired += SetRef(so, "_idleRewardText", idleRewardText, "TempleView") ? 1 : 0;
            wired += SetRef(so, "_creatureTeaser", creatureTeaser, "TempleView") ? 1 : 0;
            wired += SetRef(so, "_nextLevelButton", nextLevelButton, "TempleView") ? 1 : 0;

            var rowsProp = so.FindProperty("_upgradeRows");
            if (rowsProp == null)
            {
                Debug.LogError("[Editor] Поле \"_upgradeRows\" не найдено на TempleView — строки прокачки не прошиты.");
            }
            else
            {
                rowsProp.arraySize = rows.Count;
                for (int i = 0; i < rows.Count; i++)
                    rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
                wired++;
                Debug.Log($"[Editor] TempleView._upgradeRows: прошито {rows.Count} строк.");
            }

            so.ApplyModifiedProperties();
            totalWired += wired;
            LogWireResult("TempleView", wired, expected);
        }

        /// <summary>
        /// Текст суммы офлайн-дохода слева от CollectIdleButton (фикс ревью шага 8:
        /// TempleView.ShowIdleReward раньше игнорировал сумму). Пустой по умолчанию —
        /// заполняет presenter вместе с показом кнопки.
        /// </summary>
        private static TextMeshProUGUI BuildIdleRewardText(Transform panelTransform, TMP_FontAsset fontAsset)
        {
            var textGo = FindOrCreateUIChild(panelTransform, "IdleRewardText");
            var textRt = (RectTransform)textGo.transform;
            SetAnchor(textRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(-286f, -92f), new Vector2(260f, 48f));

            var text = textGo.GetComponent<TextMeshProUGUI>();
            if (text == null) text = textGo.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.maskable = false;
            text.font = fontAsset;
            text.fontSize = 30f;
            text.alignment = TextAlignmentOptions.Right;
            // Текст не трогаем при повторном прогоне; на свежесозданном — пусто (заполняет presenter).

            return text;
        }

        private static TextMeshProUGUI BuildCoinsRow(Transform panelTransform, TMP_FontAsset fontAsset)
        {
            var rowGo = FindOrCreateUIChild(panelTransform, "CoinsRow");
            var rowRt = (RectTransform)rowGo.transform;
            SetAnchor(rowRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -24f), new Vector2(220f, 56f));

            var iconGo = FindOrCreateUIChild(rowGo.transform, "Icon");
            var iconRt = (RectTransform)iconGo.transform;
            SetAnchor(iconRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(48f, 48f));
            var iconImage = iconGo.GetComponent<Image>();
            if (iconImage == null) iconImage = iconGo.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.maskable = false;
            iconImage.preserveAspect = true;
            var coinSprite = EditorSpriteUtility.EnsureSpriteAtPath(EditorSpriteUtility.HudIconPaths.Coin);
            if (coinSprite != null) iconImage.sprite = coinSprite;

            var textGo = FindOrCreateUIChild(rowGo.transform, "CoinsText");
            var textRt = (RectTransform)textGo.transform;
            SetAnchor(textRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(56f, 0f), new Vector2(-56f, 48f));
            var coinsText = textGo.GetComponent<TextMeshProUGUI>();
            if (coinsText == null) coinsText = textGo.AddComponent<TextMeshProUGUI>();
            coinsText.raycastTarget = false;
            coinsText.maskable = false;
            coinsText.font = fontAsset;
            coinsText.fontSize = 32f;
            coinsText.alignment = TextAlignmentOptions.MidlineLeft;
            if (string.IsNullOrEmpty(coinsText.text)) coinsText.text = "0";

            return coinsText;
        }

        /// <summary>
        /// «Собрать идле-доход» — GameObject выключен по умолчанию (контракт задачи: "включает
        /// presenter"), не Button.interactable — presenter решает, есть ли что собирать, тем же
        /// приёмом, что LockedOverlay/CompletedMark на LevelCardView (состояние по прогрессу решает
        /// презентер, не editor-скрипт).
        /// </summary>
        private static Button BuildCollectIdleButton(Transform panelTransform, TMP_FontAsset fontAsset)
        {
            var button = BuildTempleButton(panelTransform, "CollectIdleButton", fontAsset, "Collect",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -92f), new Vector2(220f, 64f));

            if (button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(false);
                Debug.Log("[Editor] CollectIdleButton: активна по умолчанию -> выключена (включается презентером, когда есть что собрать).");
            }

            return button;
        }

        private static UpgradeRowView BuildUpgradeRow(Transform columnTransform, UpgradeCategory category, string rowTitle,
            TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var rowGo = FindOrCreateUIChild(columnTransform, "UpgradeRow_" + category);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.sizeDelta = new Vector2(720f, 96f);

            var bg = rowGo.GetComponent<Image>();
            if (bg == null) bg = rowGo.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.maskable = false;
            bg.color = new Color(0.14f, 0.14f, 0.18f, 1f);

            var titleGo = FindOrCreateUIChild(rowGo.transform, "TitleText");
            var titleRt = (RectTransform)titleGo.transform;
            SetAnchor(titleRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 0f), new Vector2(220f, 40f));
            var titleText = titleGo.GetComponent<TextMeshProUGUI>();
            if (titleText == null) titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.raycastTarget = false;
            titleText.maskable = false;
            titleText.font = fontAsset;
            titleText.fontSize = 26f;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.text = rowTitle; // статичная подпись категории — перезаписываем всегда.

            var levelGo = FindOrCreateUIChild(rowGo.transform, "LevelText");
            var levelRt = (RectTransform)levelGo.transform;
            SetAnchor(levelRt, new Vector2(0.4f, 0.5f), new Vector2(0.4f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(100f, 40f));
            var levelText = levelGo.GetComponent<TextMeshProUGUI>();
            if (levelText == null) levelText = levelGo.AddComponent<TextMeshProUGUI>();
            levelText.raycastTarget = false;
            levelText.maskable = false;
            levelText.font = fontAsset;
            levelText.fontSize = 24f;
            levelText.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(levelText.text)) levelText.text = "Lv. 0";

            var effectGo = FindOrCreateUIChild(rowGo.transform, "EffectText");
            var effectRt = (RectTransform)effectGo.transform;
            SetAnchor(effectRt, new Vector2(0.6f, 0.5f), new Vector2(0.6f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(140f, 40f));
            var effectText = effectGo.GetComponent<TextMeshProUGUI>();
            if (effectText == null) effectText = effectGo.AddComponent<TextMeshProUGUI>();
            effectText.raycastTarget = false;
            effectText.maskable = false;
            effectText.font = fontAsset;
            effectText.fontSize = 22f;
            effectText.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(effectText.text)) effectText.text = "+0";

            var priceGo = FindOrCreateUIChild(rowGo.transform, "PriceText");
            var priceRt = (RectTransform)priceGo.transform;
            SetAnchor(priceRt, new Vector2(0.8f, 0.5f), new Vector2(0.8f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(100f, 40f));
            var priceText = priceGo.GetComponent<TextMeshProUGUI>();
            if (priceText == null) priceText = priceGo.AddComponent<TextMeshProUGUI>();
            priceText.raycastTarget = false;
            priceText.maskable = false;
            priceText.font = fontAsset;
            priceText.fontSize = 22f;
            priceText.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(priceText.text)) priceText.text = "0";

            var buyButton = BuildTempleButton(rowGo.transform, "BuyButton", fontAsset, "Buy",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(140f, 64f));

            var rowView = rowGo.GetComponent<UpgradeRowView>();
            if (rowView == null) rowView = rowGo.AddComponent<UpgradeRowView>();

            const int expected = 5;
            totalExpected += expected;
            var ownerLabel = "UpgradeRowView(" + category + ")";
            var so = new SerializedObject(rowView);
            int wired = 0;
            wired += SetRef(so, "_titleText", titleText, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_levelText", levelText, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_effectText", effectText, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_priceText", priceText, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_buyButton", buyButton, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();
            totalWired += wired;
            LogWireResult(ownerLabel, wired, expected);

            return rowView;
        }

        /// <summary>
        /// Заглушка будущего существа — полупрозрачная карточка без кнопок; raycastTarget=false
        /// ВЕЗДЕ (контракт задачи) — она чисто декоративная, не должна перехватывать тапы по панели.
        /// </summary>
        private static CreatureTeaserView BuildCreatureTeaser(Transform panelTransform, TMP_FontAsset fontAsset,
            ref int totalWired, ref int totalExpected)
        {
            var cardGo = FindOrCreateUIChild(panelTransform, "CreatureTeaser");
            var cardRt = (RectTransform)cardGo.transform;
            SetAnchor(cardRt, new Vector2(0f, 0.04f), new Vector2(0f, 0.04f), new Vector2(0f, 0f),
                new Vector2(24f, 24f), new Vector2(320f, 140f));

            var bg = cardGo.GetComponent<Image>();
            if (bg == null) bg = cardGo.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.maskable = false;
            bg.color = new Color(0.2f, 0.2f, 0.24f, 0.5f); // полупрозрачная — "заглушка"

            var labelGo = FindOrCreateUIChild(cardGo.transform, "Label");
            var labelRt = (RectTransform)labelGo.transform;
            SetAnchor(labelRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            if (label == null) label = labelGo.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.maskable = false;
            label.font = fontAsset;
            label.fontSize = 24f;
            label.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(label.text)) label.text = "Creature — Coming soon";

            var view = cardGo.GetComponent<CreatureTeaserView>();
            if (view == null) view = cardGo.AddComponent<CreatureTeaserView>();

            const int expected = 1;
            totalExpected += expected;
            var so = new SerializedObject(view);
            int wired = SetRef(so, "_root", cardGo, "CreatureTeaserView") ? 1 : 0;
            so.ApplyModifiedProperties();
            totalWired += wired;
            LogWireResult("CreatureTeaserView", wired, expected);

            return view;
        }

        // ------------------------------------------------------------------------------------
        // WelcomeBackOverlay — сиблинг TemplePanel (CLAUDE.md §7: оверлей не потомок гасимого узла)
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Публичный — переиспользуется <c>MainMenuCanvasSetupEditor</c> НЕ напрямую (структура
        /// дублируется по тому же приёму, что и остальные общие блоки Editor.UI — избегаем
        /// циклической зависимости между файлами канвасов). Затемняющий фон-блокер
        /// (raycastTarget=true), панель с суммой, Collect, Close — выключен по умолчанию.
        /// </summary>
        private static void BuildWelcomeBackOverlay(Transform canvasRoot, TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var containerGo = FindOrCreateUIChild(canvasRoot, "WelcomeBackOverlay");
            var containerRt = (RectTransform)containerGo.transform;
            SetAnchor(containerRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var dimGo = FindOrCreateUIChild(containerGo.transform, "Dim");
            var dimRt = (RectTransform)dimGo.transform;
            SetAnchor(dimRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var dimImage = dimGo.GetComponent<Image>();
            if (dimImage == null) dimImage = dimGo.AddComponent<Image>();
            dimImage.raycastTarget = true; // блокер тапов, пока оверлей открыт
            dimImage.maskable = false;
            dimImage.color = new Color(0f, 0f, 0f, 0.6f);

            var panelGo = FindOrCreateUIChild(containerGo.transform, "Panel");
            var panelRt = (RectTransform)panelGo.transform;
            SetAnchor(panelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(520f, 320f));
            var panelImage = panelGo.GetComponent<Image>();
            if (panelImage == null) panelImage = panelGo.AddComponent<Image>();
            panelImage.raycastTarget = true;
            panelImage.maskable = false;
            panelImage.color = new Color(0.1f, 0.1f, 0.13f, 0.95f);

            var titleGo = FindOrCreateUIChild(panelGo.transform, "Title");
            var titleRt = (RectTransform)titleGo.transform;
            SetAnchor(titleRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -32f), new Vector2(440f, 56f));
            var title = titleGo.GetComponent<TextMeshProUGUI>();
            if (title == null) title = titleGo.AddComponent<TextMeshProUGUI>();
            title.raycastTarget = false;
            title.maskable = false;
            title.font = fontAsset;
            title.fontSize = 40f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(title.text)) title.text = "Welcome back!";

            var coinsGo = FindOrCreateUIChild(panelGo.transform, "CoinsText");
            var coinsRt = (RectTransform)coinsGo.transform;
            SetAnchor(coinsRt, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(440f, 56f));
            var coinsText = coinsGo.GetComponent<TextMeshProUGUI>();
            if (coinsText == null) coinsText = coinsGo.AddComponent<TextMeshProUGUI>();
            coinsText.raycastTarget = false;
            coinsText.maskable = false;
            coinsText.font = fontAsset;
            coinsText.fontSize = 32f;
            coinsText.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(coinsText.text)) coinsText.text = "+0 coins";

            var collectButton = BuildOverlayButton(panelGo.transform, "CollectButton", fontAsset, "Collect", 24f, new Vector2(280f, 72f));

            var closeButton = BuildOverlayButton(panelGo.transform, "CloseButton", fontAsset, "Close", -24f, new Vector2(180f, 56f));
            // Close — компактнее и ниже Collect (Collect якорится от низа панели, Close — тоже, но
            // c дополнительным отступом ниже, чтобы кнопки не перекрывались).
            var closeRt = (RectTransform)closeButton.transform;
            closeRt.anchoredPosition = new Vector2(0f, -96f);

            if (containerGo.activeSelf)
            {
                containerGo.SetActive(false);
                Debug.Log("[Editor] WelcomeBackOverlay: активен по умолчанию -> выключен.");
            }

            if (containerGo.transform.GetSiblingIndex() != canvasRoot.childCount - 1)
                containerGo.transform.SetAsLastSibling();

            var view = containerGo.GetComponent<WelcomeBackView>();
            if (view == null) view = containerGo.AddComponent<WelcomeBackView>();

            const int expected = 4;
            totalExpected += expected;
            var so = new SerializedObject(view);
            int wired = 0;
            wired += SetRef(so, "_root", containerGo, "WelcomeBackView") ? 1 : 0;
            wired += SetRef(so, "_coinsText", coinsText, "WelcomeBackView") ? 1 : 0;
            wired += SetRef(so, "_collectButton", collectButton, "WelcomeBackView") ? 1 : 0;
            wired += SetRef(so, "_closeButton", closeButton, "WelcomeBackView") ? 1 : 0;
            so.ApplyModifiedProperties();
            totalWired += wired;
            LogWireResult("WelcomeBackView", wired, expected);
        }

        // ------------------------------------------------------------------------------------
        // Кнопки (CLAUDE.md §7: Color Tint + targetGraphic, selectedColor=normalColor, Navigation=None)
        // ------------------------------------------------------------------------------------

        private static Button BuildTempleButton(Transform parent, string name, TMP_FontAsset fontAsset, string caption,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            var go = FindOrCreateUIChild(parent, name);
            var rt = (RectTransform)go.transform;
            SetAnchor(rt, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), anchoredPosition, size);

            var bg = go.GetComponent<Image>();
            if (bg == null) bg = go.AddComponent<Image>();
            bg.raycastTarget = true;
            bg.maskable = false;
            bg.color = new Color(0.25f, 0.45f, 0.65f, 1f);

            var button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var textGo = FindOrCreateUIChild(go.transform, "Label");
            var textRt = (RectTransform)textGo.transform;
            SetAnchor(textRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.maskable = false;
            tmp.font = fontAsset;
            tmp.fontSize = 26f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = caption; // статичная подпись — перезаписываем всегда.

            return button;
        }

        private static Button BuildOverlayButton(Transform panel, string name, TMP_FontAsset fontAsset,
            string caption, float anchoredY, Vector2 size)
        {
            var go = FindOrCreateUIChild(panel, name);
            var rt = (RectTransform)go.transform;
            SetAnchor(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, anchoredY), size);

            var bg = go.GetComponent<Image>();
            if (bg == null) bg = go.AddComponent<Image>();
            bg.raycastTarget = true;
            bg.maskable = false;
            bg.color = new Color(0.25f, 0.55f, 0.35f, 1f);

            var button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var textGo = FindOrCreateUIChild(go.transform, "Label");
            var textRt = (RectTransform)textGo.transform;
            SetAnchor(textRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.maskable = false;
            tmp.font = fontAsset;
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = caption;

            return button;
        }

        // ------------------------------------------------------------------------------------
        // Утилиты (дублируют LevelHudSetupEditor/MainMenuCanvasSetupEditor — тот же приём, что и в
        // остальных Editor-скриптах проекта, избегаем циклической зависимости между файлами Editor.UI).
        // ------------------------------------------------------------------------------------

        private static void LogWireResult(string ownerLabel, int wired, int expected)
        {
            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected}.");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей.");
        }

        private static GameObject FindOrCreateUIChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        private static void SetAnchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }

        private static bool SetRef(SerializedObject so, string propertyName, Object value, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — прошивка пропущена.");
                return false;
            }

            if (value == null)
            {
                Debug.LogWarning($"[Editor] Значение для \"{propertyName}\" на {ownerLabel} не найдено — оставлено как было.");
                return false;
            }

            prop.objectReferenceValue = value;
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
    }
}
