using System;
using UnityEngine;

namespace BlackWhiteLike.Views.UI.LevelSelect
{
    /// <summary>
    /// MVP-View экрана выбора уровня (game-design §7, шаг 7). Маршрутизацию по конкретной карточке
    /// делает реализация (<c>LevelSelectView</c>); презентер работает только с индексом записи
    /// каталога.
    /// </summary>
    public interface ILevelSelectView
    {
        void SetVisible(bool visible);

        /// <summary>Число карточек в разметке — презентер может иметь в каталоге меньше/больше записей.</summary>
        int CardCount { get; }

        void BindCard(int index, string title, Sprite preview, bool locked, bool completed);

        /// <summary>Карточек в каталоге меньше, чем в разметке, — лишние скрываются.</summary>
        void HideCard(int index);

        /// <summary>Клик по карточке — индекс в <c>LevelCatalogConfig.Entries</c> (в т.ч. по заблокированной — презентер сам её игнорирует).</summary>
        event Action<int> CardClicked;

        event Action BackClicked;
    }
}
