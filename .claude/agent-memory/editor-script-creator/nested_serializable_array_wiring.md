---
name: nested-serializable-array-wiring
description: How to wire SO arrays of [Serializable] nested structs (NeedAweEntry, TerrorFearEntry) via SerializedObject
metadata:
  type: project
---

Шаг 5 добавил `WorshipConfig` с двумя массивами вложенных `[Serializable]`-классов:
`_aweByNeed` (`NeedAweEntry { _type, _awe }`) и `_fearByAct` (`TerrorFearEntry { _act, _fear }`).

Паттерн прошивки в инициализаторе `ConfigAssetCreatorEditor.InitWorshipConfig`
(`Assets/Development/Scripts/Editor/Configs/ConfigAssetCreatorEditor.cs`):

```csharp
var arrayProp = so.FindProperty("_aweByNeed");
arrayProp.arraySize = entries.Length;
for (int i = 0; i < entries.Length; i++)
{
    var element = arrayProp.GetArrayElementAtIndex(i);
    element.FindPropertyRelative("_type").enumValueIndex = (int)entries[i].Type;
    element.FindPropertyRelative("_awe").intValue = entries[i].Awe;
}
```

Ключевое отличие от плоских enum-полей уже существующего `DefaultEventTimeline` в
`TestVillageSceneSetupEditor` (та же техника, другое имя полей: `type`/`triggerTime` без
подчёркивания — там сериализуемая структура объявлена с public-полями без `_`, здесь — приватные
`[SerializeField]` с `_`). Перед прошивкой ВСЕГДА проверять реальные имена полей вложенного
класса в рантайм-файле (`FindPropertyRelative` молча возвращает null на опечатке, а
`element.FindPropertyRelative(...).enumValueIndex = ...` на null-результате бросает
NullReferenceException, а не тихо пропускает).
