using System;
using System.Collections.Generic;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Data.Configs.Tutorial;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Meta;
using BlackWhiteLike.Services.Village;
using UnityEngine;

namespace BlackWhiteLike.Services.Tutorial
{
    /// <summary>
    /// Реализация <see cref="ITutorialService"/> (шаг 10 «Polish»). Pure C# — подписывается на
    /// события действий игрока (тот же приём, что <c>WorshipService</c>) вместо прямых ссылок на
    /// сервисы-источники. Активна только для <see cref="TutorialConfig.TargetLevel"/> текущего
    /// уровня и пока <see cref="IPlayerProfile.TutorialCompleted"/> == false — оба условия
    /// вычисляются РОВНО ОДИН РАЗ в конструкторе (ни уровень, ни факт прохождения самого
    /// туториала в рамках сессии не меняются, кроме как через завершение этого же туториала).
    /// Если <see cref="IsActive"/> == false — на шину не подписывается вовсе (нечего слушать).
    /// </summary>
    /// <remarks>
    /// Последовательность шагов — линейная. Шаг с <c>ActivationTrigger.Type != None</c> скрыт
    /// (<see cref="HasVisibleStep"/> == false) до срабатывания активирующего триггера; после
    /// активации каждое совпадение <c>CompletionTrigger</c> продвигает <see cref="CurrentProgress"/>
    /// до <c>TargetCount</c>, по достижении — переход к следующему шагу. После последнего шага —
    /// <see cref="IPlayerProfile.MarkTutorialCompleted"/> и дальнейшая инертность
    /// (<see cref="HasVisibleStep"/> == false до конца сессии). <see cref="LevelCompletedEvent"/>
    /// завершает туториал безусловно, на каком бы шаге игрок ни остановился (§ВАЖНО 6 ревью).
    /// <para>
    /// БЛОКЕР 1/2 (ревью шага 10): <see cref="_activeNeedsByType"/> — латч состояния мира "нужда
    /// какого типа сейчас активна и на каком сайте", а не однократное событие. Решает две проблемы
    /// прежней реализации (одиночное поле <c>_activeNeedSiteIndex</c> без типизации):
    /// <list type="bullet">
    /// <item>Событие-активатор (например, <c>NeedTriggeredEvent(Fire)</c> таймлайна, публикуемое
    /// РОВНО ОДИН РАЗ) могло прийти раньше, чем шаг с таким <c>ActivationTrigger</c> стал текущим
    /// (игрок ещё не бросил жителя на шаге 1) — событие терялось безвозвратно, туториал зависал.
    /// Теперь <see cref="ActivateCurrentStepIfEligible"/> перепроверяет латч при каждом продвижении
    /// шага (и в конструкторе для первого шага).</item>
    /// <item>Указатель <see cref="TryGetWorldTargetPosition"/> резолвил ПОСЛЕДНЮЮ сработавшую нужду
    /// любого типа — на уровне 1 к моменту шага 3 активны сразу Fire (цель) и потенциально
    /// Disease/Hunger дальше по таймлайну, стрелка вела не туда. Теперь резолвится по
    /// <c>CurrentStep.CompletionTrigger.NeedType</c>.</item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class TutorialService : ITutorialService, IDisposable
    {
        private readonly IPlayerProfile _profile;
        private readonly IReadOnlyList<TutorialStep> _steps;
        private readonly INeedSiteRegistry _needSiteRegistry;

        private readonly bool _isActive;
        private int _currentStepIndex;
        private bool _stepActivated;
        private int _progress;

        // БЛОКЕР 1/2: активная нужда КАЖДОГО типа — отдельно (siteIndex последнего
        // NeedTriggeredEvent этого типа, снимается NeedResolvedEvent/NeedExpiredEvent с тем же
        // siteIndex). См. remarks класса.
        private readonly Dictionary<NeedType, int> _activeNeedsByType = new Dictionary<NeedType, int>();

        // Playtest-фикс шага 10 (2026-09-11): латч (Id, Kind) последнего захваченного рукой объекта —
        // ObjectThrownEvent вид объекта не несёт (см. VillagerTossRules). Раньше VillagerTossedByHand
        // детектился подпиской на VillagerStateChangedEvent (To=Tossed, Cause=Grabbed), что на деле
        // ловило сам ЗАХВАТ, а не бросок (FSM жителя входит в Tossed уже при захвате).
        private GrabbableId _lastGrabbedId;
        private GrabbableKind _lastGrabbedKind;
        private bool _hasGrabbedLatch;

        private readonly IDisposable _objectGrabbedSubscription;
        private readonly IDisposable _objectThrownSubscription;
        private readonly IDisposable _needTriggeredSubscription;
        private readonly IDisposable _needResolvedSubscription;
        private readonly IDisposable _needExpiredSubscription;
        private readonly IDisposable _miracleSelectedSubscription;
        private readonly IDisposable _worshipChangedSubscription;
        private readonly IDisposable _levelCompletedSubscription;

        public event Action StepChanged;

        public TutorialService(
            IEventBus eventBus,
            TutorialConfig config,
            IPlayerProfile profile,
            LevelConfig levelConfig,
            INeedSiteRegistry needSiteRegistry)
        {
            _profile = profile;
            _needSiteRegistry = needSiteRegistry;
            _steps = config.Steps ?? new TutorialStep[0];

            _isActive = levelConfig.Id == config.TargetLevel && !profile.TutorialCompleted && _steps.Count > 0;

            if (!_isActive) return; // Незначительное (ревью): нечего слушать — IsActive не переоценивается в рантайме.

            ActivateCurrentStepIfEligible();

            _objectGrabbedSubscription = eventBus.Subscribe<ObjectGrabbedEvent>(OnObjectGrabbed);
            _objectThrownSubscription = eventBus.Subscribe<ObjectThrownEvent>(OnObjectThrown);
            _needTriggeredSubscription = eventBus.Subscribe<NeedTriggeredEvent>(OnNeedTriggered);
            _needResolvedSubscription = eventBus.Subscribe<NeedResolvedEvent>(OnNeedResolved);
            _needExpiredSubscription = eventBus.Subscribe<NeedExpiredEvent>(OnNeedExpired);
            _miracleSelectedSubscription = eventBus.Subscribe<MiracleSelectedEvent>(OnMiracleSelected);
            _worshipChangedSubscription = eventBus.Subscribe<WorshipChangedEvent>(OnWorshipChanged);
            _levelCompletedSubscription = eventBus.Subscribe<LevelCompletedEvent>(OnLevelCompleted);
        }

        public bool IsActive => _isActive;
        public bool HasVisibleStep => _isActive && _currentStepIndex < _steps.Count && _stepActivated;
        public TutorialStep CurrentStep => HasVisibleStep ? _steps[_currentStepIndex] : null;
        public int CurrentProgress => _progress;

        public bool TryGetWorldTargetPosition(out Vector3 position)
        {
            position = default;
            if (!HasVisibleStep) return false;

            // БЛОКЕР 2: резолвим нужду ИМЕННО текущего шага (см. remarks класса), а не "последнюю
            // сработавшую нужду любого типа".
            var completion = CurrentStep.CompletionTrigger;
            if (completion.Type != TutorialTriggerType.NeedTriggered && completion.Type != TutorialTriggerType.NeedResolvedByPlayer)
                return false;

            if (!_activeNeedsByType.TryGetValue(completion.NeedType, out var siteIndex)) return false;

            return _needSiteRegistry.TryGetSitePosition(siteIndex, out position);
        }

        /// <summary>
        /// Playtest-фикс шага 10: латчит вид и Id только что захваченного объекта — ObjectThrownEvent
        /// вид не несёт (см. VillagerTossRules). Перезаписывает предыдущий латч безусловно — рука
        /// держит максимум один объект одновременно.
        /// </summary>
        private void OnObjectGrabbed(ObjectGrabbedEvent evt)
        {
            _lastGrabbedId = evt.Id;
            _lastGrabbedKind = evt.Kind;
            _hasGrabbedLatch = true;
        }

        /// <summary>
        /// Playtest-фикс шага 10: предикат "брошен именно рукой" вынесен в общий VillagerTossRules —
        /// тот же хелпер использует LevelEventService (AfterVillagerTossed), не дублируем проверку.
        /// Сопоставление по Id с латчем последнего захвата — см. OnObjectGrabbed.
        /// </summary>
        private void OnObjectThrown(ObjectThrownEvent evt)
        {
            if (!_hasGrabbedLatch || evt.Id != _lastGrabbedId) return;
            if (!VillagerTossRules.IsVillagerThrow(_lastGrabbedKind, evt.Velocity)) return;

            ProcessTrigger(TutorialTriggerType.VillagerTossedByHand, default, default, 0);
        }

        private void OnNeedTriggered(NeedTriggeredEvent evt)
        {
            _activeNeedsByType[evt.Type] = evt.SiteIndex;

            ProcessTrigger(TutorialTriggerType.NeedTriggered, evt.Type, default, 0);
        }

        private void OnNeedResolved(NeedResolvedEvent evt)
        {
            ClearActiveNeedSite(evt.Type, evt.SiteIndex);

            if (evt.ByPlayer)
            {
                ProcessTrigger(TutorialTriggerType.NeedResolvedByPlayer, evt.Type, default, 0);
            }
        }

        private void OnNeedExpired(NeedExpiredEvent evt)
        {
            ClearActiveNeedSite(evt.Type, evt.SiteIndex);

            // БЛОКЕР 1: если текущий шаг ждёт NeedResolvedByPlayer именно этого типа нужды, а
            // активных нужд этого типа больше не осталось вообще — цель шага объективно
            // недостижима (истекла раньше, чем игрок успел её разрешить). Запирать
            // последовательность навсегда нельзя — продвигаем шаг принудительно, минуя обычный
            // счётчик TargetCount.
            if (!_stepActivated || _currentStepIndex >= _steps.Count) return;

            var step = _steps[_currentStepIndex];
            if (step.CompletionTrigger.Type != TutorialTriggerType.NeedResolvedByPlayer) return;
            if (step.CompletionTrigger.NeedType != evt.Type) return;
            if (_activeNeedsByType.ContainsKey(evt.Type)) return; // есть другая активная нужда того же типа — цель ещё жива

            AdvanceStep();
            StepChanged?.Invoke();
        }

        private void ClearActiveNeedSite(NeedType type, int siteIndex)
        {
            if (_activeNeedsByType.TryGetValue(type, out var idx) && idx == siteIndex)
            {
                _activeNeedsByType.Remove(type);
            }
        }

        private void OnMiracleSelected(MiracleSelectedEvent evt)
        {
            if (evt.Selected)
            {
                ProcessTrigger(TutorialTriggerType.MiracleSelected, default, evt.Type, 0);
            }
        }

        private void OnWorshipChanged(WorshipChangedEvent evt) =>
            ProcessTrigger(TutorialTriggerType.WorshipReached, default, default, evt.NewValue);

        /// <summary>
        /// ВАЖНО 6 (ревью шага 10): уровень пройден — обучать больше нечему, независимо от того,
        /// на каком шаге остановился игрок (например, набрал Worship раньше, чем долистал все шаги
        /// туториала). Разрешение безусловное; <see cref="IPlayerProfile.MarkTutorialCompleted"/>
        /// идемпотентен — повторный вызов не проблема.
        /// </summary>
        private void OnLevelCompleted(LevelCompletedEvent evt)
        {
            _stepActivated = false; // гасим текущий видимый шаг (HasVisibleStep -> false)
            _profile.MarkTutorialCompleted();
            StepChanged?.Invoke();
        }

        /// <summary>
        /// Единая точка сверки входящего события с активирующим/завершающим триггером текущего
        /// шага (см. remarks класса). Событие, не относящееся ни к активирующему, ни к
        /// завершающему триггеру текущего шага, — no-op без публикации <see cref="StepChanged"/>.
        /// </summary>
        private void ProcessTrigger(TutorialTriggerType incoming, NeedType needType, MiracleType miracleType, int worshipValue)
        {
            if (_currentStepIndex >= _steps.Count) return; // туториал уже пройден целиком

            var step = _steps[_currentStepIndex];

            if (!_stepActivated)
            {
                if (Matches(step.ActivationTrigger, incoming, needType, miracleType, worshipValue))
                {
                    _stepActivated = true;
                    _progress = 0;
                    StepChanged?.Invoke();
                }

                return;
            }

            if (!Matches(step.CompletionTrigger, incoming, needType, miracleType, worshipValue)) return;

            _progress++;
            if (_progress >= step.TargetCount)
            {
                AdvanceStep();
            }

            StepChanged?.Invoke();
        }

        private void AdvanceStep()
        {
            _currentStepIndex++;
            _progress = 0;
            _stepActivated = false;

            if (_currentStepIndex >= _steps.Count)
            {
                _profile.MarkTutorialCompleted();
                return;
            }

            ActivateCurrentStepIfEligible();
        }

        /// <summary>
        /// БЛОКЕР 1: решает, должен ли шаг <see cref="_currentStepIndex"/> считаться
        /// активированным СРАЗУ — без ожидания нового события — потому что мир уже находится в
        /// нужном состоянии (латч <see cref="_activeNeedsByType"/>). Вызывается из конструктора
        /// (первый шаг) и <see cref="AdvanceStep"/> (каждый следующий шаг): событие-активатор
        /// могло прийти раньше, чем этот шаг стал текущим.
        /// </summary>
        private void ActivateCurrentStepIfEligible()
        {
            if (_currentStepIndex >= _steps.Count) return;

            var trigger = _steps[_currentStepIndex].ActivationTrigger;

            if (trigger.Type == TutorialTriggerType.None)
            {
                _stepActivated = true;
                return;
            }

            if (trigger.Type == TutorialTriggerType.NeedTriggered && _activeNeedsByType.ContainsKey(trigger.NeedType))
            {
                _stepActivated = true;
            }
        }

        private static bool Matches(
            TutorialTrigger trigger,
            TutorialTriggerType incoming,
            NeedType needType,
            MiracleType miracleType,
            int worshipValue)
        {
            if (trigger.Type == TutorialTriggerType.None) return false; // None — сентинел "сразу", не событие
            if (trigger.Type != incoming) return false;

            switch (trigger.Type)
            {
                case TutorialTriggerType.NeedTriggered:
                case TutorialTriggerType.NeedResolvedByPlayer:
                    return trigger.NeedType == needType;

                case TutorialTriggerType.MiracleSelected:
                    return trigger.MiracleType == miracleType;

                case TutorialTriggerType.WorshipReached:
                    return worshipValue >= trigger.WorshipThreshold;

                default: // VillagerTossedByHand — без доп. фильтра
                    return true;
            }
        }

        public void Dispose()
        {
            _objectGrabbedSubscription?.Dispose();
            _objectThrownSubscription?.Dispose();
            _needTriggeredSubscription?.Dispose();
            _needResolvedSubscription?.Dispose();
            _needExpiredSubscription?.Dispose();
            _miracleSelectedSubscription?.Dispose();
            _worshipChangedSubscription?.Dispose();
            _levelCompletedSubscription?.Dispose();
        }
    }
}
