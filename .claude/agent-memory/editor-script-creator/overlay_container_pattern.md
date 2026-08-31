---
name: overlay-container-pattern
description: Idempotent pattern for building HUD overlays (Dim blocker + centered Panel) as siblings of the gated node, per CLAUDE.md п.7
metadata:
  type: project
---

Шаг 5 (Worship + win) ввёл три HUD-оверлея (LevelComplete/LevelStalled/Pause) в
`Assets/Development/Scripts/Editor/UI/LevelHudSetupEditor.cs`. Общий каркас — метод
`BuildOverlayContainer(canvasRoot, name, panelSize) -> Transform panelTransform`:

- Контейнер — сиблинг остальных HUD-узлов (MiraclePanel/FaithBar/WorshipBar) под тем же канвасом,
  НЕ потомок какого-либо гасимого узла (CLAUDE.md п.7).
- Внутри контейнера: `Dim` (Image, полноэкранный, чёрный alpha 0.6, `raycastTarget = true` —
  блокирует тапы в мир, пока оверлей открыт) + `Panel` (центрированный Image заданного размера,
  `raycastTarget = true`).
- Контейнер выключается (`SetActive(false)`) только если сейчас включён — лог только при
  фактической смене (идемпотентность логов, см. паттерн SelectedFrame в BuildMiracleButton).
- Контейнер переносится на последний сиблинг (`SetAsLastSibling`) при каждом прогоне, если он
  ещё не последний — гарантирует, что оверлеи рисуются поверх HUD, даже если кто-то потом
  добавит новый HUD-элемент после них в коде сборки.
- View-компонент (`LevelCompleteView` и т.п.) вешается на КОНТЕЙНЕР (не на Panel) — `_root`
  ссылается на сам контейнер, `SetActive` на нём гасит/включает весь оверлей разом.

Вспомогательные билдеры внутри Panel (не в отдельном файле — private static в
LevelHudSetupEditor):
- `BuildOverlayLabel(panel, name, font, size, defaultText, anchoredY, size, bold)` — якорь
  верх-центр панели, `anchoredY` — отрицательное смещение вниз от верхнего края.
- `BuildOverlayButton(panel, name, font, caption, anchoredY, size)` — якорь низ-центр панели,
  текст кнопки ВСЕГДА перезаписывается (это статичная подпись, не рантайм-данные, в отличие от
  Worship/Faith лейблов, которые дефолтятся только если пустые).

Используется для трёх новых оверлеев; при добавлении четвёртого — переиспользовать эти три
хелпера, не копировать код Dim/Panel заново.
