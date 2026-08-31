namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Контекст ввода (game-design §3, §13, шаг 5) — на что сейчас реагируют потребители pointer'а
    /// (рука, камера, таргетинг чуда). Единственный источник значений; вычисляется
    /// <c>InputRouter</c>'ом из <c>IGameFlowService.Current</c>.
    /// </summary>
    public enum InputContext
    {
        Gameplay,
        UI
    }
}
