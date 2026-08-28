using UnityEngine;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Таблица чистых функций движения жителя (границы уровня, направление бегства, выпрямление
    /// корпуса, поиск ближайшей точки спавна) — вынесена из <c>VillagerController</c> (рефакторинг
    /// по итогам архитектурного аудита после шага 3). Это набор статических хелперов вида
    /// <c>Mathf</c>, а не DI-сервис: осознанное отклонение от правила "каждый сервис имеет
    /// интерфейс" — без состояния и без обращений к Unity API, кроме Vector3/Quaternion/Mathf,
    /// поэтому без интерфейса и без регистрации в GameLifetimeScope. Тестируется без Unity-сцены.
    /// </summary>
    public static class VillagerMovementRules
    {
        /// <summary>Квадрат длины, ниже которого направление считается вырожденным (нулевым).</summary>
        public const float DirectionEpsilonSqr = 0.0001f;

        /// <summary>
        /// Клампит точку в квадрат [-halfExtent; halfExtent] по X/Z (Y не трогаем — высота не часть
        /// границы уровня). Чистая арифметика без обращений к состоянию компонента — тестируется без
        /// Unity-сцены (см. VillagerMovementRulesBoundsTests).
        /// </summary>
        public static Vector3 ClampToVillageBounds(Vector3 point, float halfExtent)
        {
            return new Vector3(
                Mathf.Clamp(point.x, -halfExtent, halfExtent),
                point.y,
                Mathf.Clamp(point.z, -halfExtent, halfExtent));
        }

        /// <summary>
        /// Направление бегства у границы уровня: если позиция уже на грани/за гранью и направление
        /// продолжает толкать ЕЩЁ дальше по какой-то оси — эта составляющая гасится (не полная
        /// остановка — скольжение вдоль края по оставшейся оси, как и просил владелец после
        /// критического дефекта третьего playtest'а: "убегание от источника опасности у обрыва
        /// гарантированно уводит за край, разворот вдоль неё, а не остановка"). Если это выродило
        /// направление в ноль (уткнулся ровно в угол по обеим осям) — тангенциальный разворот на 90°
        /// от исходного направления, лишь бы не стоять на месте у обрыва. Чистая арифметика — без
        /// обращений к Transform/Rigidbody, тестируется без Unity-сцены.
        /// </summary>
        public static Vector3 ClampFleeDirectionToBounds(Vector3 position, Vector3 direction, float halfExtent)
        {
            var clamped = ZeroOutwardComponents(position, direction, halfExtent);

            if (clamped.sqrMagnitude <= DirectionEpsilonSqr)
            {
                // Уткнулся в угол по обеим осям сразу — поворот исходного направления на 90° вокруг
                // Y вместо остановки. Следующий тик пересчитает направление по новой позиции заново.
                var tangent = new Vector3(-direction.z, 0f, direction.x);

                // Вычитка диффа целиком (п.5): повёрнутый вектор мог толкать наружу по ВТОРОЙ оси —
                // прогоняем его через те же проверки ещё раз, иначе функция нарушала бы собственный
                // контракт "не возвращаю направление наружу".
                clamped = ZeroOutwardComponents(position, tangent, halfExtent);

                if (clamped.sqrMagnitude <= DirectionEpsilonSqr)
                {
                    // Теоретически недостижимо (тангенциальный вектор всегда содержит составляющую
                    // хотя бы по одной "свободной" оси угла) — фолбэк ниже страхует от NaN.
                    clamped = tangent;
                }
            }

            return clamped.sqrMagnitude > DirectionEpsilonSqr ? clamped.normalized : Vector3.forward;
        }

        private static Vector3 ZeroOutwardComponents(Vector3 position, Vector3 direction, float halfExtent)
        {
            var result = direction;

            if (position.x >= halfExtent && result.x > 0f) result.x = 0f;
            if (position.x <= -halfExtent && result.x < 0f) result.x = 0f;
            if (position.z >= halfExtent && result.z > 0f) result.z = 0f;
            if (position.z <= -halfExtent && result.z < 0f) result.z = 0f;

            return result;
        }

        /// <summary>
        /// Жёсткий кламп скорости у границы уровня (правка после живого прогона: кламп ЦЕЛИ
        /// блуждания/бегства недостаточен — физика/столкновения с другими жителями толкают дальше
        /// клампнутой цели, и это подтверждено логом: FallKillHeight срабатывал регулярно). Гасится
        /// ТОЛЬКО составляющая скорости, направленная НАРУЖУ за границу по конкретной оси —
        /// тангенциальная составляющая (вдоль границы) сохраняется, житель продолжает скользить
        /// вдоль края, а не "примерзает" к нему. Y (гравитация/вертикальная скорость) не трогается
        /// никогда. Чистая арифметика — без обращений к Transform/Rigidbody, тестируется без Unity-сцены.
        /// </summary>
        public static Vector3 ClampVelocityToBounds(Vector3 position, Vector3 velocity, float halfExtent)
        {
            // Ревью аудита после шага 3: тело было побайтово тем же, что ZeroOutwardComponents (пока
            // функции жили в разных концах контроллера, дубль был неочевиден). Правило одно —
            // «гасим составляющую, толкающую наружу по оси, где позиция уже на границе», — и для
            // направления бегства, и для скорости; публичная сигнатура сохранена ради call-site'ов и тестов.
            return ZeroOutwardComponents(position, velocity, halfExtent);
        }

        /// <summary>
        /// Целевой поворот "корпус вертикален, yaw сохранён" (пятый playtest шага 3): в Tossed
        /// constraints снимаются ради кувырка, при приземлении FreezeRotation замораживает тело в
        /// ЛЮБОЙ ориентации касания — без этого хелпера житель мог навсегда остаться лежать на боку
        /// или вниз головой (скриншот владельца), потому что никто его не выпрямлял. Чистая
        /// арифметика над Quaternion/Vector3 — тестируется без Unity-сцены.
        /// </summary>
        public static Quaternion ComputeUprightRotation(Quaternion current)
        {
            var forward = current * Vector3.forward;
            var flatForward = new Vector3(forward.x, 0f, forward.z);

            if (flatForward.sqrMagnitude < DirectionEpsilonSqr)
            {
                // forward почти вертикален (житель лежит мордой вниз/вверх) — горизонтальной
                // проекции forward нет, но up гарантированно горизонтален (ортогонален forward).
                var up = current * Vector3.up;
                flatForward = new Vector3(up.x, 0f, up.z);
            }

            if (flatForward.sqrMagnitude < DirectionEpsilonSqr)
            {
                // Теоретически недостижимо (up и forward не могут оба оказаться вертикальными
                // одновременно — они ортогональны) — детерминированный фолбэк без NaN на всякий случай.
                flatForward = Vector3.forward;
            }

            return Quaternion.LookRotation(flatForward.normalized, Vector3.up);
        }

        /// <summary>
        /// Шаг 4: скорость разлёта жителя от взрыва чуда (Fireball, game-design §4). Горизонтальное
        /// направление — от <paramref name="origin"/> (точка каста) к <paramref name="position"/>
        /// жителя; вырожденный случай (житель ровно в эпицентре) — Vector3.forward вместо NaN, тот
        /// же приём, что в UpdateFlee. <paramref name="upwardBias"/> подмешивает вертикальную
        /// составляющую (доля от <paramref name="power"/>) — иначе взрыв расшвыривал бы жителей
        /// строго по земле. Чистая арифметика — тестируется без Unity-сцены.
        /// </summary>
        public static Vector3 ComputeBlastVelocity(Vector3 origin, Vector3 position, float power, float upwardBias)
        {
            var flat = position - origin;
            flat.y = 0f;

            var flatDirection = flat.sqrMagnitude > DirectionEpsilonSqr ? flat.normalized : Vector3.forward;

            return (flatDirection + Vector3.up * upwardBias).normalized * power;
        }

        /// <summary>Ближайшая (по горизонтали) точка спавна к упавшему жителю; при пустом списке — координатный центр деревни как последний фолбэк.</summary>
        public static Vector3 FindNearestSpawnPosition(Vector3 fromPosition, Vector3[] spawnPositions)
        {
            if (spawnPositions == null || spawnPositions.Length == 0) return Vector3.zero;

            var nearest = spawnPositions[0];
            var nearestDistanceSqr = HorizontalDistanceSqr(fromPosition, nearest);

            for (var i = 1; i < spawnPositions.Length; i++)
            {
                var distanceSqr = HorizontalDistanceSqr(fromPosition, spawnPositions[i]);
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearest = spawnPositions[i];
                }
            }

            return nearest;
        }

        private static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
