using UnityEngine;

namespace BlackWhiteLike.Core.Input
{
    /// <summary>
    /// Pointer-абстракция ввода (game-design §3): touch и mouse дают одинаковые события.
    /// Единственная реализация — <see cref="PointerInputProvider"/>, платформенные различия
    /// (pinch vs колесо) инкапсулированы внутри неё.
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>Текущая позиция pointer'а в экранных координатах.</summary>
        Vector2 PointerPosition { get; }

        /// <summary>Смещение pointer'а с прошлого кадра.</summary>
        Vector2 PointerDelta { get; }

        /// <summary>
        /// One-shot: true ровно один render-кадр — тот, в котором pointer коснулся экрана/была нажата ЛКМ.
        /// Потребитель, исполняющий действие в FixedUpdate, обязан буферизовать флагом в Update
        /// и сбрасывать после потребления (см. .claude/rules/physics-input.md) — иначе нажатие сгорает
        /// на кадре без физтика.
        /// </summary>
        bool PointerDown { get; }

        /// <summary>
        /// One-shot: true ровно один render-кадр — тот, в котором pointer отпущен/поднят.
        /// Та же оговорка про FixedUpdate-потребителей, что и у <see cref="PointerDown"/>.
        /// </summary>
        bool PointerUp { get; }

        /// <summary>Continuous: true все кадры, пока pointer удерживается (палец/ЛКМ не отпущены).</summary>
        bool IsPointerHeld { get; }

        /// <summary>Время удержания pointer'а в секундах; сбрасывается в 0 при отпускании.</summary>
        float PointerHoldTime { get; }

        /// <summary>
        /// Дельта панорамирования камеры за кадр (RMB-drag на PC / одиночный тач на мобиле).
        /// Различение "пустое место vs interactable" — на потребителе (InputRouter, шаг 2).
        /// </summary>
        Vector2 Pan { get; }

        /// <summary>Дельта зума за кадр (колесо мыши на PC, pinch на мобиле).</summary>
        float Zoom { get; }

        /// <summary>
        /// One-shot: true ровно один render-кадр — тот, в котором нажата Pause-иконка/Esc.
        /// Та же оговорка про FixedUpdate-потребителей, что и у <see cref="PointerDown"/>.
        /// </summary>
        bool PausePressed { get; }
    }
}
