namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>MVP-View бара Faith (game-design §4, §10).</summary>
    public interface IFaithBarView
    {
        /// <summary>Обновляет заполнение бара и числовой лейбл.</summary>
        void SetFaith(float current, float capacity);
    }
}
