using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BlackWhiteLike.Editor.Setup
{
    /// <summary>
    /// Вставка между шагами 2 и 3 (game-design.md §19.9 описывает то, что этот скрипт НЕ делает —
    /// safe area, хит-зоны, хаптика; это отдельный шаг "Mobile polish"):
    /// первичная настройка PlayerSettings под Android-смоук-сборку — ориентация, идентификация,
    /// скриптинг-бэкенд, Min SDK, уровень качества по умолчанию, Active Input Handling.
    /// Идемпотентен: значение меняется и логируется, только если оно фактически отличается
    /// от нужного; уже настроенные поля молча пропускаются.
    /// Все правки — только через PlayerSettings/EditorUserBuildSettings API, без правки
    /// ProjectSettings/*.asset напрямую (открытый редактор затрёт ручную правку файла).
    /// </summary>
    public static class MobilePlayerSettingsEditor
    {
        private const string CompanyName = "Meluta";
        private const string ProductName = "GodHand";
        private const string AndroidBundleId = "com.meluta.godhand";
        private const string MobileQualityLevelName = "Mobile";

        // activeInputHandler в ProjectSettings.asset: 0 = Input Manager (Old), 1 = Input System
        // Package (New), 2 = Both. Значения проверены чтением ProjectSettings.asset вживую
        // (у проекта до правки стояло 2 — Both), имена enum-констант UnityEditor не выставляет
        // публично для этого поля.
        private const int InputHandlerOld = 0;
        private const int InputHandlerNew = 1;
        private const int InputHandlerBoth = 2;

        [MenuItem("BlackWhiteLike/Setup/Apply Mobile Player Settings")]
        public static void ApplyMobilePlayerSettings()
        {
            ApplyOrientation();
            ApplyIdentification();
            ApplyScriptingBackend();
            ApplyMinSdk();
            ApplyAndroidDefaultQuality();
            ApplyActiveInputHandling();

            Debug.Log("[Editor] Настройка PlayerSettings под мобильную сборку завершена.");
        }

        /// <summary>
        /// Ландшафтная ориентация в обе стороны (решение владельца проекта):
        /// AutoRotation с выключенными portrait-поворотами.
        /// </summary>
        private static void ApplyOrientation()
        {
            SetIfChanged(
                nameof(PlayerSettings.defaultInterfaceOrientation),
                PlayerSettings.defaultInterfaceOrientation,
                UIOrientation.AutoRotation,
                v => PlayerSettings.defaultInterfaceOrientation = v);

            SetIfChanged(
                nameof(PlayerSettings.allowedAutorotateToPortrait),
                PlayerSettings.allowedAutorotateToPortrait,
                false,
                v => PlayerSettings.allowedAutorotateToPortrait = v);

            SetIfChanged(
                nameof(PlayerSettings.allowedAutorotateToPortraitUpsideDown),
                PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                false,
                v => PlayerSettings.allowedAutorotateToPortraitUpsideDown = v);

            SetIfChanged(
                nameof(PlayerSettings.allowedAutorotateToLandscapeLeft),
                PlayerSettings.allowedAutorotateToLandscapeLeft,
                true,
                v => PlayerSettings.allowedAutorotateToLandscapeLeft = v);

            SetIfChanged(
                nameof(PlayerSettings.allowedAutorotateToLandscapeRight),
                PlayerSettings.allowedAutorotateToLandscapeRight,
                true,
                v => PlayerSettings.allowedAutorotateToLandscapeRight = v);
        }

        /// <summary>
        /// Company/Product/BundleId. Рабочее название проекта по game-design — "GodHand",
        /// файл проекта называется BlackWhiteLike — расхождение сознательное, третий вариант
        /// не придумываем, отмечаем в логе.
        /// </summary>
        private static void ApplyIdentification()
        {
            if (PlayerSettings.productName == "BlackWhiteLike")
            {
                Debug.Log("[Editor] Внимание: имя файла проекта — \"BlackWhiteLike\", " +
                          "productName ставим \"GodHand\" по рабочему названию из game-design.md. " +
                          "Расхождение ожидаемое, не баг.");
            }

            SetIfChanged(
                nameof(PlayerSettings.companyName),
                PlayerSettings.companyName,
                CompanyName,
                v => PlayerSettings.companyName = v);

            SetIfChanged(
                nameof(PlayerSettings.productName),
                PlayerSettings.productName,
                ProductName,
                v => PlayerSettings.productName = v);

            var currentBundleId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            SetIfChanged(
                "ApplicationIdentifier(Android)",
                currentBundleId,
                AndroidBundleId,
                v => PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, v));
        }

        /// <summary>
        /// IL2CPP + ARM64 — боевой путь публикации в Google Play (Mono и ARMv7 больше не подходят
        /// как основной таргет), и именно на IL2CPP/ARM64 вылезают проблемы AOT-компиляции,
        /// которых нет в Mono/редакторе — лучше поймать их сейчас, на смоук-сборке, чем в релизе.
        /// </summary>
        private static void ApplyScriptingBackend()
        {
            var currentBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            SetIfChanged(
                "ScriptingBackend(Android)",
                currentBackend,
                ScriptingImplementation.IL2CPP,
                v => PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, v));

            var currentArch = PlayerSettings.Android.targetArchitectures;
            SetIfChanged(
                nameof(PlayerSettings.Android.targetArchitectures),
                currentArch,
                AndroidArchitecture.ARM64,
                v => PlayerSettings.Android.targetArchitectures = v);
        }

        /// <summary>
        /// Min SDK не трогаем, если текущее значение валидно для этой версии редактора.
        /// "Валидно" проверяем не хардкодом числа, а фактическим перечнем уровней, которые
        /// сама эта версия Unity знает (минимальный элемент enum, кроме "Auto") — если текущий
        /// уровень ниже этого минимума, поднимаем до него и логируем.
        /// </summary>
        private static void ApplyMinSdk()
        {
            var current = PlayerSettings.Android.minSdkVersion;

            var knownLevels = Enum.GetValues(typeof(AndroidSdkVersions))
                .Cast<AndroidSdkVersions>()
                .Where(v => v != AndroidSdkVersions.AndroidApiLevelAuto)
                .OrderBy(v => (int)v)
                .ToList();

            if (knownLevels.Count == 0)
            {
                Debug.LogWarning("[Editor] Не удалось определить минимальный поддерживаемый Android API level " +
                                  "этой версии редактора — Min SDK не проверен.");
                return;
            }

            var editorMinimum = knownLevels[0];

            if (current == AndroidSdkVersions.AndroidApiLevelAuto)
            {
                Debug.Log("[Editor] Min SDK выставлен в Auto — оставляем как есть (валидное значение).");
                return;
            }

            if ((int)current < (int)editorMinimum)
            {
                Debug.Log($"[Editor] Min SDK {(int)current} ниже минимума, который поддерживает " +
                          $"эта версия редактора ({(int)editorMinimum}) — поднимаем.");
                PlayerSettings.Android.minSdkVersion = editorMinimum;
            }
            else
            {
                Debug.Log($"[Editor] Min SDK уже валиден: {(int)current} (минимум редактора {(int)editorMinimum}) — не трогаем.");
            }
        }

        /// <summary>
        /// Уровень качества по умолчанию для Android — "Mobile". Публичного PlayerSettings/
        /// EditorUserBuildSettings API для per-platform default quality level в Unity 6 нет
        /// (подтверждено по документации: QualitySettings.SetQualityLevel меняет только ТЕКУЩИЙ
        /// глобальный уровень в редакторе/рантайме, а не дефолт конкретной платформы — тот
        /// выставляется в окне Project Settings → Quality, per-platform матрица).
        /// Читаем фактическое значение через SerializedObject на самом QualitySettings-ассете
        /// (тот же приём, что для TagManager.asset в .claude/rules/editor-files.md) — если оно
        /// уже указывает на "Mobile", ничего не делаем и подтверждаем в логе; если отличается —
        /// пробуем выставить тем же способом, а при неудаче честно предупреждаем и объясняем
        /// ручной шаг, вместо того чтобы делать вид, что всё настроено.
        /// </summary>
        private static void ApplyAndroidDefaultQuality()
        {
            var qualityLevelNames = QualitySettings.names;
            int mobileIndex = Array.IndexOf(qualityLevelNames, MobileQualityLevelName);
            if (mobileIndex < 0)
            {
                Debug.LogWarning($"[Editor] В проекте не найден уровень качества \"{MobileQualityLevelName}\" " +
                                  "— пропускаем настройку дефолтного качества для Android.");
                return;
            }

            var qualityAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qualityAssets == null || qualityAssets.Length == 0)
            {
                LogQualityManualInstruction("не удалось загрузить QualitySettings.asset");
                return;
            }

            var so = new SerializedObject(qualityAssets[0]);
            var perPlatformProp = so.FindProperty("m_PerPlatformDefaultQuality");
            var androidProp = perPlatformProp?.FindPropertyRelative("Android");

            if (androidProp == null)
            {
                LogQualityManualInstruction("поле m_PerPlatformDefaultQuality.Android недоступно через SerializedObject в этой версии Unity");
                return;
            }

            if (androidProp.intValue == mobileIndex)
            {
                Debug.Log($"[Editor] Дефолтный уровень качества для Android уже \"{MobileQualityLevelName}\" (index {mobileIndex}) — не трогаем.");
                return;
            }

            int oldIndex = androidProp.intValue;
            androidProp.intValue = mobileIndex;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Editor] Дефолтный уровень качества для Android: {oldIndex} -> {mobileIndex} (\"{MobileQualityLevelName}\").");
        }

        /// <summary>
        /// Active Input Handling → Input System Package (New). Решение владельца после первого
        /// провала Android-смоук-сборки: Unity ругается, что "Both" не поддерживается на Android
        /// и может влиять на ввод/производительность. Параллельно code-writer переводит
        /// PointerInputProvider с legacy UnityEngine.Input на UnityEngine.InputSystem.
        /// Публичного PlayerSettings-API нет — значение живёт в ProjectSettings.asset как
        /// activeInputHandler, правим тем же приёмом SerializedObject, что и per-platform quality
        /// выше и TagManager.asset в .claude/rules/editor-files.md.
        /// Смена этого поля требует перезапуска редактора — Unity не применяет её "на лету",
        /// поэтому предупреждение в логе обязательно, а не косметика.
        /// </summary>
        private static void ApplyActiveInputHandling()
        {
            var projectSettingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (projectSettingsAssets == null || projectSettingsAssets.Length == 0)
            {
                Debug.LogWarning("[Editor] Не удалось загрузить ProjectSettings.asset — Active Input Handling не проверен. " +
                                  "Проверь вручную: Edit → Project Settings → Player → Other Settings → Active Input Handling.");
                return;
            }

            var so = new SerializedObject(projectSettingsAssets[0]);
            var handlerProp = so.FindProperty("activeInputHandler");
            if (handlerProp == null)
            {
                Debug.LogWarning("[Editor] Поле activeInputHandler недоступно через SerializedObject в этой версии Unity. " +
                                  "Проверь вручную: Edit → Project Settings → Player → Other Settings → Active Input Handling → " +
                                  "\"Input System Package (New)\".");
                return;
            }

            if (handlerProp.intValue == InputHandlerNew)
            {
                Debug.Log("[Editor] Active Input Handling уже \"Input System Package (New)\" — не трогаем.");
                return;
            }

            int oldValue = handlerProp.intValue;
            handlerProp.intValue = InputHandlerNew;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            string oldLabel = oldValue == InputHandlerOld ? "Input Manager (Old)"
                : oldValue == InputHandlerBoth ? "Both"
                : oldValue.ToString();

            Debug.Log($"[Editor] Active Input Handling: \"{oldLabel}\" -> \"Input System Package (New)\".");
            Debug.LogWarning("[Editor] Active Input Handling изменён — Unity должен перезапустить редактор, чтобы " +
                              "применить смену бэкенда ввода. Если авто-перезапуска/диалога не было — перезапусти " +
                              "редактор вручную ПЕРЕД Play Mode или сборкой: до перезапуска ввод может вести себя " +
                              "непредсказуемо (в т.ч. падать с исключением на legacy UnityEngine.Input).");
        }

        private static void LogQualityManualInstruction(string reason)
        {
            Debug.LogWarning("[Editor] Не удалось программно проверить/выставить дефолтный уровень качества " +
                              $"для Android ({reason}). Публичного PlayerSettings-API для этого нет в Unity 6. " +
                              "Проверь вручную: Edit → Project Settings → Quality → в матрице платформ найди " +
                              "строку Android и убедись, что галка стоит на уровне \"Mobile\".");
        }

        private static void SetIfChanged<T>(string fieldLabel, T current, T desired, Action<T> setter)
        {
            if (Equals(current, desired))
            {
                Debug.Log($"[Editor] {fieldLabel} уже \"{desired}\" — не трогаем.");
                return;
            }

            setter(desired);
            Debug.Log($"[Editor] {fieldLabel}: \"{current}\" -> \"{desired}\".");
        }
    }
}
