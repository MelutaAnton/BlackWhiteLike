using System;

namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Типобезопасная обёртка над InstanceID объекта (game-design §12, §13) — идентификатор
    /// <see cref="Core.Interaction.IGrabbable"/> в событиях руки (<c>Core/Events/Events.Hand.cs</c>).
    /// Обёртка над <c>int</c>, а не голый <c>int</c>, — чтобы сигнатуры событий/сервисов не путали
    /// идентификатор объекта с произвольным числом (масса, урон и т.п.).
    /// </summary>
    public readonly struct GrabbableId : IEquatable<GrabbableId>
    {
        public readonly int Value;

        private GrabbableId(int value)
        {
            Value = value;
        }

        /// <summary>Строит идентификатор из <c>GameObject.GetInstanceID()</c> захватываемого объекта.</summary>
        public static GrabbableId FromInstanceId(int instanceId) => new GrabbableId(instanceId);

        public bool Equals(GrabbableId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GrabbableId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(GrabbableId left, GrabbableId right) => left.Equals(right);
        public static bool operator !=(GrabbableId left, GrabbableId right) => !left.Equals(right);
    }
}
