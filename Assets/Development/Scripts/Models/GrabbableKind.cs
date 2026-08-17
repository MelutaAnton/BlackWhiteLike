namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Тип объекта, реализующего <see cref="Core.Interaction.IGrabbable"/> (game-design §4, §12, §13).
    /// Различает жителя (полноценная FSM, шаг 3) и физический объект (бочка/камень, шаг 2).
    /// </summary>
    public enum GrabbableKind
    {
        Villager,
        Throwable
    }
}
