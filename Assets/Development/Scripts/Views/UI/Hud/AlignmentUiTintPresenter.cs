using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Alignment;
using BlackWhiteLike.Services.Worship;
using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// UI-тинт HUD по alignment-стейджу (game-design §7, §11.1 — отложка шага 6, реализовано шагом
    /// 10 «Polish», волна 2). Pure C# EntryPoint: стартовый pull из <see cref="IAlignmentService.Score"/>
    /// в <see cref="Start"/> (тот же приём, что <c>AlignmentVisualController.Construct</c> — стартовый
    /// стейдж применяется явно, событие для старта не публикуется), дальше подписка на
    /// <see cref="AlignmentChangedEvent"/>.
    /// </summary>
    /// <remarks>
    /// Индекс стейджа переиспользует таблицу переходов <see cref="AlignmentVisualConfig.GetStageIndex"/>
    /// (та же сортировка по убыванию ScoreThreshold, что использует <c>AlignmentVisualController</c>
    /// для материалов руки/храма) — сама сортировка здесь НЕ дублируется, только маппинг позиции
    /// стейджа (0..4) на именованный тон <see cref="AlignmentUiPalette"/> (Saint/Kind/Neutral/Cruel/
    /// Demon), который в текущем ассете (AlignmentVisual.asset) устроен ровно как 5 стейджей с теми
    /// же порогами — если стейджей меньше/больше пяти, позиции за пределами 0..3 клампятся на Demon.
    /// </remarks>
    public sealed class AlignmentUiTintPresenter : IStartable, IDisposable
    {
        private readonly IAlignmentService _alignmentService;
        private readonly AlignmentVisualConfig _config;
        private readonly ILevelHudView _hudView;
        private readonly IEventBus _eventBus;

        private IDisposable _alignmentChangedSubscription;

        public AlignmentUiTintPresenter(IAlignmentService alignmentService, AlignmentVisualConfig config, ILevelHudView hudView, IEventBus eventBus)
        {
            _alignmentService = alignmentService;
            _config = config;
            _hudView = hudView;
            _eventBus = eventBus;
        }

        public void Start()
        {
            _alignmentChangedSubscription = _eventBus.Subscribe<AlignmentChangedEvent>(OnAlignmentChanged);
            ApplyTint(_alignmentService.Score);
        }

        public void Dispose()
        {
            _alignmentChangedSubscription?.Dispose();
        }

        private void OnAlignmentChanged(AlignmentChangedEvent evt) => ApplyTint(evt.NewScore);

        private void ApplyTint(float score)
        {
            var stageIndex = _config.GetStageIndex(score);
            _hudView.SetAlignmentTint(SelectTint(_config.UiPalette, stageIndex));
        }

        private static Color SelectTint(AlignmentUiPalette palette, int stageIndex)
        {
            switch (stageIndex)
            {
                case 0: return palette.SaintTint;
                case 1: return palette.KindTint;
                case 2: return palette.NeutralTint;
                case 3: return palette.CruelTint;
                default: return palette.DemonTint;
            }
        }
    }
}
