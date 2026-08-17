using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Core.Input
{
    /// <summary>
    /// Единственная реализация <see cref="IInputProvider"/> (game-design §3).
    /// Pure C# + <see cref="ITickable"/> — тикается VContainer каждый кадр, MonoBehaviour не нужен.
    /// Использует legacy <c>UnityEngine.Input</c> (Active Input Handling = Both, см. CLAUDE.md);
    /// mouse и touch дают одинаковые логические события через свойства <see cref="IInputProvider"/>.
    /// </summary>
    /// <remarks>
    /// Внутри файла Unity-класс ввода всегда квалифицируется как <c>UnityEngine.Input</c> —
    /// namespace текущего файла (<c>BlackWhiteLike.Core.Input</c>) заканчивается на "Input"
    /// и бare-имя может схлопнуться с ним (см. CLAUDE.md, чек-лист планирования, п.3).
    /// </remarks>
    public sealed class PointerInputProvider : IInputProvider, ITickable
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

        public void Tick()
        {
            // One-shot флаги обновляются каждый кадр заново (не аккумулируются).
            _pausePressed = UnityEngine.Input.GetKeyDown(KeyCode.Escape);
            _pan = Vector2.zero;
            _zoom = 0f;

            var isTouchActive = UnityEngine.Input.touchCount > 0;
            if (isTouchActive != _wasTouchActive)
            {
                // Переключение mouse↔touch: _pointerPosition относится к другому источнику ввода,
                // дельта от неё была бы бессмысленным скачком.
                _hasPreviousPosition = false;
            }

            _wasTouchActive = isTouchActive;

            if (isTouchActive)
            {
                UpdateFromTouch();
            }
            else
            {
                UpdateFromMouse();
            }
        }

        private void UpdateFromMouse()
        {
            var currentPosition = (Vector2)UnityEngine.Input.mousePosition;
            _pointerDelta = _hasPreviousPosition ? currentPosition - _pointerPosition : Vector2.zero;
            _pointerPosition = currentPosition;
            _hasPreviousPosition = true;

            var held = UnityEngine.Input.GetMouseButton(0);
            _pointerDown = UnityEngine.Input.GetMouseButtonDown(0);
            _pointerUp = UnityEngine.Input.GetMouseButtonUp(0);
            _isPointerHeld = held;
            // unscaledDeltaTime — удержание должно считаться и на паузе (timeScale = 0), иначе Pause
            // "замерзает" сам себя: PausePressed продолжает работать, а зависимая от holdTime логика — нет.
            _pointerHoldTime = held ? _pointerHoldTime + Time.unscaledDeltaTime : 0f;

            // Pan — RMB-drag на PC (game-design §3). Различение "пустое место" — на InputRouter (шаг 2).
            if (UnityEngine.Input.GetMouseButton(1))
            {
                _pan = _pointerDelta;
            }

            _zoom = UnityEngine.Input.mouseScrollDelta.y * ScrollZoomSensitivity;
            _pinchActive = false;
        }

        private void UpdateFromTouch()
        {
            var primaryTouch = UnityEngine.Input.GetTouch(0);
            var currentPosition = primaryTouch.position;

            // Began или отсутствие валидной предыдущей точки — дельта не считается.
            var isFirstSample = !_hasPreviousPosition || primaryTouch.phase == TouchPhase.Began;
            _pointerDelta = isFirstSample ? Vector2.zero : currentPosition - _pointerPosition;
            _pointerPosition = currentPosition;
            _hasPreviousPosition = true;

            var held = primaryTouch.phase == TouchPhase.Began
                       || primaryTouch.phase == TouchPhase.Moved
                       || primaryTouch.phase == TouchPhase.Stationary;
            var released = primaryTouch.phase == TouchPhase.Ended || primaryTouch.phase == TouchPhase.Canceled;

            _pointerDown = primaryTouch.phase == TouchPhase.Began;
            _pointerUp = released;
            _isPointerHeld = held;
            // unscaledDeltaTime — см. комментарий в UpdateFromMouse.
            _pointerHoldTime = held ? _pointerHoldTime + Time.unscaledDeltaTime : 0f;

            if (UnityEngine.Input.touchCount == 2)
            {
                // Pinch: два пальца — зум. Это не тап и не удержание одним пальцем, поэтому
                // pointer-семантика гасится полностью — иначе щипок читается как удержание.
                _pointerDown = false;
                _pointerUp = false;
                _isPointerHeld = false;
                _pointerHoldTime = 0f;

                var secondTouch = UnityEngine.Input.GetTouch(1);
                var currentDistance = Vector2.Distance(primaryTouch.position, secondTouch.position);

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

            if (released || UnityEngine.Input.touchCount == 0)
            {
                // Палец поднят — следующее касание не должно считать дельту от этой точки.
                _hasPreviousPosition = false;
            }
        }
    }
}
