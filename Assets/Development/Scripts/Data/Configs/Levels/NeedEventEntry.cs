using System;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Data.Configs.Levels
{
    /// <summary>
    /// Один элемент скриптованного таймлайна событий-нужд уровня (game-design §5, §6) —
    /// массив таких элементов хранится в <see cref="LevelConfig.EventTimeline"/> и читается
    /// <c>LevelEventService</c>. Простая сериализуемая структура-данные (не сервис/SO) — поля
    /// публичные без инкапсуляции, как задано контрактом game-design §6 и удобно для
    /// Inspector-массива.
    /// </summary>
    [Serializable]
    public struct NeedEventEntry
    {
        public NeedType type;
        public float triggerTime;
        public int targetSiteIndex;
        public float duration;
    }
}
