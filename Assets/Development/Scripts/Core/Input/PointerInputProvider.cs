using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.Utilities;
using VContainer.Unity;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace BlackWhiteLike.Core.Input
{
    /// <summary>
    /// Единственная реализация <see cref="IInputProvider"/> (game-design §3).
    /// Pure C# + <see cref="ITickable"/> — тикается VContainer каждый кадр, MonoBehaviour не нужен.
    /// Использует новый Input System (Active Input Handling = Both, но провайдер не трогает legacy
    /// <c>UnityEngine.Input</c> вовсе — переход сделан ради Android, где "Both" не поддерживается,
    /// см. CLAUDE.md). Mouse и touch дают одинаковые логические события через свойства
    /// <see cref="IInputProvider"/>: позиция/press читаются с общего <see cref="Pointer.current"/>
    /// (единый путь для мыши и тача — в этом и смысл pointer-абстракции), платформенные различия
    /// (pinch vs колесо, RMB-pan vs single-touch-pan) инкапсулированы внутри отдельных веток.
    /// </summary>
    /// <remarks>
    /// Внутри файла Unity-класс ввода всегда квалифицируется как <c>UnityEngine.Input</c> (там, где
    /// ещё встречается) — namespace текущего файла (<c>BlackWhiteLike.Core.Input</c>) заканчивается
    /// на "Input", и bare-имя может схлопнуться с ним (см. CLAUDE.md, чек-лист планирования, п.3).
    /// По той же причине тип pinch-тача — <c>UnityEngine.InputSystem.EnhancedTouch.Touch</c> —
    /// заведён через алиас <c>using Touch = ...</c>, чтобы не путать с полем на будущее.
    /// </remarks>
    public sealed class PointerInputProvider : IInputProvider, ITickable, IStartable, IDisposable
    {
        // Коэффициенты чувствительности — временные магические числа шага 1.
        // Вынести в SO на шаге 2, когда появится IsometricCameraConfig (game-design §11).
        private const float ScrollZoomSensitivity = 1f;
        private const float PinchZoomSensitivity = 0.01f;

        private Vector2 _pointerPosition;
        private Vector2 _pointerDelta;
        private bool _pointerDown;
        private bool _pointerUp;
        private bool _isPointerHeld;
        private float _pointerHoldTime;
        private Vector2 _pan;
        private float _zoom;
        private bool _pausePressed;

        // Нет валидной позиции с прошлого кадра — например, самый первый Tick или пойнтер только
        // что появился/сменил источник (mouse↔touch). Пока флаг false, дельта не считается,
        // только запоминается точка — иначе первый кадр даёт скачок на всю дистанцию до (0,0)
        // или до точки другого источника ввода.
        private bool _hasPreviousPosition;
        private bool _wasTouchActive;

        private float _previousPinchDistance;
        private bool _pinchActive;

        public Vector2 PointerPosition => _pointerPosition;
        public Vector2 PointerDelta => _pointerDelta;
        public bool PointerDown => _pointerDown;
        public bool PointerUp => _pointerUp;
        public bool IsPointerHeld => _isPointerHeld;
        public float PointerHoldTime => _pointerHoldTime;
        public Vector2 Pan => _pan;
        public float Zoom => _zoom;
        public bool PausePressed => _pausePressed;

        /// <summary>
        /// EnhancedTouch — глобальный статический подписчик на низкоуровневые touch-события
        /// (нужен для <see cref="Touch.activeTouches"/>, которым провайдер определяет pinch).
        /// Включается ровно один раз за время жизни синглтона; парная отписка — <see cref="Dispose"/>.
        /// Звать <see cref="EnhancedTouchSupport.Enable"/> "на всякий случай" из конструктора нельзя —
        /// без парного Disable подписка переживает LifetimeScope и копится при повторных запусках
        /// Play Mode в редакторе.
        /// </summary>
        public void Start()
        {
            EnhancedTouchSupport.Enable();
        }

        public void Dispose()
        {
            EnhancedTouchSupport.Disable();
        }

        public void Tick()
        {
            // One-shot флаги обновляются каждый кадр заново (не аккумулируются). Keyboard.current
            // может быть null (Android без подключённой клавиатуры) — проверяем каждое обращение.
            var keyboard = Keyboard.current;
            _pausePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            _pan = Vector2.zero;
            _zoom = 0f;

            // ReadOnlyArray — struct-обёртка над переиспользуемым внутренним буфером EnhancedTouch,
            // сам вызов не аллоцирует; берём один раз за Tick и передаём дальше, не перечитываем.
            var activeTouches = Touch.activeTouches;
            var touchCount = activeTouches.Count;
            var isTouchActive = touchCount > 0;
            if (isTouchActive != _wasTouchActive)
            {
                // Переключение mouse↔touch: _pointerPosition относится к другому источнику ввода,
                // дельта от неё была бы бессмысленным скачком.
                _hasPreviousPosition = false;
            }

            _wasTouchActive = isTouchActive;

            UpdatePointer(activeTouches, touchCount);

            if (isTouchActive)
            {
                UpdateTouchGestures(activeTouches, touchCount);
            }
            else
            {
                UpdateMouseGestures();
            }
        }

        /// <summary>
        /// Общая позиция/press-семантика для мыши и тача — <see cref="Pointer.current"/> сам
        /// переключается на последнее активное устройство (Mouse/Touchscreen), это и есть единый
        /// путь чтения pointer'а. Может быть null (нет ни мыши, ни тача, ни пера).
        /// </summary>
        private void UpdatePointer(ReadOnlyArray<Touch> activeTouches, int touchCount)
        {
            var pointer = Pointer.current;
            if (pointer == null)
            {
                _pointerDown = false;
                _pointerUp = false;
                _isPointerHeld = false;
                _pointerHoldTime = 0f;
                _pointerDelta = Vector2.zero;
                return;
            }

            var currentPosition = pointer.position.ReadValue();

            // Доп. защита от скачка дельты: первый сэмпл нового касания (Phase.Began) не должен
            // считать дельту от предыдущей точки, даже если _hasPreviousPosition ещё не успел
            // сброситься на переходе touchCount 0→1 (см. isTouchActive-гейт в Tick) — например,
            // если предыдущий тач кончился и новый начался в один и тот же render-кадр.
            var isFirstTouchSample = touchCount > 0 && activeTouches[0].began;
            var isFirstSample = !_hasPreviousPosition || isFirstTouchSample;

            _pointerDelta = isFirstSample ? Vector2.zero : currentPosition - _pointerPosition;
            _pointerPosition = currentPosition;
            _hasPreviousPosition = true;

            var held = pointer.press.isPressed;
            _pointerDown = pointer.press.wasPressedThisFrame;
            _pointerUp = pointer.press.wasReleasedThisFrame;
            _isPointerHeld = held;
            // unscaledDeltaTime — удержание должно считаться и на паузе (timeScale = 0), иначе Pause
            // "замерзает" сам себя: PausePressed продолжает работать, а зависимая от holdTime логика — нет.
            _pointerHoldTime = held ? _pointerHoldTime + Time.unscaledDeltaTime : 0f;
        }

        private void UpdateMouseGestures()
        {
            _pinchActive = false;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // Pan — RMB-drag на PC (game-design §3). Различение "пустое место" — на InputRouter (шаг 2).
            if (mouse.rightButton.isPressed)
            {
                _pan = _pointerDelta;
            }

            _zoom = mouse.scroll.ReadValue().y * ScrollZoomSensitivity;
        }

        private void UpdateTouchGestures(ReadOnlyArray<Touch> activeTouches, int touchCount)
        {
            if (touchCount >= 2)
            {
                // Pinch: два пальца — зум. Это не тап и не удержание одним пальцем, поэтому
                // pointer-семантика гасится полностью — иначе щипок читается как удержание.
                _pointerDown = false;
                _pointerUp = false;
                _isPointerHeld = false;
                _pointerHoldTime = 0f;

                var currentDistance = Vector2.Distance(activeTouches[0].screenPosition, activeTouches[1].screenPosition);

                if (_pinchActive)
                {
                    _zoom = (currentDistance - _previousPinchDistance) * PinchZoomSensitivity;
                }

                _previousPinchDistance = currentDistance;
                _pinchActive = true;
            }
            else
            {
                // Одиночный тач — кандидат в Pan (game-design §3); решает InputRouter (шаг 2).
                _pan = _pointerDelta;
                _pinchActive = false;
            }
        }
    }
}
