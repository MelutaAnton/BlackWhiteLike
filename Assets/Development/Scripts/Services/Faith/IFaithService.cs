namespace BlackWhiteLike.Services.Faith
{
    /// <summary>
    /// Мана Faith (game-design §4, §12) — расходуется чудесами (<c>MiracleService</c>), генерируется
    /// пассивно от числа жителей в состоянии Worship. Pure C#, время приходит аргументом в
    /// <see cref="Tick"/> — тестируется без Unity. Публикует <c>FaithChangedEvent</c> только при
    /// реальном изменении значения.
    /// </summary>
    public interface IFaithService
    {
        /// <summary>Текущее значение Faith, 0..<see cref="Capacity"/>.</summary>
        float Current { get; }

        /// <summary>Потолок Faith (из FaithConfig, задел на апгрейды FaithCapacity, шаг 8).</summary>
        float Capacity { get; }

        /// <summary>Хватает ли Faith на <paramref name="amount"/> прямо сейчас.</summary>
        bool CanAfford(float amount);

        /// <summary>
        /// Списывает <paramref name="amount"/> целиком, если хватает (всё или ничего — частичное
        /// списание не поддерживается). <paramref name="amount"/> ≤ 0 — no-op, возвращает true.
        /// </summary>
        bool TrySpend(float amount);

        /// <summary>Пассивная генерация: RegenPerWorshipper × число жителей в Worship × deltaTime, кламп по Capacity.</summary>
        void Tick(float deltaTime);

        /// <summary>
        /// Мгновенно заполняет Faith до Capacity (game-design §9, "Soft-fail", шаг 5) — вызывается
        /// rewarded-заглушкой "Пополнить веру" (см. <c>LevelStalledPresenter</c>, // ADS_STUB).
        /// Публикует FaithChangedEvent при реальном изменении.
        /// </summary>
        void RefillToCapacity();
    }
}
