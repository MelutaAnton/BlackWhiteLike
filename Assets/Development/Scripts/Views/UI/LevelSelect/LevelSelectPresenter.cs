using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Flow;
using BlackWhiteLike.Services.Level;
using BlackWhiteLike.Services.Meta;
using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.LevelSelect
{
    /// <summary>
    /// MVP-Presenter экрана выбора уровня (game-design §7, §9, шаг 7). Pure C#. Карточки биндятся
    /// заново при каждом входе в <c>GameState.LevelSelect</c> — locked/completed могли измениться с
    /// прошлого показа (игрок только что прошёл уровень). Клик по открытой карточке выбирает
    /// уровень и грузит сцену Village_Base.
    /// </summary>
    public sealed class LevelSelectPresenter : IStartable, IDisposable
    {
        private readonly ILevelSelectView _view;
        private readonly IGameFlowService _gameFlow;
        private readonly IEventBus _eventBus;
        private readonly LevelCatalogConfig _catalog;
        private readonly ILevelProgressService _progress;
        private readonly ILevelSelectionService _selection;
        private readonly ISceneLoaderService _sceneLoader;

        private IDisposable _stateChangedSubscription;

        public LevelSelectPresenter(
            ILevelSelectView view,
            IGameFlowService gameFlow,
            IEventBus eventBus,
            LevelCatalogConfig catalog,
            ILevelProgressService progress,
            ILevelSelectionService selection,
            ISceneLoaderService sceneLoader)
        {
            _view = view;
            _gameFlow = gameFlow;
            _eventBus = eventBus;
            _catalog = catalog;
            _progress = progress;
            _selection = selection;
            _sceneLoader = sceneLoader;
        }

        public void Start()
        {
            var visible = _gameFlow.Current == GameState.LevelSelect;
            _view.SetVisible(visible);
            if (visible) BindCards();

            _view.CardClicked += OnCardClicked;
            _view.BackClicked += OnBackClicked;
            _stateChangedSubscription = _eventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        public void Dispose()
        {
            _view.CardClicked -= OnCardClicked;
            _view.BackClicked -= OnBackClicked;
            _stateChangedSubscription?.Dispose();
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            var visible = evt.To == GameState.LevelSelect;
            _view.SetVisible(visible);
            if (visible) BindCards();
        }

        /// <summary>Карточек в каталоге может быть меньше/больше, чем в разметке (см. ILevelSelectView) — лишние скрываются, недостающие логируются.</summary>
        private void BindCards()
        {
            var entries = _catalog.Entries;
            var cardCount = _view.CardCount;

            for (var i = 0; i < cardCount; i++)
            {
                if (i >= entries.Length)
                {
                    _view.HideCard(i);
                    continue;
                }

                var entry = entries[i];
                if (entry == null || entry.Level == null)
                {
                    _view.HideCard(i);
                    continue;
                }

                var locked = !_progress.IsUnlocked(entry.Level.Id);
                var completed = _progress.IsCompleted(entry.Level.Id);
                _view.BindCard(i, entry.Level.Id.ToString(), entry.Preview, locked, completed);
            }

            if (entries.Length > cardCount)
            {
                Debug.LogWarning($"[LevelSelectPresenter] В каталоге {entries.Length} уровней, а в разметке {cardCount} карточек — лишние уровни не будут показаны.");
            }
        }

        private void OnCardClicked(int index)
        {
            var entries = _catalog.Entries;
            if (index < 0 || index >= entries.Length) return;

            var entry = entries[index];
            if (entry == null || entry.Level == null) return;

            // Предохранитель — заблокированная карточка не должна быть кликабельна
            // (LevelCardView.SetLocked гасит Button.interactable), но проверка и на уровне логики.
            if (!_progress.IsUnlocked(entry.Level.Id)) return;

            _selection.Select(entry.Level);
            _sceneLoader.Load(SceneNames.VillageBase);
        }

        private void OnBackClicked() => _gameFlow.ChangeState(GameState.MainMenu);
    }
}
