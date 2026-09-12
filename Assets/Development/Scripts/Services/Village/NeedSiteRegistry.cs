using System.Collections.Generic;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Реализация <see cref="INeedSiteRegistry"/>. Pure C# — хранит Dictionary siteIndex → (тип, Transform).
    /// Шаг периодических нужд (уровни 2–5): тип хранится вместе с точкой, чтобы <see cref="CollectSites"/>
    /// мог фильтровать по нему без внешнего источника соответствия siteIndex → NeedType.
    /// </summary>
    public sealed class NeedSiteRegistry : INeedSiteRegistry
    {
        private readonly struct SiteEntry
        {
            public readonly NeedType Type;
            public readonly Transform Site;

            public SiteEntry(NeedType type, Transform site)
            {
                Type = type;
                Site = site;
            }
        }

        private readonly Dictionary<int, SiteEntry> _sites = new Dictionary<int, SiteEntry>();

        public void Register(int siteIndex, NeedType type, Transform site)
        {
            // Незначительное (ревью шага 10): перезапись siteIndex ДРУГИМ Transform'ом обычно
            // означает битый LevelConfig/сцену (два NeedSiteController с одинаковым siteIndex) —
            // тот же самый Transform (повторный Register из OnEnable при toggle) молчаливый no-op.
            // Ревью периодических нужд: смена ТИПА на том же Transform — тоже ошибка данных (опечатка
            // _needType на префабе тихо перекинула бы точку в другой пул целей спавнера).
            if (_sites.TryGetValue(siteIndex, out var existing) && existing.Site != null
                && (existing.Site != site || existing.Type != type))
            {
                Debug.LogWarning(
                    $"[NeedSiteRegistry] siteIndex {siteIndex} уже зарегистрирован на \"{existing.Site.name}\" ({existing.Type}) — " +
                    $"перезаписан \"{site.name}\" ({type}). Вероятно, дублирующийся siteIndex в LevelConfig/сцене.");
            }

            _sites[siteIndex] = new SiteEntry(type, site);
        }

        public void Unregister(int siteIndex)
        {
            _sites.Remove(siteIndex);
        }

        public bool TryGetSitePosition(int siteIndex, out Vector3 position)
        {
            // Явный == (CLAUDE.md — ?? / ?. запрещены с Unity-объектами): fake-null уничтоженного
            // Transform обходит `??`, здесь проверяется честно.
            if (_sites.TryGetValue(siteIndex, out var entry) && entry.Site != null)
            {
                position = entry.Site.position;
                return true;
            }

            position = default;
            return false;
        }

        public void CollectSites(NeedType type, List<int> buffer)
        {
            buffer.Clear();
            foreach (var pair in _sites)
            {
                if (pair.Value.Type != type) continue;
                if (pair.Value.Site == null) continue; // fake-null уничтоженного Transform — явный ==
                buffer.Add(pair.Key);
            }
        }
    }
}
