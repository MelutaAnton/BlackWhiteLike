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
    }
}
