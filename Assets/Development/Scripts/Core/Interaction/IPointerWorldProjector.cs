using UnityEngine;

namespace BlackWhiteLike.Core.Interaction
{
    /// <summary>
    /// Единственный источник ответа на "что под pointer'ом" (game-design §3, §4, §11). И рука
    /// (захват), и камера (gate pan'а на IGrabbable, проекция на землю для drag'а) обязаны ходить
    /// сюда, а не дублировать raycast каждая у себя — до этого хелпера у них были независимые
    /// критерии (луч руки по маске земли, луч камеры по маске грабблов), которые расходились и
    /// давали то мёртвый захват, то одновременный pan+захват (ревью шага 2, Б1+Б3).
    /// </summary>
    public interface IPointerWorldProjector
    {
        /// <summary>
        /// Проецирует экранную точку на землю: сперва raycast по <paramref name="groundMask"/>,
        /// при промахе — fallback на математическую плоскость y=0 (работает, даже если земля сцены
        /// ещё не размечена нужным слоем).
        /// </summary>
        bool TryProjectToGround(Vector2 screenPosition, LayerMask groundMask, out Vector3 worldPoint);

        /// <summary>Raycast по <paramref name="grabbableMask"/> и попытка получить <see cref="IGrabbable"/> с попадания.</summary>
        bool TryPickGrabbable(Vector2 screenPosition, LayerMask grabbableMask, out IGrabbable grabbable);
    }
}
