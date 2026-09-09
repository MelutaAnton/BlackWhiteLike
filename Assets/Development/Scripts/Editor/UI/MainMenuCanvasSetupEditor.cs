using System.Collections.Generic;
using System.IO;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Views.UI.LevelSelect;
using BlackWhiteLike.Views.UI.MainMenu;
using BlackWhiteLike.Views.UI.Overlay;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BlackWhiteLike.Editor.UI
{
    /// <summary>
    /// Шаг 7 — Кампания из 5 уровней (game-design.md, шаг 7). Собирает ОДИН префаб
    /// <c>Assets/Development/Prefabs/UI/MainMenuCanvas.prefab</c> (CLAUDE.md §7: канвас, повторяющийся
    /// в сценах, существует одним префабом, сцены держат connected-инстансы — образец LevelHudCanvas):
    /// <c>MainMenuPanel</c> (заголовок игры + кнопка Play) и <c>LevelSelectPanel</c> (сетка из 5
    /// карточек <see cref="LevelCardView"/> + кнопка Back) — СИБЛИНГИ под одним канвасом,
    /// LevelSelectPanel выключена по умолчанию (CLAUDE.md §7: оверлей/альтернативный экран — не
    /// потомок гасимого узла, тот же паттерн, что оверлеи LevelHudCanvas).
    /// Идемпотентен: <see cref="PrefabUtility.LoadPrefabContents"/>/<see cref="PrefabUtility.SaveAsPrefabAsset"/>
    /// (образец LevelHudSetupEditor) — существующий префаб дособирается по имени child'ов, без дублей.
    /// </summary>
    public static class MainMenuCanvasSetupEditor
    {
        private const string PrefabsFolder = "Assets/Development/Prefabs/UI";
        private const string PrefabPath = PrefabsFolder + "/MainMenuCanvas.prefab";
        private const string FontAssetPath = "Assets/Development/Art/Fonts/Nunito-Regular SDF.asset";

        private const int LevelCardCount = 5;

        [MenuItem("BlackWhiteLike/UI/Build Main Menu Canvas")]
        public static void BuildMainMenuCanvasMenuItem()
        {
            BuildOrUpdateCanvasPrefab();
        }

        /// <summary>
        /// Публичный — переиспользуется <c>MainMenuSceneSetupEditor</c>. Возвращает null, если TMP
        /// Font Asset ещё не создан (сначала прогоните "BlackWhiteLike/UI/Build Level HUD" — единственный
        /// источник шрифта проекта, см. LevelHudSetupEditor.EnsureFontAsset).
        /// </summary>
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
                : new GameObject("MainMenuCanvas", typeof(RectTransform));

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

            BuildMainMenuPanel(root.transform, fontAsset, ref totalWired, ref totalExpected);
            BuildLevelSelectPanel(root.transform, fontAsset, ref totalWired, ref totalExpected);
            // Шаг 8 (Meta + idle) — оверлей WelcomeBack, сиблинг панелей (CLAUDE.md §7).
            BuildWelcomeBackOverlay(root.transform, fontAsset, ref totalWired, ref totalExpected);

            Debug.Log($"[Editor] MainMenuCanvas: прошито {totalWired} из {totalExpected} полей суммарно.");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (existed)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        // ------------------------------------------------------------------------------------
        // MainMenuPanel — заголовок + кнопка Play
        // ------------------------------------------------------------------------------------

        private static void BuildMainMenuPanel(Transform canvasRoot, TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var panelGo = FindOrCreateUIChild(canvasRoot, "MainMenuPanel");
            var panelRt = (RectTransform)panelGo.transform;
            SetAnchor(panelRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var bg = panelGo.GetComponent<Image>();
            if (bg == null) bg = panelGo.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.maskable = false;
            bg.color = new Color(0.08f, 0.08f, 0.12f, 1f);

            var titleGo = FindOrCreateUIChild(panelGo.transform, "Title");
            var titleRt = (RectTransform)titleGo.transform;
            SetAnchor(titleRt, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(800f, 100f));
            var title = titleGo.GetComponent<TextMeshProUGUI>();
            if (title == null) title = titleGo.AddComponent<TextMeshProUGUI>();
            title.raycastTarget = false;
            title.maskable = false;
            title.font = fontAsset;
            title.fontSize = 64f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(title.text)) title.text = "BlackWhiteLike";

            var playButton = BuildMenuButton(panelGo.transform, "PlayButton", fontAsset, "Play", new Vector2(0f, -80f), new Vector2(320f, 88f));
            // Шаг 8 (Meta + idle) — вход в Temple.unity, ниже Play.
            var templeButton = BuildMenuButton(panelGo.transform, "TempleButton", fontAsset, "Temple", new Vector2(0f, -184f), new Vector2(320f, 88f));

            var view = panelGo.GetComponent<MainMenuView>();
            if (view == null) view = panelGo.AddComponent<MainMenuView>();

            const int expected = 3;
            totalExpected += expected;
            var so = new SerializedObject(view);
            int wired = 0;
            wired += SetRef(so, "_playButton", playButton, "MainMenuView") ? 1 : 0;
            wired += SetRef(so, "_root", panelGo, "MainMenuView") ? 1 : 0;
            wired += SetRef(so, "_templeButton", templeButton, "MainMenuView") ? 1 : 0;
            so.ApplyModifiedProperties();
            totalWired += wired;
            LogWireResult("MainMenuView", wired, expected);
        }

        // ------------------------------------------------------------------------------------
        // LevelSelectPanel — сетка из 5 карточек + Back
        // ------------------------------------------------------------------------------------

        private static void BuildLevelSelectPanel(Transform canvasRoot, TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var panelGo = FindOrCreateUIChild(canvasRoot, "LevelSelectPanel");
            var panelRt = (RectTransform)panelGo.transform;
            SetAnchor(panelRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var bg = panelGo.GetComponent<Image>();
            if (bg == null) bg = panelGo.AddComponent<Image>();
            bg.raycastTarget = true; // блокер тапов в фон, пока экран открыт
            bg.maskable = false;
            bg.color = new Color(0.08f, 0.08f, 0.12f, 1f);

            var titleGo = FindOrCreateUIChild(panelGo.transform, "Title");
            var titleRt = (RectTransform)titleGo.transform;
            SetAnchor(titleRt, new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(600f, 80f));
            var title = titleGo.GetComponent<TextMeshProUGUI>();
            if (title == null) title = titleGo.AddComponent<TextMeshProUGUI>();
            title.raycastTarget = false;
            title.maskable = false;
            title.font = fontAsset;
            title.fontSize = 44f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(title.text)) title.text = "Select Level";

            var gridGo = FindOrCreateUIChild(panelGo.transform, "CardsGrid");
            var gridRt = (RectTransform)gridGo.transform;
            SetAnchor(gridRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(LevelCardCount * 280f + (LevelCardCount - 1) * 32f, 360f));

            var layout = gridGo.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = gridGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var cards = new List<LevelCardView>(LevelCardCount);
            for (int i = 0; i < LevelCardCount; i++)
                cards.Add(BuildLevelCard(gridGo.transform, i + 1, fontAsset, ref totalWired, ref totalExpected));

            var backButton = BuildMenuButton(panelGo.transform, "BackButton", fontAsset, "Back", new Vector2(0f, 60f), new Vector2(240f, 72f));
            // Back — нижний левый угол.
            var backRt = (RectTransform)backButton.transform;
            SetAnchor(backRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 48f), new Vector2(240f, 72f));

            if (panelGo.activeSelf)
            {
                panelGo.SetActive(false);
                Debug.Log("[Editor] LevelSelectPanel: активна по умолчанию -> выключена (открывается по клику Play).");
            }

            var view = panelGo.GetComponent<LevelSelectView>();
            if (view == null) view = panelGo.AddComponent<LevelSelectView>();

            const int expected = 3;
            totalExpected += expected;
            var so = new SerializedObject(view);
            int wired = 0;
            wired += SetRef(so, "_root", panelGo, "LevelSelectView") ? 1 : 0;
            wired += SetRef(so, "_backButton", backButton, "LevelSelectView") ? 1 : 0;

            var cardsProp = so.FindProperty("_cards");
            if (cardsProp == null)
            {
                Debug.LogError("[Editor] Поле \"_cards\" не найдено на LevelSelectView — карточки не прошиты.");
            }
            else
            {
                cardsProp.arraySize = cards.Count;
                for (int i = 0; i < cards.Count; i++)
                    cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
                wired++;
                Debug.Log($"[Editor] LevelSelectView._cards: прошито {cards.Count} карточек.");
            }

            so.ApplyModifiedProperties();
            totalWired += wired;
            LogWireResult("LevelSelectView", wired, expected);
        }

        private static LevelCardView BuildLevelCard(Transform gridTransform, int levelNumber, TMP_FontAsset fontAsset,
            ref int totalWired, ref int totalExpected)
        {
            var cardGo = FindOrCreateUIChild(gridTransform, "LevelCard_" + levelNumber);
            var cardRt = (RectTransform)cardGo.transform;
            cardRt.sizeDelta = new Vector2(280f, 360f);

            var bg = cardGo.GetComponent<Image>();
            if (bg == null) bg = cardGo.AddComponent<Image>();
            bg.raycastTarget = true; // цель тапа
            bg.maskable = false;
            bg.color = new Color(0.16f, 0.16f, 0.2f, 1f);

            var button = cardGo.GetComponent<Button>();
            if (button == null) button = cardGo.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.selectedColor = colors.normalColor; // без залипания после тапа
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var previewGo = FindOrCreateUIChild(cardGo.transform, "Preview");
            var previewRt = (RectTransform)previewGo.transform;
            SetAnchor(previewRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), new Vector2(200f, 200f));
            var previewImage = previewGo.GetComponent<Image>();
            if (previewImage == null) previewImage = previewGo.AddComponent<Image>();
            previewImage.raycastTarget = false;
            previewImage.maskable = false;
            previewImage.preserveAspect = true;

            var titleGo = FindOrCreateUIChild(cardGo.transform, "TitleText");
            var titleRt = (RectTransform)titleGo.transform;
            SetAnchor(titleRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(240f, 48f));
            var titleText = titleGo.GetComponent<TextMeshProUGUI>();
            if (titleText == null) titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.raycastTarget = false;
            titleText.maskable = false;
            titleText.font = fontAsset;
            titleText.fontSize = 30f;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.text = "Level " + levelNumber; // статичная подпись — перезаписываем всегда.

            // LockedOverlay — полноразмерная тёмная плашка + иконка замка, поверх всей карточки.
            var lockedGo = FindOrCreateUIChild(cardGo.transform, "LockedOverlay");
            var lockedRt = (RectTransform)lockedGo.transform;
            SetAnchor(lockedRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var lockedDim = lockedGo.GetComponent<Image>();
            if (lockedDim == null) lockedDim = lockedGo.AddComponent<Image>();
            lockedDim.raycastTarget = false; // визуал состояния, не блокер тапа — блокировку решает презентер через Button.interactable
            lockedDim.maskable = false;
            lockedDim.color = new Color(0f, 0f, 0f, 0.7f);

            var lockIconGo = FindOrCreateUIChild(lockedGo.transform, "LockIcon");
            var lockIconRt = (RectTransform)lockIconGo.transform;
            SetAnchor(lockIconRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(72f, 72f));
            var lockIconImage = lockIconGo.GetComponent<Image>();
            if (lockIconImage == null) lockIconImage = lockIconGo.AddComponent<Image>();
            lockIconImage.raycastTarget = false;
            lockIconImage.maskable = false;
            lockIconImage.preserveAspect = true;
            var lockSprite = EditorSpriteUtility.EnsureSpriteAtPath(EditorSpriteUtility.LevelIconPaths.Locked);
            if (lockSprite != null) lockIconImage.sprite = lockSprite;

            lockedGo.SetActive(false); // состояние прошивает презентер по прогрессу игрока — не editor-скрипт.

            // CompletedMark — маленькая звезда в углу карточки.
            var completedGo = FindOrCreateUIChild(cardGo.transform, "CompletedMark");
            var completedRt = (RectTransform)completedGo.transform;
            SetAnchor(completedRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-12f, -12f), new Vector2(48f, 48f));
            var completedImage = completedGo.GetComponent<Image>();
            if (completedImage == null) completedImage = completedGo.AddComponent<Image>();
            completedImage.raycastTarget = false;
            completedImage.maskable = false;
            completedImage.preserveAspect = true;
            var completedSprite = EditorSpriteUtility.EnsureSpriteAtPath(EditorSpriteUtility.LevelIconPaths.Completed);
            if (completedSprite != null) completedImage.sprite = completedSprite;

            completedGo.SetActive(false); // состояние прошивает презентер — не editor-скрипт.

            var cardView = cardGo.GetComponent<LevelCardView>();
            if (cardView == null) cardView = cardGo.AddComponent<LevelCardView>();

            const int expected = 5;
            totalExpected += expected;
            var ownerLabel = "LevelCardView(Level " + levelNumber + ")";
            var so = new SerializedObject(cardView);
            int wired = 0;
            wired += SetRef(so, "_button", button, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_preview", previewImage, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_titleText", titleText, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_lockedOverlay", lockedGo, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_completedMark", completedGo, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();
            totalWired += wired;
            LogWireResult(ownerLabel, wired, expected);

            return cardView;
        }

        // ------------------------------------------------------------------------------------
        // WelcomeBackOverlay — сиблинг MainMenuPanel/LevelSelectPanel (CLAUDE.md §7). Структура
        // дублирует TempleCanvasSetupEditor.BuildWelcomeBackOverlay (тот же приём, что и остальные
        // общие блоки Editor.UI — избегаем циклической зависимости между файлами канвасов).
        // ------------------------------------------------------------------------------------

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
        // Кнопка (CLAUDE.md §7: Color Tint + targetGraphic, selectedColor=normalColor, Navigation=None)
        // ------------------------------------------------------------------------------------

        private static Button BuildMenuButton(Transform parent, string name, TMP_FontAsset fontAsset, string caption,
            Vector2 anchoredPosition, Vector2 size)
        {
            var go = FindOrCreateUIChild(parent, name);
            var rt = (RectTransform)go.transform;
            SetAnchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, size);

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
            tmp.fontSize = 32f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = caption; // статичная подпись — перезаписываем всегда.

            return button;
        }

        // ------------------------------------------------------------------------------------
        // Утилиты (дублируют LevelHudSetupEditor — тот же приём, что и в остальных Editor-скриптах
        // проекта, избегаем циклической зависимости между файлами Editor.UI).
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
