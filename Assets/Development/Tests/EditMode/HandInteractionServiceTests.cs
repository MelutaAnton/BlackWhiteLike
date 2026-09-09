using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BlackWhiteLike.Core.Interaction;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Hand;
using BlackWhiteLike.Services.Meta;

namespace BlackWhiteLike.Tests
{
    /// <summary>Тесты <see cref="HandInteractionService"/> (game-design §19, шаг 2; ревью шага 2 — Б1, В2, В9, В15; §8, шаг 8 — эффективные статы).</summary>
    public class HandInteractionServiceTests
    {
        /// <summary>
        /// Фейковый IGrabbable для тестов CanGrab — без Unity-сцены, Body может быть null
        /// (проверяется отдельным кейсом) либо реальным Rigidbody на временном GameObject'е,
        /// который тест обязан уничтожить.
        /// </summary>
        private sealed class FakeGrabbable : IGrabbable
        {
            public GrabbableId Id => default;
            public GrabbableKind Kind => GrabbableKind.Throwable;
            public Rigidbody Body { get; set; }
            public bool CanBeGrabbed { get; set; } = true;
            public void OnGrabbed() { }
            public void OnReleased(Vector3 throwVelocity) { }
        }

        /// <summary>
        /// HandConfig не выставляет сеттеры на публичные свойства (namespace Data/Configs —
        /// только SerializeField + геттеры, как у всех SO проекта). Тесты прошивают приватные
        /// поля через SerializedObject — тот же приём, что используют editor-скрипты проекта
        /// для заполнения Composition Root (.claude/rules/editor-files.md), здесь применён к
        /// созданному в памяти ScriptableObject.CreateInstance. Это чище, чем заводить
        /// internal-метод инициализации только ради тестов.
        /// </summary>
        private static HandConfig CreateConfig(
            float maxGrabMass = 20f,
            float carryHeight = 1.5f,
            float followStiffness = 12f,
            float followMaxSpeed = 15f,
            float throwForceScale = 1f,
            float maxThrowForce = 20f,
            float throwVelocitySampleWindow = 0.2f,
            float softPlacementSpeedThreshold = 0.05f)
        {
            var config = ScriptableObject.CreateInstance<HandConfig>();
            var so = new SerializedObject(config);
            so.FindProperty("_maxGrabMass").floatValue = maxGrabMass;
            so.FindProperty("_carryHeight").floatValue = carryHeight;
            so.FindProperty("_followStiffness").floatValue = followStiffness;
            so.FindProperty("_followMaxSpeed").floatValue = followMaxSpeed;
            so.FindProperty("_throwForceScale").floatValue = throwForceScale;
            so.FindProperty("_maxThrowForce").floatValue = maxThrowForce;
            so.FindProperty("_throwVelocitySampleWindow").floatValue = throwVelocitySampleWindow;
            so.FindProperty("_softPlacementSpeedThreshold").floatValue = softPlacementSpeedThreshold;
            // _landingImpactThreshold удалён из HandConfig на шаге 3 (перенесён в ThrowableConfig —
            // см. ThrowableConfig.cs, HandConfig.cs), поэтому здесь больше не прошивается.
            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private static HandInteractionService CreateService(HandConfig config, IEffectiveStatsService stats = null) =>
            new HandInteractionService(config, stats ?? new FakeEffectiveStatsService());

        // --- CanGrab ---

        [Test]
        public void CanGrab_NullGrabbable_ReturnsFalse()
        {
            var service = CreateService(CreateConfig());

            Assert.IsFalse(service.CanGrab(null));
        }

        [Test]
        public void CanGrab_CanBeGrabbedFalse_ReturnsFalse()
        {
            var service = CreateService(CreateConfig());
            var go = new GameObject("FakeBody", typeof(Rigidbody));
            try
            {
                var body = go.GetComponent<Rigidbody>();
                body.mass = 1f;
                var grabbable = new FakeGrabbable { Body = body, CanBeGrabbed = false };

                Assert.IsFalse(service.CanGrab(grabbable));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CanGrab_BodyIsNull_ReturnsFalse()
        {
            // В15 (ревью шага 2): комментарий у FakeGrabbable давно обещал этот кейс, теста не было.
            var service = CreateService(CreateConfig());
            var grabbable = new FakeGrabbable { Body = null, CanBeGrabbed = true };

            Assert.IsFalse(service.CanGrab(grabbable));
        }

        [Test]
        public void CanGrab_MassAboveLimit_ReturnsFalse()
        {
            var service = CreateService(CreateConfig(maxGrabMass: 10f));
            var go = new GameObject("FakeBody", typeof(Rigidbody));
            try
            {
                var body = go.GetComponent<Rigidbody>();
                body.mass = 10.01f;
                var grabbable = new FakeGrabbable { Body = body, CanBeGrabbed = true };

                Assert.IsFalse(service.CanGrab(grabbable));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CanGrab_MassWithinLimit_ReturnsTrue()
        {
            var service = CreateService(CreateConfig(maxGrabMass: 10f));
            var go = new GameObject("FakeBody", typeof(Rigidbody));
            try
            {
                var body = go.GetComponent<Rigidbody>();
                body.mass = 10f;
                var grabbable = new FakeGrabbable { Body = body, CanBeGrabbed = true };

                Assert.IsTrue(service.CanGrab(grabbable));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CanGrab_MassAboveRawLimit_ButWithinBoostedLimit_ReturnsTrue()
        {
            // Шаг 8: HandMassMultiplier > 1 (апгрейд HandPower) поднимает реальный предел массы.
            var stats = new FakeEffectiveStatsService { HandMassMultiplier = 2f };
            var service = CreateService(CreateConfig(maxGrabMass: 10f), stats);
            var go = new GameObject("FakeBody", typeof(Rigidbody));
            try
            {
                var body = go.GetComponent<Rigidbody>();
                body.mass = 15f; // выше raw 10, но в пределах 10*2=20
                var grabbable = new FakeGrabbable { Body = body, CanBeGrabbed = true };

                Assert.IsTrue(service.CanGrab(grabbable));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // --- IsHolding / BeginHold / EndHold ---

        [Test]
        public void IsHolding_TogglesWithBeginAndEndHold()
        {
            var service = CreateService(CreateConfig());

            Assert.IsFalse(service.IsHolding);

            service.BeginHold(Vector3.zero, 0f);
            Assert.IsTrue(service.IsHolding);

            service.EndHold();
            Assert.IsFalse(service.IsHolding);
        }

        [Test]
        public void BeginHold_ClearsPreviousHistory()
        {
            // Окно = 2с, специально широкое: если бы BeginHold НЕ чистил историю, семплы первого
            // удержания (t=0, t=1) остались бы валидны и на момент второго BeginHold (t=2).
            var service = CreateService(CreateConfig(throwVelocitySampleWindow: 2f, maxThrowForce: 1000f));

            service.BeginHold(Vector3.zero, 0f);
            service.RecordSample(new Vector3(10f, 0f, 0f), 1f);

            service.BeginHold(new Vector3(5f, 0f, 0f), 2f);
            var velocityRightAfterBeginHold = service.CalculateThrowVelocity(2f);

            // Сразу после нового BeginHold в истории только 1 семпл — недостаточно для броска.
            Assert.AreEqual(Vector3.zero, velocityRightAfterBeginHold);
        }

        [Test]
        public void EndHold_ClearsHistory_SubsequentThrowIsZero()
        {
            var service = CreateService(CreateConfig());

            service.BeginHold(Vector3.zero, 0f);
            service.RecordSample(new Vector3(5f, 0f, 0f), 1f);
            service.EndHold();

            var velocity = service.CalculateThrowVelocity(1f);

            Assert.AreEqual(Vector3.zero, velocity);
        }

        // --- CalculateThrowVelocity ---

        [Test]
        public void CalculateThrowVelocity_FewerThanTwoSamples_ReturnsZero()
        {
            var service = CreateService(CreateConfig());

            service.BeginHold(Vector3.zero, 0f);

            var velocity = service.CalculateThrowVelocity(0f);

            Assert.AreEqual(Vector3.zero, velocity);
        }

        [Test]
        public void CalculateThrowVelocity_NoSamplesAtAll_ReturnsZero()
        {
            var service = CreateService(CreateConfig());

            var velocity = service.CalculateThrowVelocity(1f);

            Assert.AreEqual(Vector3.zero, velocity);
        }

        [Test]
        public void CalculateThrowVelocity_SamplesOlderThanWindow_AreIgnored()
        {
            // Окно 0.2с. Старый семпл на t=0 (сместился на 100м — если бы учитывался, дал бы
            // огромную скорость), затем в окне два свежих семпла с маленьким смещением.
            var service = CreateService(CreateConfig(throwVelocitySampleWindow: 0.2f, throwForceScale: 1f, maxThrowForce: 1000f));

            service.BeginHold(new Vector3(100f, 0f, 0f), 0f); // старее окна к моменту t=1
            service.RecordSample(new Vector3(0f, 0f, 0f), 0.9f);
            service.RecordSample(new Vector3(1f, 0f, 0f), 1f);

            var velocity = service.CalculateThrowVelocity(1f);

            // Только последние два семпла валидны: (1,0,0)-(0,0,0) за 0.1с = скорость (10,0,0).
            Assert.AreEqual(10f, velocity.x, 0.001f);
            Assert.AreEqual(0f, velocity.y, 0.001f);
            Assert.AreEqual(0f, velocity.z, 0.001f);
        }

        [Test]
        public void CalculateThrowVelocity_WindowLeavesExactlyOneValidSample_ReturnsZero()
        {
            // В15 (ревью шага 2): "в окне остался ровно один валидный семпл" — отдельно от кейса
            // "семплов вообще меньше двух" (там их физически 1-0), здесь их несколько, но окно
            // (0.1с, windowStart=0.9) вырезает все, кроме последнего.
            var service = CreateService(CreateConfig(throwVelocitySampleWindow: 0.1f, maxThrowForce: 1000f));

            service.BeginHold(new Vector3(0f, 0f, 0f), 0f);
            service.RecordSample(new Vector3(1f, 0f, 0f), 0.85f);
            service.RecordSample(new Vector3(2f, 0f, 0f), 1f);

            var velocity = service.CalculateThrowVelocity(1f);

            Assert.AreEqual(Vector3.zero, velocity);
        }

        [Test]
        public void CalculateThrowVelocity_TwoSamplesAtSameTime_ReturnsZero()
        {
            // В15 (ревью шага 2): dt <= 0 — например, два семпла в один и тот же render-кадр
            // (одинаковый unscaledTime). Деление на 0 не должно давать NaN/Infinity, только 0.
            var service = CreateService(CreateConfig(throwVelocitySampleWindow: 2f, maxThrowForce: 1000f));

            service.BeginHold(Vector3.zero, 1f);
            service.RecordSample(new Vector3(5f, 0f, 0f), 1f);

            var velocity = service.CalculateThrowVelocity(1f);

            Assert.AreEqual(Vector3.zero, velocity);
        }

        [Test]
        public void CalculateThrowVelocity_ScalesByThrowForceScale()
        {
            // Окно должно перекрывать интервал между семплами (1с) — иначе старый семпл отфильтруется
            // как "старше окна" (см. CalculateThrowVelocity_SamplesOlderThanWindow_AreIgnored) и тест
            // проверял бы не то, что заявлено в имени.
            var service = CreateService(CreateConfig(throwForceScale: 3f, maxThrowForce: 1000f, throwVelocitySampleWindow: 2f));

            service.BeginHold(Vector3.zero, 0f);
            service.RecordSample(new Vector3(2f, 0f, 0f), 1f);

            var velocity = service.CalculateThrowVelocity(1f);

            // Сырая скорость (2,0,0)/1с = (2,0,0), умножена на ThrowForceScale=3 → (6,0,0).
            Assert.AreEqual(6f, velocity.x, 0.001f);
        }

        [Test]
        public void CalculateThrowVelocity_ClampsToMaxThrowForce()
        {
            var service = CreateService(CreateConfig(throwForceScale: 1f, maxThrowForce: 5f, throwVelocitySampleWindow: 2f));

            service.BeginHold(Vector3.zero, 0f);
            service.RecordSample(new Vector3(100f, 0f, 0f), 1f); // сырая скорость 100 >> maxThrowForce

            var velocity = service.CalculateThrowVelocity(1f);

            Assert.AreEqual(5f, velocity.magnitude, 0.001f);
            Assert.Greater(velocity.x, 0f); // направление сохранено
        }

        [Test]
        public void CalculateThrowVelocity_ClampsToBoostedMaxThrowForce_FromEffectiveStats()
        {
            // Шаг 8: HandForceMultiplier > 1 (апгрейд HandPower) поднимает реальный предел броска.
            var stats = new FakeEffectiveStatsService { HandForceMultiplier = 2f };
            var service = CreateService(CreateConfig(throwForceScale: 1f, maxThrowForce: 5f, throwVelocitySampleWindow: 2f), stats);

            service.BeginHold(Vector3.zero, 0f);
            service.RecordSample(new Vector3(100f, 0f, 0f), 1f); // сырая скорость 100 >> maxThrowForce*multiplier(10)

            var velocity = service.CalculateThrowVelocity(1f);

            Assert.AreEqual(10f, velocity.magnitude, 0.001f);
        }

        [Test]
        public void RecordSample_ExceedsCapacityWithinWindow_RingBufferWrapsWithoutCorruption()
        {
            // В2 (ревью шага 2): ёмкость буфера рассчитана на AssumedMaxSampleRateHz (480 Гц) в
            // пределах окна. Здесь сознательно пишем плотнее (эффективно ~10 000 Гц), чтобы
            // гарантированно переполнить буфер, ДАЖЕ когда все семплы по времени попадают в окно
            // (window=1с), и убедиться, что расчёт не "тянет" вытесненный первый (аномальный) семпл.
            var service = CreateService(CreateConfig(
                throwForceScale: 1f, maxThrowForce: 1_000_000f, throwVelocitySampleWindow: 1f));

            service.BeginHold(new Vector3(100000f, 0f, 0f), 0f); // аномальный первый семпл — должен быть вытеснен

            const int sampleCount = 600; // заведомо больше расчётной ёмкости при window=1с (ceil(1*480)+4=484)
            for (var i = 1; i <= sampleCount; i++)
            {
                service.RecordSample(new Vector3(i * 0.001f, 0f, 0f), i * 0.0001f);
            }

            var velocity = service.CalculateThrowVelocity(sampleCount * 0.0001f);

            // Если бы буфер не вытеснил семпл BeginHold (100000, t=0), скорость была бы огромной
            // (сотни тысяч м/с). После вытеснения — скорость в разумных пределах жеста.
            Assert.Less(velocity.magnitude, 1000f);
            Assert.IsFalse(float.IsNaN(velocity.magnitude));
        }

        // --- CalculateFollowVelocity ---

        [Test]
        public void CalculateFollowVelocity_PointsTowardTarget()
        {
            var service = CreateService(CreateConfig(followStiffness: 2f, followMaxSpeed: 1000f));

            var velocity = service.CalculateFollowVelocity(Vector3.zero, new Vector3(3f, 0f, 0f));

            // (target - current) * stiffness = (3,0,0) * 2 = (6,0,0).
            Assert.AreEqual(6f, velocity.x, 0.001f);
        }

        [Test]
        public void CalculateFollowVelocity_ClampsToFollowMaxSpeed()
        {
            var service = CreateService(CreateConfig(followStiffness: 10f, followMaxSpeed: 4f));

            var velocity = service.CalculateFollowVelocity(Vector3.zero, new Vector3(100f, 0f, 0f));

            Assert.AreEqual(4f, velocity.magnitude, 0.001f);
        }

        [Test]
        public void CalculateFollowVelocity_ClampsToFollowMaxSpeed_PreservesDirection()
        {
            // В15 (ревью шага 2): раньше проверялся только модуль, не направление.
            var service = CreateService(CreateConfig(followStiffness: 10f, followMaxSpeed: 4f));

            var target = new Vector3(100f, 0f, 50f);
            var velocity = service.CalculateFollowVelocity(Vector3.zero, target);

            var expectedDirection = target.normalized;
            var actualDirection = velocity.normalized;

            Assert.AreEqual(4f, velocity.magnitude, 0.001f);
            Assert.AreEqual(expectedDirection.x, actualDirection.x, 0.001f);
            Assert.AreEqual(expectedDirection.y, actualDirection.y, 0.001f);
            Assert.AreEqual(expectedDirection.z, actualDirection.z, 0.001f);
        }

        // --- IsSoftPlacement ---

        [Test]
        public void IsSoftPlacement_SpeedBelowThreshold_ReturnsTrue()
        {
            var service = CreateService(CreateConfig(softPlacementSpeedThreshold: 0.1f));

            Assert.IsTrue(service.IsSoftPlacement(new Vector3(0.05f, 0f, 0f)));
        }

        [Test]
        public void IsSoftPlacement_SpeedAboveThreshold_ReturnsFalse()
        {
            var service = CreateService(CreateConfig(softPlacementSpeedThreshold: 0.1f));

            Assert.IsFalse(service.IsSoftPlacement(new Vector3(5f, 0f, 0f)));
        }
    }
}
