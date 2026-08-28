using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Hand
{
    /// <summary>
    /// Режим руки бога (game-design §3, §4): <c>Grab</c> (палец = рука, хватает/бросает) или
    /// <c>Miracle</c> (выбрано чудо — тап = каст). Переключается выбором в панели чудес
    /// (<c>MiraclePanelPresenter</c>) или отменой того же чуда повторным тапом на его иконку.
    /// Pure C# — публикует <c>MiracleSelectedEvent</c> при каждом реальном изменении выбора.
    /// </summary>
    public interface IHandModeService
    {
        /// <summary>Текущий режим руки. Старт — <see cref="HandMode.Grab"/> (чудо не выбрано).</summary>
        HandMode Mode { get; }

        /// <summary>Текущее выбранное чудо, если <see cref="Mode"/> == Miracle.</summary>
        bool TryGetSelectedMiracle(out MiracleType type);

        /// <summary>
        /// Выбирает чудо. Повторный выбор того же типа — отмена (возврат в Grab, публикует
        /// Selected=false для отменённого). Выбор другого типа во время активного выбора —
        /// переключение: публикует Selected=false для старого и Selected=true для нового.
        /// </summary>
        void SelectMiracle(MiracleType type);

        /// <summary>Явная отмена выбора (например, тап по пустому месту вне цели чуда, шаг 5+) — возврат в Grab.</summary>
        void ClearSelection();
    }
}
