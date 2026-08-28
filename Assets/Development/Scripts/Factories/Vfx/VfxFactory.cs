using System.Collections.Generic;
using BlackWhiteLike.Controllers.Vfx;
using BlackWhiteLike.Core.Pool;
using BlackWhiteLike.Data.Configs.Vfx;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Factories.Vfx
{
    /// <summary>
    /// Реализация <see cref="IVfxFactory"/>. Pure C#, единственный потребитель <see cref="IPoolManager"/>
    /// для VFX-эффектов (CLAUDE.md). VFX-инстансу DI-инъекция не нужна (нет зависимостей) —
    /// в отличие от VillagerFactory, здесь нет <c>InjectGameObject</c>.
    /// </summary>
    public sealed class VfxFactory : IVfxFactory
    {
        private readonly IPoolManager _poolManager;
        private readonly VfxConfig _config;

        // Не спамить лог каждый кадр за отсутствующую запись/префаб — предупреждаем один раз на тип.
        private readonly HashSet<VfxEffectType> _warnedMissing = new HashSet<VfxEffectType>();

        public VfxFactory(IPoolManager poolManager, VfxConfig config)
        {
            _poolManager = poolManager;
            _config = config;

            var entries = _config.Entries;
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry == null || entry.Prefab == null) continue;
                _poolManager.Prewarm(entry.Prefab, entry.PoolSize);
            }
        }

        public void Play(VfxEffectType type, Vector3 position)
        {
            if (!_config.TryGet(type, out var entry) || entry.Prefab == null)
            {
                if (_warnedMissing.Add(type))
                {
                    Debug.LogWarning($"[VfxFactory] Нет записи/префаба для {type} в VfxConfig — эффект не проигран.");
                }

                return;
            }

            var instance = _poolManager.Get<VfxInstanceController>(entry.Prefab, position, Quaternion.identity);
            if (instance == null) return; // PoolManager уже залогировал причину (нет компонента на префабе)

            instance.Launch(OnFinished);
        }

        private void OnFinished(VfxInstanceController instance)
        {
            _poolManager.Release(instance.gameObject);
        }
    }
}
