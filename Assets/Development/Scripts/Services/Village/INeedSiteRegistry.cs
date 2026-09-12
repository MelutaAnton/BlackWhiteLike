using System.Collections.Generic;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Реестр мировых позиций точек нужды деревни (game-design §5, шаг 10 «Polish»). Единственный
    /// источник позиции NeedSite по его <c>siteIndex</c> — нужен screen-space указателю туториала
    /// (<c>TutorialPresenter</c>), worship-попапам и, с шага периодических нужд (уровни 2–5),
    /// <c>PeriodicNeedService</c> для выбора случайной свободной цели. Регистрация —
    /// <c>NeedSiteController</c> на своём <c>Construct</c>/<c>OnDisable</c> (для villager-hosted нужд —
    /// <c>VillagerController</c>, см. <c>NeedHostRules.IsVillagerHosted</c>).
    /// </summary>
    public interface INeedSiteRegistry
    {
        /// <summary>
        /// Регистрирует точку <paramref name="siteIndex"/> типа <paramref name="type"/> с мировой
        /// позицией <paramref name="site"/>. Тип хранится вместе с точкой — <see cref="CollectSites"/>
        /// фильтрует по нему, не полагаясь на внешний источник соответствия siteIndex → NeedType.
        /// </summary>
        void Register(int siteIndex, NeedType type, Transform site);

        void Unregister(int siteIndex);

        /// <summary>false — точка не зарегистрирована ИЛИ её Transform уничтожен (fake-null).</summary>
        bool TryGetSitePosition(int siteIndex, out Vector3 position);

        /// <summary>
        /// Шаг периодических нужд (уровни 2–5): собирает в <paramref name="buffer"/> (предварительно
        /// очищается) siteIndex всех живых (Transform не уничтожен, явный <c>==</c> — CLAUDE.md)
        /// зарегистрированных точек типа <paramref name="type"/>. Источник кандидатов для
        /// <c>PeriodicNeedService</c> — точка считается "свободной" только после сверки с
        /// <c>ILevelEventService.IsNeedActive</c>, сюда это уже не входит.
        /// </summary>
        void CollectSites(NeedType type, List<int> buffer);
    }
}
