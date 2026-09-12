using UnityEngine;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Data.Configs.Levels
{
    /// <summary>
    /// Уровень = деревня (game-design §6). Один инстанс на уровень; наполнение сцены (дома, поля,
    /// спавн-точки, NeedSite'ы, границы камеры) собирается editor-скриптом "Build Village Level"
    /// (шаг 7, здесь не реализуется) — на шаге 3 конфиг черновой, заполняется вручную под тестовую сцену.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "BlackWhiteLike/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [SerializeField] private LevelId _id;

        [Tooltip("Порог Worship, при достижении которого уровень считается пройденным (WorshipService, шаг 5).")]
        [SerializeField] private int _worshipTarget = 100;

        [SerializeField] private int _villagerCount = 5;
        [SerializeField] private VillageLayoutConfig _layout;

        [Tooltip("Скриптованный таймлайн событий-нужд (game-design §5) — единственный источник событий уровня, никакой генерации в рантайме (cli-workflow §Шаг 3).")]
        [SerializeField] private NeedEventEntry[] _eventTimeline = new NeedEventEntry[0];

        [Tooltip("Периодический режим нужд (уровни 2–5): каждые N секунд срабатывает случайная свободная нужда. 0 — режим выключен, уровень живёт по EventTimeline (L01, туториал).")]
        [SerializeField] private float _eventIntervalSeconds;

        [Tooltip("Длительность периодической нужды до истечения. 0 — не истекает (сейчас; задел под будущий стейт «руины/смерть» по истечению).")]
        [SerializeField] private float _periodicNeedDuration;

        [Tooltip("0 = опциональный таймер выключен (по умолчанию).")]
        [SerializeField] private float _optionalTimerSeconds;

        [SerializeField] private int _coinsReward = 10;

        [Tooltip(
            "ПРЕДОХРАНИТЕЛЬ §15: позиция будущего спавна существа (Progression/Creature, Scalability " +
            "Hook) — Vector3, а не Transform: SO не должен держать ссылку на сценный объект. В срезе не используется.")]
        [SerializeField] private bool _hasCreatureSpawn;
        [SerializeField] private Vector3 _creatureSpawnPosition;

        public LevelId Id => _id;
        public int WorshipTarget => _worshipTarget;
        public int VillagerCount => _villagerCount;
        public VillageLayoutConfig Layout => _layout;
        public NeedEventEntry[] EventTimeline => _eventTimeline;
        public float EventIntervalSeconds => _eventIntervalSeconds;
        public float PeriodicNeedDuration => _periodicNeedDuration;
        public float OptionalTimerSeconds => _optionalTimerSeconds;
        public int CoinsReward => _coinsReward;

        /// <summary>
        /// ПРЕДОХРАНИТЕЛЬ §15 (ревью шага 3): Vector3.zero неотличим от честно выставленного центра
        /// карты — прямая properties была бы тихим источником бага существа (заспавнится в мировом
        /// нуле), а не сигналом "конфиг пуст". <see cref="_hasCreatureSpawn"/> — явный флаг "задано".
        /// </summary>
        public bool TryGetCreatureSpawn(out Vector3 position)
        {
            position = _creatureSpawnPosition;
            return _hasCreatureSpawn;
        }
    }
}
