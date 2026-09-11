using System;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Levels
{
    /// <summary>
    /// Один элемент скриптованного таймлайна событий-нужд уровня (game-design §5, §6) —
    /// массив таких элементов хранится в <see cref="LevelConfig.EventTimeline"/> и читается
    /// <c>LevelEventService</c>. Простая сериализуемая структура-данные (не сервис/SO) — поля
    /// публичные без инкапсуляции, как задано контрактом game-design §6 и удобно для
    /// Inspector-массива. Единственное исключение — <see cref="_triggerMode"/>/<see cref="TriggerMode"/>
    /// (шаг 10, событийная цепочка): инкапсулировано по контракту оркестратора этого шага, остальные
    /// поля не тронуты.
    /// </summary>
    /// <remarks>
    /// <see cref="triggerTime"/> читается <c>LevelEventService</c> ТОЛЬКО при
    /// <see cref="TriggerMode"/> == <see cref="NeedTriggerMode.AtTime"/> — для
    /// <c>AfterVillagerTossed</c>/<c>AfterPreviousResolved</c> запись взводится событием, значение
    /// поля игнорируется.
    /// <para>
    /// <see cref="duration"/> &lt;= 0 — нужда НЕ истекает (бесконечная, до явного разрешения игроком
    /// через <c>ILevelEventService.ResolveNeed</c>) — правило действует для ЛЮБОГО
    /// <see cref="TriggerMode"/>, не только для событийных.
    /// </para>
    /// </remarks>
    [Serializable]
    public struct NeedEventEntry
    {
        public NeedType type;
        public float triggerTime;
        public int targetSiteIndex;
        public float duration;

        [SerializeField] private NeedTriggerMode _triggerMode;

        /// <summary>Режим взведения записи (см. remarks класса и <see cref="NeedTriggerMode"/>).</summary>
        public NeedTriggerMode TriggerMode
        {
            get => _triggerMode;
            set => _triggerMode = value;
        }
    }
}
