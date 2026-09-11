using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Factories.Ui
{
    /// <summary>
    /// Единственная точка доступа к пулу попапов Worship (CLAUDE.md — доступ к IPoolManager
    /// инкапсулируется в фабрике, game-design §10, §13, шаг 5).
    /// </summary>
    public interface IWorshipPopupFactory
    {
        /// <summary>
        /// Показывает попап "+N Awe/Fear". Шаг 10 (волна 2): <paramref name="anchoredPosition"/> —
        /// готовая (уже посчитанная <c>WorshipPopupPresenter</c> через world→screen→canvas проекцию)
        /// позиция в системе координат <see cref="Views.UI.Hud.IWorshipPopupHost.PopupRoot"/>; null —
        /// прежняя дефолтная HUD-точка (исходная позиция префаба под PopupRoot).
        /// </summary>
        void Show(int amount, WorshipSource source, Vector2? anchoredPosition = null);
    }
}
