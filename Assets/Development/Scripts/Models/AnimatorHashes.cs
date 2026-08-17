namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Единственный источник хэшей Animator-параметров проекта (см. CLAUDE.md — "Запрещённые практики").
    /// Контракт: каждый параметр Animator объявляется здесь как
    /// <c>public static readonly int XxxHash = UnityEngine.Animator.StringToHash("Xxx");</c>
    /// Наполняется на шаге 3 (жители/анимации) — пока полей нет, класс существует
    /// как зафиксированное место для будущих хэшей, чтобы они не расползлись по контроллерам.
    /// </summary>
    public static class AnimatorHashes
    {
    }
}
