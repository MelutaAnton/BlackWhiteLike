using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Camera
{
    /// <summary>
    /// Параметры изометрической камеры (game-design §11, §12). Решение владельца (шаг 2):
    /// камера перспективная, зум = изменение дистанции dolly (не FOV), наклон фиксированный,
    /// вращения нет — <c>CameraRig</c> выставляет Pitch/Yaw один раз при старте.
    /// </summary>
    [CreateAssetMenu(fileName = "IsometricCameraConfig", menuName = "BlackWhiteLike/Isometric Camera Config")]
    public sealed class IsometricCameraConfig : ScriptableObject
    {
        [Header("Ориентация (фиксированная, без вращения игроком)")]
        [Tooltip("Наклон pivot'а рига вокруг локальной X, градусы.")]
        [SerializeField] private float _pitch = 45f;

        [Tooltip("Поворот pivot'а вокруг Y, градусы — фиксированный ракурс уровня.")]
        [SerializeField] private float _yaw = 0f;

        [Header("Zoom (dolly-дистанция камеры от pivot'а, метры)")]
        [SerializeField] private float _minZoomDistance = 6f;
        [SerializeField] private float _maxZoomDistance = 20f;
        [SerializeField] private float _defaultZoomDistance = 12f;
        [Tooltip("Скорость изменения dolly-дистанции на единицу IInputProvider.Zoom.")]
        [SerializeField] private float _zoomSpeed = 1f;

        [Header("Pan")]
        [Tooltip(
            "База для порога аномалии pan-дельты (мир.юниты за кадр = PanSpeed × множитель, см. " +
            "IsometricPanZoomBehaviour) — НЕ прямой множитель дельты: обычный pan идёт 1:1 с миром, " +
            "а не 'дельта экрана × скорость' (см. .claude/rules и game-design §3). Порог существует, " +
            "чтобы отловить аномальный скачок raycast'а (просадка FPS), а не штатный быстрый флик.")]
        [SerializeField] private float _panSpeed = 30f;

        [Tooltip(
            "Половина стороны квадрата вокруг (0,0), за пределы которого pan не пускает pivot камеры. " +
            "Временная заглушка шага 2 — настоящие границы придут из VillageLayoutConfig на шаге 7.")]
        [SerializeField] private float _panBoundsHalfExtent = 25f;

        public float Pitch => _pitch;
        public float Yaw => _yaw;

        // Названы MinZoom/MaxZoom (не MinZoomDistance/MaxZoomDistance) — так же, как в game-design §11
        // и как их использует editor-скрипт сборки тестовой сцены (TestHandSceneSetupEditor).
        public float MinZoom => _minZoomDistance;
        public float MaxZoom => _maxZoomDistance;
        public float DefaultZoomDistance => _defaultZoomDistance;
        public float ZoomSpeed => _zoomSpeed;
        public float PanSpeed => _panSpeed;
        public float PanBoundsHalfExtent => _panBoundsHalfExtent;

        // НБ2 (ревью шага 2): без OnValidate дизайнер мог выставить DefaultZoomDistance вне
        // Min..Max или Max < Min прямо в Inspector, и это тихо ломало зум в рантайме.
        private void OnValidate()
        {
            _minZoomDistance = Mathf.Max(0f, _minZoomDistance);
            _maxZoomDistance = Mathf.Max(_minZoomDistance, _maxZoomDistance);
            _defaultZoomDistance = Mathf.Clamp(_defaultZoomDistance, _minZoomDistance, _maxZoomDistance);
        }
    }
}
