using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Faith;
using BlackWhiteLike.Services.Miracles;

namespace BlackWhiteLike.Tests
{
    /// <summary>
    /// Тесты <see cref="MiracleService"/> (game-design §4, §19, шаг 4) — сессия каста, симметричная
    /// <c>HandInteractionService.BeginHold/EndHold</c>.
    /// </summary>
    public class MiracleServiceTests
    {
        /// <summary>Фейковый IFaithService — полный контроль над Current/CanAfford/TrySpend без реального VillagerStateService.</summary>
        private sealed class FakeFaithService : IFaithService
        {
            public float Current { get; set; }
            public float Capacity { get; set; } = 1000f;

            public bool CanAfford(float amount) => amount <= 0f || Current >= amount;

            public bool TrySpend(float amount)
            {
                if (amount <= 0f) return true;
                if (Current < amount) return false;

                Current -= amount;
                return true;
            }

            public void Tick(float deltaTime)
            {
            }

            public void RefillToCapacity()
            {
            }
        }

        private static MiracleConfig CreateMiracleConfig(
            MiracleType type = MiracleType.Rain,
            float baseFaithCost = 20f,
            float holdCostPerSecond = 10f,
            float baseRadius = 2f,
            float maxRadius = 5f,
            float holdSecondsToMaxRadius = 1.5f,
            float effectPower = 8f,
            float holdGraceSeconds = 0f)
        {
            var config = ScriptableObject.CreateInstance<MiracleConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_type").enumValueIndex = (int)type;
            so.FindProperty("_baseFaithCost").floatValue = baseFaithCost;
            so.FindProperty("_holdCostPerSecond").floatValue = holdCostPerSecond;
            so.FindProperty("_baseRadius").floatValue = baseRadius;
            so.FindProperty("_maxRadius").floatValue = maxRadius;
            so.FindProperty("_holdSecondsToMaxRadius").floatValue = holdSecondsToMaxRadius;
            so.FindProperty("_effectPower").floatValue = effectPower;
            so.FindProperty("_holdGraceSeconds").floatValue = holdGraceSeconds;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>_miracles — приватный SerializeField (см. MiracleCatalogConfig.cs) — прошивается reflection'ом, тот же приём, что LevelEventServiceTests.CreateLevelConfig.</summary>
        private static MiracleCatalogConfig CreateCatalog(params MiracleConfig[] miracles)
        {
            var catalog = ScriptableObject.CreateInstance<MiracleCatalogConfig>();
            var field = typeof(MiracleCatalogConfig).GetField("_miracles", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(catalog, miracles);
            return catalog;
        }

        // --- CanCast ---

        [Test]
        public void CanCast_NoConfigInCatalog_ReturnsFalse()
        {
            var catalog = CreateCatalog();
            var service = new MiracleService(new FakeEventBus(), new FakeFaithService(), catalog);

            Assert.IsFalse(service.CanCast(MiracleType.Rain));
        }

        [Test]
        public void CanCast_NotEnoughFaith_ReturnsFalse()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(baseFaithCost: 20f));
            var faith = new FakeFaithService { Current = 5f };
            var service = new MiracleService(new FakeEventBus(), faith, catalog);

            Assert.IsFalse(service.CanCast(MiracleType.Rain));
        }

        [Test]
        public void CanCast_EnoughFaithAndConfig_ReturnsTrue()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(baseFaithCost: 20f));
            var faith = new FakeFaithService { Current = 20f };
            var service = new MiracleService(new FakeEventBus(), faith, catalog);

            Assert.IsTrue(service.CanCast(MiracleType.Rain));
        }

        // --- BeginCast ---

        [Test]
        public void BeginCast_SpendsBaseFaithCost_AndStartsAtBaseRadius()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(baseFaithCost: 20f, baseRadius: 2f));
            var faith = new FakeFaithService { Current = 100f };
            var service = new MiracleService(new FakeEventBus(), faith, catalog);

            var began = service.BeginCast(MiracleType.Rain, Vector3.zero);

            Assert.IsTrue(began);
            Assert.IsTrue(service.IsCasting);
            Assert.AreEqual(80f, faith.Current, 0.001f);
            Assert.AreEqual(2f, service.CurrentRadius, 0.001f);
        }

        [Test]
        public void BeginCast_NoConfigInCatalog_ReturnsFalse_AndDoesNotSpend()
        {
            var catalog = CreateCatalog();
            var faith = new FakeFaithService { Current = 100f };
            var service = new MiracleService(new FakeEventBus(), faith, catalog);

            var began = service.BeginCast(MiracleType.Rain, Vector3.zero);

            Assert.IsFalse(began);
            Assert.IsFalse(service.IsCasting);
            Assert.AreEqual(100f, faith.Current, 0.001f);
        }

        [Test]
        public void BeginCast_NotEnoughFaith_ReturnsFalse_AndDoesNotOpenSession()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(baseFaithCost: 20f));
            var faith = new FakeFaithService { Current = 5f };
            var service = new MiracleService(new FakeEventBus(), faith, catalog);

            var began = service.BeginCast(MiracleType.Rain, Vector3.zero);

            Assert.IsFalse(began);
            Assert.IsFalse(service.IsCasting);
            Assert.AreEqual(5f, faith.Current, 0.001f);
        }

        [Test]
        public void BeginCast_WhileAlreadyCasting_ReturnsFalse()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(baseFaithCost: 10f));
            var faith = new FakeFaithService { Current = 100f };
            var service = new MiracleService(new FakeEventBus(), faith, catalog);
            service.BeginCast(MiracleType.Rain, Vector3.zero);

            var secondBegin = service.BeginCast(MiracleType.Rain, Vector3.zero);

            Assert.IsFalse(secondBegin);
        }

        // --- UpdateCast ---

        [Test]
        public void UpdateCast_GrowsRadiusToMaxOverHoldSeconds_AndSpendsHoldCost()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(
                baseFaithCost: 0f, holdCostPerSecond: 10f, baseRadius: 2f, maxRadius: 5f, holdSecondsToMaxRadius: 1.5f));
            var faith = new FakeFaithService { Current = 1000f };
            var service = new MiracleService(new FakeEventBus(), faith, catalog);
            service.BeginCast(MiracleType.Rain, Vector3.zero);

            service.UpdateCast(1.5f, Vector3.zero);

            Assert.AreEqual(5f, service.CurrentRadius, 0.001f);
            Assert.AreEqual(1000f - 1.5f * 10f, faith.Current, 0.001f);
        }

        [Test]
        public void UpdateCast_NotEnoughFaithForHoldCost_RadiusFreezes_SessionStaysAlive()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(
                baseFaithCost: 10f, holdCostPerSecond: 10f, baseRadius: 2f, maxRadius: 5f, holdSecondsToMaxRadius: 1f));
            var faith = new FakeFaithService { Current = 10f }; // хватает ровно на BeginCast, на UpdateCast — нет
            var service = new MiracleService(new FakeEventBus(), faith, catalog);
            service.BeginCast(MiracleType.Rain, Vector3.zero);

            service.UpdateCast(1f, Vector3.zero);

            Assert.IsTrue(service.IsCasting);
            Assert.AreEqual(2f, service.CurrentRadius, 0.001f);
        }

        [Test]
        public void UpdateCast_WithinGraceWindow_SpendsNothing_AndRadiusStaysBase()
        {
            // Льготное окно (game-design, playtest шага 5): человеческий тап (~0.1с) не должен
            // стоить дороже BaseFaithCost — hold-стоимость не списывается, радиус не растёт.
            var catalog = CreateCatalog(CreateMiracleConfig(
                baseFaithCost: 20f, holdCostPerSecond: 10f, baseRadius: 2f, maxRadius: 5f,
                holdSecondsToMaxRadius: 1.5f, holdGraceSeconds: 0.15f));
            var faith = new FakeFaithService { Current = 100f };
            var service = new MiracleService(new FakeEventBus(), faith, catalog);

            service.BeginCast(MiracleType.Rain, Vector3.zero); // списывает ровно BaseFaithCost (20)
            service.UpdateCast(0.05f, Vector3.zero);
            service.UpdateCast(0.05f, Vector3.zero); // суммарно 0.1с — в пределах grace (0.15с)

            Assert.AreEqual(80f, faith.Current, 0.001f); // 100 - BaseFaithCost, hold-стоимость не списана
            Assert.AreEqual(2f, service.CurrentRadius, 0.001f); // BaseRadius, накачка ещё не началась
        }

        [Test]
        public void UpdateCast_PastGraceWindow_SpendsAndGrowsRadius_FromGracePoint()
        {
            // После истечения grace-окна накачиваемое время растёт вместе с holdTime — за
            // HoldSecondsToMaxRadius накачиваемого времени радиус достигает MaxRadius.
            var catalog = CreateCatalog(CreateMiracleConfig(
                baseFaithCost: 0f, holdCostPerSecond: 10f, baseRadius: 2f, maxRadius: 5f,
                holdSecondsToMaxRadius: 1.5f, holdGraceSeconds: 0.15f));
            var faith = new FakeFaithService { Current = 1000f };
            var service = new MiracleService(new FakeEventBus(), faith, catalog);
            service.BeginCast(MiracleType.Rain, Vector3.zero);

            service.UpdateCast(0.15f, Vector3.zero); // ровно grace — накачка ещё не началась
            Assert.AreEqual(2f, service.CurrentRadius, 0.001f);
            Assert.AreEqual(1000f, faith.Current, 0.001f);

            service.UpdateCast(1.5f, Vector3.zero); // 1.5с накачиваемого времени сверх grace -> MaxRadius

            Assert.AreEqual(5f, service.CurrentRadius, 0.001f);
            Assert.AreEqual(1000f - 1.5f * 10f, faith.Current, 0.001f); // списано только за накачиваемое время
        }

        [Test]
        public void UpdateCast_WithoutSession_IsNoOp()
        {
            var catalog = CreateCatalog(CreateMiracleConfig());
            var faith = new FakeFaithService { Current = 100f };
            var service = new MiracleService(new FakeEventBus(), faith, catalog);

            Assert.DoesNotThrow(() => service.UpdateCast(1f, Vector3.zero));
            Assert.IsFalse(service.IsCasting);
            Assert.AreEqual(100f, faith.Current, 0.001f);
        }

        // --- ReleaseCast ---

        [Test]
        public void ReleaseCast_PublishesMiracleCastEvent_WithScaledPower_AndClosesSession()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(
                baseFaithCost: 0f, holdCostPerSecond: 0f, baseRadius: 2f, maxRadius: 4f, effectPower: 8f, holdSecondsToMaxRadius: 1f));
            var faith = new FakeFaithService { Current = 1000f };
            var eventBus = new FakeEventBus();
            var service = new MiracleService(eventBus, faith, catalog);
            var point = new Vector3(1f, 0f, 2f);
            service.BeginCast(MiracleType.Rain, point);
            service.UpdateCast(1f, point); // радиус -> MaxRadius (4)

            service.ReleaseCast();

            var evt = eventBus.PublishedEvents.OfType<MiracleCastEvent>().Single();
            Assert.AreEqual(MiracleType.Rain, evt.Type);
            Assert.AreEqual(point, evt.Point);
            Assert.AreEqual(16f, evt.Power, 0.001f); // EffectPower(8) * radius(4)/BaseRadius(2)
            Assert.AreEqual(4f, evt.Radius, 0.001f);
            Assert.IsFalse(service.IsCasting);
        }

        [Test]
        public void ReleaseCast_PublishesLatestPointFromUpdateCast_NotBeginCastPoint()
        {
            // Незначительное (ревью шага 4): точка сессии обновляется в UpdateCast (MiracleService.UpdateCast
            // переприсваивает _point) — ReleaseCast обязан опубликовать её, а не точку BeginCast, иначе
            // MiracleTargetingController.ReleaseCast() (release по _lastValidPoint) сработал бы мимо цели.
            var catalog = CreateCatalog(CreateMiracleConfig(baseFaithCost: 0f, holdCostPerSecond: 0f));
            var faith = new FakeFaithService { Current = 1000f };
            var eventBus = new FakeEventBus();
            var service = new MiracleService(eventBus, faith, catalog);
            var beginPoint = new Vector3(1f, 0f, 1f);
            var updatedPoint = new Vector3(5f, 0f, 9f);

            service.BeginCast(MiracleType.Rain, beginPoint);
            service.UpdateCast(0.1f, updatedPoint);
            service.ReleaseCast();

            var evt = eventBus.PublishedEvents.OfType<MiracleCastEvent>().Single();
            Assert.AreEqual(updatedPoint, evt.Point);
        }

        [Test]
        public void ReleaseCast_TapWithoutUpdate_UsesBaseRadius()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(baseFaithCost: 0f, baseRadius: 2f, effectPower: 8f));
            var faith = new FakeFaithService { Current = 100f };
            var eventBus = new FakeEventBus();
            var service = new MiracleService(eventBus, faith, catalog);

            service.BeginCast(MiracleType.Rain, Vector3.zero);
            service.ReleaseCast();

            var evt = eventBus.PublishedEvents.OfType<MiracleCastEvent>().Single();
            Assert.AreEqual(2f, evt.Radius, 0.001f);
            Assert.AreEqual(8f, evt.Power, 0.001f);
        }

        [Test]
        public void ReleaseCast_WithoutSession_DoesNotPublish()
        {
            var catalog = CreateCatalog(CreateMiracleConfig());
            var eventBus = new FakeEventBus();
            var service = new MiracleService(eventBus, new FakeFaithService(), catalog);

            service.ReleaseCast();

            Assert.IsEmpty(eventBus.PublishedEvents.OfType<MiracleCastEvent>());
        }

        // --- CancelCast ---

        [Test]
        public void CancelCast_ClosesSession_WithoutEvent_AndDoesNotRefundFaith()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(baseFaithCost: 20f));
            var faith = new FakeFaithService { Current = 100f };
            var eventBus = new FakeEventBus();
            var service = new MiracleService(eventBus, faith, catalog);
            service.BeginCast(MiracleType.Rain, Vector3.zero);

            service.CancelCast();

            Assert.IsFalse(service.IsCasting);
            Assert.IsEmpty(eventBus.PublishedEvents.OfType<MiracleCastEvent>());
            Assert.AreEqual(80f, faith.Current, 0.001f);
        }

        [Test]
        public void CancelCast_WithoutSession_IsNoOp()
        {
            var catalog = CreateCatalog(CreateMiracleConfig());
            var service = new MiracleService(new FakeEventBus(), new FakeFaithService(), catalog);

            Assert.DoesNotThrow(() => service.CancelCast());
            Assert.IsFalse(service.IsCasting);
        }

        [Test]
        public void CurrentRadius_OutsideSession_IsZero()
        {
            var catalog = CreateCatalog(CreateMiracleConfig());
            var service = new MiracleService(new FakeEventBus(), new FakeFaithService(), catalog);

            Assert.AreEqual(0f, service.CurrentRadius);
        }
    }
}
