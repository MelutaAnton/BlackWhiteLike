using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Factories.Ui;
using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// MVP-Presenter попапов Worship (game-design §10, §13, шаг 5). Pure C#.
    /// </summary>
    /// <remarks>
    /// Шаг 10 (волна 2): если источник прироста несёт мировую позицию
    /// (<see cref="WorshipChangedEvent.HasWorldPosition"/>), попап проецируется world→screen→canvas
    /// (тот же приём, что <c>TutorialPresenter.UpdateWorldPointer</c>/<c>TutorialView.ShowPointerAtScreenPoint</c> —
    /// камера по тегу MainCamera, кэшируется, конверсия через <c>RectTransformUtility.ScreenPointToLocalPointInRectangle</c>
    /// с учётом <c>Canvas.renderMode</c>). Фоллбэк на прежнюю HUD-точку (null — <c>WorshipPopupFactory</c>
    /// сама подставит дефолт) — нет позиции, нет камеры, цель за спиной камеры или вне viewport.
    /// </remarks>
    public sealed class WorshipPopupPresenter : IStartable, IDisposable
    {
        private readonly IWorshipPopupFactory _factory;
        private readonly IEventBus _eventBus;
        private readonly IWorshipPopupHost _host;

        private IDisposable _worshipChangedSubscription;

        // Кэш камеры/канваса (тот же приём, что TutorialPresenter._camera) — переполучаем только
        // если ссылка стала null (fake-null после выгрузки сцены — явный ==, не ??).
        private UnityEngine.Camera _camera;
        private Canvas _canvas;

        public WorshipPopupPresenter(IWorshipPopupFactory factory, IEventBus eventBus, IWorshipPopupHost host)
        {
            _factory = factory;
            _eventBus = eventBus;
            _host = host;
        }

        public void Start()
        {
            _worshipChangedSubscription = _eventBus.Subscribe<WorshipChangedEvent>(OnWorshipChanged);
        }

        public void Dispose()
        {
            _worshipChangedSubscription?.Dispose();
        }

        private void OnWorshipChanged(WorshipChangedEvent evt)
        {
            if (evt.NewValue <= evt.OldValue) return; // затухание/уменьшение — попап не показываем
            _factory.Show(evt.NewValue - evt.OldValue, evt.Source, TryResolveAnchoredPosition(evt));
        }

        private Vector2? TryResolveAnchoredPosition(WorshipChangedEvent evt)
        {
            if (!evt.HasWorldPosition) return null;

            if (_camera == null) _camera = UnityEngine.Camera.main;
            if (_camera == null) return null;

            var viewportPoint = _camera.WorldToViewportPoint(evt.WorldPosition);

            // За спиной камеры или вне видимой области — фоллбэк на прежнюю HUD-точку (в отличие от
            // TutorialPresenter/off-screen-указателя, попап не прижимается к краю — он привязан к
            // конкретному месту события, вне кадра ему естественнее вернуться к HUD).
            if (viewportPoint.z <= 0f) return null;
            if (viewportPoint.x < 0f || viewportPoint.x > 1f || viewportPoint.y < 0f || viewportPoint.y > 1f) return null;

            var popupRoot = _host.PopupRoot;
            if (popupRoot == null) return null;

            if (_canvas == null) _canvas = popupRoot.GetComponentInParent<Canvas>();

            var screenPoint = _camera.ViewportToScreenPoint(viewportPoint);
            var worldCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(popupRoot, screenPoint, worldCamera, out var localPoint))
                return null;

            return localPoint;
        }
    }
}
