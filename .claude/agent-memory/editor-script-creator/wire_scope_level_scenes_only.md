---
name: wire-scope-level-scenes-only
description: There is no dedicated multi-scene "Wire All Scenes" loop script — level-scene-only scope wiring lives in TestVillageSceneSetupEditor.WireScope, called once per scene by two separate MenuItem entry points
metadata:
  type: project
---

Проект НЕ имеет отдельного editor-скрипта, который открывает все сцены в цикле
(`EditorSceneManager.OpenScene` в `foreach`) и прошивает `GameLifetimeScope`. Вместо этого:

- `TestVillageSceneSetupEditor.BuildVillageSceneCore(scenePath, initialState, isTemporarySandbox)` —
  общее ядро сборки ОДНОЙ деревенской сцены (структуры, рука, камера, [Systems], [Miracles], HUD,
  Alignment, `WireScope`). Вызывается ДВАЖДЫ, каждый раз из своего MenuItem, с разным `scenePath`:
  - `BuildTestVillageScene()` (тот же файл) → `_TestVillage.unity` (temp sandbox).
  - `VillageLevelSetupEditor.BuildVillageLevel()` → `Village_Base.unity` (боевая, одна на всю
    кампанию — LevelId выбирается в рантайме, не сцена-на-уровень).
- Добавление нового SerializeField на `GameLifetimeScope`, который нужен только в игровых сценах
  (не в MainMenu/Temple/Bootstrap) → расширяется СИГНАТУРА `WireScope` (+ параметр, + `SetRef`
  вызов внутри, + инкремент `const int expected`), а не создаётся отдельный скрипт. Значение
  ассета грузится через `AssetDatabase.LoadAssetAtPath` в общем блоке загрузки конфигов
  `BuildVillageSceneCore` (ПОСЛЕ `OpenScene`, см. правило editor-files.md), сценный компонент —
  через `Object.FindAnyObjectByType<T>(FindObjectsInactive.Include)` там же.
- Мета-сцены (MainMenu/Temple/Bootstrap) не вызывают `WireScope` вообще — поле там просто не
  прошивается, никакого warning не будет (это нормально: warning только если поле НЕ найдено на
  scope или значение не найдено В СЦЕНЕ, ГДЕ WireScope реально вызывается).

Пример (шаг Tutorial): `_tutorialConfig`/`_tutorialView` добавлены именно так — `TutorialConfig`
грузится по константному пути (`TutorialConfig_Level01.asset`, один вариант, без выбора по имени
сцены — второй уровень с туториалом пока не существует), `TutorialView` ищется как компонент на
корне `LevelHudCanvas.prefab` (тот же root, что `LevelHudView`/`MiraclePanelView`).

См. также [[tutorial-pointer-icon-path]].
