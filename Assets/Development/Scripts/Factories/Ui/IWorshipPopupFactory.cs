using BlackWhiteLike.Models;

namespace BlackWhiteLike.Factories.Ui
{
    /// <summary>
    /// Единственная точка доступа к пулу попапов Worship (CLAUDE.md — доступ к IPoolManager
    /// инкапсулируется в фабрике, game-design §10, §13, шаг 5).
    /// </summary>
    public interface IWorshipPopupFactory
    {
        /// <summary>Показывает попап "+N Awe/Fear", заякоренный к Worship-бару HUD (<see cref="Views.UI.Hud.IWorshipPopupHost"/>).</summary>
        void Show(int amount, WorshipSource source);
    }
}
