using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Levels
{
    /// <summary>
    /// Черновая раскладка деревни для одного уровня (game-design §6) — позиции домов/полей/
    /// спавн-точек жителей и границы pan-камеры. Наполнение сцены из раскладки — editor-скрипт
    /// "Build Village Level" (шаг 7, здесь не реализуется). Позиции — локальные Vector3
    /// (не Transform-ссылки на сцену: SO не должен ссылаться на сценные объекты, см.
    /// LevelConfig.TryGetCreatureSpawn — тот же принцип).
    /// </summary>
    [CreateAssetMenu(fileName = "VillageLayoutConfig", menuName = "BlackWhiteLike/Village Layout Config")]
    public sealed class VillageLayoutConfig : ScriptableObject
    {
        [SerializeField] private Vector3[] _housePositions = new Vector3[0];
        [SerializeField] private Vector3[] _fieldPositions = new Vector3[0];
        [SerializeField] private Vector3[] _villagerSpawnPositions = new Vector3[0];

        [Tooltip("Половина стороны квадрата вокруг центра, за пределы которого pan-камера не пускает pivot (game-design §11).")]
        [SerializeField] private float _cameraBoundsHalfExtent = 25f;

        [Tooltip(
            "Третий playtest шага 3 (критический дефект — жители падали за край земли): половина " +
            "стороны квадрата вокруг центра, за пределы которого НЕ должна уходить цель блуждания/" +
            "бегства жителя. Это физическая граница уровня (край земли), а не CameraBoundsHalfExtent " +
            "— тот про камеру и почти вдвое шире (25), клампить им блуждание значило бы пускать " +
            "жителей за реальный край карты. Дефолт (9) — под тестовую сцену: земля 20x20 с центром " +
            "в нуле (край на ±10), запас в 1 unit внутрь на инерцию/овершут физики.")]
        [SerializeField] private float _villagerBoundsHalfExtent = 9f;

        public Vector3[] HousePositions => _housePositions;
        public Vector3[] FieldPositions => _fieldPositions;
        public Vector3[] VillagerSpawnPositions => _villagerSpawnPositions;
        public float CameraBoundsHalfExtent => _cameraBoundsHalfExtent;
        public float VillagerBoundsHalfExtent => _villagerBoundsHalfExtent;
    }
}
