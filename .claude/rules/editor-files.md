---
path: "**/Editor/**"
---

## Известные нюансы Editor-скриптов

### Требования к Editor-скриптам
- Каждый скрипт доступен через меню: `[MenuItem("BlackWhiteLike/...")]` (категории и naming — в skill `add-editor-script`)
- Поддержка Undo: `Undo.RegisterCreatedObjectUndo()` для всех созданных объектов
- Логирование: `Debug.Log("[Editor] Created ...")` для каждого действия
- Идемпотентность: повторный запуск не дублирует объекты (проверять существующие)
- Все editor-скрипты обёрнуты в `#if UNITY_EDITOR` или лежат в папке Editor/

### Идемпотентность: проверять компонент, а не только GameObject
При создании объектов на сцене стандартный паттерн `if (GameObject.Find("X") != null) return`
не работает если в сцене уже есть объект с таким именем (например, дефолтная `Main Camera`
от Unity), но без нужного компонента. Всегда проверять наличие именно компонента:
```csharp
// НЕВЕРНО — выйдет раньше, не добавив компонент к существующему объекту:
if (FindExisting("Main Camera") != null) return;

// ВЕРНО — используем существующий объект, добавляем компонент если его нет:
var camGo = FindExisting("Main Camera");
if (camGo == null) camGo = new GameObject("Main Camera");   // явный ==, не ?? (правило CLAUDE.md)
if (camGo.GetComponent<CameraController>() == null)
    camGo.AddComponent<CameraController>();
```
Поиск существующих объектов — только через `FindObjectsByType<T>(FindObjectsInactive.Include, ...)`:
`GameObject.Find` не видит **выключенные** объекты, поэтому выключенный `[Systems]` или `Barrel_03`
приводит к созданию дубля (а для scope — к двум корневым контейнерам и двойной регистрации).

### Смена «чем объект должен быть» требует миграции, а не только дописывания
Идемпотентность «дособрать недостающее» защищает от дублей, но НЕ переносит изменения типа
объекта на уже собранные сцены. Реальный случай: земля тестовой сцены переехала с `Plane` на
`Cube` (тонкий коллайдер пропускал брошенные объекты сквозь пол); скрипт обновлял только
`Transform` существующего объекта, а `MeshFilter.sharedMesh` и тип коллайдера задавал лишь
новосозданному — в собранной сцене земля навсегда осталась плоскостью с `MeshCollider`,
сколько бы раз пункт меню ни прогоняли. Хуже того, новый `position.y` в сочетании со старым
мешем сдвинул поверхность на полметра от `y = 0`, куда смотрит raycast-фоллбэк.

Правило: если правка меняет меш, тип коллайдера, шейдер материала или состав компонентов —
пиши явную ветку миграции для существующего объекта (заменить меш при несовпадении ссылки,
удалить устаревший компонент через `Undo.DestroyObjectImmediate`, добавить новый), не пересоздавая
GameObject (иначе теряются слой и ручные правки). Замена по несовпадению ссылки, а не безусловная,
делает повторный прогон настоящим no-op — это видно по отсутствию миграционных логов на втором
прогоне.

### Проверять результат численно, а не «скрипт отработал без ошибок»
Оба дефекта, дошедшие до проверяющего на этом проекте, прошли самопроверку вида «прогнал пункт
меню, ошибок в консоли нет»: непрошитые `SerializeField` (сцена собралась, но в Play Mode ничего
не работало) и неотмигрированная земля. Прогон без ошибок не доказывает результат. После
editor-скрипта проверять фактические значения: `LayerMask` — численно (`m_Bits`: `1 << 8` = 256),
меш — по `MeshRenderer.localBounds.size` (у `Cube` это `(1,1,1)`, у `Plane` — `(10, ~0, 10)`),
ссылки — что поле не пустое, списки (`autoInjectGameObjects`) — что элементов ровно столько,
сколько ожидается, и после второго прогона их не стало вдвое больше. Настройки ассетов, которые
мост не читает (переходы `AnimatorController`), проверяются чтением YAML самого ассета.

### OpenScene выгружает ассеты — ссылки перечитывать ПОСЛЕ открытия сцены
`EditorSceneManager.OpenScene(..., OpenSceneMode.Single)` выгружает неиспользуемые ассеты,
а managed-ссылка C# НЕ удерживает нативный Unity-объект: конфиг/префаб, загруженный или
созданный ДО обхода сцен, к моменту прошивки становится fake-null — и правило «не
присваивать null» молча пропускает поле во ВСЕХ сценах (виден только Warning).
Правило: в скриптах вида «Wire X (All Scenes)» ассеты загружаются `AssetDatabase.
LoadAssetAtPath` **внутри цикла, после каждого OpenScene**, не до цикла.
Сопутствующее: `Undo.RegisterCreatedObjectUndo` применим только к сценным объектам —
на ассетах некорректен (создание ассета в undo не регистрировать).

### AudioListener обязателен в каждой сцене
Без единственного AudioListener Unity не воспроизводит НИЧЕГО («There are no audio
listeners in the scene»). Мета-сцены с UI-камерами без слушателя — немые. Editor-скрипт,
собирающий сцену со звуком, обязан гарантировать слушателя (на камеру сцены; без
камеры — на служебный объект; существующие не дублировать) — образец
`EnsureAudioListener` в `SetupAudioEditor.cs`.


### Модальные диалоги ломают автоматизацию — их нельзя звать из пунктов меню
`EditorUtility.DisplayDialog` блокирует главный поток Unity до клика мышью. Пока он открыт,
MCP-мост не отвечает **ни на что**, включая read-only-запросы: со стороны это выглядит как
намертво зависший редактор, и разблокировать может только человек. Реальный случай: пункт
`Build Test Village Scene` спросил про несохранённую сцену — прогон конвейера потерян,
диагностика заняла больше времени, чем сама правка.

Правило: пункт меню, который может быть вызван автоматизацией (а у нас это все `BlackWhiteLike/...`),
модальных диалогов не открывает. Вместо «спросить»:
- сохранить самому (`EditorSceneManager.SaveScene`) и продолжить, залогировав действие;
- либо отказаться работать с `Debug.LogError` и внятной инструкцией, что сделать руками.

Осторожно с неявными модалками: сохранение никогда не сохранявшейся сцены само откроет
системный «Save As», то есть проблему не решит — такой случай надо отсекать ошибкой.

### ThirdParty-префаб — только через unpack completely, никогда инстансом
Голый `PrefabUtility.InstantiatePrefab(<ассет из Assets/ThirdParty>)` в сцене или внутри
собираемого нашего префаба запрещён (CLAUDE.md, «Из ThirdParty в сцену... только визуал»).
Реальный случай (шаг 4): четыре нарушения сразу — дома/поле тестовой сцены были инстансами
префабов Synty с нашим `NeedSiteController` сверху, префабы жителя и руки держали вложенные
инстансы `SM_Chr_Peasant_Male_01` и `MaleHand.FBX`, VFX-обёртка — вложенный CFXR-префаб с
`clearBehavior = Destroy` (уничтожал бы пуловый объект). Ни одно не поймано самопроверкой,
потому что «сцена собралась и работает».

Правило: единственная точка входа — `EditorPrefabUtility.InstantiateUnpacked(prefab, parent)`
(`Editor/Common/`): `InstantiatePrefab` → `UnpackPrefabInstance(..., PrefabUnpackMode.Completely)`.
Структуры сцены (дома, поля) — сначала свой префаб в `Development/Prefabs/Village/` (отдельный
пункт `Prefabs/Setup ...`), сцена инстанцирует уже его. Скрипт, который создаёт объекты, в конце
вызывает `EditorPrefabUtility.AssertNoThirdPartyPrefabLinks(root)` и логирует результат; пункт
`BlackWhiteLike/Validation/Find ThirdParty Prefab Instances` — общая проверка открытой сцены и
всех `Development/Prefabs`. Проверка численно — по `PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot`
/ `GetCorrespondingObjectFromSource`, путь начинается с `Assets/ThirdParty/` = нарушение.

### Всё, на что ссылается сохраняемый ассет, само обязано быть ассетом
`new Material(shader)`, `new Texture2D(...)`, `TMP_FontAsset.CreateFontAsset(font)` (атлас +
материал) живут в памяти: пока объект в сцене текущей сессии — работает, после сохранения в
префаб/`.asset` и перезагрузки ссылка становится missing (розовый меш, `MissingReferenceException`
у TMP). Два случая за один playtest шага 4: атлас шрифта HUD и материалы маркеров NeedSite.
Правило: либо `AssetDatabase.CreateAsset` в свою папку (`Art/Materials/...`, один ассет на
роль, переиспользуется), либо `AssetDatabase.AddObjectToAsset(obj, владелец)` саб-ассетом —
и численная проверка после сохранения (`AssetDatabase.IsSubAsset`, `sharedMaterial != null`,
`m_Materials` без `{fileID: 0}` в YAML). Скрипт с миграцией обязан чинить и уже собранные
объекты с missing-ссылкой.

### Программное создание тегов
Теги в Unity должны существовать в TagManager до их назначения объекту.
Если префаб создаётся Editor-скриптом с кастомным тегом — добавить тег программно:
```csharp
var tagManager = new SerializedObject(
    AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
var tagsProp = tagManager.FindProperty("tags");
// проверить наличие и добавить при необходимости
tagManager.ApplyModifiedProperties();
```

### Автоматическое заполнение ссылок на Composition Root
LifetimeScope (Composition Root) содержит SerializeField-ссылки на конфиги и сцено-объекты.
Заполнять их вручную через Inspector ненадёжно и медленно. Автоматизировать через
`SerializedObject` + `FindProperty` + `FindAnyObjectByType` / `AssetDatabase.LoadAssetAtPath`:
```csharp
var so = new SerializedObject(scope);
so.FindProperty("_playerController").objectReferenceValue =
    Object.FindAnyObjectByType<PlayerController>();
so.FindProperty("_bulletConfig").objectReferenceValue =
    AssetDatabase.LoadAssetAtPath<BulletConfig>("Assets/Development/Data/Configs/BulletConfig.asset");
so.ApplyModifiedProperties();
```
Этот шаг должен быть последним в Wizard'е — после создания всех объектов и ассетов.

Два обязательных правила прошивки:
- **Вариантные конфиги выбирать по контексту, а не хардкодом.** Если у конфига есть
  варианты под сцену/режим/платформу (`Config_Default`, `Config_<Variant>`), скрипт
  обязан определить актуальный вариант (обычно по `SceneManager.GetActiveScene().name`
  против констант имён сцен проекта) и прошить именно его. Жёсткая прошивка
  `_Default`-ассета молча затирает вариантные настройки при прогоне меню в другой
  сцене — регрессия проявляется не сразу и далеко от места правки.
- **Не присваивать null**: если ассет/объект не найден — `Debug.LogWarning` и оставить
  существующее значение поля, иначе повторный прогон ломает рабочую проводку сцены.

### Пример структуры Editor-скрипта
```csharp
using UnityEditor;
using UnityEngine;

namespace BlackWhiteLike.Editor.Setup
{
    public static class SceneSetupEditor
    {
        [MenuItem("BlackWhiteLike/Setup/Build Complete Scene")]
        public static void BuildScene()
        {
            if (FindExisting("Player"))
            {
                if (!EditorUtility.DisplayDialog("Scene Setup",
                    "Scene already contains objects. Rebuild?", "Yes", "Cancel"))
                    return;
            }

            Undo.SetCurrentGroupName("Build Complete Scene");

            CreateArena();
            PlaceTower();
            CreatePlayer();
            CreateEnemy();
            SetupCamera();
            CreateGameLifetimeScope();

            Debug.Log("[Editor] Scene setup complete.");
        }

        private static GameObject FindExisting(string name) =>
            GameObject.Find(name);
    }
}
```

### Derived-значения между ассетами штампуются сборкой
Значение, зависящее от другого ассета (длина клипа, скорость animator-состояния, порог
блендтри), не запоминается человеком и не документируется тултипом «держать в синхроне» —
его штампует editor-скрипт при сборке (образец: `StampStandUpDuration` в
`VillagerPrefabSetupEditor` — `StandUpDuration = длина клипа ÷ StandUpPlaybackSpeed`).
Штамп идемпотентен: совпадающее значение не перезаписывается и не логируется, изменение
логируется «старое → новое». Полные правила сборки AnimatorController (слои, блендтри, AvatarMask, грабли) — глобальный skill `unity-animator`, триггерится на «аниматор»/«blend tree»/«AvatarMask»; при сборке контроллера editor-скриптом загрузить его обязательно.
