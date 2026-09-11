using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Localization;
using BlackWhiteLike.Views.UI.Common;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="LocalizedTextApplyEntryPoint"/> (шаг 10 «Polish», волна 2 — зачистка
    /// рантайм-скана сцены: источник view'ов теперь коллекция <see cref="LocalizedTextRegistry"/>,
    /// а не <c>Object.FindObjectsByType</c>).
    /// </summary>
    public class LocalizedTextApplyEntryPointTests
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

        private sealed class FakeLocalizationService : ILocalizationService
        {
            public GameLocale CurrentLocale { get; set; } = GameLocale.Ru;

            public string GetText(string key) => $"[{key}]";
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private LocalizedTextView CreateView(string key)
        {
            var go = CreateGameObject($"View_{key}");
            go.AddComponent<TextMeshProUGUI>();

            var view = go.AddComponent<LocalizedTextView>();
            var so = new SerializedObject(view);
            so.FindProperty("_key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        /// <summary>
        /// Элементы <paramref name="views"/> могут содержать null (кейс "реестр с null-view" —
        /// arraySize увеличен, элемент не заполнен objectReferenceValue).
        /// </summary>
        private LocalizedTextRegistry CreateRegistry(params LocalizedTextView[] views)
        {
            var go = CreateGameObject("Registry");
            var registry = go.AddComponent<LocalizedTextRegistry>();

            var so = new SerializedObject(registry);
            var viewsProp = so.FindProperty("_views");
            viewsProp.arraySize = views.Length;

            for (var i = 0; i < views.Length; i++)
            {
                viewsProp.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return registry;
        }

        [Test]
        public void Start_AppliesTextFromRegistryViews_ToEachView()
        {
            var localization = new FakeLocalizationService();
            var viewA = CreateView("key_a");
            var viewB = CreateView("key_b");
            var registry = CreateRegistry(viewA, viewB);

            var entryPoint = new LocalizedTextApplyEntryPoint(localization, new[] { registry });
            entryPoint.Start();

            Assert.AreEqual("[key_a]", viewA.GetComponent<TMP_Text>().text);
            Assert.AreEqual("[key_b]", viewB.GetComponent<TMP_Text>().text);
        }

        [Test]
        public void Start_NullRegistriesList_DoesNotThrow()
        {
            var entryPoint = new LocalizedTextApplyEntryPoint(new FakeLocalizationService(), null);

            Assert.DoesNotThrow(() => entryPoint.Start());
        }

        [Test]
        public void Start_EmptyRegistries_DoesNotThrow()
        {
            var entryPoint = new LocalizedTextApplyEntryPoint(new FakeLocalizationService(), new LocalizedTextRegistry[0]);

            Assert.DoesNotThrow(() => entryPoint.Start());
        }

        [Test]
        public void Start_RegistryWithNullView_SkipsGracefully()
        {
            var localization = new FakeLocalizationService();
            var viewA = CreateView("key_a");
            var registry = CreateRegistry(viewA, null); // второй элемент — пустая ссылка в массиве

            var entryPoint = new LocalizedTextApplyEntryPoint(localization, new[] { registry });

            Assert.DoesNotThrow(() => entryPoint.Start());
            Assert.AreEqual("[key_a]", viewA.GetComponent<TMP_Text>().text);
        }
    }
}
