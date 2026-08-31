using System;
using TMPro;
using UnityEngine;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// Пуловый попап "+N Awe/Fear" (game-design §10, §13, шаг 5) — Unity API only: подъём +
    /// затухание по времени, колбэк возврата в пул по истечении (тот же прецедент, что
    /// <c>VfxInstanceController</c>). DI-инъекция не нужна — <c>WorshipPopupFactory</c> вызывает
    /// <see cref="Launch"/> напрямую после <c>PoolManager.Get</c>.
    /// </summary>
    public sealed class WorshipPopupView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _lifetime = 1.2f;
        [SerializeField] private float _riseDistance = 60f;

        private RectTransform _rectTransform;
        private Vector2 _startAnchoredPosition;
        private bool _hasStartPosition;

        private Action<WorshipPopupView> _onFinished;
        private float _elapsed;
        private bool _isRunning;

        /// <summary>
        /// Запускает попап с нуля (пул мог переиспользовать инстанс с прошлой анимации) и
        /// запоминает колбэк завершения. Вызывается фабрикой сразу после спавна — не из
        /// Awake/OnEnable (CLAUDE.md: запуск поведения — только явный Launch).
        /// </summary>
        public void Launch(string text, Color color, Action<WorshipPopupView> onFinished)
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();

            if (!_hasStartPosition)
            {
                // Запоминается один раз при первом Launch — дальнейшие запуски (переиспользование
                // из пула) сбрасываются к этой же исходной позиции, а не к позиции конца прошлой анимации.
                _startAnchoredPosition = _rectTransform.anchoredPosition;
                _hasStartPosition = true;
            }

            _rectTransform.anchoredPosition = _startAnchoredPosition;

            if (_label != null)
            {
                _label.text = text;
                _label.color = color;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 1f;

            _onFinished = onFinished;
            _elapsed = 0f;
            _isRunning = true;
        }

        private void Update()
        {
            if (!_isRunning) return;

            _elapsed += Time.deltaTime;
            var t = _lifetime > 0f ? Mathf.Clamp01(_elapsed / _lifetime) : 1f;

            if (_rectTransform != null)
            {
                var pos = _startAnchoredPosition;
                pos.y += _riseDistance * t;
                _rectTransform.anchoredPosition = pos;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 1f - t;

            if (t >= 1f) Finish();
        }

        private void OnDisable()
        {
            // Возврат в пул мимо этого компонента (Despawn сцены и т.п.) — сброс без колбэка,
            // тот же контракт, что VfxInstanceController.OnDisable.
            _isRunning = false;
            _onFinished = null;
        }

        private void Finish()
        {
            _isRunning = false;

            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke(this);
        }
    }
}
