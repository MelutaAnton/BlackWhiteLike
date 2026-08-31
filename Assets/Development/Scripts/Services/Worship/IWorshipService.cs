namespace BlackWhiteLike.Services.Worship
{
    /// <summary>
    /// Единая шкала Worship 0..Target (game-design §4, §7, §12, §13, шаг 5) — источники: Awe за
    /// разрешённую нужду игрока, Fear за акт террора игрока (величины из <c>WorshipConfig</c>).
    /// Pure C#, время приходит аргументом в <see cref="Tick"/> — тестируется без Unity. Публикует
    /// <c>WorshipChangedEvent</c> только при реальном изменении <see cref="Current"/>.
    /// </summary>
    public interface IWorshipService
    {
        /// <summary>Текущее значение Worship, 0..<see cref="Target"/>.</summary>
        int Current { get; }

        /// <summary>Порог победы уровня (<c>LevelConfig.WorshipTarget</c>).</summary>
        int Target { get; }

        /// <summary>Накопленная (некламплённая по Target) сумма Awe за уровень — для экрана итогов.</summary>
        int AweTotal { get; }

        /// <summary>Накопленная (некламплённая по Target) сумма Fear за уровень — для экрана итогов.</summary>
        int FearTotal { get; }

        /// <summary>true, если <see cref="Current"/> достиг <see cref="Target"/>.</summary>
        bool IsTargetReached { get; }

        /// <summary>Пассивное затухание Worship (WorshipConfig.WorshipDecayPerSecond, по умолчанию 0 — выключено).</summary>
        void Tick(float dt);
    }
}
