---
name: project_step10_tutorial_pointer_offscreen
description: Playtest-фикс шага 10 — off-screen-индикатор указателя туториала (TutorialPointerRules); прецедент для любого будущего world-space UI-индикатора
type: project
---

Playtest-фикс 2026-09-11 (после первого прохода шага 10, [[project_step10_tutorial_localization]]):
`TutorialPresenter` раньше скрывал указатель на мировую цель, как только она выходила за экран
или за спину камеры — игрок терял ориентир (горящий дом вне кадра).

**Что добавлено (не дублировать)**:
- `Core/UI/TutorialPointerRules.cs` — pure C# static, тот же прецедент, что `PinchGestureRules`/
  `VillagerMovementRules` (без состояния, без Unity API кроме Vector2/Vector3/Mathf, без
  интерфейса, без регистрации в DI). `ComputeIndicator(viewportPoint, marginX, marginY, out
  clampedViewport, out angleDeg, out isClamped)` — классический off-screen-indicator: цель за
  камерой (`viewportPoint.z <= 0`) даёт ЗЕРКАЛЬНЫЕ x/y от `WorldToViewportPoint` (перспективное
  деление на отрицательный z) — обязательно отражать обратно (`x = 1-x; y = 1-y`) ПЕРЕД расчётом
  направления, иначе индикатор указывает в противоположную сторону. Клампинг — пересечение луча
  из центра экрана с прямоугольником отступов (`scale = min(halfWidth/|dx|, halfHeight/|dy|)`).
  Угол — `Atan2(dy,dx)` в математической системе (0°=вправо), имеет смысл только при
  `isClamped==true`; `isClamped==false` → View обязан не поворачивать спрайт (прежний вид).
  9 unit-тестов (центр/внутри маржи/4 края/угол/за камерой зеркалит НЕ туда же/вырожденный
  центр-за-камерой без NaN/граница маржи включительно).
- `TutorialView.ShowPointerAtScreenPoint` — сигнатура расширена до `(screenPoint, angleDeg,
  isClamped)`, крутит `_pointer.localEulerAngles.z` только при `isClamped`; новый SerializeField
  `_pointerRotationOffsetDeg` (default -90) — поправка на разворот спрайта стрелки по умолчанию
  ("смотрит вверх"), не хардкод, подбирается в инспекторе под конкретный арт.
  `ShowPointerOverRect` (UI-цели — кнопка чуда/Worship bar) теперь явно сбрасывает
  `localEulerAngles = Vector3.zero` — указатель переиспользуется между мировой и UI-целями,
  без сброса UI-цель наследовала бы разворот от предыдущего world-кадра.
- `TutorialPresenter.UpdateWorldPointer` — `WorldToViewportPoint` → `TutorialPointerRules.
  ComputeIndicator` → `Camera.ViewportToScreenPoint` (не ручная арифметика Screen.width/height —
  учитывает `camera.pixelRect`). Константы маржи `PointerMarginViewportX=0.08f/Y=0.1f` — доли
  viewport, не пиксели. Старый ранний `return` при `screenPoint.z < 0f` убран целиком — теперь
  единственные причины `HidePointer()` — нет камеры или нет активной цели у сервиса.

**Why**: off-screen-индикаторы — стандартная UX-задача любого дальнейшего world-space HUD-элемента
(маркер новой нужды, направление на врага и т.п.) — код не переоткрывать, использовать
`TutorialPointerRules` как есть или как шаблон.

**How to apply**: новый world-space индикатор с той же механикой — переиспользовать
`TutorialPointerRules.ComputeIndicator` напрямую (он не завязан на туториал, только на
viewport-координаты); если нужен другой формфактор рамки (не прямоугольник, круг) — новый метод
в том же файле, не встраивать в презентер.

Связано: [[project_step10_tutorial_localization]] (TutorialPresenter/TutorialView — база, до фикса).
