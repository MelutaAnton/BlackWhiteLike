using System.Collections.Generic;
using UnityEngine;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>Реализация <see cref="INeedSiteRegistry"/>. Pure C# — хранит только Dictionary siteIndex → Transform.</summary>
    public sealed class NeedSiteRegistry : INeedSiteRegistry
    {
        private readonly Dictionary<int, Transform> _sites = new Dictionary<int, Transform>();

        public void Register(int siteIndex, Transform site)
        {
            // Незначительное (ревью шага 10): перезапись siteIndex ДРУГИМ Transform'ом обычно
            // означает битый LevelConfig/сцену (два NeedSiteController с одинаковым siteIndex) —
            // тот же самый Transform (повторный Register из OnEnable при toggle) молчаливый no-op.
            if (_sites.TryGetValue(siteIndex, out var existing) && existing != null && existing != site)
            {
                Debug.LogWarning(
                    $"[NeedSiteRegistry] siteIndex {siteIndex} уже зарегистрирован на \"{existing.name}\" — " +
                    $"перезаписан \"{site.name}\". Вероятно, дублирующийся siteIndex в LevelConfig/сцене.");
            }

            _sites[siteIndex] = site;
        }

        public void Unregister(int siteIndex)
        {
            _sites.Remove(siteIndex);
        }

        public bool TryGetSitePosition(int siteIndex, out Vector3 position)
        {
            // Явный == (CLAUDE.md — ?? / ?. запрещены с Unity-объектами): fake-null уничтоженного
            // Transform обходит `??`, здесь проверяется честно.
            if (_sites.TryGetValue(siteIndex, out var site) && site != null)
            {
                position = site.position;
                return true;
            }

            position = default;
            return false;
        }
    }
}
