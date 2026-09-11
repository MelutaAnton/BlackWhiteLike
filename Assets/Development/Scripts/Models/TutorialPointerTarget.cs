namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Цель screen-space указателя туториала (шаг 10 «Polish») — единственный источник значений.
    /// Читается <c>TutorialPresenter</c> для выбора способа проекции (мировая точка vs UI-rect).
    /// </summary>
    public enum TutorialPointerTarget
    {
        /// <summary>Указатель не показывается.</summary>
        None,

        /// <summary>Активная точка нужды (мировая позиция из <c>INeedSiteRegistry</c> — WorldToScreenPoint).</summary>
        ActiveNeedSite,

        /// <summary>Кнопка чуда на панели (<c>TutorialStep.HighlightedMiracle</c> — UI-rect).</summary>
        MiracleButton,

        /// <summary>Шкала Worship на HUD (UI-rect).</summary>
        WorshipBar
    }
}
