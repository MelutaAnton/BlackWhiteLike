using BlackWhiteLike.Core.Pool;
using BlackWhiteLike.Models;
using BlackWhiteLike.Views.UI.Hud;
using UnityEngine;

namespace BlackWhiteLike.Factories.Ui
{
    /// <summary>
    /// Реализация <see cref="IWorshipPopupFactory"/>. Pure C#, единственный потребитель
    /// <see cref="IPoolManager"/> для попапов Worship (CLAUDE.md). Мировых координат в событиях
    /// Worship нет (решение владельца, шаг 5) — попап всегда спавнится сразу под
    /// <see cref="IWorshipPopupHost.PopupRoot"/>.
    /// </summary>
    public sealed class WorshipPopupFactory : IWorshipPopupFactory
    {
        private static readonly Color AweColor = new Color(1f, 0.84f, 0.35f); // золотистый
        private static readonly Color FearColor = new Color(0.85f, 0.15f, 0.15f); // красный

        private readonly IPoolManager _poolManager;
        private readonly IWorshipPopupHost _host;

        public WorshipPopupFactory(IPoolManager poolManager, IWorshipPopupHost host)
        {
            _poolManager = poolManager;
            _host = host;

            if (_host.PopupPrefab != null)
            {
                _poolManager.Prewarm(_host.PopupPrefab.gameObject, 6);
            }
        }

        public void Show(int amount, WorshipSource source)
        {
            if (_host.PopupPrefab == null) return; // сцена без попапов — штатный no-op

            var view = _poolManager.Get<WorshipPopupView>(_host.PopupPrefab.gameObject, Vector3.zero, Quaternion.identity);
            if (view == null) return; // PoolManager уже залогировал причину

            var rectTransform = (RectTransform)view.transform;
            rectTransform.SetParent(_host.PopupRoot, false);

            var color = source == WorshipSource.Awe ? AweColor : FearColor;
            view.Launch($"+{amount} {source}", color, OnFinished);
        }

        private void OnFinished(WorshipPopupView view)
        {
            _poolManager.Release(view.gameObject);
        }
    }
}
