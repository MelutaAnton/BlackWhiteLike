using TMPro;
using UnityEngine;

namespace BlackWhiteLike.Views.UI.Common
{
    /// <summary>
    /// Тупой держатель ключа локализации на статичном TMP-лейбле (шаг 10 «Polish», волна 2). Без
    /// DI — применяется <see cref="LocalizedTextApplyEntryPoint"/> напрямую через
    /// <see cref="Apply"/>, вешается editor-скриптами сборки канвасов (MainMenuCanvasSetupEditor/
    /// TempleCanvasSetupEditor/LevelHudSetupEditor).
    /// </summary>
    public sealed class LocalizedTextView : MonoBehaviour
    {
        [SerializeField] private string _key;

        public string Key => _key;

        /// <summary>Применяет уже разрешённый текст к TMP_Text на этом же GameObject. Null-гард — компонент может отсутствовать.</summary>
        public void Apply(string text)
        {
            var label = GetComponent<TMP_Text>();
            if (label == null) return;

            label.text = text;
        }
    }
}
