using UnityEngine;
using BlackWhiteLike.Core.StateMachine;

namespace BlackWhiteLike.Services.Flow.States
{
    /// <summary>
    /// Состояние экрана итогов уровня (game-design §9, шаг 5; шаг 10 волна 2 — заморозка мира) —
    /// сам экран собирает <c>LevelCompletePresenter</c> (подписан на <c>LevelCompletedEvent</c>
    /// напрямую, не на Enter этого состояния). Мир ставится на паузу через
    /// <see cref="ITimeScaleProvider"/> — симметрично <see cref="PausedState"/> (та же страховка:
    /// если запомненное значение само было 0 — Exit восстанавливает 1, не 0).
    /// </summary>
    /// <remarks>
    /// Предохранитель (шаг 10, волна 2): сегодня <c>PausedState</c> Push'нуть поверх этого состояния
    /// физически недостижимо — Dim-блокер оверлея Level Complete перекрывает кнопку паузы, игрок не
    /// может её нажать. Но если такой путь (например, Esc) появится в будущем — учесть:
    /// <see cref="StateMachine{TKey}.Push"/> вызывает <c>Enter()</c> только у состояния, на которое
    /// заходят, а <see cref="StateMachine{TKey}.Pop"/> НЕ вызывает <c>Enter()</c> повторно у
    /// состояния, под которое возвращаемся (оно всё это время было лишь приостановлено в стеке, см.
    /// <c>StateMachine.Pop</c>) — то есть <see cref="Enter"/> этого класса не перезапустится при
    /// возврате из вложенного <c>PausedState</c>. <see cref="_previousTimeScale"/> при этом не
    /// учитывает вложенный push <c>PausedState</c> поверх себя (тот запомнит и восстановит свой
    /// собственный TimeScale, а не тот, что был ДО LevelComplete) — кто в итоге отвечает за
    /// корректный TimeScale в таком двойном сценарии, сейчас не определено, пересмотреть тогда.
    /// </remarks>
    public sealed class LevelCompleteState : IState
    {
        private readonly ITimeScaleProvider _timeScaleProvider;
        private float _previousTimeScale = 1f;

        public LevelCompleteState(ITimeScaleProvider timeScaleProvider)
        {
            _timeScaleProvider = timeScaleProvider;
        }

        public void Enter()
        {
            _previousTimeScale = _timeScaleProvider.TimeScale;
            _timeScaleProvider.TimeScale = 0f;
            Debug.Log("[LevelCompleteState] Enter");
        }

        public void Exit()
        {
            // Страховка (симметрично PausedState): запомненное значение само было 0 — восстанавливаем
            // 1, а не 0, иначе мир навсегда остался бы физически на паузе.
            _timeScaleProvider.TimeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            Debug.Log("[LevelCompleteState] Exit");
        }

        public void Tick()
        {
        }
    }
}
