using BlackWhiteLike.Models;

namespace BlackWhiteLike.Core.Input
{
    /// <summary>
    /// Контекст ввода (game-design §3, §13, шаг 5) — единственный источник ответа "сейчас Gameplay
    /// или UI/меню" для потребителей pointer'а (рука, камера, таргетинг чуда). Не путать с
    /// <see cref="IInputProvider.IsPointerOverUi"/> — тот про состояние HUD под pointer'ом прямо
    /// сейчас, этот — про состояние игрового потока (<c>IGameFlowService.Current</c>).
    /// </summary>
    public interface IInputRouter
    {
        /// <summary>Текущий контекст — Gameplay ⇔ IGameFlowService.Current == LevelPlaying, иначе UI.</summary>
        InputContext Context { get; }

        /// <summary>Удобный шорткат для <see cref="Context"/> == InputContext.Gameplay.</summary>
        bool IsGameplay { get; }
    }
}
