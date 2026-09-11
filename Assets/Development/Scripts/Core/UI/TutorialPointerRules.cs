using UnityEngine;

namespace BlackWhiteLike.Core.UI
{
    /// <summary>
    /// Чистая арифметика классического off-screen-индикатора для мирового указателя туториала
    /// (playtest-фикс шага 10, 2026-09-11): раньше <c>TutorialPresenter</c> скрывал указатель, как
    /// только цель выходила за экран или за спину камеры — игрок терял ориентир (горящий дом вне
    /// кадра, стрелка застревала в углу без смысла). Static-класс чистых функций, тот же прецедент,
    /// что <c>PinchGestureRules</c>/<c>VillagerMovementRules</c>: без состояния и без завязки на
    /// Unity API, кроме Vector2/Vector3/Mathf — тестируется без сцены, без интерфейса и без
    /// регистрации в GameLifetimeScope.
    /// </summary>
    public static class TutorialPointerRules
    {
        private const float ViewportCenter = 0.5f;
        private const float DirectionEpsilon = 0.0001f;

        /// <summary>
        /// Считает позицию и поворот индикатора по viewport-координатам цели.
        /// </summary>
        /// <param name="viewportPoint">
        /// Результат <c>Camera.WorldToViewportPoint</c> — x/y в [0..1] при видимой цели, z — знаковая
        /// дистанция вдоль forward камеры (z &lt;= 0, когда цель за спиной камеры; в этом случае x/y
        /// отдаются ЗЕРКАЛЬНО относительно центра — перспективное деление на отрицательный z
        /// переворачивает проекцию, это стандартное поведение Unity, а не баг вызывающего кода).
        /// </param>
        /// <param name="marginViewportX">Отступ от левого/правого края экрана в долях viewport (0..0.5).</param>
        /// <param name="marginViewportY">Отступ от верхнего/нижнего края экрана в долях viewport (0..0.5).</param>
        /// <param name="clampedViewport">
        /// Итоговая viewport-позиция индикатора: сама цель, если она видима и внутри отступов;
        /// иначе — точка на прямоугольной рамке [marginX..1-marginX] x [marginY..1-marginY] на луче
        /// от центра экрана к цели (направление сохраняется, длина обрезается).
        /// </param>
        /// <param name="angleDeg">
        /// Направление от центра экрана к цели в математической системе (0° — вправо, против часовой
        /// стрелки, как <c>Mathf.Atan2</c>). Имеет смысл только при <paramref name="isClamped"/> ==
        /// true — View поворачивает спрайт стрелки на этот угол (с поправкой на разворот спрайта по
        /// умолчанию, если он не "смотрит" вправо). При false возвращается 0, и View обязан
        /// игнорировать поворот, сохраняя прежний вид (указатель висит над целью без разворота).
        /// </param>
        /// <param name="isClamped">true — цель вне экрана (или за камерой), позиция прижата к краю.</param>
        public static void ComputeIndicator(
            Vector3 viewportPoint,
            float marginViewportX,
            float marginViewportY,
            out Vector2 clampedViewport,
            out float angleDeg,
            out bool isClamped)
        {
            var x = viewportPoint.x;
            var y = viewportPoint.y;

            // Ревью (важное 3): цель ровно в плоскости камеры (w == 0) даёт ±Infinity из
            // WorldToViewportPoint; дальше Inf * 0 = NaN уехал бы в anchoredPosition/eulerAngles.
            // Нефинитный вход сводим к вырожденной ветке "направления нет".
            if (!float.IsFinite(x) || !float.IsFinite(y))
            {
                angleDeg = 90f;
                clampedViewport = new Vector2(ViewportCenter, 1f - marginViewportY);
                isClamped = true;
                return;
            }

            // Цель за камерой: перспективное деление на z<=0 зеркалит x/y относительно центра.
            // Отражаем обратно, чтобы направление считалось в сторону РЕАЛЬНОЙ цели, а не в
            // противоположную (классический трюк off-screen-индикатора).
            if (viewportPoint.z <= 0f)
            {
                x = 1f - x;
                y = 1f - y;
            }

            var isOnScreen = viewportPoint.z > 0f
                && x >= marginViewportX && x <= 1f - marginViewportX
                && y >= marginViewportY && y <= 1f - marginViewportY;

            if (isOnScreen)
            {
                clampedViewport = new Vector2(x, y);
                angleDeg = 0f;
                isClamped = false;
                return;
            }

            isClamped = true;

            var dx = x - ViewportCenter;
            var dy = y - ViewportCenter;

            if (Mathf.Abs(dx) < DirectionEpsilon && Mathf.Abs(dy) < DirectionEpsilon)
            {
                // Вырожденный случай: цель проецируется ровно в центр экрана, но при этом не видима
                // (точно за камерой — зеркалирование вернуло координаты в центр). Направления нет —
                // произвольно указываем вверх, лишь бы не NaN и не дёргался угол между кадрами.
                angleDeg = 90f;
                clampedViewport = new Vector2(ViewportCenter, 1f - marginViewportY);
                return;
            }

            angleDeg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            clampedViewport = ClampToViewportEdge(dx, dy, marginViewportX, marginViewportY);
        }

        /// <summary>Пересечение луча из центра экрана с прямоугольной рамкой отступов — сохраняет направление, обрезает длину.</summary>
        private static Vector2 ClampToViewportEdge(float dx, float dy, float marginViewportX, float marginViewportY)
        {
            var halfWidth = ViewportCenter - marginViewportX;
            var halfHeight = ViewportCenter - marginViewportY;

            // ">=", а не ">": граница eps должна попадать в валидную ось, иначе случай
            // |dx| == |dy| == eps не покрыт ни вырожденной веткой (< eps), ни этой — обе шкалы
            // стали бы Infinity и дали NaN (ревью, незначительное 5).
            var scaleX = Mathf.Abs(dx) >= DirectionEpsilon ? halfWidth / Mathf.Abs(dx) : float.PositiveInfinity;
            var scaleY = Mathf.Abs(dy) >= DirectionEpsilon ? halfHeight / Mathf.Abs(dy) : float.PositiveInfinity;
            var scale = Mathf.Min(scaleX, scaleY);

            return new Vector2(ViewportCenter + dx * scale, ViewportCenter + dy * scale);
        }

        /// <summary>
        /// Итоговый Z-поворот спрайта стрелки: угол на цель (математическая система, 0° — вправо)
        /// плюс поправка на разворот арта. Прошитый спрайт 03_Arrow_bot "смотрит" вниз (вектор
        /// (0,-1) = -90°), его поправка — +90°: цель справа (0°) → поворот 90° (шеврон-вниз,
        /// повёрнутый против часовой, смотрит вправо); цель сверху (90°) → 180° (смотрит вверх).
        /// Композиция вынесена сюда из View, чтобы знак поправки был под тестами (ревью: блокер
        /// "стрелка в противоположную сторону" жил именно в ней).
        /// </summary>
        public static float ToSpriteRotation(float angleDeg, float spriteOffsetDeg)
        {
            return angleDeg + spriteOffsetDeg;
        }
    }
}
