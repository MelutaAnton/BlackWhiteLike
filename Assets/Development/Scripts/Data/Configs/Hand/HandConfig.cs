using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Hand
{
    /// <summary>
    /// Параметры руки бога (game-design §4, §12). Дефолты подобраны "на глаз" для метрической
    /// сцены (1 unit = 1 метр, кубы тестовой сцены ~0.5-1м) — крутятся по playtest
    /// (game-design §18, Open Question #3).
    /// </summary>
    [CreateAssetMenu(fileName = "HandConfig", menuName = "BlackWhiteLike/Hand Config")]
    public sealed class HandConfig : ScriptableObject
    {
        [Header("Захват")]
        [Tooltip("Максимальная масса Rigidbody, которую можно взять рукой.")]
        [SerializeField] private float _maxGrabMass = 20f;

        // GrabRadius удалён (ревью шага 2, Б1+Б3): рука и камера обязаны считать "что под pointer'ом"
        // одинаково — через единственный IPointerWorldProjector.TryPickGrabbable (тонкий raycast,
        // без отдельного радиуса допуска). Отдельный физический допуск у руки развёл бы критерии
        // снова: камера могла бы не увидеть грабблу под пальцем (и начать pan), а рука — взять его же
        // за счёт своего радиуса, ровно тот конфликт, который этот шаг фиксит.

        [Tooltip("Высота, на которой рука держит объект над точкой земли под pointer'ом.")]
        [SerializeField] private float _carryHeight = 1.5f;

        [Header("Удержание (velocity-follow, Open Question #1 — без joint)")]
        [Tooltip("Коэффициент velocity-follow: velocity = (target - current) * FollowStiffness.")]
        [SerializeField] private float _followStiffness = 12f;

        [Tooltip("Кламп модуля скорости следования Rigidbody за точкой в ладони.")]
        [SerializeField] private float _followMaxSpeed = 15f;

        [Header("Бросок")]
        [Tooltip("Множитель скорости жеста (из истории позиций pointer'а) в импульс броска.")]
        [SerializeField] private float _throwForceScale = 1.2f;

        [Tooltip("Кламп модуля импульса броска.")]
        [SerializeField] private float _maxThrowForce = 20f;

        [Tooltip("Окно (сек) истории позиций pointer'а, по которому считается скорость жеста при отпускании.")]
        [SerializeField] private float _throwVelocitySampleWindow = 0.15f;

        [Tooltip(
            "Порог модуля скорости отпускания (м/с), ниже которого это мягкая постановка, а не бросок " +
            "(вместо сравнения с точным нулём в контроллере, см. IHandInteractionService.IsSoftPlacement).")]
        [SerializeField] private float _softPlacementSpeedThreshold = 0.05f;

        [Header("Приземление")]
        // TODO шаг 3: когда появятся структуры (дома и т.п.), возможно, имеет смысл вынести в отдельный
        // конфиг физических объектов (ThrowableConfig) — сейчас намеренно оставлено в HandConfig,
        // отдельный конфиг под один параметр создавать преждевременно (ревью шага 2).
        [Tooltip("Порог относительной скорости столкновения, начиная с которого публикуется ObjectLandedEvent.")]
        [SerializeField] private float _landingImpactThreshold = 3f;

        public float MaxGrabMass => _maxGrabMass;
        public float CarryHeight => _carryHeight;
        public float FollowStiffness => _followStiffness;
        public float FollowMaxSpeed => _followMaxSpeed;
        public float ThrowForceScale => _throwForceScale;
        public float MaxThrowForce => _maxThrowForce;
        public float ThrowVelocitySampleWindow => _throwVelocitySampleWindow;
        public float SoftPlacementSpeedThreshold => _softPlacementSpeedThreshold;
        public float LandingImpactThreshold => _landingImpactThreshold;
    }
}
