using UnityEngine;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// HUD-хост попапов Worship (game-design §10, §13, шаг 5) — попапы якорятся к Worship-бару,
    /// мировых координат в событиях нет (решение владельца, шаг 5). Реализует <c>LevelHudView</c>.
    /// </summary>
    public interface IWorshipPopupHost
    {
        WorshipPopupView PopupPrefab { get; }
        RectTransform PopupRoot { get; }
    }
}
