using System;
using UnityEngine;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>MVP-View HUD уровня — Worship-бар, Alignment-индикатор, кнопка паузы (game-design §10, §13, шаг 5).</summary>
    public interface ILevelHudView
    {
        /// <summary>Обновляет заполнение Worship-бара и числовой лейбл "{current} / {target}".</summary>
        void SetWorship(int current, int target);

        /// <summary>
        /// Обновляет позицию Alignment-ползунка. Принимает НОРМАЛИЗОВАННОЕ значение −1..+1
        /// (шаг 5, аудит п.12) — деление на диапазон <c>IAlignmentService.MinScore/MaxScore</c>
        /// делает презентер, view диапазон Score не знает и не хардкодит.
        /// </summary>
        void SetAlignment(float normalized);

        /// <summary>
        /// Тинт по alignment-стейджу (game-design §7, §11.1, отложка шага 6, шаг 10 волна 2) —
        /// применяется к fill'у шкалы Worship и меткам Evil/Good. Цвет уже выбран презентером
        /// (<c>AlignmentUiTintPresenter</c>) из <c>AlignmentVisualConfig.UiPalette</c>.
        /// </summary>
        void SetAlignmentTint(Color color);

        /// <summary>Клик по кнопке паузы — презентер транслирует его в <c>IGameFlowService.PushState(Paused)</c>.</summary>
        event Action PauseClicked;
    }
}
