using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackWhiteLike.Core.StateMachine
{
    /// <summary>
    /// Generic FSM с push-pop (см. CLAUDE.md, game-design §9). Стек хранит ключи состояний;
    /// на вершине стека — активное состояние, тикается только оно.
    /// <para>
    /// Ключ допустимо помещать в стек без зарегистрированного <see cref="IState"/> — тогда
    /// Enter/Exit/Tick для него не вызываются, а в консоль пишется <see cref="Debug.LogError"/>
    /// (см. <see cref="TryGetState"/>) — незарегистрированный ключ почти всегда опечатка или
    /// забытая регистрация, а не намеренный no-op. Для намеренной заглушки (состояние, поведение
    /// которого появится позже) регистрировать <see cref="NullState"/> явно — тогда предупреждения не будет.
    /// </para>
    /// </summary>
    public sealed class StateMachine<TKey> where TKey : struct, Enum
    {
        private readonly Dictionary<TKey, IState> _states = new Dictionary<TKey, IState>();

        // Используется как стек: вершина — последний элемент (Add/RemoveAt(Count - 1)).
        private readonly List<TKey> _stack = new List<TKey>();

        /// <summary>Ключ состояния на вершине стека. Пока стек пуст — default(TKey); см. <see cref="IsRunning"/>.</summary>
        public TKey Current => IsRunning ? _stack[_stack.Count - 1] : default;

        /// <summary>
        /// true, если в стеке есть хотя бы одно состояние. Отличает "FSM ещё не стартовала"
        /// от "мы в состоянии default(TKey)" — <see cref="Current"/> сам по себе эти случаи не различает.
        /// </summary>
        public bool IsRunning => _stack.Count > 0;

        public bool HasState(TKey key) => _states.ContainsKey(key);

        public void Register(TKey key, IState state)
        {
            _states[key] = state;
        }

        /// <summary>Exit текущего (если стек не пуст) → Enter нового. Стек не растёт.</summary>
        public void ChangeState(TKey key)
        {
            if (IsRunning)
            {
                TryGetState(_stack[_stack.Count - 1])?.Exit();
                _stack[_stack.Count - 1] = key;
            }
            else
            {
                _stack.Add(key);
            }

            TryGetState(key)?.Enter();
        }

        /// <summary>Текущее состояние остаётся в стеке приостановленным (его Exit не вызывается). Новое — Enter.</summary>
        public void Push(TKey key)
        {
            _stack.Add(key);
            TryGetState(key)?.Enter();
        }

        /// <summary>
        /// Exit верхнего, возврат к предыдущему без повторного Enter (оно было лишь приостановлено).
        /// Возвращает true, если транзиция реально произошла, false — если стек не позволил
        /// (пуст или в нём единственное состояние). Вызывающий код должен ориентироваться
        /// на возвращённое значение, а не сравнивать состояние "до" и "после" — они могут
        /// совпасть и при реальной транзиции (Push(X) поверх X).
        /// </summary>
        public bool Pop()
        {
            if (_stack.Count <= 1)
            {
                Debug.LogWarning(_stack.Count == 0
                    ? "[StateMachine] Pop вызван при пустом стеке — игнорируется."
                    : "[StateMachine] Pop вызван при единственном состоянии в стеке — игнорируется.");
                return false;
            }

            TryGetState(_stack[_stack.Count - 1])?.Exit();
            _stack.RemoveAt(_stack.Count - 1);
            return true;
        }

        /// <summary>Тикает только состояние на вершине стека.</summary>
        public void Tick()
        {
            if (!IsRunning) return;
            TryGetState(_stack[_stack.Count - 1])?.Tick();
        }

        private IState TryGetState(TKey key)
        {
            if (!HasState(key))
            {
                Debug.LogError($"[StateMachine] Состояние {key} не зарегистрировано.");
                return null;
            }

            return _states[key];
        }
    }
}
