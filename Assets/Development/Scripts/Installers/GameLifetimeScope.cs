using BlackWhiteLike.Controllers.Camera;
using BlackWhiteLike.Controllers.Hand;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.Input;
using BlackWhiteLike.Core.Interaction;
using BlackWhiteLike.Data.Configs.Camera;
using BlackWhiteLike.Data.Configs.Hand;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Flow.States;
using BlackWhiteLike.Services.Hand;
using BlackWhiteLike.Services.Idle;
using BlackWhiteLike.Services.Meta;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BlackWhiteLike.Installers
{
    /// <summary>
    /// Composition Root (game-design §19). Шаг 2 добавил первые SerializeField — конфиги
    /// руки/камеры и сцено-объекты руки/камеры. Поля намеренно nullable: сцены без руки/камеры
    /// (MainMenu и т.п.) не обязаны их прошивать — см. фоллбэки в <see cref="Configure"/>.
    /// </summary>
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [Header("Шаг 2 — Камера + рука")]
        [SerializeField] private HandConfig _handConfig;
        [SerializeField] private IsometricCameraConfig _isometricCameraConfig;
        [SerializeField] private GodHandController _godHandController;
        [SerializeField] private CameraRig _cameraRig;

        protected override void Configure(IContainerBuilder builder)
        {
            // Core
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventBus>();

            // Services
            builder.Register<SystemTimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
            builder.Register<PlayerPrefsProfileStorage>(Lifetime.Singleton).As<IProfileStorage>();
            builder.Register<MainMenuState>(Lifetime.Singleton);

            // Input — ITickable, тикается VContainer'ом каждый кадр без MonoBehaviour.
            // RegisterEntryPoint делает AsImplementedInterfaces() сам по себе — доп. .As<>() после него
            // был бы no-op, поэтому оба контракта (IInputProvider, ITickable) перечислены явно.
            builder.Register<PointerInputProvider>(Lifetime.Singleton).As<IInputProvider, ITickable>();

            // GameFlowService — тоже ITickable (см. MainMenuState.Tick и IGameFlowService.Tick).
            // ВАЖНО: порядок регистрации ITickable в VContainer = порядок тика. Ввод обязан тикаться
            // раньше своих потребителей (FSM, шаг 2+ — камера/хват), поэтому регистрация — строго
            // после PointerInputProvider. При добавлении новых tickable'ов порядок не нарушать.
            // RegisterEntryPoint даёт и IGameFlowService, и ITickable через AsImplementedInterfaces().
            builder.RegisterEntryPoint<GameFlowService>(Lifetime.Singleton);

            // Шаг 2 — Камера + рука.
            RegisterHandAndCamera(builder);

            // Точка входа
            builder.RegisterEntryPoint<GameBootstrap>();
        }

        /// <summary>
        /// Конфиги и сцено-объекты руки/камеры (game-design §19, шаг 2). Конфиги — если поле не
        /// прошито, регистрация null дала бы NRE у любого резолвера; вместо этого — fallback-инстанс
        /// с дефолтами, чтобы контейнер собрался и сцена не падала молча (НБ10, ревью шага 2:
        /// LogWarning, а не LogError, — сцены без руки/камеры, например MainMenu, штатно не
        /// прошивают эти поля, это не авария). Контроллеры — MonoBehaviour, регистрируются условно
        /// по той же причине.
        /// </summary>
        private void RegisterHandAndCamera(IContainerBuilder builder)
        {
            var handConfig = _handConfig;
            if (handConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _handConfig не прошит (ожидаемо для сцен без руки) — используется fallback HandConfig с дефолтами.");
                handConfig = ScriptableObject.CreateInstance<HandConfig>();
            }

            builder.RegisterInstance(handConfig);

            var isometricCameraConfig = _isometricCameraConfig;
            if (isometricCameraConfig == null)
            {
                Debug.LogWarning("[GameLifetimeScope] _isometricCameraConfig не прошит (ожидаемо для сцен без изометрической камеры) — используется fallback IsometricCameraConfig с дефолтами.");
                isometricCameraConfig = ScriptableObject.CreateInstance<IsometricCameraConfig>();
            }

            builder.RegisterInstance(isometricCameraConfig);

            // Б1+Б3 (ревью шага 2): единственный источник ответа "что под pointer'ом" — рука и камера
            // обе резолвят его отсюда, вместо независимых (и расходящихся) raycast'ов.
            builder.Register<PointerWorldProjector>(Lifetime.Singleton).As<IPointerWorldProjector>();

            builder.Register<HandInteractionService>(Lifetime.Singleton).As<IHandInteractionService>();

            if (_godHandController != null)
            {
                builder.RegisterComponent(_godHandController);
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _godHandController не прошит — рука не будет работать в этой сцене.");
            }

            if (_cameraRig != null)
            {
                builder.RegisterComponent(_cameraRig);

                // PanZoomBehaviour берётся с самого CameraRig (его сериализует CameraRig, не Scope) —
                // единственная точка ссылки на сцено-объект, без дублирования SerializeField'а здесь.
                if (_cameraRig.PanZoomBehaviour != null)
                {
                    builder.RegisterComponent(_cameraRig.PanZoomBehaviour);
                }
                else
                {
                    Debug.LogWarning("[GameLifetimeScope] CameraRig.PanZoomBehaviour не прошит — pan/zoom не будет работать.");
                }
            }
            else
            {
                Debug.LogWarning("[GameLifetimeScope] _cameraRig не прошит — камера не будет работать в этой сцене.");
            }
        }
    }
}
