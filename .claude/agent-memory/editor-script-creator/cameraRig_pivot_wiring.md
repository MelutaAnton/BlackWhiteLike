---
name: camerarig-pivot-zoom-wiring
description: How CameraRig / IsometricPanZoomBehaviour's pivot-vs-dolly transform hierarchy is meant to be wired by Editor scripts in this project
metadata:
  type: project
---

`BlackWhiteLike.Controllers.Camera.CameraRig` and `IsometricPanZoomBehaviour` (game-design §11,
step 2) share one `Transform _pivot` field each — by design this is the **CameraRig's own
transform**, not a separate child object:

- `_pivot` = the `CameraRig` GameObject's own `transform`. `CameraRig.Construct()` sets
  `_pivot.localRotation = Euler(Pitch, Yaw, 0)` once at runtime; `IsometricPanZoomBehaviour` pans
  by moving `_pivot.position` in world XZ.
- The `CinemachineCamera` lives as a **child** of the pivot, named e.g. `IsoVCam`, with **identity
  local rotation** — it inherits the pivot's pitch/yaw. Its `transform` doubles as
  `IsometricPanZoomBehaviour._zoomTransform`: zoom changes `_zoomTransform.localPosition.z` only
  (dolly distance), never the camera's own rotation.
- `CameraRig._cinemachineCamera` = the vcam component; `CameraRig._panZoomBehaviour` = the
  `IsometricPanZoomBehaviour` component (same GameObject as the rig, since it's the only active
  camera-behaviour on step 2 — see `CameraRig.PanZoomBehaviour` property used by
  `GameLifetimeScope.RegisterHandAndCamera`).

An Editor script assembling this rig from scratch should NOT rotate the vcam's own transform for
pitch — only the pivot (rig root) should carry rotation; the vcam only gets `localPosition =
(0, 0, -distance)` with identity local rotation. Getting this backwards (rotating the vcam instead
of the pivot) still *looks* plausible in the Scene view but breaks pan (which moves the pivot) and
zoom (which assumes the vcam's local -Z is the dolly axis relative to an already-rotated parent).

Used in `Assets/Development/Scripts/Editor/Setup/TestHandSceneSetupEditor.cs`
(`EnsureCameraRig`/`WireCameraRig`).
