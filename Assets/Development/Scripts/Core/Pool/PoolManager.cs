using System.Collections.Generic;
using System;
using UnityEngine;

namespace BlackWhiteLike.Core.Pool
{
    /// <summary>
    /// Реализация <see cref="IPoolManager"/> (game-design §12): словарь префаб → пул, ключ —
    /// <c>GetInstanceID()</c> префаба (единственный идентификатор, без строковых ключей —
    /// CLAUDE.md). Дополнительно держит обратную карту выданный-инстанс → пул, чтобы
    /// <see cref="Release"/> находил нужный пул без перебора всех и не требовал от вызывающего
    /// кода помнить, из какого префаба был инстанс.
    /// </summary>
    /// <remarks>
    /// В2 (ревью шага 3): PoolManager — синглтон СЦЕНО-скоупа (GameLifetimeScope живёт per-scene,
    /// root-scope через VContainer отложен до шага 7 — CLAUDE.md/memory), поэтому его корневой
    /// GameObject больше НЕ помечается DontDestroyOnLoad: при смене сцены он обязан умереть вместе
    /// со своим scope, а не плодить второй "[PoolManager]" при следующей загрузке. Реализует
    /// IDisposable — VContainer вызывает Dispose() у любого IDisposable-синглтона при закрытии
    /// scope (см. комментарий в GameLifetimeScope про PointerInputProvider), Dispose уничтожает
    /// корень со всеми пуловыми инстансами явно, не полагаясь на выгрузку сцены.
    /// </remarks>
    public sealed class PoolManager : IPoolManager, IDisposable
    {
        private readonly Dictionary<int, GameObjectPool> _poolsByPrefabId = new Dictionary<int, GameObjectPool>();
        private readonly Dictionary<int, GameObjectPool> _poolByInstanceId = new Dictionary<int, GameObjectPool>();
        private readonly Transform _root;

        public PoolManager()
        {
            // Единственный корневой контейнер для всех пулов текущего scope — не засоряет корень сцены.
            var rootGo = new GameObject("[PoolManager]");
            _root = rootGo.transform;
        }

        public void Dispose()
        {
            // UnityEngine.Object — квалифицируем явно: "using System;" (для IDisposable) делает
            // голое "Object" неоднозначным (CS0104) между System.Object и UnityEngine.Object.
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
        }

        public T Get<T>(GameObject prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var pool = GetOrCreatePool(prefab);
            var instance = pool.Get(position, rotation);
            _poolByInstanceId[instance.GetInstanceID()] = pool;

            var component = instance.GetComponent<T>();
            if (component == null)
            {
                Debug.LogError($"[PoolManager] Префаб '{prefab.name}' не содержит компонент {typeof(T).Name} на корне.");
            }

            return component;
        }

        public bool Release(GameObject instance)
        {
            if (instance == null) return false;

            var instanceId = instance.GetInstanceID();
            if (!_poolByInstanceId.TryGetValue(instanceId, out var pool))
            {
                Debug.LogWarning($"[PoolManager] Попытка вернуть объект '{instance.name}', не выданный этим пулом (или уже возвращённый) — игнорируется.");
                return false;
            }

            _poolByInstanceId.Remove(instanceId);
            pool.Release(instance);
            return true;
        }

        public void Prewarm(GameObject prefab, int count)
        {
            var pool = GetOrCreatePool(prefab);
            pool.Prewarm(count);
        }

        private GameObjectPool GetOrCreatePool(GameObject prefab)
        {
            var prefabId = prefab.GetInstanceID();
            if (_poolsByPrefabId.TryGetValue(prefabId, out var pool)) return pool;

            var container = new GameObject($"Pool_{prefab.name}").transform;
            container.SetParent(_root, worldPositionStays: false);

            pool = new GameObjectPool(prefab, container);
            _poolsByPrefabId[prefabId] = pool;
            return pool;
        }
    }
}
