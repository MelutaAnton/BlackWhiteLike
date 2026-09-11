using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="NeedSiteRegistry"/> (шаг 10 «Polish»).</summary>
    public class NeedSiteRegistryTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        private Transform CreateTransform(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawned.Add(go);
            return go.transform;
        }

        [Test]
        public void Register_TryGetSitePosition_ReturnsRegisteredPosition()
        {
            var registry = new NeedSiteRegistry();
            var site = CreateTransform("Site0", new Vector3(1f, 2f, 3f));

            registry.Register(0, site);

            Assert.IsTrue(registry.TryGetSitePosition(0, out var position));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), position);
        }

        [Test]
        public void TryGetSitePosition_UnknownIndex_ReturnsFalse()
        {
            var registry = new NeedSiteRegistry();

            Assert.IsFalse(registry.TryGetSitePosition(42, out _));
        }

        [Test]
        public void Unregister_TryGetSitePosition_ReturnsFalse()
        {
            var registry = new NeedSiteRegistry();
            var site = CreateTransform("Site1", Vector3.zero);
            registry.Register(1, site);

            registry.Unregister(1);

            Assert.IsFalse(registry.TryGetSitePosition(1, out _));
        }

        [Test]
        public void TryGetSitePosition_DestroyedTransform_ReturnsFalse()
        {
            // Fake-null уничтоженного Transform (CLAUDE.md — явный == вместо ??) — регистрация не
            // отписана, но объект уже мёртв.
            var registry = new NeedSiteRegistry();
            var go = new GameObject("Site2");
            var site = go.transform;
            registry.Register(2, site);

            Object.DestroyImmediate(go);

            Assert.IsFalse(registry.TryGetSitePosition(2, out _));
        }
    }
}
