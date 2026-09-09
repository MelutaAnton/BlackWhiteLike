using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Rewards
{
    /// <summary>Параметры наград за прохождение уровня (game-design §8, шаг 8).</summary>
    [CreateAssetMenu(fileName = "RewardConfig", menuName = "BlackWhiteLike/Rewards/Reward Config")]
    public sealed class RewardConfig : ScriptableObject
    {
        [Tooltip("Множитель монет за ПЕРВОЕ прохождение уровня (game-design §8). Повторное прохождение — coins как есть.")]
        [SerializeField] private float _firstClearMultiplier = 2f;

        public float FirstClearMultiplier => _firstClearMultiplier;

        private void OnValidate()
        {
            // Множитель < 1 давал бы first-clear награду МЕНЬШЕ повторного прохождения — не по дизайну.
            _firstClearMultiplier = Mathf.Max(1f, _firstClearMultiplier);
        }
    }
}
