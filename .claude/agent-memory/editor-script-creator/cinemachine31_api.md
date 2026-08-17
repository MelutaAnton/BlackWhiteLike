---
name: cinemachine-3-1-editor-api
description: Cinemachine 3.1 class names and namespace confirmed from this project's PackageCache, for use in Editor scripts building camera rigs
metadata:
  type: project
---

Confirmed by reading `Library/PackageCache/com.unity.cinemachine@*/Runtime/Behaviours/CinemachineCamera.cs`
and `CinemachineBrain.cs` in this project.

- Namespace is `Unity.Cinemachine` (NOT `Cinemachine`, that's CM2). `using Unity.Cinemachine;`.
- The single-camera vcam class is `CinemachineCamera : CinemachineVirtualCameraBase` (replaces CM2's
  `CinemachineVirtualCamera`). Has a public `LensSettings Lens` field (default `LensSettings.Default`).
- `CinemachineBrain` goes on the actual `UnityEngine.Camera` GameObject (e.g. Main Camera), same as CM2.
- No `Follow`/`LookAt` needed for a static isometric rig — position/rotate the `CinemachineCamera`'s own
  transform directly (it acts as the camera when no target is set).

Project-specific namespace collision: `BlackWhiteLike.Controllers.Camera` and
`BlackWhiteLike.Data.Configs.Camera` both exist as sub-namespaces, so a bare `Camera` identifier in any
file importing those namespaces resolves to the namespace, not `UnityEngine.Camera` (CS0118). Always
write `UnityEngine.Camera` fully qualified in files under `Editor/Setup/` that also touch
`Controllers.Camera` types (e.g. `CameraRig`). See
`Assets/Development/Scripts/Editor/Setup/TestHandSceneSetupEditor.cs` for the pattern.
