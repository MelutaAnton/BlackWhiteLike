using System;
using System.Collections.Generic;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Worship;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;
using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Services.Worship
{
    /// <summary>
    /// Реализация <see cref="IWorshipService"/>. Pure C# — подписывается на события действий игрока
    /// (game-design §7, шаг 5) вместо того, чтобы держать ссылки на сервисы-источники: разрешённая
    /// нужда (<c>NeedResolvedEvent.ByPlayer</c>) даёт Awe по типу нужды, созданная нужда
    /// (<c>NeedTriggeredEvent.ByPlayer</c> + <see cref="TerrorActRules"/>) и жёсткое приземление
    /// жителя ОТ РУКИ (<c>VillagerStateChangedEvent</c> Tossed → Stunned, но только если в Tossed
    /// житель попал <c>VillagerStimulus.Grabbed</c>) дают Fear.
    /// </summary>
    /// <remarks>
    /// Решение владельца (2026-08-29): жёсткое приземление жителя от взрыва чуда — отдельная ставка
    /// Fear (<see cref="TerrorAct.VillagerBlasted"/>), не задваивающая счёт с попаданием чуда по
    /// структуре (<see cref="TerrorAct.FireballOnStructure"/> начисляется отдельно через
    /// <c>NeedTriggeredEvent</c> + <see cref="TerrorActRules"/>) — это два разных акта: поджог дома
    /// и травма жителя. Причина входа в Tossed определяет ставку: <c>VillagerStimulus.Grabbed</c> →
    /// <see cref="TerrorAct.VillagerHardLanding"/>, <c>VillagerStimulus.Blasted</c> →
    /// <see cref="TerrorAct.VillagerBlasted"/>.
    /// Решение владельца (2026-09-01, шаг 6): удар молнии — отдельный путь, минующий Tossed (FSM
    /// ведёт Idle/Panic/Worship напрямую в Stunned, см. <c>VillagerStateService.TryResolveTarget</c>,
    /// <see cref="VillagerStimulus.LightningStruck"/>) — ставка <see cref="TerrorAct.VillagerStruckByLightning"/>,
    /// та же структура прецедента (отдельно от <see cref="TerrorAct.LightningStrike"/> — поджог дома
    /// молнией — отдельный акт, двойного счёта нет).
    /// </remarks>
    public sealed class WorshipService : IWorshipService, ITickable, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly WorshipConfig _config;
        private readonly LevelConfig _levelConfig;
        private readonly INeedSiteRegistry _needSiteRegistry;

        private int _current;
        private int _aweTotal;
        private int _fearTotal;
        private float _decayAccumulator;

        // Причина входа в Tossed по каждому жителю (game-design §5, шаг 5, ВАЖНО 5) — записывается
        // на To == Tossed, потребляется на Tossed -> Stunned (используется и удаляется), чистится
        // без начисления на Tossed -> Idle (мягкое приземление — запись больше не актуальна).
        private readonly Dictionary<VillagerId, VillagerStimulus> _tossedCauseByVillager = new Dictionary<VillagerId, VillagerStimulus>();

        // Затухание — не источник прироста само по себе (см. WorshipChangedEvent.Source): событие
        // decay публикуется с источником ПОСЛЕДНЕГО реального прироста. Дефолт Awe — до первого
        // реального прироста decay произойти не может (Current стартует с 0, Tick выходит рано).
        private WorshipSource _lastSource = WorshipSource.Awe;

        // Шаг 10 (волна 2): та же логика "последний реальный прирост" — decay-событие якорит попап
        // туда же, куда ушёл предыдущий реальный прирост, а не в никуда.
        private Vector3 _lastWorldPosition;
        private bool _lastHasWorldPosition;

        private readonly IDisposable _needResolvedSubscription;
        private readonly IDisposable _needTriggeredSubscription;
        private readonly IDisposable _villagerStateChangedSubscription;

        public WorshipService(IEventBus eventBus, WorshipConfig config, LevelConfig levelConfig, INeedSiteRegistry needSiteRegistry)
        {
            _eventBus = eventBus;
            _config = config;
            _levelConfig = levelConfig;
            _needSiteRegistry = needSiteRegistry;

            _needResolvedSubscription = _eventBus.Subscribe<NeedResolvedEvent>(OnNeedResolved);
            _needTriggeredSubscription = _eventBus.Subscribe<NeedTriggeredEvent>(OnNeedTriggered);
            _villagerStateChangedSubscription = _eventBus.Subscribe<VillagerStateChangedEvent>(OnVillagerStateChanged);

            // Аудит п.9: Target <= 0 — победа математически недостижима (Current стартует с 0 и
            // растёт только вверх), почти наверняка кривой LevelConfig, а не намеренный дизайн.
            if (_levelConfig.WorshipTarget <= 0)
            {
                Debug.LogWarning($"[WorshipService] LevelConfig.WorshipTarget = {_levelConfig.WorshipTarget} (<= 0) — победа на этом уровне недостижима.");
            }
        }

        public int Current => _current;
        public int Target => _levelConfig.WorshipTarget;
        public int AweTotal => _aweTotal;
        public int FearTotal => _fearTotal;
        public bool IsTargetReached => _current >= Target;

        private void OnNeedResolved(NeedResolvedEvent evt)
        {
            if (!evt.ByPlayer) return; // CauseMiracle валиден только при ByPlayer (см. Events.Village.cs)
            var hasPosition = _needSiteRegistry.TryGetSitePosition(evt.SiteIndex, out var position);
            ApplyAwe(_config.GetAwe(evt.Type), hasPosition, position);
        }

        private void OnNeedTriggered(NeedTriggeredEvent evt)
        {
            if (!evt.ByPlayer) return;
            if (!TerrorActRules.TryFromMiracle(evt.CauseMiracle, out var act)) return; // Rain/Heal не акты террора
            var hasPosition = _needSiteRegistry.TryGetSitePosition(evt.SiteIndex, out var position);
            ApplyFear(_config.GetFear(act), hasPosition, position);
        }

        private void OnVillagerStateChanged(VillagerStateChangedEvent evt)
        {
            if (evt.To == VillagerState.Tossed)
            {
                _tossedCauseByVillager[evt.Id] = evt.Cause;
                return;
            }

            // Удар молнии (шаг 6, решение владельца 2026-09-01) — Idle/Panic/Worship -> Stunned
            // напрямую, минуя Tossed (VillagerStateService.TryResolveTarget, LightningStruck).
            // Self-loop Stunned -> Stunned (повторный удар) сюда не попадает — VillagerStateService
            // не публикует событие для target == current, то есть Fear не фармится повторным ударом.
            if (evt.To == VillagerState.Stunned && evt.Cause == VillagerStimulus.LightningStruck)
            {
                ApplyFear(_config.GetFear(TerrorAct.VillagerStruckByLightning), evt.HasWorldPosition, evt.WorldPosition);
                return;
            }

            if (evt.From != VillagerState.Tossed) return;

            if (evt.To == VillagerState.Stunned)
            {
                // Потребляем запись — используется ровно один раз, следующий Tossed этого же
                // жителя запишет причину заново.
                _tossedCauseByVillager.TryGetValue(evt.Id, out var cause);
                _tossedCauseByVillager.Remove(evt.Id);

                // Ставка Fear зависит от причины броска — рука и взрыв чуда считаются раздельно
                // (см. remarks класса, решение владельца 2026-08-29).
                if (cause == VillagerStimulus.Grabbed)
                {
                    ApplyFear(_config.GetFear(TerrorAct.VillagerHardLanding), evt.HasWorldPosition, evt.WorldPosition);
                }
                else if (cause == VillagerStimulus.Blasted)
                {
                    ApplyFear(_config.GetFear(TerrorAct.VillagerBlasted), evt.HasWorldPosition, evt.WorldPosition);
                }
            }
            else
            {
                // Мягкое приземление (Tossed -> Idle) или страховочный таймаут — запись устарела,
                // без начисления Fear.
                _tossedCauseByVillager.Remove(evt.Id);
            }
        }

        private void ApplyAwe(int amount, bool hasPosition, Vector3 position)
        {
            if (amount <= 0) return; // отсутствующая запись WorshipConfig (0) — не публикуем впустую
            _aweTotal += amount; // некламплённая сумма — для LevelCompletedEvent/экрана итогов
            IncreaseCurrent(amount, WorshipSource.Awe, hasPosition, position);
        }

        private void ApplyFear(int amount, bool hasPosition, Vector3 position)
        {
            if (amount <= 0) return;
            _fearTotal += amount;
            IncreaseCurrent(amount, WorshipSource.Fear, hasPosition, position);
        }

        private void IncreaseCurrent(int amount, WorshipSource source, bool hasPosition, Vector3 position)
        {
            _lastSource = source;
            _lastHasWorldPosition = hasPosition;
            _lastWorldPosition = position;
            var clamped = Mathf.Min(_current + amount, Target);
            SetCurrent(clamped, source, hasPosition, position);
        }

        public void Tick(float dt)
        {
            if (_config.WorshipDecayPerSecond <= 0f) return; // decay выключен (Open Question #2)
            if (_current <= 0) return;

            _decayAccumulator += dt * _config.WorshipDecayPerSecond;
            var whole = Mathf.FloorToInt(_decayAccumulator);
            if (whole <= 0) return;

            _decayAccumulator -= whole;
            var newValue = Mathf.Max(0, _current - whole);
            SetCurrent(newValue, _lastSource, _lastHasWorldPosition, _lastWorldPosition);
        }

        /// <summary>Мост для VContainer ITickable — единственное место сервиса, читающее UnityEngine.Time.</summary>
        public void Tick() => Tick(UnityEngine.Time.deltaTime);

        private void SetCurrent(int value, WorshipSource source, bool hasPosition, Vector3 position)
        {
            if (value == _current) return; // реального изменения нет — событие не публикуем

            var old = _current;
            _current = value;
            _eventBus.Publish(new WorshipChangedEvent(old, _current, Target, source, position, hasPosition));
        }

        public void Dispose()
        {
            _needResolvedSubscription?.Dispose();
            _needTriggeredSubscription?.Dispose();
            _villagerStateChangedSubscription?.Dispose();
        }
    }
}
