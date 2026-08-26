using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// FSM жителя (game-design §5): <c>Idle → Panic → Worship → Tossed → Stunned → Idle</c>.
    /// Pure C# — состояние держится по каждому жителю отдельно (не единая глобальная FSM), время
    /// всегда приходит аргументом в <see cref="Tick"/> (не читается из UnityEngine.Time внутри) —
    /// тестируется без Unity. Публикует <c>VillagerStateChangedEvent</c> на каждый реальный переход.
    /// </summary>
    public interface IVillagerStateService
    {
        /// <summary>Регистрирует нового жителя в состоянии Idle. Повторная регистрация уже известного id — предупреждение, без изменения записи.</summary>
        void Register(VillagerId id);

        /// <summary>Снимает запись жителя (например, при возврате в пул). Отсутствующий id — безопасный no-op.</summary>
        void Unregister(VillagerId id);

        /// <summary>Текущее состояние. Незарегистрированный id — предупреждение и Idle по умолчанию.</summary>
        VillagerState GetState(VillagerId id);

        /// <summary>
        /// Сообщает о внешнем стимуле (game-design §5: рядом пожар/взрыв → Panic; нужда разрешена
        /// рядом → Worship; брошен рукой → Tossed; приземлился/приземлился жёстко → Idle/Stunned).
        /// Запрещённый для текущего состояния переход (например, стимул во время Tossed/Stunned,
        /// кроме тех, что явно разрешены) молча игнорируется.
        /// </summary>
        void ReportStimulus(VillagerId id, VillagerStimulus stimulus);

        /// <summary>Тикает таймеры состояний с истечением (Panic/Worship/Stunned) — по истечении возврат в Idle.</summary>
        void Tick(float deltaTime);
    }
}
