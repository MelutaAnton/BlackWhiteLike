namespace BlackWhiteLike.Services.Haptics
{
    /// <summary>
    /// Тактильный отклик на игровые действия (§19.9, шаг 9 — Mobile polish). Контрактная точка:
    /// на срезе шага 9 регистрируется только no-op заглушка (<see cref="NullHapticsService"/>) —
    /// реализация с настоящей вибрацией (Android <c>Vibrator</c> / iOS Taptic Engine, либо готовый
    /// плагин) добавляется ПОСЛЕ среза, без изменения точек вызова (GodHandController,
    /// MiracleTargetingController, LevelCompletePresenter уже вызывают методы этого интерфейса).
    /// </summary>
    public interface IHapticsService
    {
        /// <summary>Лёгкий отклик — короткие, часто повторяющиеся действия (захват объекта рукой, тап по кнопке UI).</summary>
        void LightTap();

        /// <summary>Средний отклик — заметное однократное действие (бросок объекта рукой, релиз наложенного чуда).</summary>
        void MediumImpact();

        /// <summary>Отклик на успех — победа на уровне.</summary>
        void Success();
    }
}
