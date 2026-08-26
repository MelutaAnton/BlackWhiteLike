using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Throwable
{
    /// <summary>
    /// Параметры физического броскового объекта (game-design §7, §12). Порог приземления
    /// перенесён сюда из <c>HandConfig</c> на шаге 3 (TODO из ревью шага 2): с появлением
    /// второго типа <c>IGrabbable</c> (житель, свой порог в VillagerConfig) один общий порог
    /// на бочку и человека перестал быть выразим одним числом. Ссылка на этот конфиг — прямое
    /// поле на самом префабе (<c>ThrowableObjectController</c>), не через DI: разные виды броскового
    /// декора (бочка/камень) смогут держать разные .asset без правки Composition Root.
    /// </summary>
    [CreateAssetMenu(fileName = "ThrowableConfig", menuName = "BlackWhiteLike/Throwable Config")]
    public sealed class ThrowableConfig : ScriptableObject
    {
        [Tooltip("Порог относительной скорости столкновения, начиная с которого публикуется ObjectLandedEvent.")]
        [SerializeField] private float _landingImpactThreshold = 3f;

        public float LandingImpactThreshold => _landingImpactThreshold;
    }
}
