using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Miracles
{
    /// <summary>
    /// Каталог чудес (game-design §4) — определяет состав панели чудес. Шаг 4: только Rain и
    /// Fireball; Heal и Lightning заполняются на шаге 6 (cli-workflow.md, "Шаг 4"). Отсутствие
    /// чуда в каталоге — легальное состояние (см. <see cref="TryGet"/>), не ошибка.
    /// </summary>
    [CreateAssetMenu(fileName = "MiracleCatalogConfig", menuName = "BlackWhiteLike/Miracles/Miracle Catalog Config")]
    public sealed class MiracleCatalogConfig : ScriptableObject
    {
        [SerializeField] private MiracleConfig[] _miracles = new MiracleConfig[0];

        public MiracleConfig[] Miracles => _miracles;

        /// <summary>Ищет конфиг по типу. Null-элементы массива (незаполненный слот в Inspector) пропускаются, не роняют поиск.</summary>
        public bool TryGet(MiracleType type, out MiracleConfig config)
        {
            if (_miracles != null)
            {
                foreach (var miracle in _miracles)
                {
                    if (miracle == null) continue;
                    if (miracle.Type != type) continue;

                    config = miracle;
                    return true;
                }
            }

            config = null;
            return false;
        }
    }
}
