---
name: vcontainer-autoinjectgameobjects
description: Field name and idempotent SerializedObject-list-append pattern for wiring plain scene GameObjects into VContainer's auto-inject list
metadata:
  type: project
---

Confirmed by reading `Library/PackageCache/jp.hadashikick.vcontainer@*/Runtime/Unity/LifetimeScope.cs`
(this project pins `jp.hadashikick.vcontainer@dcf16cda6a65`).

- Field is `protected [SerializeField] List<GameObject> autoInjectGameObjects;` on the base
  `LifetimeScope` class (not `GameLifetimeScope` itself — it's inherited). `protected` does not
  block `SerializedObject.FindProperty("autoInjectGameObjects")`; serialization visibility is
  driven by `[SerializeField]`, not the access modifier.
- This is the fix for the project's known VContainer gotcha (CLAUDE.md): plain scene GameObjects
  (not registered via `RegisterComponent`) never get `[Inject] Construct()` called on them unless
  they're in this list. Editor scripts that spawn scene objects carrying `[Inject]`-dependent
  MonoBehaviours (e.g. `ThrowableObjectController`) MUST add them here, not just instantiate them.
- Idempotent append pattern (avoid duplicates on rerun, don't clobber entries added by other
  scripts): read existing `objectReferenceValue`s into a `HashSet<GameObject>` first, then only
  `InsertArrayElementAtIndex` for objects not already present.

```csharp
var so = new SerializedObject(scope);
var listProp = so.FindProperty("autoInjectGameObjects");
var existing = new HashSet<GameObject>();
for (int i = 0; i < listProp.arraySize; i++)
{
    var go = listProp.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
    if (go != null) existing.Add(go);
}
foreach (var candidate in newObjects)
{
    if (candidate == null || existing.Contains(candidate)) continue;
    int index = listProp.arraySize;
    listProp.InsertArrayElementAtIndex(index);
    listProp.GetArrayElementAtIndex(index).objectReferenceValue = candidate;
    existing.Add(candidate);
}
so.ApplyModifiedProperties();
```

Used in `Assets/Development/Scripts/Editor/Setup/TestHandSceneSetupEditor.cs`
(`EnsureAutoInjectGameObjects`, wires the 8 test-scene throwable cubes).
