using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Core.Input;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Flow.States;
using BlackWhiteLike.Services.Idle;
using BlackWhiteLike.Services.Meta;
using VContainer;
using VContainer.Unity;

namespace BlackWhiteLike.Installers
{
    /// <summary>
    /// Composition Root (game-design §19, шаг 1: "Каркас"). На этом шаге нет ни одного
    /// SerializeField — все зависимости pure C#, прошивать нечего.
    /// </summary>
    public sealed class GameLifetimeScope : LifetimeScope
    {
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

            // Точка входа
            builder.RegisterEntryPoint<GameBootstrap>();
        }
    }
}
