using System.Collections.Generic;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Models;
using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Реализация <see cref="IVillagerStateService"/>. Pure C# — держит словарь
    /// <c>VillagerId → запись(State, Timer)</c>, а не один глобальный стейт: у каждого жителя
    /// своя FSM, независимая от остальных (game-design §5). <c>VillagerConfig</c> — тот же
    /// прецедент, что <c>HandInteractionService(HandConfig)</c> (шаг 2): ScriptableObject как
    /// источник данных для pure C#-сервиса допустим, обращений к остальному UnityEngine нет.
    /// </summary>
    /// <remarks>
    /// Реализует <see cref="ITickable"/> для регистрации в GameLifetimeScope, но бизнес-логика
    /// целиком в <see cref="Tick(float)"/> — единственное место, трогающее <c>UnityEngine.Time</c>,
    /// это однострочный мост <see cref="Tick()"/> (см. ниже), нужный ТОЛЬКО чтобы VContainer мог
    /// тикать сервис каждый кадр без отдельного MonoBehaviour-адаптера.
    /// </remarks>
    public sealed class VillagerStateService : IVillagerStateService, ITickable
    {
        private sealed class Record
        {
            public VillagerState State;
            public float Timer;
        }

        private readonly IEventBus _eventBus;
        private readonly VillagerConfig _config;
        private readonly Dictionary<VillagerId, Record> _records = new Dictionary<VillagerId, Record>();

        public VillagerStateService(IEventBus eventBus, VillagerConfig config)
        {
            _eventBus = eventBus;
            _config = config;
        }

        public void Register(VillagerId id)
        {
            if (_records.ContainsKey(id))
            {
                Debug.LogWarning($"[VillagerStateService] Житель {id} уже зарегистрирован — повторная регистрация проигнорирована.");
                return;
            }

            _records[id] = new Record { State = VillagerState.Idle, Timer = 0f };
        }

        public void Unregister(VillagerId id)
        {
            _records.Remove(id);
        }

        public VillagerState GetState(VillagerId id)
        {
            if (_records.TryGetValue(id, out var record)) return record.State;

            Debug.LogWarning($"[VillagerStateService] Житель {id} не зарегистрирован — возвращён Idle по умолчанию.");
            return VillagerState.Idle;
        }

        public void ReportStimulus(VillagerId id, VillagerStimulus stimulus)
        {
            if (!_records.TryGetValue(id, out var record))
            {
                Debug.LogWarning($"[VillagerStateService] Стимул {stimulus} для незарегистрированного жителя {id} проигнорирован.");
                return;
            }

            if (!TryResolveTarget(record.State, stimulus, out var target)) return; // запрещённый переход — игнор

            if (target == record.State)
            {
                // Стимул того же назначения, что и текущее состояние, — не транзиция, а продление
                // (повторный NeedResolvedNearby во время Worship продлевает молитву). Событие не публикуется.
                record.Timer = DurationFor(target);
                return;
            }

            ApplyTransition(id, record, target);
        }

        public void Tick(float deltaTime)
        {
            // В3 (ревью шага 3): раньше здесь был new List<VillagerId>(_records.Keys) каждый Tick —
            // аллокация в кадровой петле без нужды. Мутация во время foreach безопасна: ниже меняются
            // только ПОЛЯ существующего Record (тот же объект по ссылке), а не состав словаря
            // (Add/Remove) — Dictionary<TKey,TValue> не считает это "модификацией коллекции".
            foreach (var kvp in _records)
            {
                var record = kvp.Value;

                // Idle — без таймера, ждёт стимула (ReportStimulus). Остальные (Panic/Worship/
                // Stunned/Tossed — Н3, страховочный таймаут) тикают до Idle.
                if (record.State == VillagerState.Idle) continue;

                record.Timer -= deltaTime;
                if (record.Timer <= 0f)
                {
                    ApplyTransition(kvp.Key, record, VillagerState.Idle);
                }
            }
        }

        /// <summary>Мост для VContainer ITickable — единственное место сервиса, читающее UnityEngine.Time (см. remarks класса).</summary>
        public void Tick() => Tick(UnityEngine.Time.deltaTime);

        private void ApplyTransition(VillagerId id, Record record, VillagerState target)
        {
            var from = record.State;
            record.State = target;
            record.Timer = DurationFor(target);
            _eventBus.Publish(new VillagerStateChangedEvent(id, from, target));
        }

        private float DurationFor(VillagerState state)
        {
            switch (state)
            {
                case VillagerState.Panic: return _config.PanicDuration;
                case VillagerState.Worship: return _config.WorshipDuration;
                case VillagerState.Stunned: return _config.StunDuration;
                // Н3 (ревью шага 3): страховочный таймаут Tossed → Idle — если Landed/HardLanded так
                // и не пришёл (улетел за коллайдеры, застрял в геометрии), FSM не должна зависать
                // навсегда (Tossed делает CanBeGrabbed == false — житель иначе никогда не хватаем снова).
                case VillagerState.Tossed: return _config.TossedTimeout;
                default: return 0f; // Idle — без таймера
            }
        }

        /// <summary>
        /// Таблица переходов (game-design §5). <paramref name="target"/> == <paramref name="current"/>
        /// при true — легальный "self-loop" (продление таймера в ReportStimulus, без публикации события).
        /// </summary>
        private static bool TryResolveTarget(VillagerState current, VillagerStimulus stimulus, out VillagerState target)
        {
            switch (stimulus)
            {
                case VillagerStimulus.Grabbed:
                    // Взять рукой можно из любого состояния — если уже Tossed, сработает self-loop
                    // (target == current) без лишнего события.
                    target = VillagerState.Tossed;
                    return true;

                case VillagerStimulus.FireNearby:
                case VillagerStimulus.ExplosionNearby:
                    // Из Tossed/Stunned пожар/взрыв не выдёргивает — житель либо летит, либо оглушён.
                    if (current == VillagerState.Tossed || current == VillagerState.Stunned)
                    {
                        target = current;
                        return false;
                    }
                    target = VillagerState.Panic;
                    return true;

                case VillagerStimulus.NeedResolvedNearby:
                    if (current == VillagerState.Tossed || current == VillagerState.Stunned)
                    {
                        target = current;
                        return false;
                    }
                    target = VillagerState.Worship;
                    return true;

                case VillagerStimulus.Landed:
                    // "Мягкое" приземление осмысленно только из полёта.
                    if (current != VillagerState.Tossed)
                    {
                        target = current;
                        return false;
                    }
                    target = VillagerState.Idle;
                    return true;

                case VillagerStimulus.HardLanded:
                    if (current != VillagerState.Tossed)
                    {
                        target = current;
                        return false;
                    }
                    target = VillagerState.Stunned;
                    return true;

                default:
                    target = current;
                    return false;
            }
        }
    }
}
