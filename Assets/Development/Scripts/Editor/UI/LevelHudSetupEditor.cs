using System.Collections.Generic;
using System.IO;
using BlackWhiteLike.Editor.Common;
using BlackWhiteLike.Models;
using BlackWhiteLike.Views.UI.Hud;
using BlackWhiteLike.Views.UI.Overlay;
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BlackWhiteLike.Editor.UI
{
    /// <summary>
    /// Собирает HUD ОДИН префаб <c>Assets/Development/Prefabs/UI/LevelHudCanvas.prefab</c>
    /// (CLAUDE.md §7: канвас, повторяющийся в сценах, существует одним префабом, сцены держат
    /// connected-инстансы), с панелью из 4 кнопок чудес (Rain/Heal/Fireball/Lightning), полосой
    /// Faith (шаг 4, game-design.md §19.4), и шагом 5 "Worship + win" (game-design.md §19.5):
    /// полосой Worship, индикатором Alignment, кнопкой паузы, всплывающими попапами прироста
    /// Worship и тремя оверлеями (Level Complete / Level Stalled / Pause).
    /// Идемпотентен: <see cref="PrefabUtility.LoadPrefabContents"/>/<see cref="PrefabUtility.SaveAsPrefabAsset"/>
    /// (см. память агента "prefab-content-editing-for-idempotent-rebuild") — существующий префаб
    /// дособирается по имени child'ов, без дублей.
    /// </summary>
    public static class LevelHudSetupEditor
    {
        private const string PrefabsFolder = "Assets/Development/Prefabs/UI";
        private const string PrefabPath = PrefabsFolder + "/LevelHudCanvas.prefab";
        private const string WorshipPopupPrefabPath = PrefabsFolder + "/WorshipPopup.prefab";

        private const string FontsFolder = "Assets/Development/Art/Fonts";
        private const string FontAssetPath = FontsFolder + "/Nunito-Regular SDF.asset";
        private const string NunitoSourceFolder = "Assets/Content/Fonts/Nunito";

        // Пути к иконкам — единственный источник BlackWhiteLike.Editor.Common.EditorSpriteUtility.MiracleIconPaths
        // (правка после ревью: строки дублировались здесь и в ConfigAssetCreatorEditor).
        private static readonly (MiracleType Type, string IconPath)[] ButtonDefs =
        {
            (MiracleType.Rain, EditorSpriteUtility.MiracleIconPaths.Rain),
            (MiracleType.Heal, EditorSpriteUtility.MiracleIconPaths.Heal),
            (MiracleType.Fireball, EditorSpriteUtility.MiracleIconPaths.Fireball),
            (MiracleType.Lightning, EditorSpriteUtility.MiracleIconPaths.Lightning),
        };

        // ------------------------------------------------------------------------------------
        // TMP Essential Resources — неинтерактивный импорт (правка после ревью: штатный пункт
        // Window/TextMeshPro/Import TMP Essential Resources открывает окно импорта
        // (ImportPackage(..., interactive:true)) — тот же класс проблемы, что модальные диалоги
        // (.claude/rules/editor-files.md, "Модальные диалоги ломают автоматизацию").
        // ------------------------------------------------------------------------------------

        [MenuItem("BlackWhiteLike/Setup/Import TMP Essentials")]
        public static void ImportTmpEssentialsMenuItem()
        {
            EnsureTmpEssentials();
        }

        /// <summary>
        /// Возвращает true, если TMP Essential Resources уже присутствуют (или только что были
        /// найдены). Если их нет — запускает неинтерактивный импорт и возвращает false: импорт
        /// асинхронный, TMP_Settings в этом же вызове ещё недоступен, вызывающий обязан прерваться
        /// (не собирать канвас с null-шрифтом) и попросить повторный прогон.
        /// </summary>
        public static bool EnsureTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
            {
                Debug.Log("[Editor] TMP Essential Resources уже импортированы — пропуск.");
                return true;
            }

            string packagePath = TMP_EditorUtility.packageFullPath + "/Package Resources/TMP Essential Resources.unitypackage";
            if (!File.Exists(packagePath))
            {
                Debug.LogError("[Editor] TMP Essential Resources.unitypackage не найден по пути \"" + packagePath +
                                "\" — импортируйте вручную через Window/TextMeshPro/Import TMP Essential Resources.");
                return false;
            }

            AssetDatabase.ImportPackage(packagePath, interactive: false);
            Debug.LogWarning("[Editor] TMP Essentials импортируются — повторите Build Level HUD после завершения импорта.");
            return false;
        }

        // ------------------------------------------------------------------------------------
        // Build Level HUD
        // ------------------------------------------------------------------------------------

        [MenuItem("BlackWhiteLike/UI/Build Level HUD")]
        public static void BuildLevelHud()
        {
            if (!EnsureTmpEssentials())
                return; // лог уже написан EnsureTmpEssentials — импорт асинхронный, ждём следующего прогона.

            var fontAsset = EnsureFontAsset();
            if (fontAsset == null)
            {
                Debug.LogError("[Editor] Build Level HUD прерван — TMP Font Asset не создан (см. лог выше).");
                return;
            }

            EnsureFolder(PrefabsFolder);

            // Шаг 5 — WorshipPopup собирается отдельным префабом ДО открытия LevelHudCanvas.prefab
            // (LoadPrefabContents одного префаба не должен пересекаться по времени с другим).
            var popupView = BuildOrUpdateWorshipPopupPrefab(fontAsset);
            if (popupView == null)
            {
                Debug.LogError("[Editor] Build Level HUD прерван — префаб WorshipPopup не собран.");
                return;
            }

            var prefab = BuildOrUpdateCanvasPrefab(fontAsset, popupView);
            if (prefab == null)
            {
                Debug.LogError("[Editor] Build Level HUD прерван — префаб LevelHudCanvas не собран.");
                return;
            }

            EnsureEventSystem();
            var sceneInstance = EnsureSceneInstance(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Editor] Build Level HUD завершён: " + PrefabPath +
                      (sceneInstance != null ? ", инстанс в сцене готов." : "."));
        }

        /// <summary>
        /// TMP Font Asset проекта под HUD — единственный источник (создаётся один раз, переиспользуется
        /// всеми TMP-текстами HUD). Ищет исходный .ttf с "Regular" в имени под Content/Fonts/Nunito —
        /// Content/ не редактируется (CLAUDE.md), поэтому производный SDF-ассет живёт в Development/Art/Fonts.
        /// </summary>
        private static TMP_FontAsset EnsureFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null && HasPersistedAtlasAndMaterial(existing)) return existing;

            var font = FindRegularNunito();
            if (font == null) return null;

            if (existing != null)
            {
                // Миграция (первый playtest шага 4): ассет, созданный старой версией скрипта, хранил
                // атлас и материал только В ПАМЯТИ — после перезагрузки ассетов m_AtlasTextures[0]
                // стал missing-ссылкой и каждый TMP-текст падал с MissingReferenceException при
                // первом же рендере. Перегенерируем данные внутрь ТОГО ЖЕ ассета: GUID сохраняется,
                // ссылки префаба HUD на шрифт остаются валидными.
                var fresh = TMP_FontAsset.CreateFontAsset(font);
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = FontAssetName;
                AttachAtlasAndMaterialSubAssets(existing);
                UnityEngine.Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                Debug.Log("[Editor] TMP Font Asset " + FontAssetPath + ": атлас и материал перегенерированы и сохранены саб-ассетами (миграция).");
                return existing;
            }

            EnsureFolder(FontsFolder);
            var fontAsset = TMP_FontAsset.CreateFontAsset(font);
            fontAsset.name = FontAssetName;
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            AttachAtlasAndMaterialSubAssets(fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log("[Editor] Создан TMP Font Asset: " + FontAssetPath + " (атлас динамический, атлас и материал — саб-ассеты).");
            return fontAsset;
        }

        private const string FontAssetName = "Nunito-Regular SDF";

        /// <summary>
        /// Атлас и материал — саб-ассеты внутри .asset шрифта (ровно так делает штатный TMP Font Asset
        /// Creator): <c>CreateFontAsset</c> создаёт их в памяти, и без AddObjectToAsset они теряются
        /// при первой перезагрузке ассетов (см. миграцию в EnsureFontAsset).
        /// </summary>
        private static void AttachAtlasAndMaterialSubAssets(TMP_FontAsset fontAsset)
        {
            var atlas = fontAsset.atlasTexture;
            if (atlas != null)
            {
                atlas.name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            var material = fontAsset.material;
            if (material != null)
            {
                material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }
        }

        /// <summary>Проверка численно, а не «ассет есть»: у валидного шрифта атлас и материал — сохранённые саб-ассеты.</summary>
        private static bool HasPersistedAtlasAndMaterial(TMP_FontAsset fontAsset)
        {
            if (fontAsset.material == null) return false;
            var atlases = fontAsset.atlasTextures;
            if (atlases == null || atlases.Length == 0 || atlases[0] == null) return false;
            return AssetDatabase.IsSubAsset(atlases[0]) && AssetDatabase.IsSubAsset(fontAsset.material);
        }

        private static Font FindRegularNunito()
        {
            var guids = AssetDatabase.FindAssets("t:Font", new[] { NunitoSourceFolder });
            string regularPath = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("Regular", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    regularPath = path;
                    break;
                }
            }

            if (regularPath == null)
            {
                Debug.LogError("[Editor] Не найден .ttf с \"Regular\" в имени под " + NunitoSourceFolder + " — TMP Font Asset не создан.");
                return null;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(regularPath);
            if (font == null)
            {
                Debug.LogError("[Editor] " + regularPath + " не загрузился как Font.");
            }

            return font;
        }

        // ------------------------------------------------------------------------------------
        // Сборка префаба
        // ------------------------------------------------------------------------------------

        private static GameObject BuildOrUpdateCanvasPrefab(TMP_FontAsset fontAsset, WorshipPopupView popupPrefabView)
        {
            bool existed = File.Exists(PrefabPath);
            GameObject root = existed
                ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject("LevelHudCanvas", typeof(RectTransform));

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

            var levelHudView = root.GetComponent<LevelHudView>();
            if (levelHudView == null) levelHudView = root.AddComponent<LevelHudView>();

            int totalWired = 0;
            int totalExpected = 0;

            BuildMiraclePanel(root.transform, fontAsset, ref totalWired, ref totalExpected);
            BuildFaithBar(root.transform, fontAsset, ref totalWired, ref totalExpected);

            // Шаг 5 — Worship + win (game-design.md §19.5).
            BuildWorshipBar(root.transform, fontAsset, out var worshipFill, out var worshipLabel);
            BuildAlignmentIndicator(root.transform, fontAsset, out var alignmentTrack, out var alignmentKnob);
            BuildPauseButton(root.transform, fontAsset, out var pauseButton);
            var popupRoot = BuildPopupRoot(root.transform);

            var hudSo = new SerializedObject(levelHudView);
            int hudWired = 0;
            hudWired += SetRef(hudSo, "_worshipFill", worshipFill, "LevelHudView") ? 1 : 0;
            hudWired += SetRef(hudSo, "_worshipLabel", worshipLabel, "LevelHudView") ? 1 : 0;
            hudWired += SetRef(hudSo, "_alignmentKnob", alignmentKnob, "LevelHudView") ? 1 : 0;
            hudWired += SetRef(hudSo, "_alignmentTrack", alignmentTrack, "LevelHudView") ? 1 : 0;
            hudWired += SetRef(hudSo, "_pauseButton", pauseButton, "LevelHudView") ? 1 : 0;
            hudWired += SetRef(hudSo, "_popupPrefab", popupPrefabView, "LevelHudView") ? 1 : 0;
            hudWired += SetRef(hudSo, "_popupRoot", popupRoot, "LevelHudView") ? 1 : 0;
            hudSo.ApplyModifiedProperties();
            LogWireResult("LevelHudView", hudWired, 7);
            totalWired += hudWired;
            totalExpected += 7;

            // Оверлеи — сиблинги MiraclePanel/FaithBar под тем же канвасом (CLAUDE.md п.7: оверлей не
            // потомок гасимого узла), выключены по умолчанию, последними сиблингами (поверх HUD).
            BuildLevelCompleteOverlay(root.transform, fontAsset, ref totalWired, ref totalExpected);
            BuildLevelStalledOverlay(root.transform, fontAsset, ref totalWired, ref totalExpected);
            BuildPauseOverlay(root.transform, fontAsset, ref totalWired, ref totalExpected);

            Debug.Log($"[Editor] LevelHudCanvas: прошито {totalWired} из {totalExpected} полей суммарно.");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (existed)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);

            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        /// <summary>Нижний центр экрана: 4 кнопки чудес в HorizontalLayoutGroup.</summary>
        private static void BuildMiraclePanel(Transform canvasRoot, TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var panelGo = FindOrCreateUIChild(canvasRoot, "MiraclePanel");
            var panelRt = (RectTransform)panelGo.transform;
            SetAnchor(panelRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(4 * 128f + 3 * 16f, 128f));

            var layout = panelGo.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = panelGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var panelView = panelGo.GetComponent<MiraclePanelView>();
            if (panelView == null) panelView = panelGo.AddComponent<MiraclePanelView>();

            var buttonViews = new List<MiracleButtonView>(ButtonDefs.Length);
            foreach (var (type, iconPath) in ButtonDefs)
                buttonViews.Add(BuildMiracleButton(panelGo.transform, type, iconPath, fontAsset, ref totalWired, ref totalExpected));

            const int expected = 1;
            totalExpected += expected;
            var panelSo = new SerializedObject(panelView);
            var buttonsProp = panelSo.FindProperty("_buttons");
            if (buttonsProp == null)
            {
                Debug.LogError("[Editor] Поле \"_buttons\" не найдено на MiraclePanelView — кнопки не прошиты.");
            }
            else
            {
                buttonsProp.arraySize = buttonViews.Count;
                for (int i = 0; i < buttonViews.Count; i++)
                    buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttonViews[i];
                panelSo.ApplyModifiedProperties();
                totalWired += expected;
                Debug.Log($"[Editor] MiraclePanelView._buttons: прошито {buttonViews.Count} кнопок.");
            }
        }

        private static MiracleButtonView BuildMiracleButton(Transform panelTransform, MiracleType type, string iconPath,
            TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var buttonGo = FindOrCreateUIChild(panelTransform, "MiracleButton_" + type);
            var buttonRt = (RectTransform)buttonGo.transform;
            buttonRt.sizeDelta = new Vector2(128f, 128f);

            var backgroundImage = buttonGo.GetComponent<Image>();
            if (backgroundImage == null) backgroundImage = buttonGo.AddComponent<Image>();
            backgroundImage.raycastTarget = true; // цель тапа
            backgroundImage.maskable = false;

            var button = buttonGo.GetComponent<Button>();
            if (button == null) button = buttonGo.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = backgroundImage;
            var colors = button.colors;
            colors.selectedColor = colors.normalColor; // без залипания после тапа
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var canvasGroup = buttonGo.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = buttonGo.AddComponent<CanvasGroup>();

            var buttonView = buttonGo.GetComponent<MiracleButtonView>();
            if (buttonView == null) buttonView = buttonGo.AddComponent<MiracleButtonView>();

            // Icon
            var iconGo = FindOrCreateUIChild(buttonGo.transform, "Icon");
            var iconRt = (RectTransform)iconGo.transform;
            SetAnchor(iconRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(96f, 96f));
            var iconImage = iconGo.GetComponent<Image>();
            if (iconImage == null) iconImage = iconGo.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.maskable = false;
            iconImage.preserveAspect = true;
            var iconSprite = EditorSpriteUtility.EnsureSpriteAtPath(iconPath);
            if (iconSprite != null) iconImage.sprite = iconSprite;

            // Cost
            var costGo = FindOrCreateUIChild(buttonGo.transform, "Cost");
            var costRt = (RectTransform)costGo.transform;
            SetAnchor(costRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 6f), new Vector2(120f, 32f));
            var costLabel = costGo.GetComponent<TextMeshProUGUI>();
            if (costLabel == null) costLabel = costGo.AddComponent<TextMeshProUGUI>();
            costLabel.raycastTarget = false;
            costLabel.maskable = false;
            costLabel.font = fontAsset;
            costLabel.fontSize = 28f;
            costLabel.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(costLabel.text)) costLabel.text = "—";

            // SelectedFrame — подсветка фона выбранного чуда (правка после ревью: без sprite/цвета,
            // растянутая на всю кнопку и ПОСЛЕДНИМ ребёнком, при SetSelected(true) рамка рисовалась
            // непрозрачным белым квадратом ПОВЕРХ иконки и cost-лейбла). Полупрозрачный жёлтый тон,
            // на 8px больше кнопки во все стороны и ПЕРВЫМ ребёнком — под иконкой/текстом, читается
            // как подсветка-рамка вокруг, не перекрывает контент. Выключена по умолчанию.
            var frameGo = FindOrCreateUIChild(buttonGo.transform, "SelectedFrame");
            var frameRt = (RectTransform)frameGo.transform;
            SetAnchor(frameRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            frameRt.offsetMin = new Vector2(-8f, -8f);
            frameRt.offsetMax = new Vector2(8f, 8f);
            var frameImage = frameGo.GetComponent<Image>();
            if (frameImage == null) frameImage = frameGo.AddComponent<Image>();
            var expectedFrameColor = new Color(1f, 0.85f, 0.2f, 0.55f);
            if (frameImage.sprite != null)
            {
                Debug.Log("[Editor] SelectedFrame.sprite: " + frameImage.sprite.name + " -> null (миграция на подсветку без спрайта).");
                frameImage.sprite = null;
            }
            if (frameImage.color != expectedFrameColor)
            {
                Debug.Log($"[Editor] SelectedFrame.color: {frameImage.color} -> {expectedFrameColor}.");
                frameImage.color = expectedFrameColor;
            }
            if (frameImage.raycastTarget)
            {
                Debug.Log("[Editor] SelectedFrame.raycastTarget: true -> false.");
                frameImage.raycastTarget = false;
            }
            if (frameImage.maskable)
            {
                Debug.Log("[Editor] SelectedFrame.maskable: true -> false.");
                frameImage.maskable = false;
            }
            if (frameGo.transform.GetSiblingIndex() != 0)
            {
                Debug.Log("[Editor] SelectedFrame: siblingIndex -> 0 (под иконку/текст).");
                frameGo.transform.SetAsFirstSibling();
            }
            frameGo.SetActive(false);

            const int expected = 6;
            totalExpected += expected;
            var ownerLabel = "MiracleButtonView(" + type + ")";
            var so = new SerializedObject(buttonView);
            int wired = 0;
            wired += SetEnumProp(so, "_type", (int)type, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_button", button, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_icon", iconImage, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_costLabel", costLabel, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_selectedFrame", frameGo, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_canvasGroup", canvasGroup, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();
            totalWired += wired;

            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected}.");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей.");

            return buttonView;
        }

        /// <summary>Верхний левый угол: полоса Faith (заполнение + подпись значения).</summary>
        private static void BuildFaithBar(Transform canvasRoot, TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var barGo = FindOrCreateUIChild(canvasRoot, "FaithBar");
            var barRt = (RectTransform)barGo.transform;
            SetAnchor(barRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -24f), new Vector2(360f, 32f));

            var backgroundImage = barGo.GetComponent<Image>();
            if (backgroundImage == null) backgroundImage = barGo.AddComponent<Image>();
            backgroundImage.raycastTarget = false;
            backgroundImage.maskable = false;
            backgroundImage.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);

            var fillGo = FindOrCreateUIChild(barGo.transform, "Fill");
            var fillRt = (RectTransform)fillGo.transform;
            SetAnchor(fillRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var fillImage = fillGo.GetComponent<Image>();
            if (fillImage == null) fillImage = fillGo.AddComponent<Image>();
            fillImage.raycastTarget = false;
            fillImage.maskable = false;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.color = new Color(0.3f, 0.65f, 0.95f, 1f);

            var valueGo = FindOrCreateUIChild(barGo.transform, "Value");
            var valueRt = (RectTransform)valueGo.transform;
            SetAnchor(valueRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var valueLabel = valueGo.GetComponent<TextMeshProUGUI>();
            if (valueLabel == null) valueLabel = valueGo.AddComponent<TextMeshProUGUI>();
            valueLabel.raycastTarget = false;
            valueLabel.maskable = false;
            valueLabel.font = fontAsset;
            valueLabel.fontSize = 20f;
            valueLabel.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(valueLabel.text)) valueLabel.text = "0/0";

            var faithBarView = barGo.GetComponent<FaithBarView>();
            if (faithBarView == null) faithBarView = barGo.AddComponent<FaithBarView>();

            const int expected = 2;
            totalExpected += expected;
            var ownerLabel = "FaithBarView";
            var so = new SerializedObject(faithBarView);
            int wired = 0;
            wired += SetRef(so, "_fillImage", fillImage, ownerLabel) ? 1 : 0;
            wired += SetRef(so, "_valueLabel", valueLabel, ownerLabel) ? 1 : 0;
            so.ApplyModifiedProperties();
            totalWired += wired;

            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected}.");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей.");
        }

        // ------------------------------------------------------------------------------------
        // Шаг 5 — Worship + win (game-design.md §19.5)
        // ------------------------------------------------------------------------------------

        /// <summary>Верхний центр экрана, НАД MiraclePanel/FaithBar — полоса накопленного Worship.</summary>
        private static void BuildWorshipBar(Transform canvasRoot, TMP_FontAsset fontAsset, out Image fillImage, out TextMeshProUGUI label)
        {
            var barGo = FindOrCreateUIChild(canvasRoot, "WorshipBar");
            var barRt = (RectTransform)barGo.transform;
            SetAnchor(barRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(640f, 40f));

            var backgroundImage = barGo.GetComponent<Image>();
            if (backgroundImage == null) backgroundImage = barGo.AddComponent<Image>();
            backgroundImage.raycastTarget = false;
            backgroundImage.maskable = false;
            backgroundImage.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);

            var fillGo = FindOrCreateUIChild(barGo.transform, "Fill");
            var fillRt = (RectTransform)fillGo.transform;
            SetAnchor(fillRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fillImage = fillGo.GetComponent<Image>();
            if (fillImage == null) fillImage = fillGo.AddComponent<Image>();
            fillImage.raycastTarget = false;
            fillImage.maskable = false;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.color = new Color(1f, 0.8f, 0.25f, 1f);

            var labelGo = FindOrCreateUIChild(barGo.transform, "Label");
            var labelRt = (RectTransform)labelGo.transform;
            SetAnchor(labelRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            label = labelGo.GetComponent<TextMeshProUGUI>();
            if (label == null) label = labelGo.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.maskable = false;
            label.font = fontAsset;
            label.fontSize = 26f;
            label.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(label.text)) label.text = "0 / 100";

            // ThresholdMarker — визуальная метка порога у правого края бара, без прошивки в код
            // (чисто декоративный элемент, порог читается из WorshipConfig рантаймом).
            var markerGo = FindOrCreateUIChild(barGo.transform, "ThresholdMarker");
            var markerRt = (RectTransform)markerGo.transform;
            SetAnchor(markerRt, new Vector2(0.9f, 0.5f), new Vector2(0.9f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(4f, 48f));
            var markerImage = markerGo.GetComponent<Image>();
            if (markerImage == null) markerImage = markerGo.AddComponent<Image>();
            markerImage.raycastTarget = false;
            markerImage.maskable = false;
            markerImage.color = new Color(1f, 1f, 1f, 0.7f);
        }

        /// <summary>Под WorshipBar — трек Evil/Good с ползунком текущего Alignment.</summary>
        private static void BuildAlignmentIndicator(Transform canvasRoot, TMP_FontAsset fontAsset, out RectTransform trackRt, out RectTransform knobRt)
        {
            var containerGo = FindOrCreateUIChild(canvasRoot, "AlignmentIndicator");
            var containerRt = (RectTransform)containerGo.transform;
            SetAnchor(containerRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -72f), new Vector2(320f, 16f));

            var trackGo = FindOrCreateUIChild(containerGo.transform, "Track");
            trackRt = (RectTransform)trackGo.transform;
            SetAnchor(trackRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var trackImage = trackGo.GetComponent<Image>();
            if (trackImage == null) trackImage = trackGo.AddComponent<Image>();
            trackImage.raycastTarget = false;
            trackImage.maskable = false;
            trackImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);

            var tickGo = FindOrCreateUIChild(trackGo.transform, "CenterTick");
            var tickRt = (RectTransform)tickGo.transform;
            SetAnchor(tickRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(2f, 24f));
            var tickImage = tickGo.GetComponent<Image>();
            if (tickImage == null) tickImage = tickGo.AddComponent<Image>();
            tickImage.raycastTarget = false;
            tickImage.maskable = false;
            tickImage.color = new Color(1f, 1f, 1f, 0.9f);

            var knobGo = FindOrCreateUIChild(trackGo.transform, "Knob");
            knobRt = (RectTransform)knobGo.transform;
            SetAnchor(knobRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(22f, 22f));
            var knobImage = knobGo.GetComponent<Image>();
            if (knobImage == null) knobImage = knobGo.AddComponent<Image>();
            knobImage.raycastTarget = false;
            knobImage.maskable = false;
            knobImage.color = Color.white;

            var evilGo = FindOrCreateUIChild(containerGo.transform, "EvilLabel");
            var evilRt = (RectTransform)evilGo.transform;
            SetAnchor(evilRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f), new Vector2(60f, 24f));
            var evilLabel = evilGo.GetComponent<TextMeshProUGUI>();
            if (evilLabel == null) evilLabel = evilGo.AddComponent<TextMeshProUGUI>();
            evilLabel.raycastTarget = false;
            evilLabel.maskable = false;
            evilLabel.font = fontAsset;
            evilLabel.fontSize = 18f;
            evilLabel.alignment = TextAlignmentOptions.MidlineRight;
            if (string.IsNullOrEmpty(evilLabel.text)) evilLabel.text = "Evil";

            var goodGo = FindOrCreateUIChild(containerGo.transform, "GoodLabel");
            var goodRt = (RectTransform)goodGo.transform;
            SetAnchor(goodRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(60f, 24f));
            var goodLabel = goodGo.GetComponent<TextMeshProUGUI>();
            if (goodLabel == null) goodLabel = goodGo.AddComponent<TextMeshProUGUI>();
            goodLabel.raycastTarget = false;
            goodLabel.maskable = false;
            goodLabel.font = fontAsset;
            goodLabel.fontSize = 18f;
            goodLabel.alignment = TextAlignmentOptions.MidlineLeft;
            if (string.IsNullOrEmpty(goodLabel.text)) goodLabel.text = "Good";
        }

        /// <summary>Верхний правый угол — кнопка паузы с иконкой из ThirdParty-пака (фоллбэк — текст "II").</summary>
        private static void BuildPauseButton(Transform canvasRoot, TMP_FontAsset fontAsset, out Button pauseButton)
        {
            var buttonGo = FindOrCreateUIChild(canvasRoot, "PauseButton");
            var buttonRt = (RectTransform)buttonGo.transform;
            SetAnchor(buttonRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -24f), new Vector2(96f, 96f));

            var backgroundImage = buttonGo.GetComponent<Image>();
            if (backgroundImage == null) backgroundImage = buttonGo.AddComponent<Image>();
            backgroundImage.raycastTarget = true; // цель тапа
            backgroundImage.maskable = false;
            backgroundImage.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);

            pauseButton = buttonGo.GetComponent<Button>();
            if (pauseButton == null) pauseButton = buttonGo.AddComponent<Button>();
            pauseButton.transition = Selectable.Transition.ColorTint;
            pauseButton.targetGraphic = backgroundImage;
            var colors = pauseButton.colors;
            colors.selectedColor = colors.normalColor; // без залипания после тапа
            pauseButton.colors = colors;
            pauseButton.navigation = new Navigation { mode = Navigation.Mode.None };

            var iconGo = FindOrCreateUIChild(buttonGo.transform, "Icon");
            var iconRt = (RectTransform)iconGo.transform;
            SetAnchor(iconRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(64f, 64f));

            var pauseSprite = EditorSpriteUtility.EnsureSpriteAtPath(EditorSpriteUtility.HudIconPaths.Pause);
            var existingLabel = iconGo.GetComponent<TextMeshProUGUI>();
            var existingImage = iconGo.GetComponent<Image>();

            if (pauseSprite != null)
            {
                if (existingLabel != null)
                {
                    Object.DestroyImmediate(existingLabel); // миграция с текстового фоллбэка на иконку
                    Debug.Log("[Editor] PauseButton/Icon: текстовый фоллбэк заменён на найденную иконку.");
                }

                var iconImage = existingImage != null ? existingImage : iconGo.AddComponent<Image>();
                iconImage.raycastTarget = false;
                iconImage.maskable = false;
                iconImage.preserveAspect = true;
                iconImage.sprite = pauseSprite;
            }
            else
            {
                if (existingImage != null) Object.DestroyImmediate(existingImage);

                var iconLabel = existingLabel != null ? existingLabel : iconGo.AddComponent<TextMeshProUGUI>();
                iconLabel.raycastTarget = false;
                iconLabel.maskable = false;
                iconLabel.font = fontAsset;
                iconLabel.fontSize = 40f;
                iconLabel.alignment = TextAlignmentOptions.Center;
                if (string.IsNullOrEmpty(iconLabel.text)) iconLabel.text = "II";

                Debug.LogWarning("[Editor] Иконка паузы не найдена по пути " + EditorSpriteUtility.HudIconPaths.Pause +
                                  " — использован текстовый фоллбэк \"II\".");
            }
        }

        /// <summary>Пустая точка спавна попапов прироста Worship — под WorshipBar.</summary>
        private static RectTransform BuildPopupRoot(Transform canvasRoot)
        {
            var go = FindOrCreateUIChild(canvasRoot, "PopupRoot");
            var rt = (RectTransform)go.transform;
            SetAnchor(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -80f), new Vector2(1f, 1f));
            return rt;
        }

        /// <summary>
        /// Отдельный префаб всплывающего попапа "+N Worship" (Prefabs/UI/WorshipPopup.prefab) —
        /// LevelHudView._popupPrefab ссылается на компонент АССЕТА (не на инстанс сцены), поэтому
        /// возвращаемое значение — WorshipPopupView, загруженный обратно из AssetDatabase после
        /// сохранения (аналогично паттерну LoadPrefabContents/SaveAsPrefabAsset).
        /// </summary>
        private static WorshipPopupView BuildOrUpdateWorshipPopupPrefab(TMP_FontAsset fontAsset)
        {
            bool existed = File.Exists(WorshipPopupPrefabPath);
            GameObject root = existed
                ? PrefabUtility.LoadPrefabContents(WorshipPopupPrefabPath)
                : new GameObject("WorshipPopup", typeof(RectTransform));

            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(240f, 48f);

            var canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();

            var labelGo = FindOrCreateUIChild(root.transform, "Label");
            var labelRt = (RectTransform)labelGo.transform;
            SetAnchor(labelRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            if (label == null) label = labelGo.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.maskable = false;
            label.font = fontAsset;
            label.fontSize = 36f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(label.text)) label.text = "+0";

            var view = root.GetComponent<WorshipPopupView>();
            if (view == null) view = root.AddComponent<WorshipPopupView>();

            var so = new SerializedObject(view);
            int wired = 0;
            wired += SetRef(so, "_label", label, "WorshipPopupView") ? 1 : 0;
            wired += SetRef(so, "_canvasGroup", canvasGroup, "WorshipPopupView") ? 1 : 0;
            so.ApplyModifiedProperties();
            LogWireResult("WorshipPopupView", wired, 2);

            PrefabUtility.SaveAsPrefabAsset(root, WorshipPopupPrefabPath);
            if (existed)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);

            var savedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WorshipPopupPrefabPath);
            return savedAsset != null ? savedAsset.GetComponent<WorshipPopupView>() : null;
        }

        // ------------------------------------------------------------------------------------
        // Шаг 5 — Оверлеи (Level Complete / Level Stalled / Pause)
        // ------------------------------------------------------------------------------------

        private const float DefaultOverlayPanelWidth = 560f;
        private const float DefaultOverlayPanelHeight = 360f;

        /// <summary>
        /// Общий каркас оверлея: полноэкранный Dim-блокер тапов + центральная Panel. Контейнер —
        /// сиблинг MiraclePanel/FaithBar/WorshipBar под тем же канвасом (CLAUDE.md п.7 — оверлей не
        /// потомок гасимого узла), выключен по умолчанию, ставится последним сиблингом (поверх HUD).
        /// </summary>
        private static Transform BuildOverlayContainer(Transform canvasRoot, string containerName, Vector2 panelSize)
        {
            var containerGo = FindOrCreateUIChild(canvasRoot, containerName);
            var containerRt = (RectTransform)containerGo.transform;
            SetAnchor(containerRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var dimGo = FindOrCreateUIChild(containerGo.transform, "Dim");
            var dimRt = (RectTransform)dimGo.transform;
            SetAnchor(dimRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var dimImage = dimGo.GetComponent<Image>();
            if (dimImage == null) dimImage = dimGo.AddComponent<Image>();
            dimImage.raycastTarget = true; // блокер тапов в мир, пока оверлей открыт
            dimImage.maskable = false;
            dimImage.color = new Color(0f, 0f, 0f, 0.6f);

            var panelGo = FindOrCreateUIChild(containerGo.transform, "Panel");
            var panelRt = (RectTransform)panelGo.transform;
            SetAnchor(panelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, panelSize);
            var panelImage = panelGo.GetComponent<Image>();
            if (panelImage == null) panelImage = panelGo.AddComponent<Image>();
            panelImage.raycastTarget = true;
            panelImage.maskable = false;
            panelImage.color = new Color(0.1f, 0.1f, 0.13f, 0.95f);

            if (containerGo.activeSelf)
            {
                containerGo.SetActive(false);
                Debug.Log($"[Editor] {containerName}: активен по умолчанию -> выключен.");
            }

            if (containerGo.transform.GetSiblingIndex() != canvasRoot.childCount - 1)
                containerGo.transform.SetAsLastSibling();

            return panelGo.transform;
        }

        /// <summary>Заголовок/строка текста внутри панели оверлея, якорь верх-центр панели.</summary>
        private static TextMeshProUGUI BuildOverlayLabel(Transform panel, string name, TMP_FontAsset fontAsset,
            float fontSize, string defaultText, float anchoredY, Vector2 size, bool bold = false)
        {
            var go = FindOrCreateUIChild(panel, name);
            var rt = (RectTransform)go.transform;
            SetAnchor(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, anchoredY), size);
            var label = go.GetComponent<TextMeshProUGUI>();
            if (label == null) label = go.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.maskable = false;
            label.font = fontAsset;
            label.fontSize = fontSize;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            if (string.IsNullOrEmpty(label.text)) label.text = defaultText;
            return label;
        }

        /// <summary>Кнопка внутри панели оверлея, якорь низ-центр панели.</summary>
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
            tmp.text = caption; // статичная подпись кнопки — не пользовательские данные, перезаписываем всегда

            return button;
        }

        private static void BuildLevelCompleteOverlay(Transform canvasRoot, TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var panel = BuildOverlayContainer(canvasRoot, "LevelCompleteOverlay", new Vector2(DefaultOverlayPanelWidth, DefaultOverlayPanelHeight));
            var containerGo = panel.parent.gameObject;

            BuildOverlayLabel(panel, "Title", fontAsset, 44f, "Level Complete", -24f, new Vector2(500f, 50f), bold: true);
            var worshipLabel = BuildOverlayLabel(panel, "WorshipLabel", fontAsset, 24f, "Worship 0 / 100", -88f, new Vector2(480f, 32f));
            var aweLabel = BuildOverlayLabel(panel, "AweLabel", fontAsset, 22f, "Awe 0", -124f, new Vector2(480f, 28f));
            var fearLabel = BuildOverlayLabel(panel, "FearLabel", fontAsset, 22f, "Fear 0", -154f, new Vector2(480f, 28f));
            var coinsLabel = BuildOverlayLabel(panel, "CoinsLabel", fontAsset, 22f, "+0 coins", -184f, new Vector2(480f, 28f));
            var continueButton = BuildOverlayButton(panel, "ContinueButton", fontAsset, "Continue", 24f, new Vector2(280f, 72f));

            var view = containerGo.GetComponent<LevelCompleteView>();
            if (view == null) view = containerGo.AddComponent<LevelCompleteView>();

            var so = new SerializedObject(view);
            int wired = 0;
            wired += SetRef(so, "_root", containerGo, "LevelCompleteView") ? 1 : 0;
            wired += SetRef(so, "_worshipLabel", worshipLabel, "LevelCompleteView") ? 1 : 0;
            wired += SetRef(so, "_aweLabel", aweLabel, "LevelCompleteView") ? 1 : 0;
            wired += SetRef(so, "_fearLabel", fearLabel, "LevelCompleteView") ? 1 : 0;
            wired += SetRef(so, "_coinsLabel", coinsLabel, "LevelCompleteView") ? 1 : 0;
            wired += SetRef(so, "_continueButton", continueButton, "LevelCompleteView") ? 1 : 0;
            so.ApplyModifiedProperties();
            LogWireResult("LevelCompleteView", wired, 6);
            totalWired += wired;
            totalExpected += 6;
        }

        private static void BuildLevelStalledOverlay(Transform canvasRoot, TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var panel = BuildOverlayContainer(canvasRoot, "LevelStalledOverlay", new Vector2(DefaultOverlayPanelWidth, 260f));
            var containerGo = panel.parent.gameObject;

            BuildOverlayLabel(panel, "Title", fontAsset, 36f, "Вера иссякла", -24f, new Vector2(480f, 50f), bold: true);
            BuildOverlayLabel(panel, "Subtitle", fontAsset, 22f, "Пополнить веру, чтобы продолжить", -84f, new Vector2(480f, 40f));
            // Пополнение веры — заглушка rewarded-рекламы (ADS_STUB реализуется в рантайм-сервисе,
            // вызываемом по клику RefillButton; сам Editor-скрипт не содержит бизнес-логики).
            var refillButton = BuildOverlayButton(panel, "RefillButton", fontAsset, "Пополнить веру", 24f, new Vector2(320f, 72f));

            var view = containerGo.GetComponent<LevelStalledView>();
            if (view == null) view = containerGo.AddComponent<LevelStalledView>();

            var so = new SerializedObject(view);
            int wired = 0;
            wired += SetRef(so, "_root", containerGo, "LevelStalledView") ? 1 : 0;
            wired += SetRef(so, "_refillButton", refillButton, "LevelStalledView") ? 1 : 0;
            so.ApplyModifiedProperties();
            LogWireResult("LevelStalledView", wired, 2);
            totalWired += wired;
            totalExpected += 2;
        }

        private static void BuildPauseOverlay(Transform canvasRoot, TMP_FontAsset fontAsset, ref int totalWired, ref int totalExpected)
        {
            var panel = BuildOverlayContainer(canvasRoot, "PauseOverlay", new Vector2(DefaultOverlayPanelWidth, DefaultOverlayPanelHeight));
            var containerGo = panel.parent.gameObject;

            BuildOverlayLabel(panel, "Title", fontAsset, 44f, "Пауза", -40f, new Vector2(300f, 60f), bold: true);
            var resumeButton = BuildOverlayButton(panel, "ResumeButton", fontAsset, "Продолжить", 24f, new Vector2(280f, 72f));

            var view = containerGo.GetComponent<PauseOverlayView>();
            if (view == null) view = containerGo.AddComponent<PauseOverlayView>();

            var so = new SerializedObject(view);
            int wired = 0;
            wired += SetRef(so, "_root", containerGo, "PauseOverlayView") ? 1 : 0;
            wired += SetRef(so, "_resumeButton", resumeButton, "PauseOverlayView") ? 1 : 0;
            so.ApplyModifiedProperties();
            LogWireResult("PauseOverlayView", wired, 2);
            totalWired += wired;
            totalExpected += 2;
        }

        private static void LogWireResult(string ownerLabel, int wired, int expected)
        {
            if (wired == expected)
                Debug.Log($"[Editor] {ownerLabel}: прошито {wired} из {expected}.");
            else
                Debug.LogError($"[Editor] {ownerLabel}: прошито только {wired} из {expected} обязательных полей.");
        }

        // ------------------------------------------------------------------------------------
        // EventSystem (Input System New) — публичный, переиспользуется TestVillageSceneSetupEditor
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// Гарантирует единственный EventSystem в СЦЕНЕ (не в префабе — это singleton уровня сцены)
        /// с <see cref="InputSystemUIInputModule"/> (Input Handling = New, CLAUDE.md — legacy
        /// StandaloneInputModule бросает исключение). Существующий StandaloneInputModule мигрируется
        /// (удаляется, добавляется InputSystem-модуль), не дублируется.
        /// </summary>
        public static void EnsureEventSystem()
        {
            var existing = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            GameObject eventSystemGo;
            if (existing == null)
            {
                eventSystemGo = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
                Undo.AddComponent<EventSystem>(eventSystemGo);
                Debug.Log("[Editor] Создан EventSystem.");
            }
            else
            {
                eventSystemGo = existing.gameObject;
            }

            var legacyModule = eventSystemGo.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Undo.DestroyObjectImmediate(legacyModule);
                Debug.Log("[Editor] StandaloneInputModule удалён с EventSystem — несовместим с Input Handling = New.");
            }

            if (eventSystemGo.GetComponent<InputSystemUIInputModule>() == null)
            {
                Undo.AddComponent<InputSystemUIInputModule>(eventSystemGo);
                Debug.Log("[Editor] InputSystemUIInputModule добавлен на EventSystem.");
            }
        }

        /// <summary>Инстанс HUD-канваса в текущей сцене — по наличию MiraclePanelView (включая неактивные объекты).</summary>
        public static GameObject EnsureSceneInstance(GameObject prefab)
        {
            var existingView = Object.FindAnyObjectByType<MiraclePanelView>(FindObjectsInactive.Include);
            if (existingView != null)
                return existingView.transform.root.gameObject;

            if (prefab == null)
            {
                Debug.LogError("[Editor] LevelHudCanvas.prefab не передан — инстанс в сцене не создан.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(instance, "Create LevelHudCanvas Instance");
            Debug.Log("[Editor] Инстанс LevelHudCanvas.prefab создан в текущей сцене.");
            return instance;
        }

        // ------------------------------------------------------------------------------------
        // Утилиты
        // ------------------------------------------------------------------------------------

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

        private static bool SetEnumProp(SerializedObject so, string propertyName, int enumIndex, string ownerLabel)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[Editor] Поле \"{propertyName}\" не найдено на {ownerLabel} — прошивка пропущена.");
                return false;
            }

            prop.enumValueIndex = enumIndex;
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
