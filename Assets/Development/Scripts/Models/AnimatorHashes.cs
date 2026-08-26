namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Единственный источник хэшей Animator-параметров проекта (см. CLAUDE.md — "Запрещённые практики").
    /// Контракт: каждый параметр Animator объявляется здесь парой — строковая константа с именем
    /// (используется editor-скриптами, которые строят параметры Animator Controller'а, чтобы имя не
    /// дублировалось в коде и в редакторе) и хэш от неё для рантайма:
    /// <c>public const string XxxName = "Xxx"; public static readonly int Xxx = UnityEngine.Animator.StringToHash(XxxName);</c>
    /// Наполняется на шаге 3 (жители/анимации) — далее продолжит расти по мере появления новых
    /// Animator Controller'ов, чтобы имена параметров не расползлись по контроллерам.
    /// </summary>
    public static class AnimatorHashes
    {
        // Рука бога (game-design §4, шаг 2) — параметры Animator Controller'а GodHand.controller.
        public const string GrabName = "Grab";
        public static readonly int Grab = UnityEngine.Animator.StringToHash(GrabName);

        public const string ReleaseName = "Release";
        public static readonly int Release = UnityEngine.Animator.StringToHash(ReleaseName);

        public const string PlaceName = "Place";
        public static readonly int Place = UnityEngine.Animator.StringToHash(PlaceName);

        public const string IsCarryingName = "IsCarrying";
        public static readonly int IsCarrying = UnityEngine.Animator.StringToHash(IsCarryingName);

        // Житель (game-design §5, шаг 3) — параметры Animator Controller'а Villager.controller.
        public const string MoveSpeedName = "MoveSpeed";
        public static readonly int MoveSpeed = UnityEngine.Animator.StringToHash(MoveSpeedName);

        public const string IsWorshippingName = "IsWorshipping";
        public static readonly int IsWorshipping = UnityEngine.Animator.StringToHash(IsWorshippingName);

        public const string IsStunnedName = "IsStunned";
        public static readonly int IsStunned = UnityEngine.Animator.StringToHash(IsStunnedName);

        public const string ScaredName = "Scared";
        public static readonly int Scared = UnityEngine.Animator.StringToHash(ScaredName);

        // StandUp (ревью плейтеста шага 3, Д2) — триггер выхода из Stunned в Idle (аниматор
        // проигрывает Mixamo "Stand Up" вместо телепорта в Idle).
        public const string StandUpName = "StandUp";
        public static readonly int StandUp = UnityEngine.Animator.StringToHash(StandUpName);

        // IsLying (шестой playtest шага 3) - лежание на земле после жёсткого приземления,
        // отдельно от IsStunned (Dizzy стоя в руке/в полёте) - владелец развёл две склеенные позы.
        public const string IsLyingName = "IsLying";
        public static readonly int IsLying = UnityEngine.Animator.StringToHash(IsLyingName);
    }
}
