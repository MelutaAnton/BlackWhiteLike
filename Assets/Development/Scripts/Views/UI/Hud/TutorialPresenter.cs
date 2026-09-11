using System;
using BlackWhiteLike.Core.UI;
using BlackWhiteLike.Data.Configs.Tutorial;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Localization;
using BlackWhiteLike.Services.Tutorial;
using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// MVP-Presenter онбординг-туториала (шаг 10 «Polish»). Pure C#. <see cref="Tick"/> нужен для
    /// указателя на мировую цель (<c>TutorialPointerTarget.ActiveNeedSite</c>) — его screen-space
    /// позиция меняется с паном/зумом камеры каждый кадр, даже когда сам текст/прогресс шага не
    /// изменился; текст/прогресс/подсветка пересчитываются тем же <see cref="Refresh"/>. Аллокаций
    /// в горячем пути нет (строка прогресса кэширована), но повторные SetActive(true)/присваивание
    /// TMP.text каждый кадр остаются — осознанная цена простоты (ревью шага 10, незначительное 10);
    /// гейтить по изменению стоит, только если профайлер на устройстве покажет вклад.
    /// </summary>
    public sealed class TutorialPresenter : IStartable, ITickable, IDisposable
    {
        private readonly ITutorialService _tutorialService;
        private readonly TutorialView _view;
        private readonly ILocalizationService _localization;

        // Кэш камеры (CLAUDE.md/контракт задачи — не Camera.main в горячем цикле без кэша, тот же
        // приём, что PointerWorldProjector): переполучаем только если ссылка стала null (fake-null
        // после выгрузки сцены — явный ==, не ??).
        private UnityEngine.Camera _camera;

        private MiracleType? _highlightedMiracle;

        // ВАЖНО 4 (ревью шага 10): строка прогресса пересобирается Format'ом только когда
        // Progress/TargetCount реально изменились — раньше string.Format вызывался каждый Tick,
        // пока виден шаг с TargetCount > 1.
        private int _cachedProgress = -1;
        private int _cachedTargetCount = -1;
        private string _cachedProgressText = string.Empty;

        // Playtest-фикс шага 10 (2026-09-11): отступ от края экрана для off-screen-индикатора
        // (TutorialPointerRules) — доли viewport, не пиксели, не зависят от разрешения.
        private const float PointerMarginViewportX = 0.08f;
        private const float PointerMarginViewportY = 0.1f;

        public TutorialPresenter(ITutorialService tutorialService, TutorialView view, ILocalizationService localization)
        {
            _tutorialService = tutorialService;
            _view = view;
            _localization = localization;
        }

        public void Start()
        {
            _tutorialService.StepChanged += OnStepChanged;

            // Playtest-фикс (2026-09-12): плашка цели в префабе активна по умолчанию, а Refresh при
            // неактивном туториале выходит ДО Hide() — без этого явного скрытия пустая тёмная
            // панель висела бы весь уровень у игрока с пройденным туториалом.
            if (!_tutorialService.IsActive)
            {
                _view.Hide();
                return;
            }

            Refresh();
        }

        public void Dispose()
        {
            _tutorialService.StepChanged -= OnStepChanged;

            // ВАЖНО 5 (ревью шага 10): если презентер разрушается посреди подсвеченного шага
            // (сцена выгружается, пока пульсирует кнопка чуда) — снять подсветку, а не оставить
            // DOTween-твин крутиться на RectTransform кнопки без владельца.
            SetHighlight(null);
        }

        public void Tick() => Refresh();

        private void OnStepChanged() => Refresh();

        private void Refresh()
        {
            // ВАЖНО 4 (ревью шага 10): IsActive вычисляется один раз в конструкторе TutorialService
            // и не меняется всю сессию — при false View скрыт ЯВНЫМ Hide() в Start() (не здесь!),
            // гонять Hide()/SetHighlight(null) каждый Tick незачем.
            if (!_tutorialService.IsActive) return;

            if (!_tutorialService.HasVisibleStep)
            {
                _view.Hide();
                SetHighlight(null);
                return;
            }

            var step = _tutorialService.CurrentStep;
            var progressText = BuildProgressText(step.TargetCount, _tutorialService.CurrentProgress);

            _view.SetObjective(step.Text.Resolve(_localization.CurrentLocale), progressText);

            UpdatePointer(step);
            SetHighlight(step.HighlightMiracleButton ? (MiracleType?)step.HighlightedMiracle : null);
        }

        /// <summary>ВАЖНО 4: Format вызывается только при реальном изменении Progress/TargetCount, не на каждый Tick.</summary>
        private string BuildProgressText(int targetCount, int progress)
        {
            if (targetCount <= 1) return string.Empty;

            if (_cachedTargetCount == targetCount && _cachedProgress == progress) return _cachedProgressText;

            _cachedTargetCount = targetCount;
            _cachedProgress = progress;
            _cachedProgressText = string.Format("{0}/{1}", progress, targetCount);
            return _cachedProgressText;
        }

        private void UpdatePointer(TutorialStep step)
        {
            switch (step.PointerTarget)
            {
                case TutorialPointerTarget.ActiveNeedSite:
                    UpdateWorldPointer();
                    break;

                case TutorialPointerTarget.MiracleButton:
                    _view.ShowPointerOverRect(_view.GetMiracleButtonTarget(step.HighlightedMiracle));
                    break;

                case TutorialPointerTarget.WorshipBar:
                    _view.ShowPointerOverRect(_view.WorshipBarTarget);
                    break;

                default:
                    _view.HidePointer();
                    break;
            }
        }

        /// <summary>
        /// Playtest-фикс шага 10 (2026-09-11): раньше указатель скрывался, как только цель выходила
        /// за экран или за спину камеры — игрок терял ориентир (горящий дом вне кадра). Теперь
        /// указатель виден ВСЕГДА, пока цель существует: TutorialPointerRules прижимает его к краю
        /// экрана и разворачивает в сторону цели (классический off-screen-индикатор). Скрывается
        /// только при отсутствии камеры/цели — не из-за положения на экране.
        /// </summary>
        private void UpdateWorldPointer()
        {
            if (_camera == null) _camera = UnityEngine.Camera.main;
            if (_camera == null)
            {
                _view.HidePointer();
                return;
            }

            if (!_tutorialService.TryGetWorldTargetPosition(out var worldPosition))
            {
                _view.HidePointer();
                return;
            }

            var viewportPoint = _camera.WorldToViewportPoint(worldPosition);
            TutorialPointerRules.ComputeIndicator(viewportPoint, PointerMarginViewportX, PointerMarginViewportY,
                out var clampedViewport, out var angleDeg, out var isClamped);

            var screenPoint = _camera.ViewportToScreenPoint(new Vector3(clampedViewport.x, clampedViewport.y, 0f));
            _view.ShowPointerAtScreenPoint(screenPoint, angleDeg, isClamped);
        }

        private void SetHighlight(MiracleType? miracle)
        {
            if (_highlightedMiracle == miracle) return;

            if (_highlightedMiracle.HasValue)
            {
                _view.SetMiracleHighlight(_highlightedMiracle.Value, false);
            }

            _highlightedMiracle = miracle;

            if (_highlightedMiracle.HasValue)
            {
                _view.SetMiracleHighlight(_highlightedMiracle.Value, true);
            }
        }
    }
}
