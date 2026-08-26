using UnityEngine;

namespace BlackWhiteLike.Core.Pool
{
    /// <summary>
    /// Универсальный пул GameObject'ов (game-design §5, §12) — жители, VFX, UI-попапы (по мере
    /// появления фабрик для них). Ключ пула — сам префаб (<c>GetInstanceID()</c>), без строковых
    /// идентификаторов (CLAUDE.md — единственный источник идентификатора: раз ключа-строки нет
    /// вовсе, ему нечего дублировать). Единственные легальные вызыватели — фабрики (CLAUDE.md,
    /// "Доступ к пулу инкапсулируется в фабрике").
    /// </summary>
    public interface IPoolManager
    {
        /// <summary>
        /// Выдаёт инстанс префаба (новый или переиспользованный) в указанной позиции/повороте,
        /// уже <c>SetActive(true)</c>. <typeparamref name="T"/> — компонент, который должен быть
        /// на корне префаба; если его нет — ошибка в лог, возвращает null.
        /// </summary>
        T Get<T>(GameObject prefab, Vector3 position, Quaternion rotation) where T : Component;

        /// <summary>
        /// Возвращает инстанс в пул: <c>SetActive(false)</c> и в стек свободных. Объект, не
        /// выданный этим пулом (или уже возвращённый), — <c>Debug.LogWarning</c> и игнор,
        /// не исключение. Возвращает true, если объект реально был возвращён — вызывающая сторона
        /// (например, VillagerFactory.Despawn, В5 ревью шага 3) обязана снимать объект с учёта
        /// в других системах (IVillagerStateService и т.п.) только при true, иначе состояние вроде
        /// FSM рассинхронизируется с объектом, оставшимся в сцене.
        /// </summary>
        bool Release(GameObject instance);

        /// <summary>Заранее создаёт <paramref name="count"/> неактивных инстансов префаба.</summary>
        void Prewarm(GameObject prefab, int count);
    }
}
