using BlackWhiteLike.Core.Interaction;
using BlackWhiteLike.Data.Configs.Hand;
using UnityEngine;

namespace BlackWhiteLike.Services.Hand
{
    /// <summary>
    /// Реализация <see cref="IHandInteractionService"/>. Pure C#, без обращений к UnityEngine.Time —
    /// история позиций pointer'а хранится в кольцевом буфере без аллокаций на кадр (см. CLAUDE.md —
    /// Update-петля руки не должна аллоцировать). Буфер аллоцируется один раз в конструкторе.
    /// </summary>
    public sealed class HandInteractionService : IHandInteractionService
    {
        // Ёмкость буфера считается от окна семплирования (ревью шага 2, В2): раньше был константный
        // размер 32, который переполнялся уже на 240 Гц при окне 0.15с (36 семплов) — старые семплы
        // молча перезаписывались, и один и тот же жест давал разный импульс на разных машинах
        // (буфер не покрывал всё окно). Берём частоту с большим запасом (480 Гц — выше любого
        // реалистичного рендера) и гарантируем разумный минимум на случай маленького окна.
        private const float AssumedMaxSampleRateHz = 480f;
        private const int MinSampleCapacity = 128;

        private struct PositionSample
        {
            public Vector3 Position;
            public float Time;
        }

        private readonly HandConfig _config;
        private readonly PositionSample[] _samples;
        private readonly int _sampleCapacity;
        private int _sampleCount;
        private int _nextWriteIndex;

        public bool IsHolding { get; private set; }

        public HandInteractionService(HandConfig config)
        {
            _config = config;
            _sampleCapacity = Mathf.Max(
                MinSampleCapacity,
                Mathf.CeilToInt(config.ThrowVelocitySampleWindow * AssumedMaxSampleRateHz) + 4);
            _samples = new PositionSample[_sampleCapacity];
        }

        public bool CanGrab(IGrabbable grabbable)
        {
            if (grabbable == null) return false;
            if (!grabbable.CanBeGrabbed) return false;
            if (grabbable.Body == null) return false;

            return grabbable.Body.mass <= _config.MaxGrabMass;
        }

        public void BeginHold(Vector3 worldPosition, float unscaledTime)
        {
            _sampleCount = 0;
            _nextWriteIndex = 0;
            IsHolding = true;
            RecordSample(worldPosition, unscaledTime);
        }

        public void RecordSample(Vector3 worldPosition, float unscaledTime)
        {
            _samples[_nextWriteIndex] = new PositionSample { Position = worldPosition, Time = unscaledTime };
            _nextWriteIndex = (_nextWriteIndex + 1) % _sampleCapacity;
            if (_sampleCount < _sampleCapacity)
            {
                _sampleCount++;
            }
        }

        public Vector3 CalculateThrowVelocity(float unscaledTime)
        {
            var windowStart = unscaledTime - _config.ThrowVelocitySampleWindow;

            // Кольцевой буфер: самый старый хранящийся семпл лежит по индексу (nextWrite - count),
            // самый новый — по индексу (nextWrite - 1). Перебираем в хронологическом порядке.
            var oldestStoredIndex = (_nextWriteIndex - _sampleCount + _sampleCapacity) % _sampleCapacity;

            var hasOldestValid = false;
            PositionSample oldestValid = default;
            PositionSample newest = default;
            var validCount = 0;

            for (var i = 0; i < _sampleCount; i++)
            {
                var index = (oldestStoredIndex + i) % _sampleCapacity;
                var sample = _samples[index];

                if (sample.Time < windowStart) continue; // старее окна — игнорируем

                if (!hasOldestValid)
                {
                    oldestValid = sample;
                    hasOldestValid = true;
                }

                newest = sample;
                validCount++;
            }

            if (validCount < 2) return Vector3.zero; // мягкая постановка, не бросок

            var dt = newest.Time - oldestValid.Time;
            if (dt <= 0f) return Vector3.zero; // защита от деления на 0/отрицательное время

            var velocity = (newest.Position - oldestValid.Position) / dt * _config.ThrowForceScale;
            return ClampMagnitude(velocity, _config.MaxThrowForce);
        }

        public Vector3 CalculateFollowVelocity(Vector3 currentPosition, Vector3 targetPosition)
        {
            var velocity = (targetPosition - currentPosition) * _config.FollowStiffness;
            return ClampMagnitude(velocity, _config.FollowMaxSpeed);
        }

        public bool IsSoftPlacement(Vector3 velocity) =>
            velocity.sqrMagnitude <= _config.SoftPlacementSpeedThreshold * _config.SoftPlacementSpeedThreshold;

        public void EndHold()
        {
            _sampleCount = 0;
            _nextWriteIndex = 0;
            IsHolding = false;
        }

        private static Vector3 ClampMagnitude(Vector3 value, float maxMagnitude)
        {
            var magnitude = value.magnitude;
            if (magnitude <= maxMagnitude || magnitude <= 0f) return value;

            return value * (maxMagnitude / magnitude);
        }
    }
}
