using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Idle
{
    /// <summary>Параметры офлайн-дохода меты (game-design §15, шаг 8).</summary>
    [CreateAssetMenu(fileName = "IdleIncomeConfig", menuName = "BlackWhiteLike/Idle/Idle Income Config")]
    public sealed class IdleIncomeConfig : ScriptableObject
    {
        [Tooltip("Монет в час офлайн-дохода.")]
        [SerializeField] private float _coinsPerHour = 60f;

        [Tooltip("Максимум часов, за которые начисляется офлайн-доход (защита от бесконечного накопления).")]
        [SerializeField] private float _capHours = 8f;

        [Tooltip("Минимум минут отсутствия в игре, начиная с которых офлайн-доход вообще считается.")]
        [SerializeField] private float _minOfflineMinutes = 5f;

        public float CoinsPerHour => _coinsPerHour;
        public float CapHours => _capHours;
        public float MinOfflineMinutes => _minOfflineMinutes;

        private void OnValidate()
        {
            _coinsPerHour = Mathf.Max(0f, _coinsPerHour);
            _capHours = Mathf.Max(0f, _capHours);
            _minOfflineMinutes = Mathf.Max(0f, _minOfflineMinutes);
        }
    }
}
