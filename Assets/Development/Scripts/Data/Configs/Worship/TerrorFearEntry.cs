using System;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Worship
{
    /// <summary>Строка маппинга "акт террора → Fear" внутри <see cref="WorshipConfig"/> (game-design §7, шаг 5).</summary>
    [Serializable]
    public sealed class TerrorFearEntry
    {
        [SerializeField] private TerrorAct _act;
        [SerializeField] private int _fear;

        public TerrorAct Act => _act;
        public int Fear => _fear;
    }
}
