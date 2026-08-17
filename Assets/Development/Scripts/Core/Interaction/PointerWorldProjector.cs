using UnityEngine;

namespace BlackWhiteLike.Core.Interaction
{
    /// <summary>
    /// Реализация <see cref="IPointerWorldProjector"/>. Живёт в Core/ (как <c>Core/Input/PointerInputProvider</c>) —
    /// это переиспользуемая инфраструктура ввода, а не бизнес-правило, поэтому обращение к
    /// UnityEngine.Camera/Physics здесь допустимо (в отличие от Services/, см. CLAUDE.md).
    /// Регистрируется в GameLifetimeScope как синглтон.
    /// </summary>
    /// <remarks>
    /// <see cref="UnityEngine.Camera.main"/> берётся напрямую, а не через DI-инъекцию камеры —
    /// решение зафиксировано в ревью шага 2 ("на будущее"), пересмотр — по отдельному запросу.
    /// </remarks>
    public sealed class PointerWorldProjector : IPointerWorldProjector
    {
        public bool TryProjectToGround(Vector2 screenPosition, LayerMask groundMask, out Vector3 worldPoint)
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                worldPoint = default;
                return false;
            }

            var ray = camera.ScreenPointToRay(screenPosition);

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, groundMask))
            {
                worldPoint = hit.point;
                return true;
            }

            // Fallback — математическая плоскость y=0: работает, даже если земля сцены ещё не
            // размечена нужным слоем (например, тестовая сцена собрана не до конца).
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out var distance))
            {
                worldPoint = ray.GetPoint(distance);
                return true;
            }

            worldPoint = default;
            return false;
        }

        public bool TryPickGrabbable(Vector2 screenPosition, LayerMask grabbableMask, out IGrabbable grabbable)
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                grabbable = null;
                return false;
            }

            var ray = camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, grabbableMask) &&
                hit.collider.TryGetComponent(out grabbable))
            {
                return true;
            }

            grabbable = null;
            return false;
        }
    }
}
