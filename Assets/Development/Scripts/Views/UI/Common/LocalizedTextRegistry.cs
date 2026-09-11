using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackWhiteLike.Views.UI.Common
{
    /// <summary>
    /// Тупой держатель списка <see cref="LocalizedTextView"/> канваса (шаг 10 «Polish», волна 2 —
    /// зачистка рантайм-скана сцены). Без DI — вешается editor-скриптами сборки канвасов
    /// (LevelHudCanvas/MainMenuCanvas/TempleCanvas) на КОРЕНЬ канваса, <see cref="_views"/> — все
    /// <see cref="LocalizedTextView"/> под этим канвасом (включая неактивные). Читается
    /// <see cref="LocalizedTextApplyEntryPoint"/> через коллекцию реестров сцены
    /// (GameLifetimeScope._localizedTextRegistries) — замена запрещённому
    /// <see cref="Object.FindObjectsByType{T}(FindObjectsInactive, FindObjectsSortMode)"/> в проде
    /// (CLAUDE.md, «Категорически запрещено»).
    /// </summary>
    public sealed class LocalizedTextRegistry : MonoBehaviour
    {
        [SerializeField] private LocalizedTextView[] _views = Array.Empty<LocalizedTextView>();

        public IReadOnlyList<LocalizedTextView> Views => _views;
    }
}
