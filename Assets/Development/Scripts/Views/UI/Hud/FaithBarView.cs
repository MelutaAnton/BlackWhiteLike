using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackWhiteLike.Views.UI.Hud
{
    /// <summary>Бар Faith (game-design §4, §10) — только UI, без бизнес-логики.</summary>
    public sealed class FaithBarView : MonoBehaviour, IFaithBarView
    {
        [Tooltip("Image.Type.Filled — fillAmount = current / capacity.")]
        [SerializeField] private Image _fillImage;

        [SerializeField] private TextMeshProUGUI _valueLabel;

        public void SetFaith(float current, float capacity)
        {
            if (_fillImage != null)
            {
                _fillImage.fillAmount = capacity > 0f ? Mathf.Clamp01(current / capacity) : 0f;
            }

            if (_valueLabel != null)
            {
                _valueLabel.text = string.Format("{0:0}/{1:0}", current, capacity);
            }
        }
    }
}
