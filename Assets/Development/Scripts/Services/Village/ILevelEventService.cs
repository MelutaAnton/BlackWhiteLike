using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Планировщик событий-нужд уровня (game-design §5, §6). Pure C# — читает скриптованный
    /// таймлайн (<c>LevelConfig.EventTimeline</c>) и триггерит записи строго по времени; никакой
    /// генерации случайных событий (cli-workflow §Шаг 3). Время всегда приходит аргументом в
    /// <see cref="Tick"/> — тестируется без Unity.
    /// </summary>
    public interface ILevelEventService
    {
        /// <summary>
        /// Разрешает активную нужду типа <paramref name="type"/> на точке <paramref name="siteIndex"/>:
        /// публикует <c>NeedResolvedEvent</c> и снимает её с таймера истечения. Нет такой активной
        /// нужды — безопасный no-op.
        /// </summary>
        void ResolveNeed(NeedType type, int siteIndex, bool byPlayer);

        /// <summary>Продвигает таймлайн: триггерит записи по <c>triggerTime</c>, истекает нетронутые по <c>duration</c>.</summary>
        void Tick(float deltaTime);
    }
}
