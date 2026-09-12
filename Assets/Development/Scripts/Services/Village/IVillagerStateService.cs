using System.Collections.Generic;
using BlackWhiteLike.Models;
using UnityEngine;

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
        /// <param name="worldPosition">
        /// Мировая позиция жителя в момент стимула (шаг 10, волна 2) — издатель (контроллер) знает
        /// <c>transform.position</c>, сервис её не вычисляет, только прокидывает в
        /// <c>VillagerStateChangedEvent.WorldPosition</c> на реальном переходе. null — позиция
        /// неизвестна (например, переход по таймеру внутри <see cref="Tick"/>).
        /// </param>
        void ReportStimulus(VillagerId id, VillagerStimulus stimulus, Vector3? worldPosition = null);

        /// <summary>Тикает таймеры состояний с истечением (Panic/Worship/Stunned) — по истечении возврат в Idle.</summary>
        void Tick(float deltaTime);

        /// <summary>
        /// Число зарегистрированных жителей, находящихся в <paramref name="state"/> прямо сейчас
        /// (шаг 4: FaithService считает реген по числу молящихся). Без аллокаций — линейный проход
        /// по внутреннему словарю записей.
        /// </summary>
        int CountInState(VillagerState state);

        /// <summary>
        /// Таблица game-design §5 "какая нужда пугает жителей рядом" — единственный источник этого
        /// правила, наряду с таблицей переходов внутри сервиса. Только Fire описан как источник
        /// паники; Disease/Hunger по §5 источником паники не являются → false.
        /// </summary>
        bool TryGetPanicStimulus(NeedType type, out VillagerStimulus stimulus);

        /// <summary>
        /// Шаг переноса Disease на жителя: наполняет <paramref name="buffer"/> всеми зарегистрированными
        /// id (сначала очищает его) — <c>VillagerDiseaseService</c> выбирает из них случайного
        /// кандидата на заражение. Без аллокаций, кроме роста самого буфера (переиспользуется
        /// вызывающим между вызовами).
        /// </summary>
        void CollectRegistered(List<VillagerId> buffer);
    }
}
