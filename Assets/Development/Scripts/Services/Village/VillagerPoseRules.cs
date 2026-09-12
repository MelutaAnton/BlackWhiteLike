using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Таблица чистых функций позы/анимации жителя — вынесена из <c>VillagerController</c>
    /// (рефакторинг по итогам архитектурного аудита после шага 3). Это набор статических хелперов
    /// вида <c>Mathf</c>, а не DI-сервис: осознанное отклонение от правила "каждый сервис имеет
    /// интерфейс" — без состояния и без обращений к UnityEngine вовсе, поэтому без интерфейса и без
    /// регистрации в GameLifetimeScope. Тестируется без Unity-сцены.
    /// </summary>
    public static class VillagerPoseRules
    {
        /// <summary>
        /// Правило "когда играть StandUp" (шестой playtest шага 3) — вынесено чистой статической
        /// функцией по тому же принципу, что ComputeUprightRotation/ClampToVillageBounds: тестируется
        /// без Unity-сцены, а не только косвенно через приватный ApplyStateVisuals.
        /// </summary>
        public static bool ShouldPlayStandUp(VillagerState from, VillagerState to) =>
            to == VillagerState.Idle && from == VillagerState.Stunned;

        /// <summary>
        /// Состояния, в которых Rigidbody-скорость НЕ должна влиять на MoveSpeed аниматора (шестой
        /// playtest шага 3: раньше это было завязано на прежний общий IsStunnedVisual, но теперь
        /// IsStunned/IsLying — разные предикаты позы; "когда скорость не в счёт" — третий, отдельный
        /// предикат, который остался общим для обоих состояний, поэтому вынесен отдельно, чтобы
        /// комментарии в FixedUpdate не врали про природу условия).
        /// </summary>
        public static bool ForcesZeroMoveSpeed(VillagerState state) =>
            state == VillagerState.Tossed || state == VillagerState.Stunned;

        /// <summary>
        /// Шаг переноса Disease на жителя: поза agony (Animator.IsSick) показывается ТОЛЬКО больному
        /// жителю в Idle — Panic/Worship/Tossed/Stunned перекрывают её (правило-предикат над группами
        /// состояний, не перечисление переходов, тот же принцип, что ForcesZeroMoveSpeed). Больной,
        /// схваченный рукой (Tossed) или испуганный (Panic), не должен визуально стоять в agony поверх
        /// другой анимации.
        /// </summary>
        public static bool ShowsSickPose(bool isSick, VillagerState state) =>
            isSick && state == VillagerState.Idle;
    }
}
