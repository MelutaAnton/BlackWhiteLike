---
name: tutorial-pointer-icon-path
description: EditorSpriteUtility.HudIconPaths.TutorialPointer = 03_Arrow_bot.png (Blue flat icon set) — arrow icon for TutorialView._pointer
metadata:
  type: project
---

Пак ClassicFantasyRPG_UI содержит набор стрелок в `.../UIicons/Flat/Blue/`:
`01_Arrow_right.png`, `02_Arrow_left.png`, `03_Arrow_bot.png`, `03_Arrow_top.png` и дублирующие
наборы (`05_`..`12_`, `161_`..`164_`) — разного визуального стиля, не просто повёрнутые копии.
Для `TutorialView._pointer` (указатель "смотри сюда" над целевым UI-элементом) выбран
`03_Arrow_bot.png` (стрелка вниз) — добавлен как
`EditorSpriteUtility.HudIconPaths.TutorialPointer` (единственный источник, как остальные
`HudIconPaths`/`MiracleIconPaths`).

См. также [[wire-scope-level-scenes-only]].
