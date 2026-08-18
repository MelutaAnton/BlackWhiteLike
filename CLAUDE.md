# BlackWhiteLike — правила проекта

Геймдизайн и сущности — в `docs/game-design.md`. 

## Технологии

- Unity 6 (6000.3.9f1 LTS), URP
- Cinemachine 3.1 — все игровые камеры (vcam-адаптеры реализуют `ICameraBehaviour`, переключение через Priority; при сомнении в API смотреть исходник в `Library/PackageCache/com.unity.cinemachine@*/Runtime/`, не гадать по CM2)
- VContainer (DI-контейнер)
- C# с SOLID и Clean Architecture
- Active Input Handling: **Input System Package (New)** (Project Settings → Player). `Both` не годится: Unity прямо предупреждает, что на Android он не поддерживается и бьёт по вводу и производительности. Весь ввод проекта идёт через `PointerInputProvider` на `UnityEngine.InputSystem` — legacy-класс `Input` под этой настройкой бросает исключения, использовать его нельзя
- DOTween для оптимизации анимаций и сложных математических вычислений, и приведению GUI к стандартам гибрид-кэжа
- Git **без LFS** (осознанное решение): бинарники лежат обычными блобами. Ограничения, которые нельзя нарушать — файл >100 МБ GitHub не примет, мягкий лимит репозитория 5 ГБ (на 2026-08-16 занято ~1.7 ГБ). Не коммитить билды, `.unitypackage`, исходники текстур >100 МБ

## MCP Unity (мост к редактору)

В проекте подключён MCP-сервер `mcp-unity` (пакет `com.gamelovers.mcp-unity`, конфиг `.mcp.json`, настройки `ProjectSettings/McpUnitySettings.json`; установка/грабли — глобальное правило `~/.claude/rules/mcp-unity.md`). Когда Unity открыт, Claude обязан пользоваться мостом вместо ручных просьб:

- **Прогон editor-меню** — `execute_menu_item` с нашими `BlackWhiteLike/...`-пунктами (не просить пользователя кликать, если шаг не требует его решений).
- **Диагностика «не работает»** — сначала `get_console_logs` (стектрейсы без копипаста) и инспекция иерархии/проводки сцены, потом гипотезы.
- **Проверка после editor-скриптов** — иерархия/компоненты/сериализованные поля читаются мостом.
- **Тесты** — `run_tests`.

За пользователем остаются: плейтест (ощущения, баланс, визуал), Unity-checkpoint «работает/не работает», решения по дизайну.

## Архитектурные паттерны

- **DI**: VContainer, Composition Root в `GameLifetimeScope.cs`
- **EventBus**: типизированная шина `Core/Events/EventBus.cs` (struct-события)
- **Command**: `ICommand` с `CanExecute()` / `Execute()`
- **State Machine**: generic FSM с push-pop для `GameState` (см. game-design §9); отдельная FSM жителя (§5)
- **Object Pool**: `IPoolManager` для жителей, VFX, UI-попапов
- **MVP** для UI: Model ← Presenter → View
- **Factory**: фабрики инкапсулируют пулы; внешний код не работает с `IPoolManager` напрямую
- **ScriptableObject Data-Driven**: вся конфигурация в SO

## Структура папок

```
Assets/
├── ThirdParty/                     # Купленные/импортированные сторонние ассеты
│   ├── 3D Environment/             # POLYGON Fantasy Kingdom, Farm Pack
│   ├── Animations/                 # Synty locomotion/idles
│   ├── GUI/                        # Layer Lab, ClassicFantasyRPG_UI
│   ├── SFX/                        # Звуковые библиотеки
│   ├── VFX/                        # JMO Cartoon FX, Hovl Studio
│   └── ...                         # Один паблишер/пак = одна папка
│
├── Content/                        # Сырьё «как скачано»: не наше и не пак Asset Store
│   ├── Animations/                 # Mixamo-fbx (Cheering, Panic, Pray, Stunned)
│   ├── Fonts/                      # .ttf с Google Fonts (Cinzel, Nunito)
│   └── Music/                      # Треки (Menu, Village)
│
└── Development/                    # Весь наш собственный контент и код
    ├── Scripts/
    │   ├── Core/                   # Переиспользуемый фреймворк
    │   │   ├── Commands/           # ICommand, игровые команды
    │   │   ├── DI/                 # ServiceLocator (fallback)
    │   │   ├── Events/             # EventBus + game events (struct'ы)
    │   │   ├── Input/              # IInputProvider, InputRouter, Keyboard/Mobile providers
    │   │   ├── Pool/               # GameObjectPool, PoolManager
    │   │   └── StateMachine/       # Generic StateMachine<TEnum>
    │   ├── Data/Configs/           # Определения ScriptableObject (один файл = один класс)
    │   ├── Models/                 # Чистые данные, enums (GameState, ResourceType, VfxEffectType)
    │   ├── Services/               # Бизнес-логика (pure C#, без MonoBehaviour)
    │   │   └── Progression/        # Mid-core системы (миссии, XP, energy, см. game-design §15)
    │   ├── Controllers/            # MonoBehaviour: Unity API only (input, physics, render)
    │   ├── Views/UI/               # MVP-View'ы (только UI; никаких VFX-Controller'ов здесь)
    │   ├── Factories/              # Фабрики; единственное место доступа к IPoolManager
    │   ├── Installers/             # GameLifetimeScope (VContainer Composition Root)
    │   ├── Editor/                 # Editor-скрипты (см. skill `add-editor-script`)
    │   └── GameBootstrap.cs        # Точка входа
    │
    ├── Prefabs/                    # Наши prefab'ы с логикой (см. правила ниже)
    │   ├── Village/                # Дома, поля, храм, бросаемые/разрушаемые объекты, житель
    │   ├── Hand/                   # GodHand.prefab (визуал руки + alignment-материалы)
    │   ├── VFX/                    # Наши настроенные VFX-префабы (поверх ThirdParty-эмиттеров)
    │   ├── UI/                     # Canvas'ы и UI-префабы
    │   └── Systems/                # GameLifetimeScope, EventBus root и др. service-префабы сцены
    │
    ├── Data/Configs/               # Инстансы ScriptableObject (.asset)
    │   ├── Hand/
    │   ├── Miracles/
    │   ├── Levels/
    │   └── ...                     # Полный список — game-design §12
    │
    ├── Scenes/                     # Игровые сцены
    │   ├── Bootstrap.unity
    │   ├── MainMenu.unity
    │   ├── Village_Base.unity      # Одна сцена уровня, наполнение из LevelConfig
    │   ├── Temple.unity
    │   └── _TestHand.unity         # Временные сцены, имя с подчёркиванием
    │
    └── Art/                        # Наши ДОРАБОТАННЫЕ арт-ассеты (не сырьё — оно в Content/)
        ├── Materials/              # Материалы (alignment-стейджи руки/храма)
        ├── Textures/               # Наши текстуры, decals, иконки
        ├── Sprites/                # UI-спрайты, фоны, превью уровней
        ├── Fonts/                  # TMP Font Assets (.asset), сгенерённые из Content/Fonts
        └── ...
```

**Content/ vs Development/Art/**: в `Content/` лежит сырьё «как скачано» (ttf, mp3, Mixamo-fbx) — оно не редактируется, как и ThirdParty. Всё производное от него (TMP Font Assets, AnimatorController, обрезанные/сконвертированные аудио) создаётся в `Development/`. Купленные паки Asset Store — только в `ThirdParty/`, они в `Content/` не попадают.

## Правила хранения контента

### Купленные ассеты — только в Assets/ThirdParty/

Любой импортированный пак с Asset Store / itch.io / Polyhaven кладётся в **отдельную папку под `Assets/ThirdParty/<PackName>/`**. Содержимое этой папки **не редактируется**: ни материалы, ни префабы, ни скрипты внутри пакета не меняются.

Зачем:
- Обновление пакета через Package Manager не сносит твою работу.
- Чёткая граница «что чужое, что наше» для лицензий, аудитов, git diff.
- Claude Code CLI получает команду «не читать ThirdParty» — это работает только при изоляции.

Если нужна доработка контента из пака — копируешь конкретный файл (меш, материал, текстуру) в `Assets/Development/Art/` или `Assets/Development/Prefabs/` и правишь копию.

### Префабы с нашей логикой — в Assets/Development/Prefabs/<Category>/

Любой префаб, к которому привязаны наши MonoBehaviour'ы (контроллеры, view'ы, фабрики, конфиги), хранится в `Assets/Development/Prefabs/<Category>/`. Категории — по назначению, не по типу скрипта:
- `Village/` — дома, поля, храм, житель, бросаемые и разрушаемые объекты (наши префабы поверх мешей из ThirdParty).
- `Hand/` — рука бога.
- `VFX/` — наши настроенные эффекты (даже если эмиттер из ThirdParty).
- `UI/` — Canvas'ы.
- `Systems/` — служебные префабы сцены (GameLifetimeScope и т.п.).

Префаб **никогда** не лежит:
- В `Assets/ThirdParty/...` — нарушает изоляцию.
- В `Assets/Development/Scripts/...` — скрипты и префабы разные слои.
- В корне `Assets/Development/` или `Assets/` — нет категории.

### Связь Prefab → ThirdParty через ссылки, не копированием

Префаб машины в `Development/Prefabs/Vehicles/Car_Sedan.prefab` **ссылается** на меш `Assets/ThirdParty/1Poly_DestructibleCars/Meshes/Sedan.fbx` через `MeshFilter.mesh`. Сам меш не дублируется. Это позволяет обновить пак без потери настройки префаба (Unity сохраняет ссылку по GUID).

### Editor-скрипты автоматизируют сборку

Сборка префабов из сырого ThirdParty-контента — рутина, и она автоматизируется. См. editor-скрипт `Setup Car Damage Visual` в `game-design.md` §17: выделил FBX из ThirdParty → editor-скрипт создал prefab в `Development/Prefabs/Vehicles/` с правильной структурой, контроллерами и ссылками на SO-конфиги.

## Правила комментирования
Оставлять комментарии к коду на русском языке

## Правила кодирования

- Один MonoBehaviour / ScriptableObject = один файл, имя файла = имя класса.
- Зависимости — через конструктор (pure C#) или `[Inject] Construct(...)` (MonoBehaviour).
- MonoBehaviour содержит **только** Unity API (ввод, физика, рендер). Бизнес-логика — в сервисах.
- Запуск поведения объекта вызывается явно (`Launch(direction)`), а не из `Start` / `OnEnable`.
- Сервисы — pure C#, тестируемые без Unity.
- События — `readonly struct`, публикуются через `IEventBus`. Единственное исключение из «один класс = один файл»: события группируются по доменам в `Core/Events/Events.<Domain>.cs` (`Events.Flow.cs`, `Events.Hand.cs`, `Events.Miracles.cs`, … — состав в game-design §12). На классы и SO исключение не распространяется.
- Именование: `PascalCase` для публичных, `_camelCase` для приватных полей.
- Каждый сервис имеет интерфейс (`IShootingService`, `IRagdollService`).
- Команды реализуют `ICommand` с `CanExecute()` / `Execute()`.
- Доступ к пулу инкапсулируется в фабрике; никакой внешний код не вызывает `IPoolManager` напрямую.
- Views — только для UI.
- Префабы лежат в `Assets/Development/Prefabs/<Category>/`. Сторонние ассеты — в `Assets/ThirdParty/<PackName>/`, без редактирования.
- По возможности покрывать бизнес-логику Unit-тестами.

## Известные нюансы Unity 6

- `OnEnable()` вызывается **до** VContainer `Construct()` — всегда проверять null перед подпиской и/или использовать флаг `_initialized`.
- **Legacy `UnityEngine.Input` запрещён**: при Input Handling = New любое обращение (`Input.GetKeyDown`, `Input.mousePosition`, `Input.touchCount`) бросает `InvalidOperationException` в рантайме, компилируясь при этом без единой ошибки. Демо-сцены из `ThirdParty` по этой причине неработоспособны — это ожидаемо, мы их не поставляем
- **`GetKeyDown`, потребляемый из `FixedUpdate`, сгорает** (за render-кадр может пройти 0 физтиков) — буферизовать флагом в `Update`, сбрасывать после потребления; детали — `.claude/rules/physics-input.md` (auto-loaded для Controllers/Core/Input).
- Инициализацию переносить в `Construct()`, а не в `Start()`.
- `NavMeshAgent` требует baked NavMesh — автоматизировать через Editor-скрипт.
- Mixamo-модели могут ломать Ragdoll Setup Wizard — см. skill `setup-ragdoll`.
- **`??` / `?.` запрещены с Unity-объектами** (`GetComponent<T>() ?? AddComponent<T>()` — бага): fake-null Unity-объекта обходит перегруженный `==`, в Editor-контексте `GetComponent` при промахе возвращает такой fake-null → `MissingComponentException` при первом обращении. Всегда явный `if (x == null)`.
- **Ragdoll + SkinnedMeshRenderer = исчезающий меш**: SMR не пересчитывает bounds по вершинам — когда ragdoll уводит кости от запечённого AABB, фрустум-каллинг вырезает меш в зависимости от ракурса (тело пропадает, остаются мелкие рендереры на костях). При активации ragdoll ставить `smr.updateWhenOffscreen = true` (см. `DriverController.Eject`).
- **Ragdoll модульного аватара = «иглы» на одежде при дальнем полёте**: служебные кости скелета под контейнером `Root` (не под физкостями) после детача остаются в точке активации ragdoll — вершины с их весами растягиваются от тела до места старта, тем длиннее, чем дальше улетел. Фикс: при активации ragdoll перепарентовать таз под корень персонажа, а контейнер скелета — под таз (`worldPositionStays: true`, см. `DriverController.Eject`).

## Известные нюансы Unity Physics

### Триггеры требуют Rigidbody (КРИТИЧНО)

`OnTriggerEnter` / `OnTriggerExit` **не срабатывают** если ни на одном из участников нет Rigidbody. Типичная ситуация: триггер-зона `BoxCollider(isTrigger=true)` + персонаж с `NavMeshAgent` + `CapsuleCollider` — Rigidbody нет ни у кого, событие молчит.

Решение — кинематический Rigidbody на триггер-зоне:
```csharp
var rb = triggerZone.AddComponent<Rigidbody>();
rb.isKinematic = true;
rb.useGravity  = false;
```

## Известные нюансы VContainer

### `RegisterInstance` vs `RegisterComponent` (КРИТИЧНО)

`RegisterInstance` регистрирует объект как источник зависимости, но **не вызывает** `[Inject] Construct()` на самом объекте. Для MonoBehaviour, которому нужна инъекция, использовать `RegisterComponent`. Симптом: `_initialized` остаётся `false`, ничего не работает.

```csharp
// НЕВЕРНО — Construct() не будет вызван:
builder.RegisterInstance(_playerController);
// ВЕРНО:
builder.RegisterComponent(_playerController);
// Через интерфейс:
builder.RegisterComponent<IHUDView>(_hudView);
```

`RegisterInstance` подходит только для plain C# объектов и ScriptableObject, которым инъекция не нужна (конфиги, данные).

### Объекты из пула не получают инъекцию автоматически

После `pool.Get()` нужно вручную:
```csharp
_container.InjectGameObject(gameObject);  // using VContainer.Unity
```
И затем явно вызвать `Launch(...)` — `OnEnable` уже отработал до инъекции.

## Запрещённые практики

### Дублирование строковых ключей

Любой идентификатор (имя тега, animator-параметра, ключ пула, имя сцены) определяется **один раз** — в enum или в единственном SO. Все остальные места ссылаются на это определение.

- VFX: `VfxEffectType` enum + `VfxConfig` — единственный источник.
- Animator: `static readonly int FooHash = Animator.StringToHash("Foo");`.
- Теги: `static class Tags { public const string Enemy = "Enemy"; }`.
- Сцены: enum или константы в одном файле.

Поиск нарушений автоматизирован — skill `audit-architecture`.

### `Resources.Load` запрещён

Файлы в `Resources/` не исключаются из сборки, ссылки не проверяются компилятором. Использовать `[SerializeField]` (через `GameLifetimeScope` или конфиг) или Addressables.

## Workflow (orchestrator)

Ты оркестратор. Когда пользователь просит написать код:

1. **Планирование** — разобрать требования, выделить задачи.
2. **Реализация** — делегировать `code-writer`.
3. **Параллельная работа** сразу после `code-writer`:
   - запустить `code-reviewer` для ревью качества;
   - если в ответе `code-writer` есть строка `EDITOR_SCRIPT_NEEDED: ...` — параллельно запустить `editor-script-creator`.
4. **Правки** — с учётом замечаний `code-reviewer` делегировать исправления `code-writer`.
5. **Завершение** — финальная проверка, отчёт пользователю.

### Ответственности агентов

- `code-writer` — новый C#: интерфейс + сервис + регистрация.
- `code-reviewer` — ревью git diff на SOLID, Clean Architecture, правила CLAUDE.md.
- `editor-script-creator` — Editor-скрипты для автоматизации Unity Editor.
- `unity-error-analyzer` — разбор stacktrace из Unity Console.

### Чек-лист этапа планирования (пройти ДО делегирования)

Каждый пункт — накопленные грабли проекта; проверять на этапе плана, а не после ошибки компиляции:

1. **Память**: прочитать step-файлы из MEMORY.md по теме шага — уроки там, а не в коде.
2. **Зависимость на код ThirdParty-пака**: проверить наличие asmdef у пака. Нет asmdef → его классы в Assembly-CSharp и из asmdef-сборок проекта НЕДОСТУПНЫ. Варианты: (а) своя реализация; (б) мост в `Assets/Development/BridgeScripts/` (+`BridgeScripts/Editor/`) — папки БЕЗ asmdef компилируются в Assembly-CSharp(-Editor) и видят и классы пака, и публичное API BlackWhiteLike (autoReferenced). Мост — тонкая проводка без бизнес-логики и без DI.
3. **Новые классы в под-неймспейсах**: точные `using` для под-неймспейсов в промптах агентам (C# не импортирует детей через родительский); в `Controllers.*`/`Views.*` голые имена Unity-типов могут схлопнуться с соседним под-неймспейсом (например `Camera` → под-неймспейс `<Root>.Controllers.Camera`, CS0118) — квалифицировать `UnityEngine.*`.
4. **Параллельные агенты** (code-writer ∥ editor-script-creator): контракт имён — классы, SerializeField-поля, пункты меню, пути префабов — зафиксировать в ОБОИХ промптах заранее, дословно.
5. **Новые SerializeField на scope**: поле может быть не прошито в части сцен → SO-конфиг регистрировать с фоллбэк-инстансом (`RegisterInstance(null)` = NRE), MonoBehaviour-view — условный `RegisterComponent` по `!= null`.
6. **Расширение интерфейса или ctor**: перечислить в промпте ВСЕ тестовые фейки и call-sites (иначе CS0535/CS7036).
7. **UI-иерархия**: оверлей (панель результата/смерти/паузы) не должен быть ПОТОМКОМ узла, который гасится кодом (`SetActive(false)` родителя выключает всё поддерево — `Show()` ребёнка молча не работает). Выносить оверлеи сиблингами гасимого контейнера в том же канвасе; новые канвасы не плодить (отдельный канвас — только по слою поверх других фич или для изоляции часто обновляемой геометрии). Pointer-held элементы (стики, кнопки-удержания) сбрасывают состояние в `OnDisable`.
   Любая тапабельная кнопка (включая кастомные pointer-view поверх `IPointerDown/Up`) собирается ВМЕСТЕ с компонентом `Button` — тап должен визуально считываться. Настройка: `Transition = Color Tint` с прошитым `targetGraphic` (без него tint молча не работает), `selectedColor = normalColor` (иначе после тапа кнопка залипает в selected-цвете), `Navigation = None` на тач-HUD. `Button` и кастомный pointer-view сосуществуют на одном GameObject без конфликтов.
   Канвас, повторяющийся в нескольких сценах, существует ОДНИМ префабом в `Prefabs/UI/`, а не пер-сценными копиями: editor-скрипт собирает его один раз и сохраняет через `PrefabUtility.SaveAsPrefabAssetAndConnect`, сцены держат connected-инстансы (образец — MobileInputCanvas). Дубли-копии расходятся при первой же правке, и каждый sweep/стилизация правит N мест вместо одного. Пер-сценное допустимо только в связях: ссылки инстанса на сценные объекты (scope, презентеры) прошиваются на инстансе как overrides.
   UI, собираемый editor-скриптами, выключает дефолтные флаги Graphic там, где они не нужны (Unity включает оба по умолчанию, а нужны они в меньшинстве случаев): `raycastTarget = false` на всём неинтерактивном — тексты, иконки, бары, декоративные Image (каждый включённый флаг удорожает graphic-raycast каждого тапа/клика; `true` — только на элементах, ловящих ввод: кнопки, стики, фоны-блокеры); `maskable = false`, если элемент не должен обрезаться родительской маской (`Mask`/`RectMask2D`) — флаг тянет stencil/материальный оверхед. Относится и к `Image`, и к `TextMeshProUGUI`.
8. **Editor-скрипт правит существующую сцену/канвас** → миграционная идемпотентность: дособрать недостающее в собранное старой версией, без дублей.
9. **Одноразовый ввод, потребляемый физикой** → буферизация Update→FixedUpdate (`.claude/rules/physics-input.md`).
10. **Глобальный static-стейт** (`Time.timeScale` и т.п.): переживает загрузку сцены при per-scene scope — планировать страховки сброса.

## Editor-автоматизация (обязательно)

Любая рутина в Unity Editor автоматизируется через `[MenuItem(...)]` скрипт под `BlackWhiteLike/<Category>/<Action>`. Никогда не писать "сделайте это руками в Inspector".

- Категории и менюшная иерархия — skill `add-editor-script`.
- Низкоуровневые нюансы (Undo, идемпотентность, теги, заполнение SerializeField на Composition Root) — `.claude/rules/editor-files.md` (auto-loaded для `**/Editor/**`).

## Расширение проекта — куда смотреть

| Задача | Куда |
|---|---|
| Новый VFX-эффект | skill `add-vfx-effect` |
| Новый тип ресурса | skill `add-resource-type` |
| Ragdoll-настройка | skill `setup-ragdoll` |
| Новый Editor-скрипт | skill `add-editor-script` + `.claude/rules/editor-files.md` |
| Формальный аудит кода | skill `audit-architecture` |
| Обновление CLAUDE.md / MEMORY.md | skill `update-claude-md` |
| Новый сервис | интерфейс + реализация + регистрация в `GameLifetimeScope` |
| Новое игровое состояние | значение в `GameState` enum + реализация `IState` + регистрация в `GameFlowService` |
| Новая команда | класс `: ICommand`, вызывать из контроллера |
