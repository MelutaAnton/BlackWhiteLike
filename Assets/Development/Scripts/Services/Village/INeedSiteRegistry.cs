using UnityEngine;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Реестр мировых позиций точек нужды деревни (game-design §5, шаг 10 «Polish»). Единственный
    /// источник позиции NeedSite по его <c>siteIndex</c> — нужен screen-space указателю туториала
    /// (<c>TutorialPresenter</c>) и, в следующей волне, worship-попапам. Регистрация —
    /// <c>NeedSiteController</c> на своём <c>Construct</c>/<c>OnDisable</c>.
    /// </summary>
    public interface INeedSiteRegistry
    {
        void Register(int siteIndex, Transform site);
        void Unregister(int siteIndex);

        /// <summary>false — точка не зарегистрирована ИЛИ её Transform уничтожен (fake-null).</summary>
        bool TryGetSitePosition(int siteIndex, out Vector3 position);
    }
}
