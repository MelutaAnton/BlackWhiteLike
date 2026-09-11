using System;
using BlackWhiteLike.Data.Configs.Tutorial;
using UnityEngine;

namespace BlackWhiteLike.Services.Tutorial
{
    /// <summary>
    /// Онбординг-туториал уровня (шаг 10 «Polish») — pull-модель для UI (урок шага 8: никаких
    /// Publish из Start, презентер читает состояние сам, событие — только уведомление "перечитай").
    /// Реализация — <c>TutorialService</c>.
    /// </summary>
    public interface ITutorialService
    {
        /// <summary>true, если туториал вообще актуален для текущего уровня и ещё не пройден (game-design, TargetLevel/TutorialCompleted).</summary>
        bool IsActive { get; }

        /// <summary>true, если сейчас есть шаг, который нужно показать (текущий шаг активирован и не завершён).</summary>
        bool HasVisibleStep { get; }

        /// <summary>Валиден только при <see cref="HasVisibleStep"/> == true.</summary>
        TutorialStep CurrentStep { get; }

        /// <summary>Прогресс текущего шага (0..CurrentStep.TargetCount).</summary>
        int CurrentProgress { get; }

        /// <summary>Уведомление "состояние изменилось, перечитай пул-свойства" — без данных в событии.</summary>
        event Action StepChanged;

        /// <summary>Мировая позиция активной точки нужды (для указателя <c>TutorialPointerTarget.ActiveNeedSite</c>).</summary>
        bool TryGetWorldTargetPosition(out Vector3 position);
    }
}
