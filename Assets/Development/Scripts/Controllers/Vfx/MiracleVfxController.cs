using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Factories.Vfx;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Vfx
{
    /// <summary>
    /// Мост MiracleCastEvent → VFX (game-design §4, §13). Unity API only — вся работа с пулом
    /// эффектов делегирована <see cref="IVfxFactory"/> (CLAUDE.md: единственный внешний код,
    /// вызывающий фабрику здесь, — сам не трогает IPoolManager).
    /// </summary>
    public sealed class MiracleVfxController : MonoBehaviour
    {
        private IEventBus _eventBus;
        private IVfxFactory _vfxFactory;
        private MiracleCatalogConfig _catalog;
        private bool _initialized;

        private IDisposable _miracleCastSubscription;

        [Inject]
        public void Construct(IEventBus eventBus, IVfxFactory vfxFactory, MiracleCatalogConfig catalog)
        {
            _eventBus = eventBus;
            _vfxFactory = vfxFactory;
            _catalog = catalog;

            Subscribe();

            _initialized = true;
        }

        private void OnEnable()
        {
            if (!_initialized) return;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            Unsubscribe();
            _miracleCastSubscription = _eventBus.Subscribe<MiracleCastEvent>(OnMiracleCast);
        }

        private void Unsubscribe()
        {
            _miracleCastSubscription?.Dispose();
            _miracleCastSubscription = null;
        }

        private void OnMiracleCast(MiracleCastEvent evt)
        {
            if (!_catalog.TryGet(evt.Type, out var config)) return;

            // TODO шаг 9/10: масштабировать инстанс эффекта по evt.Radius (сейчас всегда дефолтный
            // масштаб префаба) — вне скоупа этого шага, чисто косметическая доводка.
            _vfxFactory.Play(config.CastVfx, evt.Point);
        }
    }
}
