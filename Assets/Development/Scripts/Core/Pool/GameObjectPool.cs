using System.Collections.Generic;
using UnityEngine;

namespace BlackWhiteLike.Core.Pool
{
    /// <summary>
    /// Пул экземпляров одного префаба (game-design §12). Живёт в Core/ (как
    /// <c>Core/Input/PointerInputProvider</c>, <c>Core/Interaction/PointerWorldProjector</c>) —
    /// переиспользуемая инфраструктура, а не бизнес-правило, поэтому обращение к
    /// UnityEngine.GameObject/Object здесь допустимо (в отличие от Services/, см. CLAUDE.md).
    /// Владеет стеком свободных инстансов и контейнером-родителем в иерархии сцены; активирует/
    /// деактивирует их через <c>SetActive</c> при выдаче/возврате.
    /// </summary>
    public sealed class GameObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _container;
        private readonly Stack<GameObject> _free = new Stack<GameObject>();

        public GameObjectPool(GameObject prefab, Transform container)
        {
            _prefab = prefab;
            _container = container;
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject instance;
            if (_free.Count > 0)
            {
                instance = _free.Pop();
                instance.transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                instance = Object.Instantiate(_prefab, position, rotation, _container);
            }

            instance.SetActive(true);
            return instance;
        }

        public void Release(GameObject instance)
        {
            instance.SetActive(false);
            instance.transform.SetParent(_container, worldPositionStays: false);
            _free.Push(instance);
        }

        public void Prewarm(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var instance = Object.Instantiate(_prefab, Vector3.zero, Quaternion.identity, _container);
                instance.SetActive(false);
                _free.Push(instance);
            }
        }
    }
}
