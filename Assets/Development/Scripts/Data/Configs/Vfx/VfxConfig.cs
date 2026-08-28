using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Vfx
{
    /// <summary>
    /// Каталог VFX-эффектов (game-design §14.2) — единственный источник соответствия
    /// <see cref="VfxEffectType"/> → префаб/размер пула, читается <c>VfxFactory</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "VfxConfig", menuName = "BlackWhiteLike/Vfx/Vfx Config")]
    public sealed class VfxConfig : ScriptableObject
    {
        [SerializeField] private VfxEntry[] _entries = new VfxEntry[0];

        public VfxEntry[] Entries => _entries;

        /// <summary>Ищет запись по типу эффекта. Отсутствие записи — легальный случай (см. VfxFactory.Play), не ошибка.</summary>
        public bool TryGet(VfxEffectType type, out VfxEntry entry)
        {
            if (_entries != null)
            {
                foreach (var candidate in _entries)
                {
                    if (candidate == null) continue;
                    if (candidate.Type != type) continue;

                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }
}
