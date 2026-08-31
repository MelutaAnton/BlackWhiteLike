---
name: alignment-visual-material-pattern
description: How Hand_*/Temple_* alignment-stage materials are derived (tinted AssetDatabase.CopyAsset copies) and how AlignmentVisualConfig stages are wired via nested SerializedProperty
metadata:
  type: project
---

Шаг 6 добавил `AlignmentVisualSetupEditor.cs` (`Editor/Setup/`, менюшка
`BlackWhiteLike/Setup/Setup Alignment Visual`) — материалы alignment-стейджей создаются как
**производные ассеты**, не in-memory материалы (CLAUDE.md: "всё, на что ссылается сохраняемый
ассет, само обязано быть ассетом" — playtest шага 4 уже ловил именно этот класс бага на маркерах
NeedSite):

- `AssetDatabase.CopyAsset(sourcePath, destPath)` — не `new Material(shader)` — сохраняет
  оригинальный шейдер/все текстурные слоты источника, только тинтует `_BaseColor` (URP/Lit) или
  `_Color` (fallback) на копии.
- `Hand_Kind.mat`/`Hand_Cruel.mat` — тинтованные копии уже существующего `Hand_Neutral.mat`
  (собран `GodHandPrefabSetupEditor`).
- `Temple_Neutral.mat` — копия **фактического** материала меша храма: снимается с
  `Temple.prefab` (`GetComponentInChildren<MeshRenderer>(true).sharedMaterial`), НЕ со сценного
  инстанса — работает даже без открытой сцены. `Temple_Kind.mat`/`Temple_Cruel.mat` — тинтованные
  копии этого `Temple_Neutral.mat`, не самого ThirdParty-материала напрямую (два уровня копий).
- Идемпотентность — только "существующий ассет не пересоздаём" (GUID сохраняется), без
  миграции/перетинтовки при повторном прогоне (в отличие от CFXR clearBehavior или VFX
  _maxLifetime, которые мигрируются на каждый прогон) — тинт зафиксирован владельцем один раз.

`AlignmentVisualConfig._stages` — массив `[Serializable]` `AlignmentStage` (аналог
`NeedAweEntry`/`TerrorFearEntry` из `WorshipConfig`, см. память
`nested-serializable-array-wiring`), но с ДВУМЯ вложенными массивами внутри каждого элемента
(`_handMaterials`, `_templeMaterials`) — паттерн: `element.FindPropertyRelative("_handMaterials")`
возвращает ещё один `SerializedProperty` с собственным `.arraySize`/`.GetArrayElementAtIndex`,
работает без проблем (`FindPropertyRelative` рекурсивен). Контракт сортировки: массив по
`_scoreThreshold` УБЫВАЮЩЕ (Saint 75 → Kind 25 → Neutral -25 → Cruel -75 → Demon -100), рантайм
берёт первый элемент с `score >= threshold`.

Материалы должны существовать на диске ДО вызова `ConfigAssetCreatorEditor.CreateAll()` (Init
только при создании ассета) — `SetupAlignmentVisual()` явно вызывает
`AlignmentVisualSetupEditor.EnsureMaterials()` первым шагом, `CreateAll()` вторым. Пути материалов
ДУБЛИРУЮТСЯ как константы в `ConfigAssetCreatorEditor.cs` и `AlignmentVisualSetupEditor.cs` (тот же
приём, что пути VFX-префабов между `MiracleVfxPrefabSetupEditor`/`TestVillageSceneSetupEditor`) —
избегает циклической загрузки одного файла из другого при статической инициализации; при правке
путей — обновлять оба места.
