using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Services.Hand;
using BlackWhiteLike.Views.UI.Hud;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="MiraclePanelPresenter"/> (game-design §4, §10, §19, шаг 4).</summary>
    public class MiraclePanelPresenterTests
    {
        private sealed class FakeMiraclePanelView : IMiraclePanelView
        {
            public event Action<MiracleType> MiracleClicked;

            public readonly List<(MiracleType type, bool available)> AvailableCalls = new List<(MiracleType, bool)>();
            public readonly List<(MiracleType type, bool selected)> SelectedCalls = new List<(MiracleType, bool)>();
            public readonly List<(MiracleType type, bool affordable)> AffordableCalls = new List<(MiracleType, bool)>();

            public void SetAvailable(MiracleType type, bool available) => AvailableCalls.Add((type, available));
            public void SetSelected(MiracleType type, bool selected) => SelectedCalls.Add((type, selected));
            public void SetAffordable(MiracleType type, bool affordable) => AffordableCalls.Add((type, affordable));
            public void SetCost(MiracleType type, int cost)
            {
            }

            public void SetIcon(MiracleType type, Sprite icon)
            {
            }

            public void RaiseClicked(MiracleType type) => MiracleClicked?.Invoke(type);
        }

        private sealed class FakeHandModeService : IHandModeService
        {
            public MiracleType? LastSelected;

            public HandMode Mode => HandMode.Grab;
            public bool TryGetSelectedMiracle(out MiracleType type)
            {
                type = default;
                return false;
            }

            public void SelectMiracle(MiracleType type) => LastSelected = type;
            public void ClearSelection()
            {
            }
        }

        private sealed class FakeFaithService : IFaithService
        {
            public float Current { get; set; }
            public float Capacity { get; set; } = 100f;
            public bool CanAffordResult { get; set; } = true;

            public bool CanAfford(float amount) => CanAffordResult;
            public bool TrySpend(float amount) => true;
            public void Tick(float deltaTime)
            {
            }
        }

        private static MiracleConfig CreateMiracleConfig(MiracleType type, float baseFaithCost = 20f)
        {
            var config = ScriptableObject.CreateInstance<MiracleConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_type").enumValueIndex = (int)type;
            so.FindProperty("_baseFaithCost").floatValue = baseFaithCost;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private static MiracleCatalogConfig CreateCatalog(params MiracleConfig[] miracles)
        {
            var catalog = ScriptableObject.CreateInstance<MiracleCatalogConfig>();
            var field = typeof(MiracleCatalogConfig).GetField("_miracles", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(catalog, miracles);
            return catalog;
        }

        [Test]
        public void Start_TypesWithoutConfig_AreSetUnavailable_TypesWithConfig_AreSetAvailable()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain), CreateMiracleConfig(MiracleType.Fireball));
            var view = new FakeMiraclePanelView();
            var presenter = new MiraclePanelPresenter(view, new FakeHandModeService(), new FakeFaithService(), catalog, new FakeEventBus());

            presenter.Start();

            Assert.IsTrue(view.AvailableCalls.Any(c => c.type == MiracleType.Rain && c.available));
            Assert.IsTrue(view.AvailableCalls.Any(c => c.type == MiracleType.Fireball && c.available));
            Assert.IsTrue(view.AvailableCalls.Any(c => c.type == MiracleType.Heal && !c.available));
            Assert.IsTrue(view.AvailableCalls.Any(c => c.type == MiracleType.Lightning && !c.available));
        }

        [Test]
        public void MiracleClicked_CallsHandModeServiceSelectMiracle()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain));
            var view = new FakeMiraclePanelView();
            var handMode = new FakeHandModeService();
            var presenter = new MiraclePanelPresenter(view, handMode, new FakeFaithService(), catalog, new FakeEventBus());
            presenter.Start();

            view.RaiseClicked(MiracleType.Rain);

            Assert.AreEqual(MiracleType.Rain, handMode.LastSelected);
        }

        [Test]
        public void FaithChangedEvent_UpdatesAffordabilityForCatalogTypes()
        {
            // Реальный EventBus, а не FakeEventBus — FakeEventBus.Subscribe не хранит обработчик
            // (см. FakeEventBus.cs), для проверки реального round-trip Publish->handler он не годится.
            var eventBus = new EventBus();
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain, baseFaithCost: 20f));
            var view = new FakeMiraclePanelView();
            var presenter = new MiraclePanelPresenter(view, new FakeHandModeService(), new FakeFaithService(), catalog, eventBus);
            presenter.Start();
            view.AffordableCalls.Clear();

            eventBus.Publish(new FaithChangedEvent(0f, 10f, 100f)); // 10 < BaseFaithCost(20)

            Assert.IsTrue(view.AffordableCalls.Any(c => c.type == MiracleType.Rain && !c.affordable));

            eventBus.Publish(new FaithChangedEvent(10f, 30f, 100f)); // 30 >= BaseFaithCost(20)

            Assert.IsTrue(view.AffordableCalls.Any(c => c.type == MiracleType.Rain && c.affordable));
        }

        [Test]
        public void MiracleSelectedEvent_UpdatesViewSelection()
        {
            var eventBus = new EventBus();
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain));
            var view = new FakeMiraclePanelView();
            var presenter = new MiraclePanelPresenter(view, new FakeHandModeService(), new FakeFaithService(), catalog, eventBus);
            presenter.Start();

            eventBus.Publish(new MiracleSelectedEvent(MiracleType.Rain, true));

            Assert.IsTrue(view.SelectedCalls.Any(c => c.type == MiracleType.Rain && c.selected));
        }

        [Test]
        public void Dispose_UnsubscribesFromViewAndEventBus()
        {
            var eventBus = new EventBus();
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain));
            var view = new FakeMiraclePanelView();
            var handMode = new FakeHandModeService();
            var presenter = new MiraclePanelPresenter(view, handMode, new FakeFaithService(), catalog, eventBus);
            presenter.Start();

            presenter.Dispose();
            view.RaiseClicked(MiracleType.Rain);
            eventBus.Publish(new MiracleSelectedEvent(MiracleType.Rain, true));

            Assert.IsNull(handMode.LastSelected);
        }
    }
}
