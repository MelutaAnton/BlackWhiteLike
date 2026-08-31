using System;
using System.Collections.Generic;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.StateMachine;
using BlackWhiteLike.Models;
using UnityEngine;
using VContainer.Unity;
using CoreStateMachine = BlackWhiteLike.Core.StateMachine.StateMachine<BlackWhiteLike.Models.GameState>;

namespace BlackWhiteLike.Services.Flow
{
    /// <summary>
    /// Реализация <see cref="IGameFlowService"/>. Pure C#, оборачивает generic FSM (game-design §9)
    /// и публикует <see cref="Core.Events.GameStateChangedEvent"/> / <see cref="GamePausedEvent"/> /
    /// <see cref="GameResumedEvent"/> через <see cref="IEventBus"/>. Реализует <see cref="ITickable"/> —
    /// регистрируется в GameLifetimeScope через RegisterEntryPoint, тикается VContainer каждый кадр.
    /// </summary>
    /// <remarks>
    /// Шаг 5 (пересмотр решения шага 1): сборка состояний перенесена из конструктора (где раньше
    /// принимался MainMenuState напрямую) в коллекцию <see cref="IGameStateBinding"/> — список
    /// состояний вырос (MainMenu/LevelPlaying/LevelComplete/Paused + заглушки LevelSelect/
    /// TempleMeta/WelcomeBack), интерфейс конструктора одним параметром на состояние уже не
    /// масштабировался. Дубликат ключа среди биндингов — <c>Debug.LogError</c> и пропуск (первый
    /// зарегистрированный биндинг сохраняется), не исключение — тот же принцип терпимости к
    /// ошибкам конфигурации, что у <see cref="Core.StateMachine.StateMachine{TKey}.TryGetState"/>.
    /// </remarks>
    /// <remarks>
    /// Шаг 5 (аудит после первого Play Mode): переход "LevelPlaying" → "LevelComplete" по
    /// <see cref="LevelCompletedEvent"/> реализован ЗДЕСЬ, а не в <c>LevelFlowService</c>, хотя
    /// событие публикует именно он. Причина — цикл DI: <c>LevelFlowService</c> зависел от
    /// <see cref="IGameFlowService"/> напрямую, а <see cref="IGameFlowService"/> резолвится через
    /// биндинги → <c>LevelPlayingState</c> → <c>ILevelFlowService</c> → снова <c>LevelFlowService</c>
    /// — VContainer падал на Build() с "ValueFactory attempted to access the Value property of this
    /// instance" (Lazy re-entrancy). Разрыв цикла — по таблице переходов game-design §9: FSM сама
    /// подписывается на игровые события и решает, куда переходить, а не наоборот.
    /// </remarks>
    public sealed class GameFlowService : IGameFlowService, ITickable, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly CoreStateMachine _stateMachine = new CoreStateMachine();
        private readonly IDisposable _levelCompletedSubscription;

        public GameFlowService(IEventBus eventBus, IEnumerable<IGameStateBinding> bindings)
        {
            _eventBus = eventBus;

            foreach (var binding in bindings)
            {
                if (_stateMachine.HasState(binding.Key))
                {
                    Debug.LogError($"[GameFlowService] Дубликат биндинга для {binding.Key} — пропущен.");
                    continue;
                }

                _stateMachine.Register(binding.Key, binding.State);
            }

            _levelCompletedSubscription = _eventBus.Subscribe<LevelCompletedEvent>(OnLevelCompleted);
        }

        public GameState Current => _stateMachine.Current;

        public bool IsPaused => Current == GameState.Paused;

        public void Start(GameState initial)
        {
            // Стартовое состояние — не транзиция, "from" не существует, событие не публикуем.
            _stateMachine.ChangeState(initial);
        }

        public void ChangeState(GameState state)
        {
            var from = _stateMachine.Current;
            _stateMachine.ChangeState(state);
            _eventBus.Publish(new GameStateChangedEvent(from, state));
        }

        public void PushState(GameState state)
        {
            var from = _stateMachine.Current;
            _stateMachine.Push(state);
            _eventBus.Publish(new GameStateChangedEvent(from, state));

            if (state == GameState.Paused)
            {
                _eventBus.Publish(new GamePausedEvent());
            }
        }

        public void PopState()
        {
            var from = _stateMachine.Current;
            var popped = _stateMachine.Pop();

            // StateMachine.Pop() на пустом/единственном состоянии — безопасный no-op (лог-варнинг, стек не меняется).
            // Ориентируемся на возвращённый флаг, а не на сравнение from/to — они могут совпасть
            // и при реальной транзиции (Push(X) поверх X), тогда Exit реально отработал.
            if (!popped) return;

            var to = _stateMachine.Current;
            _eventBus.Publish(new GameStateChangedEvent(from, to));

            if (from == GameState.Paused)
            {
                _eventBus.Publish(new GameResumedEvent());
            }
        }

        public void Tick() => _stateMachine.Tick();

        /// <summary>
        /// Таблица переходов game-design §9: "LevelPlaying" + <see cref="LevelCompletedEvent"/> →
        /// "LevelComplete". Событие вне LevelPlaying (например, повторная публикация или гонка
        /// с уже сменённым состоянием) — предупреждение и игнор, а не исключение.
        /// </summary>
        private void OnLevelCompleted(LevelCompletedEvent evt)
        {
            if (Current != GameState.LevelPlaying)
            {
                Debug.LogWarning("[GameFlowService] LevelCompletedEvent вне LevelPlaying — переход проигнорирован.");
                return;
            }

            ChangeState(GameState.LevelComplete);
        }

        public void Dispose()
        {
            _levelCompletedSubscription?.Dispose();
        }
    }
}
