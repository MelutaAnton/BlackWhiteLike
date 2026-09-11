using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Планировщик событий-нужд уровня (game-design §5, §6). Pure C# — читает скриптованный
    /// таймлайн (<c>LevelConfig.EventTimeline</c>); никакой генерации случайных событий
    /// (cli-workflow §Шаг 3). Запись взводится по-разному в зависимости от <c>NeedTriggerMode</c>
    /// (шаг 10, единственный источник семантики режимов): <c>AtTime</c> — по времени внутри
    /// <see cref="Tick"/>, <c>AfterVillagerTossed</c>/<c>AfterPreviousResolved</c> — подпиской
    /// сервиса на шину событий (<c>ObjectGrabbedEvent</c>+<c>ObjectThrownEvent</c>/
    /// <c>NeedResolvedEvent</c>), не через <see cref="Tick"/>. Время для AtTime всегда приходит
    /// аргументом в <see cref="Tick"/> — тестируется без Unity.
    /// </summary>
    public interface ILevelEventService
    {
        /// <summary>
        /// Разрешает активную нужду типа <paramref name="type"/> на точке <paramref name="siteIndex"/>:
        /// публикует <c>NeedResolvedEvent</c> (с <paramref name="causeMiracle"/>, валиден только при
        /// <paramref name="byPlayer"/> == true, см. <c>Events.Village.cs</c>) и снимает её с таймера
        /// истечения. Нет такой активной нужды — безопасный no-op.
        /// </summary>
        void ResolveNeed(NeedType type, int siteIndex, bool byPlayer, MiracleType causeMiracle);

        /// <summary>
        /// Рантайм-триггер нужды поверх скриптованного таймлайна (game-design §4, §5, шаг 4) — например,
        /// Fireball поджёг дом. Если на (<paramref name="type"/>, <paramref name="siteIndex"/>) уже есть
        /// активная запись — штатный no-op ("дом уже горит"), без предупреждения в лог. Иначе создаёт
        /// новую активную запись с истечением через <paramref name="duration"/> и публикует
        /// <c>NeedTriggeredEvent</c> с <c>ByPlayer</c> = <paramref name="byPlayer"/> и
        /// <c>CauseMiracle</c> = <paramref name="causeMiracle"/> (валиден только при <paramref name="byPlayer"/> == true).
        /// </summary>
        void TriggerNeed(NeedType type, int siteIndex, float duration, bool byPlayer, MiracleType causeMiracle);

        /// <summary>
        /// Продвигает таймлайн по времени: взводит Pending-записи с <c>TriggerMode == AtTime</c>,
        /// для которых наступил <c>triggerTime</c>. Для ЛЮБОЙ Active-записи (независимо от того, каким
        /// режимом она была взведена — AtTime, AfterVillagerTossed или AfterPreviousResolved) с
        /// <c>duration &gt; 0</c> проверяет истечение и публикует <c>NeedExpiredEvent</c>; записи с
        /// <c>duration &lt;= 0</c> не истекают никогда (шаг 10). Записи с событийными <c>TriggerMode</c>
        /// (<c>AfterVillagerTossed</c>/<c>AfterPreviousResolved</c>) сам <see cref="Tick"/> не взводит —
        /// это делает подписка сервиса на шину (<c>ObjectGrabbedEvent</c>+<c>ObjectThrownEvent</c>/
        /// <c>NeedResolvedEvent</c>).
        /// </summary>
        void Tick(float deltaTime);

        /// <summary>
        /// Шаг 10 (решение владельца — soft-fail достижим на L01): true, если есть нужда, способная
        /// разрешиться САМА по времени — Active-запись с <c>duration &gt; 0</c> (истечёт сама) ИЛИ
        /// Pending-запись с <c>TriggerMode == AtTime</c> (наступит сама). НЕ считает блокирующими
        /// вечную Active-нужду (<c>duration &lt;= 0</c>) и событийные Pending-записи
        /// (<c>AfterVillagerTossed</c>/<c>AfterPreviousResolved</c>) — их разрешение зависит только от
        /// действия игрока/другого события, не от времени, поэтому они не должны блокировать soft-fail
        /// навсегда. <c>Armed</c>-записи (ждут освобождения ключа другой записью) сами по себе тоже не
        /// блокируют — ни в статусе Active, ни в Pending они не находятся. Ревью (добивка): если
        /// запись, держащая ключ, бесконечна (<c>duration &lt;= 0</c>), а Armed-запись ждёт именно её —
        /// результат false для ОБЕИХ, осознанно: бесконечная нужда разрешается только явным действием
        /// игрока (потратить Faith на чудо), а не временем, которого у неё нет — soft-fail в этой
        /// ситуации законен, если у игрока и правда нет Faith. Используется <c>ILevelFlowService</c>
        /// для условия LevelStalled.
        /// </summary>
        bool HasBlockingNeeds { get; }
    }
}
