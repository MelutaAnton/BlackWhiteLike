namespace BlackWhiteLike.Services.Idle
{
    /// <summary>
    /// Офлайн-доход меты (game-design §15, шаг 8) — считается один раз при входе в мета-сцену
    /// (<c>IStartable.Start</c>), не тикается. <see cref="Collect"/> зачисляет посчитанный доход
    /// ровно один раз (повторный вызов — no-op).
    /// </summary>
    /// <remarks>
    /// БЛОКЕР 3 (ревью шага 8): порядок <c>IStartable.Start()</c> между сервисами и презентерами не
    /// гарантирован — если <see cref="IdleIncomeService"/>.Start() тикнет раньше, чем презентер
    /// подписался на <c>IdleIncomeReadyEvent</c>, событие теряется, фича мертва. Pull-свойства ниже —
    /// основной канал: презентеры читают их в СВОЁМ Start() (после регистрации в контейнере состояние
    /// уже посчитано, порядок значения не имеет). Публикация события сохранена как ДОПОЛНЕНИЕ
    /// (контракт §13) — на случай будущего подписчика, которому нужен именно момент появления награды.
    /// </remarks>
    public interface IIdleIncomeService
    {
        /// <summary>Готова ли к сбору посчитанная на старте награда (ещё не собрана).</summary>
        bool HasPendingReward { get; }

        /// <summary>Сумма монет посчитанной награды. 0, если <see cref="HasPendingReward"/> == false.</summary>
        int PendingCoins { get; }

        /// <summary>Число часов офлайна, за которые посчитана награда (после клампа CapHours).</summary>
        float OfflineHours { get; }

        /// <summary>Зачислить посчитанный на старте офлайн-доход через IEconomyService. Без готовой награды — no-op.</summary>
        void Collect();
    }
}
