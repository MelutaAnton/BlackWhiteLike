using System;

namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Типобезопасная обёртка над InstanceID жителя (по образцу <see cref="GrabbableId"/>,
    /// game-design §5, §12) — идентификатор в <c>IVillagerStateService</c> и
    /// <c>VillagerStateChangedEvent</c> (<c>Core/Events/Events.Village.cs</c>). Отдельный тип от
    /// <see cref="GrabbableId"/>, хотя оба строятся из одного и того же <c>GetInstanceID()</c>
    /// GameObject'а жителя, — домен руки (что можно взять) и домен деревни (FSM жителя) не должны
    /// перепутать чужой идентификатор через сигнатуру метода.
    /// </summary>
    public readonly struct VillagerId : IEquatable<VillagerId>
    {
        public readonly int Value;

        private VillagerId(int value)
        {
            Value = value;
        }

        /// <summary>Строит идентификатор из <c>GameObject.GetInstanceID()</c> жителя.</summary>
        public static VillagerId FromInstanceId(int instanceId) => new VillagerId(instanceId);

        public bool Equals(VillagerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is VillagerId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(VillagerId left, VillagerId right) => left.Equals(right);
        public static bool operator !=(VillagerId left, VillagerId right) => !left.Equals(right);
    }
}
