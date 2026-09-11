using System.Collections.Generic;
using BlackWhiteLike.Services.Localization;
using VContainer.Unity;

namespace BlackWhiteLike.Views.UI.Common
{
    /// <summary>
    /// Применяет текст локализации ко всем <see cref="LocalizedTextView"/> сцены — ОДИН раз на
    /// старте (шаг 10 «Polish», волна 2). Источник view'ов — коллекция <see cref="LocalizedTextRegistry"/>
    /// сцены (по одному на канвас — LevelHudCanvas/MainMenuCanvas/TempleCanvas), прошитая на
    /// GameLifetimeScope; рантайм-скан сцены (<c>Object.FindObjectsByType</c>) здесь БОЛЬШЕ не
    /// используется — он запрещён в проде (CLAUDE.md, «Категорически запрещено»). Регистрируется
    /// БЕЗУСЛОВНО во всех сценах (GameLifetimeScope) — статичные надписи есть и в мета-сценах
    /// (MainMenu/Temple), и в игровой (LevelHud); пустой/null список реестров — молчаливый no-op
    /// (тестовые сцены без канвасов).
    /// </summary>
    public sealed class LocalizedTextApplyEntryPoint : IStartable
    {
        private readonly ILocalizationService _localization;
        private readonly IReadOnlyList<LocalizedTextRegistry> _registries;

        public LocalizedTextApplyEntryPoint(ILocalizationService localization, IReadOnlyList<LocalizedTextRegistry> registries)
        {
            _localization = localization;
            _registries = registries;
        }

        public void Start()
        {
            if (_registries == null) return;

            foreach (var registry in _registries)
            {
                if (registry == null) continue;

                var views = registry.Views;
                if (views == null) continue;

                foreach (var view in views)
                {
                    if (view == null) continue;
                    view.Apply(_localization.GetText(view.Key));
                }
            }
        }
    }
}
