using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Data.Configs.Worship;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Worship;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="AlignmentService"/> (game-design §4, §7, §19, шаг 5).</summary>
    public class AlignmentServiceTests
    {
        private static MiracleConfig CreateMiracleConfig(MiracleType type, float alignmentShift)
        {
            var config = ScriptableObject.CreateInstance<MiracleConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_type").enumValueIndex = (int)type;
            so.FindProperty("_alignmentShift").floatValue = alignmentShift;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>_miracles — приватный SerializeField на MiracleCatalogConfig, прошивается reflection'ом (см. MiracleCatalogConfigTests).</summary>
        private static MiracleCatalogConfig CreateCatalog(params MiracleConfig[] miracles)
        {
            var catalog = ScriptableObject.CreateInstance<MiracleCatalogConfig>();
            var field = typeof(MiracleCatalogConfig).GetField("_miracles", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(catalog, miracles);
            return catalog;
        }

        private static WorshipConfig CreateWorshipConfig(float handTerrorAlignmentShift = -3f)
        {
            var config = ScriptableObject.CreateInstance<WorshipConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_handTerrorAlignmentShift").floatValue = handTerrorAlignmentShift;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        [Test]
        public void Score_StartsAtZero()
        {
            var service = new AlignmentService(new EventBus(), CreateWorshipConfig(), CreateCatalog());

            Assert.AreEqual(0f, service.Score);
        }

        [Test]
        public void NeedResolved_ByPlayer_ShiftsByMiracleAlignmentShift()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain, 10f));
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(), catalog);

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));

            Assert.AreEqual(10f, service.Score, 0.001f);
        }

        [Test]
        public void NeedResolved_NotByPlayer_DoesNotShift()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain, 10f));
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(), catalog);

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: false, default));

            Assert.AreEqual(0f, service.Score, 0.001f);
        }

        [Test]
        public void NeedTriggered_ByPlayer_Fireball_ShiftsNegative()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Fireball, -10f));
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(), catalog);

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Fireball));

            Assert.AreEqual(-10f, service.Score, 0.001f);
        }

        [Test]
        public void NeedResolved_MiracleNotInCatalog_DoesNotShift()
        {
            var catalog = CreateCatalog(); // пустой каталог
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(), catalog);

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));

            Assert.AreEqual(0f, service.Score, 0.001f);
        }

        [Test]
        public void VillagerHardLanding_GrabbedThenTossedToStunned_ShiftsByHandTerrorAlignmentShift()
        {
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(handTerrorAlignmentShift: -3f), CreateCatalog());
            var id = VillagerId.FromInstanceId(1);

            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Tossed, VillagerStimulus.Grabbed));
            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Tossed, VillagerState.Stunned, VillagerStimulus.HardLanded));

            Assert.AreEqual(-3f, service.Score, 0.001f);
        }

        [Test]
        public void VillagerHardLanding_BlastedThenTossedToStunned_DoesNotShift()
        {
            // Симметрично WorshipService (аудит ВАЖНО 5) — Fireball уже даёт свой сдвиг через
            // NeedTriggeredEvent, Blasted -> Stunned не должен задваивать счёт.
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(handTerrorAlignmentShift: -3f), CreateCatalog());
            var id = VillagerId.FromInstanceId(1);

            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Tossed, VillagerStimulus.Blasted));
            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Tossed, VillagerState.Stunned, VillagerStimulus.HardLanded));

            Assert.AreEqual(0f, service.Score, 0.001f);
        }

        [Test]
        public void VillagerStateChanged_OtherTransition_DoesNotShift()
        {
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(), CreateCatalog());

            eventBus.Publish(new VillagerStateChangedEvent(VillagerId.FromInstanceId(1), VillagerState.Panic, VillagerState.Idle, VillagerStimulus.None));

            Assert.AreEqual(0f, service.Score, 0.001f);
        }

        [Test]
        public void Score_ClampsAtMax100()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain, 60f));
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(), catalog);

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 1, byPlayer: true, MiracleType.Rain));

            Assert.AreEqual(100f, service.Score, 0.001f);
        }

        [Test]
        public void Score_ClampsAtMinNegative100()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Fireball, -60f));
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(), catalog);

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Fireball));
            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 1, byPlayer: true, MiracleType.Fireball));

            Assert.AreEqual(-100f, service.Score, 0.001f);
        }

        [Test]
        public void EachRealShift_PublishesAlignmentChangedEvent()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain, 10f));
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(), catalog);

            AlignmentChangedEvent? captured = null;
            using (eventBus.Subscribe<AlignmentChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(0f, captured.Value.OldScore, 0.001f);
            Assert.AreEqual(10f, captured.Value.NewScore, 0.001f);
        }

        [Test]
        public void AtClampBoundary_FurtherShiftInSameDirection_DoesNotPublish()
        {
            var catalog = CreateCatalog(CreateMiracleConfig(MiracleType.Rain, 100f));
            var eventBus = new EventBus();
            var service = new AlignmentService(eventBus, CreateWorshipConfig(), catalog);
            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain)); // -> 100 (clamp)

            AlignmentChangedEvent? captured = null;
            using (eventBus.Subscribe<AlignmentChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 1, byPlayer: true, MiracleType.Rain)); // всё ещё 100
            }

            Assert.IsFalse(captured.HasValue);
        }
    }
}
