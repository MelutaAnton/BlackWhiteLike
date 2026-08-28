using System;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>
    /// MVP-View панели чудес (game-design §4, §10). Маршрутизация кликов и обновления по конкретной
    /// кнопке — забота реализации (<c>MiraclePanelView</c>); презентер работает только с типом чуда.
    /// </summary>
    public interface IMiraclePanelView
    {
        /// <summary>Клик по иконке чуда — презентер транслирует его в <c>IHandModeService.SelectMiracle</c>.</summary>
        event Action<MiracleType> MiracleClicked;

        /// <summary>Есть ли конфиг для этого чуда в каталоге (Heal/Lightning на шаге 4 — false, шаг 6).</summary>
        void SetAvailable(MiracleType type, bool available);

        /// <summary>Подсветка выбранного чуда (по <c>MiracleSelectedEvent</c>).</summary>
        void SetSelected(MiracleType type, bool selected);

        /// <summary>Хватает ли Faith на BaseFaithCost прямо сейчас — затемнение недоступной по цене иконки.</summary>
        void SetAffordable(MiracleType type, bool affordable);

        /// <summary>Cost-лейбл (BaseFaithCost чуда).</summary>
        void SetCost(MiracleType type, int cost);

        /// <summary>Иконка чуда из MiracleConfig.Icon.</summary>
        void SetIcon(MiracleType type, Sprite icon);
    }
}
