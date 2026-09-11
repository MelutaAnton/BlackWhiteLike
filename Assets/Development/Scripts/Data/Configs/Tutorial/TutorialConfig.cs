using System;
using System.Collections.Generic;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Tutorial
{
    /// <summary>
    /// Триггер шага туториала — данные для активации ИЛИ завершения шага (шаг 10 «Polish»).
    /// Поля-фильтры (<see cref="NeedType"/>/<see cref="MiracleType"/>/<see cref="WorshipThreshold"/>)
    /// читаются только для соответствующего <see cref="TutorialTriggerType"/> — см. <c>TutorialService</c>.
    /// Плейн-структура (не ScriptableObject) — элемент <c>TutorialStep</c>.
    /// </summary>
    [Serializable]
    public struct TutorialTrigger
    {
        [SerializeField] private TutorialTriggerType _type;
        [SerializeField] private NeedType _needType;
        [SerializeField] private MiracleType _miracleType;
        [SerializeField] private int _worshipThreshold;

        public TutorialTriggerType Type => _type;
        public NeedType NeedType => _needType;
        public MiracleType MiracleType => _miracleType;
        public int WorshipThreshold => _worshipThreshold;
    }

    /// <summary>
    /// Один шаг линейной последовательности туториала (шаг 10 «Polish»). <see cref="ActivationTrigger"/>
    /// с <see cref="TutorialTriggerType.None"/> — шаг показывается сразу при активации туториала/
    /// завершении предыдущего шага. Плейн-класс (не ScriptableObject) — элемент массива
    /// <see cref="TutorialConfig"/>, тот же прецедент, что <c>LevelCatalogEntry</c>.
    /// </summary>
    [Serializable]
    public sealed class TutorialStep
    {
        [SerializeField] private TutorialTrigger _activationTrigger;
        [SerializeField] private TutorialTrigger _completionTrigger;

        [Tooltip("Сколько раз должен сработать CompletionTrigger, чтобы шаг считался выполненным. Минимум 1.")]
        [SerializeField] private int _targetCount = 1;

        [SerializeField] private LocalizedString _text;
        [SerializeField] private TutorialPointerTarget _pointerTarget;
        [SerializeField] private bool _highlightMiracleButton;
        [SerializeField] private MiracleType _highlightedMiracle;

        public TutorialTrigger ActivationTrigger => _activationTrigger;
        public TutorialTrigger CompletionTrigger => _completionTrigger;
        public int TargetCount => Mathf.Max(1, _targetCount);
        public LocalizedString Text => _text;
        public TutorialPointerTarget PointerTarget => _pointerTarget;
        public bool HighlightMiracleButton => _highlightMiracleButton;
        public MiracleType HighlightedMiracle => _highlightedMiracle;
    }

    /// <summary>
    /// Конфиг онбординг-туториала уровня (шаг 10 «Polish») — линейная последовательность шагов.
    /// Активен только для <see cref="TargetLevel"/> — см. <c>TutorialService</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialConfig", menuName = "BlackWhiteLike/Tutorial/Tutorial Config")]
    public sealed class TutorialConfig : ScriptableObject
    {
        [SerializeField] private LevelId _targetLevel;
        [SerializeField] private TutorialStep[] _steps = new TutorialStep[0];

        public LevelId TargetLevel => _targetLevel;

        /// <summary>Незначительное (ревью шага 10): наружу — только для чтения, массив-поле остаётся приватным.</summary>
        public IReadOnlyList<TutorialStep> Steps => _steps;
    }
}
