using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Core.Events
{
    /// <summary>Публикуется <c>GodHandController</c> в момент захвата объекта (game-design §13).</summary>
    public readonly struct ObjectGrabbedEvent
    {
        public readonly GrabbableId Id;
        public readonly GrabbableKind Kind;

        public ObjectGrabbedEvent(GrabbableId id, GrabbableKind kind)
        {
            Id = id;
            Kind = kind;
        }
    }

    /// <summary>
    /// Публикуется при отпускании объекта рукой (game-design §13). <see cref="Velocity"/> ~ Vector3.zero
    /// означает мягкую постановку, а не бросок — источник Fear (game-design §7) считается по величине скорости.
    /// </summary>
    public readonly struct ObjectThrownEvent
    {
        public readonly GrabbableId Id;
        public readonly Vector3 Velocity;

        public ObjectThrownEvent(GrabbableId id, Vector3 velocity)
        {
            Id = id;
            Velocity = velocity;
        }
    }

    /// <summary>
    /// Публикуется при физическом приземлении/ударе брошенного объекта (game-design §13).
    /// Издатели: <c>ThrowableObjectController</c> (шаг 2), <c>VillagerController</c> (шаг 3).
    /// </summary>
    public readonly struct ObjectLandedEvent
    {
        public readonly GrabbableId Id;
        public readonly float ImpactSpeed;
        public readonly bool HitStructure;

        public ObjectLandedEvent(GrabbableId id, float impactSpeed, bool hitStructure)
        {
            Id = id;
            ImpactSpeed = impactSpeed;
            HitStructure = hitStructure;
        }
    }
}
