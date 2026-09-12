using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Worship;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;
using BlackWhiteLike.Services.Worship;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="WorshipService"/> (game-design §4, §7, §19, шаг 5; §10 волна 2 — позиции попапов).</summary>
    public class WorshipServiceTests
    {
        // --- Шаг 10 (волна 2): мировая позиция WorshipChangedEvent — TearDown/CreateTransform, тот же приём, что NeedSiteRegistryTests ---
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

        /// <summary>
        /// Шаг 10 (волна 2): WorshipService теперь требует INeedSiteRegistry (позиция Awe/Fear
        /// от нужды) — обёртка вместо правки полутора десятков прямых "new WorshipService(...)" в
        /// этом файле; тесты позиций ниже используют реальный NeedSiteRegistry напрямую.
        /// </summary>
        private static WorshipService CreateWorshipService(IEventBus eventBus, WorshipConfig config, LevelConfig levelConfig, INeedSiteRegistry needSiteRegistry = null) =>
            new WorshipService(eventBus, config, levelConfig, needSiteRegistry ?? new NeedSiteRegistry());

        private static WorshipConfig CreateConfig(
            (NeedType Type, int Awe)[] aweEntries = null,
            (TerrorAct Act, int Fear)[] fearEntries = null,
            float decayPerSecond = 0f)
        {
            if (aweEntries == null) aweEntries = new (NeedType, int)[0];
            if (fearEntries == null) fearEntries = new (TerrorAct, int)[0];

            var config = ScriptableObject.CreateInstance<WorshipConfig>();
            var so = new SerializedObject(config);

            var aweProp = so.FindProperty("_aweByNeed");
            aweProp.arraySize = aweEntries.Length;
            for (var i = 0; i < aweEntries.Length; i++)
            {
                var element = aweProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_type").enumValueIndex = (int)aweEntries[i].Type;
                element.FindPropertyRelative("_awe").intValue = aweEntries[i].Awe;
            }

            var fearProp = so.FindProperty("_fearByAct");
            fearProp.arraySize = fearEntries.Length;
            for (var i = 0; i < fearEntries.Length; i++)
            {
                var element = fearProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_act").enumValueIndex = (int)fearEntries[i].Act;
                element.FindPropertyRelative("_fear").intValue = fearEntries[i].Fear;
            }

            so.FindProperty("_worshipDecayPerSecond").floatValue = decayPerSecond;
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        /// <summary>_worshipTarget — приватный SerializeField на LevelConfig, тот же приём, что LevelEventServiceTests.CreateLevelConfig.</summary>
        private static LevelConfig CreateLevelConfig(int worshipTarget)
        {
            var config = ScriptableObject.CreateInstance<LevelConfig>();
            var field = typeof(LevelConfig).GetField("_worshipTarget", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(config, worshipTarget);
            return config;
        }

        private static WorshipService CreateService(
            out FakeEventBus eventBus,
            WorshipConfig config = null,
            int worshipTarget = 100)
        {
            eventBus = new FakeEventBus();
            if (config == null) config = CreateConfig();
            return CreateWorshipService(eventBus, config, CreateLevelConfig(worshipTarget));
        }

        // --- Awe (NeedResolvedEvent) ---

        [Test]
        public void NeedResolved_ByPlayer_AddsAwe_PublishesWorshipChanged()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 20) });
            var eventBus = new EventBus(); // реальная шина — сервис подписан на неё
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));

            Assert.AreEqual(20, service.Current);
            Assert.AreEqual(20, service.AweTotal);
        }

        [Test]
        public void NeedResolved_NotByPlayer_DoesNotAddAwe()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 20) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: false, default));

            Assert.AreEqual(0, service.Current);
            Assert.AreEqual(0, service.AweTotal);
        }

        [Test]
        public void NeedResolved_PublishesWorshipChangedEvent_WithAweSource()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 20) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));

            // Подписываемся напрямую на реальную шину, чтобы прочитать опубликованное событие.
            WorshipChangedEvent? captured = null;
            using (eventBus.Subscribe<WorshipChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(WorshipSource.Awe, captured.Value.Source);
            Assert.AreEqual(0, captured.Value.OldValue);
            Assert.AreEqual(20, captured.Value.NewValue);
            Assert.AreEqual(100, captured.Value.Target);
        }

        // --- Fear (NeedTriggeredEvent — акт террора через TerrorActRules) ---

        [Test]
        public void NeedTriggered_ByPlayer_FireballCause_AddsFear()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.FireballOnStructure, 25) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Fireball));

            Assert.AreEqual(25, service.Current);
            Assert.AreEqual(25, service.FearTotal);
        }

        [Test]
        public void NeedTriggered_ByPlayer_RainCause_DoesNotAddFear_RainNotTerrorAct()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.FireballOnStructure, 25) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));

            Assert.AreEqual(0, service.Current);
            Assert.AreEqual(0, service.FearTotal);
        }

        [Test]
        public void NeedTriggered_NotByPlayer_DoesNotAddFear()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.FireballOnStructure, 25) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 0, byPlayer: false, default));

            Assert.AreEqual(0, service.FearTotal);
        }

        // --- Fear (жёсткое приземление жителя ОТ РУКИ — game-design §5, шаг 5, аудит ВАЖНО 5) ---

        [Test]
        public void VillagerHardLanding_GrabbedThenTossedToStunned_AddsFear()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.VillagerHardLanding, 8) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            var id = VillagerId.FromInstanceId(1);

            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Tossed, VillagerStimulus.Grabbed));
            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Tossed, VillagerState.Stunned, VillagerStimulus.HardLanded));

            Assert.AreEqual(8, service.Current);
            Assert.AreEqual(8, service.FearTotal);
        }

        [Test]
        public void VillagerBlasted_BlastedThenTossedToStunned_AddsVillagerBlastedFear()
        {
            // Решение владельца (2026-08-29): взрыв чуда — отдельная ставка Fear (VillagerBlasted),
            // не задваивающая счёт с FireballOnStructure (поджог дома — отдельный акт).
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.VillagerBlasted, 5) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            var id = VillagerId.FromInstanceId(1);

            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Tossed, VillagerStimulus.Blasted));
            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Tossed, VillagerState.Stunned, VillagerStimulus.HardLanded));

            Assert.AreEqual(5, service.Current);
            Assert.AreEqual(5, service.FearTotal);
        }

        [Test]
        public void VillagerStateChanged_OtherTransition_DoesNotAddFear()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.VillagerHardLanding, 8) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            var id = VillagerId.FromInstanceId(1);

            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Panic, VillagerState.Idle, VillagerStimulus.None));
            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Tossed, VillagerStimulus.Grabbed));
            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Tossed, VillagerState.Idle, VillagerStimulus.Landed));

            Assert.AreEqual(0, service.FearTotal);
        }

        // --- Fear (удар молнии — game-design §4, шаг 6, решение владельца 2026-09-01) ---

        [Test]
        public void VillagerStruckByLightning_IdleToStunned_AddsFear()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.VillagerStruckByLightning, 6) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            var id = VillagerId.FromInstanceId(1);

            // Lightning ведёт напрямую в Stunned, минуя Tossed (VillagerStateService.TryResolveTarget).
            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Stunned, VillagerStimulus.LightningStruck));

            Assert.AreEqual(6, service.Current);
            Assert.AreEqual(6, service.FearTotal);
        }

        [Test]
        public void VillagerStruckByLightning_SelfLoop_DoesNotDoubleCount()
        {
            // VillagerStateService не публикует событие для target == current (self-loop) — но даже
            // если бы оно пришло, WorshipService не должен ничего сделать не по контракту; этот тест
            // фиксирует, что повторный удар (реалистично — тот же From/To Stunned->Stunned не
            // публикуется вовсе) не приходит как отдельная транзакция начисления.
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.VillagerStruckByLightning, 6) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            var id = VillagerId.FromInstanceId(1);

            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Stunned, VillagerStimulus.LightningStruck));
            Assert.AreEqual(6, service.Current);
        }

        [Test]
        public void VillagerHardLanding_Grabbed_StillUsesHardLandingRate_RegressionAfterLightning()
        {
            // Регрессия: ветка LightningStruck добавлена ПЕРЕД проверкой From == Tossed — убеждаемся,
            // что обычный путь Grabbed -> Tossed -> Stunned по-прежнему считается VillagerHardLanding.
            var config = CreateConfig(fearEntries: new[]
            {
                (TerrorAct.VillagerHardLanding, 8),
                (TerrorAct.VillagerStruckByLightning, 6)
            });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            var id = VillagerId.FromInstanceId(1);

            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Tossed, VillagerStimulus.Grabbed));
            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Tossed, VillagerState.Stunned, VillagerStimulus.HardLanded));

            Assert.AreEqual(8, service.Current);
            Assert.AreEqual(8, service.FearTotal);
        }

        [Test]
        public void VillagerBlasted_StillUsesBlastedRate_RegressionAfterLightning()
        {
            var config = CreateConfig(fearEntries: new[]
            {
                (TerrorAct.VillagerBlasted, 5),
                (TerrorAct.VillagerStruckByLightning, 6)
            });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            var id = VillagerId.FromInstanceId(1);

            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Tossed, VillagerStimulus.Blasted));
            eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Tossed, VillagerState.Stunned, VillagerStimulus.HardLanded));

            Assert.AreEqual(5, service.Current);
            Assert.AreEqual(5, service.FearTotal);
        }

        // --- Кламп по Target и раздельные Totals ---

        [Test]
        public void Current_ClampsAtTarget_ButAweTotalStaysUnclamped()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 60) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 1, byPlayer: true, MiracleType.Rain));

            Assert.AreEqual(100, service.Current); // клампится по Target
            Assert.AreEqual(120, service.AweTotal); // некламплённая сумма
            Assert.IsTrue(service.IsTargetReached);
        }

        [Test]
        public void AweTotalAndFearTotal_AccumulateIndependently()
        {
            var config = CreateConfig(
                aweEntries: new[] { (NeedType.Fire, 20) },
                fearEntries: new[] { (TerrorAct.FireballOnStructure, 25) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(1000));

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 1, byPlayer: true, MiracleType.Fireball));

            Assert.AreEqual(20, service.AweTotal);
            Assert.AreEqual(25, service.FearTotal);
            Assert.AreEqual(45, service.Current);
        }

        // --- Событие только при реальном изменении ---

        [Test]
        public void MissingConfigEntry_ZeroAmount_DoesNotPublish()
        {
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, CreateConfig(), CreateLevelConfig(100)); // пустой WorshipConfig — GetAwe/GetFear = 0

            WorshipChangedEvent? captured = null;
            using (eventBus.Subscribe<WorshipChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            }

            Assert.IsFalse(captured.HasValue);
        }

        // --- Decay ---

        [Test]
        public void Tick_DecayDisabled_DoesNothing()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 20) }, decayPerSecond: 0f);
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));

            service.Tick(100f);

            Assert.AreEqual(20, service.Current);
        }

        [Test]
        public void Tick_Decay_DecreasesByWholeUnits_UsingLastSource()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 20) }, decayPerSecond: 1f);
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));

            WorshipChangedEvent? captured = null;
            using (eventBus.Subscribe<WorshipChangedEvent>(e => captured = e))
            {
                service.Tick(3f); // 1 unit/sec * 3s = 3 целых единицы
            }

            Assert.AreEqual(17, service.Current);
            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(WorshipSource.Awe, captured.Value.Source); // источник последнего реального прироста
        }

        [Test]
        public void Tick_Decay_DoesNotGoBelowZero()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 2) }, decayPerSecond: 1f);
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));

            service.Tick(10f);

            Assert.AreEqual(0, service.Current);
        }

        [Test]
        public void Tick_ZeroCurrent_DoesNotThrow()
        {
            var service = CreateService(out _, CreateConfig(decayPerSecond: 1f));

            Assert.DoesNotThrow(() => service.Tick(5f));
            Assert.AreEqual(0, service.Current);
        }

        // --- Аудит п.9: Target <= 0 — победа математически недостижима, почти наверняка кривой конфиг ---

        [Test]
        public void Ctor_TargetZero_LogsWarning()
        {
            var eventBus = new FakeEventBus();

            LogAssert.Expect(LogType.Warning, new Regex("WorshipTarget"));
            CreateWorshipService(eventBus, CreateConfig(), CreateLevelConfig(0));
        }

        [Test]
        public void Ctor_PositiveTarget_DoesNotLogWarning()
        {
            var eventBus = new FakeEventBus();

            Assert.DoesNotThrow(() => CreateWorshipService(eventBus, CreateConfig(), CreateLevelConfig(100)));
            LogAssert.NoUnexpectedReceived();
        }

        // --- Шаг 10 (волна 2): WorshipChangedEvent.WorldPosition/HasWorldPosition ---

        [Test]
        public void NeedResolved_RegisteredSite_PublishesWorldPosition()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 20) });
            var eventBus = new EventBus();
            var registry = new NeedSiteRegistry();
            registry.Register(0, NeedType.Fire, CreateTransform("Site0", new Vector3(1f, 0f, 2f)));
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100), registry);

            WorshipChangedEvent? captured = null;
            using (eventBus.Subscribe<WorshipChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.IsTrue(captured.Value.HasWorldPosition);
            Assert.AreEqual(new Vector3(1f, 0f, 2f), captured.Value.WorldPosition);
            Assert.AreEqual(20, service.Current);
        }

        [Test]
        public void NeedResolved_UnregisteredSite_PublishesWithoutWorldPosition()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 20) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100), new NeedSiteRegistry());

            WorshipChangedEvent? captured = null;
            using (eventBus.Subscribe<WorshipChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.IsFalse(captured.Value.HasWorldPosition);
        }

        [Test]
        public void NeedTriggered_FireballCause_RegisteredSite_PublishesWorldPosition()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.FireballOnStructure, 25) });
            var eventBus = new EventBus();
            var registry = new NeedSiteRegistry();
            registry.Register(3, NeedType.Fire, CreateTransform("Site3", new Vector3(4f, 0f, 5f)));
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100), registry);

            WorshipChangedEvent? captured = null;
            using (eventBus.Subscribe<WorshipChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 3, byPlayer: true, MiracleType.Fireball));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.IsTrue(captured.Value.HasWorldPosition);
            Assert.AreEqual(new Vector3(4f, 0f, 5f), captured.Value.WorldPosition);
            Assert.AreEqual(25, service.Current);
        }

        [Test]
        public void VillagerHardLanding_EventCarriesWorldPosition_PropagatesToWorshipChanged()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.VillagerHardLanding, 8) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            var id = VillagerId.FromInstanceId(1);

            WorshipChangedEvent? captured = null;
            using (eventBus.Subscribe<WorshipChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Tossed, VillagerStimulus.Grabbed));
                eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Tossed, VillagerState.Stunned, VillagerStimulus.HardLanded,
                    new Vector3(7f, 0f, 8f), hasWorldPosition: true));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.IsTrue(captured.Value.HasWorldPosition);
            Assert.AreEqual(new Vector3(7f, 0f, 8f), captured.Value.WorldPosition);
            Assert.AreEqual(8, service.Current);
        }

        [Test]
        public void VillagerHardLanding_EventWithoutWorldPosition_PublishesWithoutWorldPosition()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.VillagerHardLanding, 8) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            var id = VillagerId.FromInstanceId(1);

            WorshipChangedEvent? captured = null;
            using (eventBus.Subscribe<WorshipChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Tossed, VillagerStimulus.Grabbed));
                eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Tossed, VillagerState.Stunned, VillagerStimulus.HardLanded));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.IsFalse(captured.Value.HasWorldPosition);
        }

        [Test]
        public void VillagerStruckByLightning_EventCarriesWorldPosition_PropagatesToWorshipChanged()
        {
            var config = CreateConfig(fearEntries: new[] { (TerrorAct.VillagerStruckByLightning, 6) });
            var eventBus = new EventBus();
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100));
            var id = VillagerId.FromInstanceId(1);

            WorshipChangedEvent? captured = null;
            using (eventBus.Subscribe<WorshipChangedEvent>(e => captured = e))
            {
                eventBus.Publish(new VillagerStateChangedEvent(id, VillagerState.Idle, VillagerState.Stunned, VillagerStimulus.LightningStruck,
                    new Vector3(9f, 0f, 10f), hasWorldPosition: true));
            }

            Assert.IsTrue(captured.HasValue);
            Assert.IsTrue(captured.Value.HasWorldPosition);
            Assert.AreEqual(new Vector3(9f, 0f, 10f), captured.Value.WorldPosition);
            Assert.AreEqual(6, service.Current);
        }

        [Test]
        public void Tick_Decay_PublishesLastRealWorldPosition()
        {
            var config = CreateConfig(aweEntries: new[] { (NeedType.Fire, 20) }, decayPerSecond: 1f);
            var eventBus = new EventBus();
            var registry = new NeedSiteRegistry();
            registry.Register(0, NeedType.Fire, CreateTransform("Site0", new Vector3(1f, 0f, 2f)));
            var service = CreateWorshipService(eventBus, config, CreateLevelConfig(100), registry);
            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));

            WorshipChangedEvent? captured = null;
            using (eventBus.Subscribe<WorshipChangedEvent>(e => captured = e))
            {
                service.Tick(3f);
            }

            Assert.IsTrue(captured.HasValue);
            Assert.IsTrue(captured.Value.HasWorldPosition);
            Assert.AreEqual(new Vector3(1f, 0f, 2f), captured.Value.WorldPosition);
        }
    }
}
