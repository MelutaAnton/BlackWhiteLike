---
name: prefab-content-editing-for-idempotent-rebuild
description: Pattern for rebuilding a prefab's generated internals on every Editor-script run while preserving user-tweaked top-level properties (e.g. root scale)
metadata:
  type: project
---

When an Editor script must fully regenerate a prefab's derived hierarchy (visual mesh, animator,
anchors) on every run, but must NOT clobber a value the user hand-tuned in the Inspector (e.g. root
`localScale` after playtest feel adjustments), use `PrefabUtility.LoadPrefabContents` /
`SaveAsPrefabAsset` / `UnloadPrefabContents` instead of instantiating fresh + `SaveAsPrefabAsset`:

```csharp
bool existed = File.Exists(prefabPath);
GameObject root = existed
    ? PrefabUtility.LoadPrefabContents(prefabPath)   // edits the actual asset content in isolation
    : new GameObject("Name");

// ...destroy + rebuild only the derived child hierarchy (e.g. "Visual") every time...
// ...only set defaults (e.g. root.transform.localScale) when !existed...

if (existed)
{
    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    PrefabUtility.UnloadPrefabContents(root);
}
else
{
    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    Object.DestroyImmediate(root);
}
```

This avoids polluting the open scene (no scene instance needed just to author the prefab asset) and
naturally satisfies "rebuild derived content, preserve user-tuned top-level fields" idempotency rules
required by `.claude/rules/editor-files.md`.

Used in `Assets/Development/Scripts/Editor/Setup/GodHandPrefabSetupEditor.cs` (rebuilds the `Visual`
child + AnimatorController/material wiring every run, but leaves an already-adjusted root scale alone).
