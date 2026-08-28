---
name: cfxr-clearbehavior-and-tmp-ugui-asmdefs
description: CFXR_Effect.clearBehavior field/enum values for pooled ThirdParty VFX, and the exact assembly names to add to an Editor asmdef for TMPro/UGUI/InputSystem UI work
metadata:
  type: project
---

Confirmed by reading source in this project (Unity 6000.3.9f1):

## CFXR (JMO Cartoon FX Remaster) — pooled explosion prefabs
`Assets/ThirdParty/VFX/JMO Assets/Cartoon FX Remaster/CFXR Assets/Scripts/CFXR_Effect.cs` has
`public enum ClearBehavior { None, Disable, Destroy }` and `public ClearBehavior clearBehavior =
ClearBehavior.Destroy;` (public field, not `[SerializeField]` private — still works with
`SerializedObject.FindProperty("clearBehavior")`, enumValueIndex 0=None/1=Disable/2=Destroy).
Default `Destroy` will delete a pooled instance after its first play — any Editor script wrapping
a CFXR prefab in our own pooled VFX wrapper (`VfxInstanceController` pattern) must force this to 0
(None) and verify numerically after saving.

**Owner rule since 2026-08-29 (CLAUDE.md «Из ThirdParty в сцену... только визуал»):** a ThirdParty
prefab is NEVER kept as a nested prefab instance inside our prefab (nor in a scene). Instantiate it
via `EditorPrefabUtility.InstantiateUnpacked` (InstantiatePrefab + UnpackPrefabInstance Completely),
so `clearBehavior = 0` is written as a plain value on the unpacked component, not as an override.
Bare `PrefabUtility.InstantiatePrefab` on an `Assets/ThirdParty/` asset is a violation — run
`BlackWhiteLike/Validation/Find ThirdParty Prefab Instances` after any script that creates objects.

This type lives in its own asmdef ("CFXR Runtime", `Assets/ThirdParty/.../CFXR Assets/Scripts/CFXR
Runtime.asmdef") which our `BlackWhiteLike.Editor` assembly does NOT reference — `typeof(CFXR_Effect)`
does not compile there. Find the component by `GetComponentsInChildren<Component>(true)` and match
`component.GetType().Name == "CFXR_Effect"` instead.

## TMPro / UGUI / InputSystem UI assembly names (Unity 6, com.unity.ugui 2.0.0)
TextMeshPro now ships bundled inside `com.unity.ugui` (no separate `com.unity.textmeshpro` package
entry). Assembly names to add to an Editor asmdef's `references` array for HUD-building scripts:
- `"Unity.TextMeshPro"` — runtime TMPro types (`TMP_FontAsset`, `TMP_Settings`, `TextMeshProUGUI`).
- `"Unity.TextMeshPro.Editor"` — `TMPro.EditorUtilities.TMP_EditorUtility` (has
  `packageFullPath` static prop, used to build the path to `TMP Essential Resources.unitypackage`
  for a **non-interactive** `AssetDatabase.ImportPackage(path, interactive: false)` — the stock
  Window/TextMeshPro/Import menu item uses `interactive: true` which opens a blocking import
  window, same class of problem as `EditorUtility.DisplayDialog` in `.claude/rules/editor-files.md`).
- `"UnityEngine.UI"` — `Button`, `Image`, `CanvasScaler`, `GraphicRaycaster`, `HorizontalLayoutGroup`.
- `"Unity.InputSystem"` — also needed in the Editor asmdef (not just the runtime one) for
  `UnityEngine.InputSystem.UI.InputSystemUIInputModule` when an Editor script builds/migrates the
  scene `EventSystem` (Input Handling = New forbids the legacy `StandaloneInputModule`,
  `UnityEngine.EventSystems` namespace, ships with UGUI not InputSystem).

`AssetDatabase.FindAssets("t:TMP_Settings")` empty-check is the reliable "are Essentials imported"
signal (`TMP_Settings.instance` can itself throw/log while probing).

Both asmdef edits (adding these 4 references) were done directly on
`Assets/Development/Scripts/Editor/BlackWhiteLike.Editor.asmdef` in step 4 (Miracles + Faith) —
check this file first before re-adding references in future editor scripts touching HUD/VFX-import.
