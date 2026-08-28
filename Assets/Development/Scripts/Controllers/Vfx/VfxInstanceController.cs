using System;
using UnityEngine;

namespace BlackWhiteLike.Controllers.Vfx
{
    /// <summary>
    /// Проигрыватель одного пулового VFX-инстанса (game-design §14.2). Unity API only — сам решает,
    /// когда партикл-система доиграла (<see cref="ParticleSystem.IsAlive"/>), и вызывает колбэк
    /// возврата в пул; DI-инъекция инстансу не нужна (нет зависимостей), <c>IVfxFactory</c> вызывает
    /// <see cref="Launch"/> напрямую после <c>PoolManager.Get</c>.
    /// </summary>
    public sealed class VfxInstanceController : MonoBehaviour
    {
        [Tooltip("Корневая ParticleSystem эффекта. Если не прошита — берётся GetComponentInChildren один раз при Launch.")]
        [SerializeField] private ParticleSystem _particleSystem;

        [Tooltip("Страховка: если эффект почему-то не доиграл (IsAlive всё ещё true) дольше этого времени — принудительный возврат в пул с предупреждением.")]
        [SerializeField] private float _maxLifetime = 10f;

        private Action<VfxInstanceController> _onFinished;
        private float _elapsed;
        private bool _isRunning;

        /// <summary>
        /// Запускает эффект с нуля (пул мог переиспользовать инстанс с "хвостом" предыдущего
        /// проигрывания) и запоминает колбэк завершения. Вызывается фабрикой сразу после спавна —
        /// не из Awake/OnEnable (CLAUDE.md: запуск поведения — только явный Launch).
        /// </summary>
        public void Launch(Action<VfxInstanceController> onFinished)
        {
            if (_particleSystem == null)
            {
                _particleSystem = GetComponentInChildren<ParticleSystem>();
            }

            _onFinished = onFinished;
            _elapsed = 0f;
            _isRunning = _particleSystem != null;

            if (_particleSystem == null)
            {
                Debug.LogError("[VfxInstanceController] Не найдена ParticleSystem — эффект не будет проигран.");
                Finish();
                return;
            }

            _particleSystem.Clear(true);
            _particleSystem.Play(true);
        }

        private void Update()
        {
            if (!_isRunning) return;

            _elapsed += Time.deltaTime;

            if (!_particleSystem.IsAlive(true))
            {
                Finish();
                return;
            }

            if (_elapsed >= _maxLifetime)
            {
                Debug.LogWarning($"[VfxInstanceController] Эффект '{name}' не доиграл за {_maxLifetime}с — принудительный возврат в пул.");
                Finish();
            }
        }

        private void OnDisable()
        {
            // Сброс колбэка без вызова — объект мог быть возвращён в пул кем-то ещё (Despawn сцены и т.п.).
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
