using BlackWhiteLike.Controllers.Village;
using UnityEngine;

namespace BlackWhiteLike.Factories.Village
{
    /// <summary>
    /// Фабрика жителей (game-design §5, §12) — единственное место в проекте, где допустим прямой
    /// вызов <c>IPoolManager</c> для жителей (CLAUDE.md — "доступ к пулу инкапсулируется в фабрике").
    /// </summary>
    public interface IVillagerFactory
    {
        /// <summary>
        /// Выдаёт жителя из пула, инжектит зависимости и вызывает ResetForReuse() — тот
        /// регистрирует жителя в IVillagerStateService и сбрасывает FSM/физику (см. VillagerController).
        /// </summary>
        VillagerController Spawn(Vector3 position, Quaternion rotation);

        /// <summary>
        /// Возвращает жителя в пул; снимает его с учёта в IVillagerStateService только при успешном
        /// возврате (В5 ревью шага 3 — иначе отвергнутый пулом объект остаётся в сцене с
        /// рассинхронизированным FSM-состоянием).
        /// </summary>
        void Despawn(VillagerController villager);
    }
}
