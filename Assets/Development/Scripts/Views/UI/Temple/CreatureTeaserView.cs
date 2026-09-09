using UnityEngine;

namespace BlackWhiteLike.Views.UI.Temple
{
    /// <summary>
    /// Заглушка тизера будущего существа (game-design §15, Scalability Hook, шаг 8) — ноль логики,
    /// только SerializeField-якорь под будущий контент. Существо/Progression НЕ реализуются в этом
    /// срезе (предохранитель §15) — TemplePresenter этот компонент не трогает.
    /// </summary>
    public sealed class CreatureTeaserView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
    }
}
