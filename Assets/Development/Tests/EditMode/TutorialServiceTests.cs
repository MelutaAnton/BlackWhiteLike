using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Tutorial;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Meta;
using BlackWhiteLike.Services.Tutorial;
using BlackWhiteLike.Services.Village;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="TutorialService"/> (шаг 10 «Polish»).</summary>
    public class TutorialServiceTests
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

        private sealed class FakeProfileStorage : IProfileStorage
        {
            private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
            private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
            private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();

            public bool HasKey(string key) => _ints.ContainsKey(key) || _floats.ContainsKey(key) || _strings.ContainsKey(key);
            public int GetInt(string key, int def = 0) => _ints.TryGetValue(key, out var v) ? v : def;
            public void SetInt(string key, int value) => _ints[key] = value;
            public float GetFloat(string key, float def = 0f) => _floats.TryGetValue(key, out var v) ? v : def;
            public void SetFloat(string key, float value) => _floats[key] = value;
            public string GetString(string key, string def = "") => _strings.TryGetValue(key, out var v) ? v : def;
            public void SetString(string key, string value) => _strings[key] = value;

            public void DeleteKey(string key)
            {
                _ints.Remove(key);
                _floats.Remove(key);
                _strings.Remove(key);
            }

            public void DeleteAll()
            {
                _ints.Clear();
                _floats.Clear();
                _strings.Clear();
            }

            public void Flush()
            {
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType()}");
            field.SetValue(target, value);
        }

        private static TutorialTrigger CreateTrigger(
            TutorialTriggerType type,
            NeedType needType = default,
            MiracleType miracleType = default,
            int worshipThreshold = 0)
        {
            object boxed = new TutorialTrigger();
            SetField(boxed, "_type", type);
            SetField(boxed, "_needType", needType);
            SetField(boxed, "_miracleType", miracleType);
            SetField(boxed, "_worshipThreshold", worshipThreshold);
            return (TutorialTrigger)boxed;
        }

        private static TutorialStep CreateStep(
            TutorialTrigger activation,
            TutorialTrigger completion,
            int targetCount = 1)
        {
            var step = new TutorialStep();
            SetField(step, "_activationTrigger", activation);
            SetField(step, "_completionTrigger", completion);
            SetField(step, "_targetCount", targetCount);
            SetField(step, "_text", new LocalizedString("шаг", "step"));
            SetField(step, "_pointerTarget", TutorialPointerTarget.None);
            SetField(step, "_highlightMiracleButton", false);
            SetField(step, "_highlightedMiracle", default(MiracleType));
            return step;
        }

        private static TutorialConfig CreateConfig(LevelId targetLevel, params TutorialStep[] steps)
        {
            var config = ScriptableObject.CreateInstance<TutorialConfig>();
            SetField(config, "_targetLevel", targetLevel);
            SetField(config, "_steps", steps);
            return config;
        }

        private static LevelConfig CreateLevel(LevelId id)
        {
            var level = ScriptableObject.CreateInstance<LevelConfig>();
            typeof(LevelConfig).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(level, id);
            return level;
        }

        // --- Playtest-фикс шага 10 (2026-09-11): "бросок жителя рукой" теперь пара ObjectGrabbedEvent
        // + ObjectThrownEvent (VillagerTossRules.IsVillagerThrow), не VillagerStateChangedEvent — тот
        // детектил сам ЗАХВАТ (FSM жителя входит в Tossed уже при захвате), а не бросок. ---

        private static readonly GrabbableId TestVillagerGrabId = GrabbableId.FromInstanceId(1);
        private static readonly Vector3 ThrowVelocity = new Vector3(5f, 0f, 0f); // заведомо выше ThrowVelocityEpsilonSqr
        private static readonly Vector3 SoftPlacementVelocity = Vector3.zero; // мягкая постановка — не бросок

        /// <summary>Публикует полную пару "захват жителя -> реальный бросок" (см. блок выше).</summary>
        private static void PublishVillagerThrow(EventBus eventBus, GrabbableId? id = null)
        {
            var grabId = id ?? TestVillagerGrabId;
            eventBus.Publish(new ObjectGrabbedEvent(grabId, GrabbableKind.Villager));
            eventBus.Publish(new ObjectThrownEvent(grabId, ThrowVelocity));
        }

        private static TutorialService CreateService(
            out EventBus eventBus,
            out PlayerProfile profile,
            TutorialConfig config,
            LevelId levelId,
            bool tutorialAlreadyCompleted = false)
        {
            eventBus = new EventBus();
            profile = new PlayerProfile(new FakeProfileStorage());
            if (tutorialAlreadyCompleted) profile.MarkTutorialCompleted();

            return new TutorialService(eventBus, config, profile, CreateLevel(levelId), new NeedSiteRegistry());
        }

        // --- Активность туториала (уровень / TutorialCompleted) ---

        [Test]
        public void Ctor_LevelMismatch_IsInactive()
        {
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.WorshipReached, worshipThreshold: 999));
            var config = CreateConfig(LevelId.Level01, step);

            var service = CreateService(out _, out _, config, LevelId.Level02);

            Assert.IsFalse(service.IsActive);
            Assert.IsFalse(service.HasVisibleStep);
        }

        [Test]
        public void Ctor_TutorialAlreadyCompleted_IsInactive()
        {
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.WorshipReached, worshipThreshold: 999));
            var config = CreateConfig(LevelId.Level01, step);

            var service = CreateService(out _, out _, config, LevelId.Level01, tutorialAlreadyCompleted: true);

            Assert.IsFalse(service.IsActive);
            Assert.IsFalse(service.HasVisibleStep);
        }

        [Test]
        public void Ctor_MatchingLevel_NotCompleted_IsActive_FirstStepImmediatelyVisible()
        {
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.WorshipReached, worshipThreshold: 999));
            var config = CreateConfig(LevelId.Level01, step);

            var service = CreateService(out _, out _, config, LevelId.Level01);

            Assert.IsTrue(service.IsActive);
            Assert.IsTrue(service.HasVisibleStep); // ActivationTrigger = None — показывается сразу
            Assert.AreSame(step, service.CurrentStep);
        }

        // --- Activation-гейт ---

        [Test]
        public void ActivationTrigger_NeedTriggered_HidesStepUntilMatchingEvent()
        {
            var step = CreateStep(
                CreateTrigger(TutorialTriggerType.NeedTriggered, needType: NeedType.Fire),
                CreateTrigger(TutorialTriggerType.WorshipReached, worshipThreshold: 999));
            var config = CreateConfig(LevelId.Level01, step);
            var service = CreateService(out var eventBus, out _, config, LevelId.Level01);

            Assert.IsFalse(service.HasVisibleStep);

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Hunger, 0, byPlayer: false, default)); // не тот NeedType
            Assert.IsFalse(service.HasVisibleStep);

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 0, byPlayer: false, default));
            Assert.IsTrue(service.HasVisibleStep);
        }

        // --- Счётчик TargetCount ---

        [Test]
        public void CompletionTrigger_TargetCountGreaterThanOne_RequiresMultipleMatches()
        {
            var step = CreateStep(
                CreateTrigger(TutorialTriggerType.None),
                CreateTrigger(TutorialTriggerType.NeedResolvedByPlayer, needType: NeedType.Fire),
                targetCount: 3);
            var config = CreateConfig(LevelId.Level01, step);
            var service = CreateService(out var eventBus, out _, config, LevelId.Level01);

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            Assert.AreEqual(1, service.CurrentProgress);
            Assert.IsTrue(service.HasVisibleStep); // шаг ещё тот же (единственный) — targetCount не достигнут

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            Assert.AreEqual(2, service.CurrentProgress);

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: true, MiracleType.Rain));
            Assert.IsFalse(service.HasVisibleStep); // единственный шаг пройден — туториал завершён
        }

        [Test]
        public void CompletionTrigger_NeedResolved_NotByPlayer_DoesNotAdvance()
        {
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.NeedResolvedByPlayer, needType: NeedType.Fire));
            var config = CreateConfig(LevelId.Level01, step);
            var service = CreateService(out var eventBus, out _, config, LevelId.Level01);

            eventBus.Publish(new NeedResolvedEvent(NeedType.Fire, 0, byPlayer: false, default));

            Assert.AreEqual(0, service.CurrentProgress);
            Assert.AreSame(step, service.CurrentStep);
        }

        // --- Житель, брошенный рукой vs захват/мягкая постановка/чужой предмет (playtest-фикс шага 10) ---

        [Test]
        public void VillagerTossedByHand_RealThrow_MatchesTrigger()
        {
            var stepA = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.VillagerTossedByHand));
            var stepB = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.WorshipReached, worshipThreshold: 999));
            var config = CreateConfig(LevelId.Level01, stepA, stepB);
            var service = CreateService(out var eventBus, out _, config, LevelId.Level01);

            PublishVillagerThrow(eventBus);

            Assert.AreSame(stepB, service.CurrentStep);
        }

        [Test]
        public void VillagerTossedByHand_GrabWithoutThrow_DoesNotMatchTrigger()
        {
            // Регресс playtest-бага шага 10 (2026-09-11): дом реагировал уже на ВЗЯТИЕ жителя в руку.
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.VillagerTossedByHand));
            var config = CreateConfig(LevelId.Level01, step);
            var service = CreateService(out var eventBus, out _, config, LevelId.Level01);

            eventBus.Publish(new ObjectGrabbedEvent(TestVillagerGrabId, GrabbableKind.Villager)); // только захват, без отпускания

            Assert.AreEqual(0, service.CurrentProgress);
            Assert.AreSame(step, service.CurrentStep);
        }

        [Test]
        public void VillagerTossedByHand_SoftPlacement_DoesNotMatchTrigger()
        {
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.VillagerTossedByHand));
            var config = CreateConfig(LevelId.Level01, step);
            var service = CreateService(out var eventBus, out _, config, LevelId.Level01);

            eventBus.Publish(new ObjectGrabbedEvent(TestVillagerGrabId, GrabbableKind.Villager));
            eventBus.Publish(new ObjectThrownEvent(TestVillagerGrabId, SoftPlacementVelocity));

            Assert.AreEqual(0, service.CurrentProgress);
            Assert.AreSame(step, service.CurrentStep);
        }

        [Test]
        public void VillagerTossedByHand_ThrowableObjectThrown_DoesNotMatchTrigger()
        {
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.VillagerTossedByHand));
            var config = CreateConfig(LevelId.Level01, step);
            var service = CreateService(out var eventBus, out _, config, LevelId.Level01);
            var throwableId = GrabbableId.FromInstanceId(2);

            eventBus.Publish(new ObjectGrabbedEvent(throwableId, GrabbableKind.Throwable));
            eventBus.Publish(new ObjectThrownEvent(throwableId, ThrowVelocity));

            Assert.AreEqual(0, service.CurrentProgress);
            Assert.AreSame(step, service.CurrentStep);
        }

        // --- Фильтр MiracleType ---

        [Test]
        public void MiracleSelected_Filter_OnlyMatchingTypeAdvances()
        {
            var stepA = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.MiracleSelected, miracleType: MiracleType.Rain));
            var stepB = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.WorshipReached, worshipThreshold: 999));
            var config = CreateConfig(LevelId.Level01, stepA, stepB);
            var service = CreateService(out var eventBus, out _, config, LevelId.Level01);

            eventBus.Publish(new MiracleSelectedEvent(MiracleType.Fireball, true));
            Assert.AreSame(stepA, service.CurrentStep); // другое чудо — без продвижения

            eventBus.Publish(new MiracleSelectedEvent(MiracleType.Rain, false));
            Assert.AreSame(stepA, service.CurrentStep); // Selected=false — без продвижения

            eventBus.Publish(new MiracleSelectedEvent(MiracleType.Rain, true));
            Assert.AreSame(stepB, service.CurrentStep);
        }

        // --- WorshipReached ---

        [Test]
        public void WorshipReached_BelowThreshold_DoesNotAdvance_AtThreshold_Advances()
        {
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.WorshipReached, worshipThreshold: 50));
            var config = CreateConfig(LevelId.Level01, step);
            var service = CreateService(out var eventBus, out var profile, config, LevelId.Level01);

            eventBus.Publish(new WorshipChangedEvent(0, 20, 100, WorshipSource.Awe));
            Assert.IsTrue(service.HasVisibleStep);
            Assert.IsFalse(profile.TutorialCompleted);

            eventBus.Publish(new WorshipChangedEvent(20, 50, 100, WorshipSource.Awe));
            Assert.IsFalse(service.HasVisibleStep); // единственный шаг — туториал завершён
            Assert.IsTrue(profile.TutorialCompleted);
        }

        // --- Завершение последнего шага и инертность после ---

        [Test]
        public void LastStep_Completion_MarksTutorialCompleted_AndBecomesInert()
        {
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.VillagerTossedByHand));
            var config = CreateConfig(LevelId.Level01, step);
            var service = CreateService(out var eventBus, out var profile, config, LevelId.Level01);

            PublishVillagerThrow(eventBus);

            Assert.IsFalse(service.HasVisibleStep);
            Assert.IsTrue(profile.TutorialCompleted);

            // Дальнейшие события — no-op, без исключений.
            Assert.DoesNotThrow(() => PublishVillagerThrow(eventBus, GrabbableId.FromInstanceId(2)));
            Assert.IsFalse(service.HasVisibleStep);
        }

        // --- БЛОКЕР 1: событие активации приходит раньше, чем шаг стал текущим ---

        [Test]
        public void ActivationEvent_ArrivesBeforeStepIsCurrent_LatchesUntilStepAdvances()
        {
            var step1 = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.VillagerTossedByHand));
            var step2 = CreateStep(
                CreateTrigger(TutorialTriggerType.NeedTriggered, needType: NeedType.Fire),
                CreateTrigger(TutorialTriggerType.MiracleSelected, miracleType: MiracleType.Rain));
            var config = CreateConfig(LevelId.Level01, step1, step2);
            var service = CreateService(out var eventBus, out _, config, LevelId.Level01);

            // Таймлайн публикует NeedTriggeredEvent(Fire) РОВНО ОДИН РАЗ, пока игрок ещё не бросил
            // жителя (шаг 1 всё ещё текущий) — событие не должно потеряться.
            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 0, byPlayer: false, default));
            Assert.AreSame(step1, service.CurrentStep);

            PublishVillagerThrow(eventBus);

            // Шаг 2 стал текущим и виден СРАЗУ — латч активации отработал ретроактивно.
            Assert.IsTrue(service.HasVisibleStep);
            Assert.AreSame(step2, service.CurrentStep);
        }

        // --- БЛОКЕР 1: цель шага истекает раньше, чем игрок успевает её выполнить ---

        [Test]
        public void CompletionTarget_NeedExpiresBeforePlayerResolves_StepAdvancesAnyway()
        {
            var step1 = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.VillagerTossedByHand));
            var step2 = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.NeedResolvedByPlayer, needType: NeedType.Fire));
            var config = CreateConfig(LevelId.Level01, step1, step2);
            var service = CreateService(out var eventBus, out var profile, config, LevelId.Level01);

            PublishVillagerThrow(eventBus);
            Assert.AreSame(step2, service.CurrentStep);

            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 0, byPlayer: false, default));
            eventBus.Publish(new NeedExpiredEvent(NeedType.Fire, 0, byPlayer: false));

            // Цель шага (разреши Fire) стала недостижимой — сервис не должен зависнуть навсегда.
            Assert.IsFalse(service.HasVisibleStep);
            Assert.IsTrue(profile.TutorialCompleted);
        }

        // --- ВАЖНО 6: уровень пройден раньше, чем игрок долистал все шаги ---

        [Test]
        public void LevelCompletedEvent_WhileActive_MarksTutorialCompleted_AndHidesCurrentStep()
        {
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.WorshipReached, worshipThreshold: 999));
            var config = CreateConfig(LevelId.Level01, step);
            var service = CreateService(out var eventBus, out var profile, config, LevelId.Level01);

            Assert.IsTrue(service.HasVisibleStep);
            Assert.IsFalse(profile.TutorialCompleted);

            eventBus.Publish(new LevelCompletedEvent(LevelId.Level01, 100, 0, 60f));

            Assert.IsTrue(profile.TutorialCompleted);
            Assert.IsFalse(service.HasVisibleStep);
        }

        // --- БЛОКЕР 2: указатель резолвится по типу нужды ТЕКУЩЕГО шага ---

        [Test]
        public void TryGetWorldTargetPosition_ResolvesByCurrentStepNeedType_WithMultipleActiveNeeds()
        {
            var step = CreateStep(CreateTrigger(TutorialTriggerType.None), CreateTrigger(TutorialTriggerType.NeedResolvedByPlayer, needType: NeedType.Fire));
            var config = CreateConfig(LevelId.Level01, step);
            var registry = new NeedSiteRegistry();
            var eventBus = new EventBus();
            var profile = new PlayerProfile(new FakeProfileStorage());
            var service = new TutorialService(eventBus, config, profile, CreateLevel(LevelId.Level01), registry);

            var fireSite = CreateTransform("FireSite", new Vector3(1f, 0f, 0f));
            var diseaseSite = CreateTransform("DiseaseSite", new Vector3(9f, 0f, 0f));
            registry.Register(0, NeedType.Fire, fireSite);
            registry.Register(1, NeedType.Disease, diseaseSite);

            // Обе нужды активны одновременно — указатель обязан вести именно к Fire (нужда шага),
            // не к последней сработавшей нужде любого типа (Disease здесь сработала последней).
            eventBus.Publish(new NeedTriggeredEvent(NeedType.Disease, 1, byPlayer: false, default));
            eventBus.Publish(new NeedTriggeredEvent(NeedType.Fire, 0, byPlayer: false, default));

            Assert.IsTrue(service.TryGetWorldTargetPosition(out var position));
            Assert.AreEqual(fireSite.position, position);
        }
    }
}
