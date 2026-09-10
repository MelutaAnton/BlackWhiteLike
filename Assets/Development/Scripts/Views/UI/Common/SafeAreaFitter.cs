using UnityEngine;

namespace BlackWhiteLike.Views.UI.Common
{
    /// <summary>
    /// Вписывает свой RectTransform в <see cref="Screen.safeArea"/> (шаг 9, Mobile polish — вырезы/
    /// чёлки/скруглённые углы). Только Unity API, без DI: читает исключительно
    /// <see cref="Screen.safeArea"/>/<see cref="Screen.width"/>/<see cref="Screen.height"/>, никаких
    /// сервисов не требует — <c>[Inject] Construct</c> не нужен. Работает и в редакторе (Device
    /// Simulator меняет Screen.safeArea в Play Mode), поэтому применение — в <see cref="LateUpdate"/>
    /// с кэшем последней применённой области, а не однократно в Awake/Start: safeArea может
    /// измениться на лету (поворот устройства, Device Simulator), а стоимость проверки кэша —
    /// сравнение четырёх float, на кадр без изменений это ноль работы (anchorMin/anchorMax не
    /// трогаются).
    /// </summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastAppliedSafeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private bool _hasAppliedOnce;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void LateUpdate()
        {
            if (_rectTransform == null) return;

            var safeArea = Screen.safeArea;
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;

            // Ноль работы, пока ни safeArea, ни размер экрана не поменялись с прошлого применения.
            if (_hasAppliedOnce &&
                safeArea == _lastAppliedSafeArea &&
                screenWidth == _lastScreenWidth &&
                screenHeight == _lastScreenHeight)
            {
                return;
            }

            ApplySafeArea(safeArea, screenWidth, screenHeight);

            _lastAppliedSafeArea = safeArea;
            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            _hasAppliedOnce = true;
        }

        private void ApplySafeArea(Rect safeArea, int screenWidth, int screenHeight)
        {
            // screenWidth/screenHeight гарантированно > 0 в рантайме Unity — защита от деления на 0 не нужна.
            var anchorMin = new Vector2(safeArea.xMin / screenWidth, safeArea.yMin / screenHeight);
            var anchorMax = new Vector2(safeArea.xMax / screenWidth, safeArea.yMax / screenHeight);

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
        }
    }
}
