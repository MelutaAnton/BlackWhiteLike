using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;

namespace BlackWhiteLike.Core.Input
{
    /// <summary>
    /// Реализация <see cref="IInputRouter"/>. Pure C# — <see cref="Context"/> вычисляется от
    /// <see cref="IGameFlowService.Current"/> при каждом обращении (game-design §3, §13).
    /// </summary>
    /// <remarks>
    /// Шаг 5 (аудит после первого Play Mode, БЛОКЕР): раньше <c>Context</c> кэшировался в ctor и
    /// обновлялся только по <c>GameStateChangedEvent</c>. Ловушка — <c>GameFlowService.Start()</c>
    /// это событие НЕ публикует (xml-doc: "до старта from не существует"), а
    /// <c>RegisterComponent</c>-callback'и сцены (рука/камера/таргетинг) резолвят
    /// <see cref="InputRouter"/> и читают его кэш РАНЬШЕ <c>GameBootstrap.Start()</c> — Context
    /// навсегда застревал в UI, уровень был неиграбелен. Фикс — вычисляемое свойство без кэша и без
    /// подписки на шину: читает <c>_gameFlow.Current</c> напрямую, событие для этого не нужно.
    /// </remarks>
    public sealed class InputRouter : IInputRouter
    {
        private readonly IGameFlowService _gameFlow;

        public InputRouter(IGameFlowService gameFlow)
        {
            _gameFlow = gameFlow;
        }

        public InputContext Context => Resolve(_gameFlow.Current);
        public bool IsGameplay => Context == InputContext.Gameplay;

        private static InputContext Resolve(GameState state) =>
            state == GameState.LevelPlaying ? InputContext.Gameplay : InputContext.UI;
    }
}
