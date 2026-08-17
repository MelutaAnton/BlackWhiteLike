namespace BlackWhiteLike.Controllers.Camera
{
    /// <summary>
    /// Поведение камеры, которое можно активировать/деактивировать (game-design §11).
    /// Переключение между поведениями — через Priority виртуальных камер Cinemachine (CLAUDE.md),
    /// а не через SetActive GameObject'а. <c>CameraService</c> появится на шаге 6, когда поведений
    /// станет 2+ и понадобится единая точка выбора активного; на шаге 2 поведение одно
    /// (<c>IsometricPanZoomBehaviour</c>) и активирует себя самостоятельно при инициализации.
    /// </summary>
    public interface ICameraBehaviour
    {
        bool IsActive { get; }

        void Activate();

        void Deactivate();
    }
}
