using System;
using System.Collections.Generic;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Miracles;
using BlackWhiteLike.Data.Configs.Worship;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Services.Worship
{
    /// <summary>
    /// Реализация <see cref="IAlignmentService"/>. Pure C# — тот же источник событий, что
    /// <see cref="WorshipService"/> (game-design §7, шаг 5), но сдвиг берётся из
    /// <c>MiracleCatalogConfig[CauseMiracle].AlignmentShift</c>, а не из <c>WorshipConfig</c>.
    /// </summary>
    /// <remarks>
    /// Аудит после первого Play Mode (ВАЖНО 5): симметрично <see cref="WorshipService"/> — сдвиг за
    /// hard landing (Tossed → Stunned) применяется, только если в Tossed житель попал
    /// <c>VillagerStimulus.Grabbed</c> (рукой), не <c>Blasted</c> (взрыв чуда, который уже даёт свой
    /// сдвиг через <c>NeedTriggeredEvent</c>). См. xml-doc <see cref="WorshipService"/> — то же
    /// рассуждение, отдельная копия состояния (каждый сервис держит свою подписку и свой словарь).
    /// Решение владельца (2026-08-29): у Blasted появилась своя ставка Fear в
    /// <see cref="WorshipService"/> (<c>TerrorAct.VillagerBlasted</c>), но Alignment за неё НЕ
    /// сдвигается — <c>HandTerrorAlignmentShift</c> остаётся только за бросок рукой (Grabbed),
    /// взрыв чуда штрафует Alignment исключительно через <c>MiracleCatalogConfig.AlignmentShift</c>
    /// самого чуда.
    /// </remarks>
    public sealed class AlignmentService : IAlignmentService, IDisposable
    {
        private const float ScoreMin = -100f;
        private const float ScoreMax = 100f;

        private readonly WorshipConfig _worshipConfig;
        private readonly MiracleCatalogConfig _catalog;
        private readonly IEventBus _eventBus;

        private float _score;

        // См. WorshipService._tossedCauseByVillager — тот же приём, отдельный экземпляр.
        private readonly Dictionary<VillagerId, VillagerStimulus> _tossedCauseByVillager = new Dictionary<VillagerId, VillagerStimulus>();

        private readonly IDisposable _needResolvedSubscription;
        private readonly IDisposable _needTriggeredSubscription;
        private readonly IDisposable _villagerStateChangedSubscription;

        public AlignmentService(IEventBus eventBus, WorshipConfig worshipConfig, MiracleCatalogConfig catalog)
        {
            _eventBus = eventBus;
            _worshipConfig = worshipConfig;
            _catalog = catalog;

            _needResolvedSubscription = _eventBus.Subscribe<NeedResolvedEvent>(evt => ApplyMiracleShift(evt.ByPlayer, evt.CauseMiracle));
            _needTriggeredSubscription = _eventBus.Subscribe<NeedTriggeredEvent>(evt => ApplyMiracleShift(evt.ByPlayer, evt.CauseMiracle));
            _villagerStateChangedSubscription = _eventBus.Subscribe<VillagerStateChangedEvent>(OnVillagerStateChanged);
        }

        public float Score => _score;
        public float MinScore => ScoreMin;
        public float MaxScore => ScoreMax;

        private void ApplyMiracleShift(bool byPlayer, MiracleType causeMiracle)
        {
            if (!byPlayer) return; // CauseMiracle валиден только при ByPlayer (см. Events.Village.cs)
            if (!_catalog.TryGet(causeMiracle, out var config)) return;

            Shift(config.AlignmentShift);
        }

        private void OnVillagerStateChanged(VillagerStateChangedEvent evt)
        {
            if (evt.To == VillagerState.Tossed)
            {
                _tossedCauseByVillager[evt.Id] = evt.Cause;
                return;
            }

            if (evt.From != VillagerState.Tossed) return;

            if (evt.To == VillagerState.Stunned)
            {
                var isHandCaused = _tossedCauseByVillager.TryGetValue(evt.Id, out var cause)
                    && cause == VillagerStimulus.Grabbed;
                _tossedCauseByVillager.Remove(evt.Id);

                if (isHandCaused)
                {
                    Shift(_worshipConfig.HandTerrorAlignmentShift);
                }
            }
            else
            {
                _tossedCauseByVillager.Remove(evt.Id);
            }
        }

        private void Shift(float delta)
        {
            var clamped = Mathf.Clamp(_score + delta, ScoreMin, ScoreMax);
            if (Mathf.Approximately(clamped, _score)) return; // реального изменения нет — событие не публикуем

            var old = _score;
            _score = clamped;
            _eventBus.Publish(new AlignmentChangedEvent(old, _score));
        }

        public void Dispose()
        {
            _needResolvedSubscription?.Dispose();
            _needTriggeredSubscription?.Dispose();
            _villagerStateChangedSubscription?.Dispose();
        }
    }
}
