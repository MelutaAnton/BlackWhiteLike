using BlackWhiteLike.Data.Configs.Camera;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Camera
{
    /// <summary>
    /// Держатель изометрической камеры (game-design §11). Только Unity API: применяет фиксированный
    /// наклон (Pitch/Yaw) из <see cref="IsometricCameraConfig"/> к pivot-трансформу один раз при
    /// старте и отдаёт наружу активное поведение (<see cref="PanZoomBehaviour"/>) — на шаге 6
    /// выбором активного поведения между несколькими займётся <c>CameraService</c> по Priority
    /// виртуальных камер Cinemachine (CLAUDE.md).
    /// </summary>
    /// <remarks>
    /// Внутри этого файла (namespace заканчивается на "Camera") имя <c>UnityEngine.Camera</c>
    /// всегда квалифицируется полностью — bare "Camera" схлопнулся бы в текущий namespace (CS0118,
    /// см. CLAUDE.md, чек-лист планирования, п.3). В этом файле Camera-типов не используется,
    /// но правило соблюдается на будущее.
    /// </remarks>
    public sealed class CameraRig : MonoBehaviour
    {
        [Tooltip("Cinemachine-камера рига (Cinemachine 3.1 — CinemachineCamera, не CinemachineVirtualCamera из CM2).")]
        [SerializeField] private CinemachineCamera _cinemachineCamera;

        [Tooltip("Pivot-трансформ: Pitch/Yaw и pan применяются к нему; камера — его ребёнок на dolly-дистанции зума.")]
        [SerializeField] private Transform _pivot;

        [Tooltip("Единственное на шаге 2 поведение камеры — pan/zoom.")]
        [SerializeField] private IsometricPanZoomBehaviour _panZoomBehaviour;

        /// <summary>Активное поведение камеры. Шаг 2 — единственное; GameLifetimeScope регистрирует его через это свойство.</summary>
        public IsometricPanZoomBehaviour PanZoomBehaviour => _panZoomBehaviour;

        [Inject]
        public void Construct(IsometricCameraConfig cameraConfig)
        {
            if (_pivot == null)
            {
                Debug.LogWarning("[CameraRig] _pivot не прошит — наклон камеры не применён.");
                return;
            }

            _pivot.localRotation = Quaternion.Euler(cameraConfig.Pitch, cameraConfig.Yaw, 0f);
        }
    }
}
