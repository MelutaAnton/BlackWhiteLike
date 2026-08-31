using System;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Worship
{
    /// <summary>Строка маппинга "нужда → Awe" внутри <see cref="WorshipConfig"/> (game-design §7, шаг 5).</summary>
    [Serializable]
    public sealed class NeedAweEntry
    {
        [SerializeField] private NeedType _type;
        [SerializeField] private int _awe;

        public NeedType Type => _type;
        public int Awe => _awe;
    }
}
