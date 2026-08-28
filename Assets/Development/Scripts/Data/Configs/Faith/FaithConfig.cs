using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Faith
{
    /// <summary>Параметры маны Faith (game-design §4, §12). Дефолты — черновые, крутятся playtest'ом.</summary>
    [CreateAssetMenu(fileName = "FaithConfig", menuName = "BlackWhiteLike/Faith/Faith Config")]
    public sealed class FaithConfig : ScriptableObject
    {
        [Tooltip("Потолок Faith без апгрейдов (FaithCapacity, game-design §8, задел на шаг 8).")]
        [SerializeField] private float _baseCapacity = 100f;

        [Tooltip("Faith/сек пассивной генерации за КАЖДОГО жителя в состоянии Worship (game-design §4).")]
        [SerializeField] private float _regenPerWorshipper = 2f;

        [Tooltip("Стартовый Faith в процентах от Capacity (0..100) на старте уровня.")]
        [Range(0f, 100f)]
        [SerializeField] private float _startingFaithPercent = 50f;

        public float BaseCapacity => _baseCapacity;
        public float RegenPerWorshipper => _regenPerWorshipper;
        public float StartingFaithPercent => _startingFaithPercent;
    }
}
