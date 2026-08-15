# CLI Workflow — работа с Claude Code CLI по проекту GodHand

Практический регламент работы с Claude Code CLI на этом конкретном проекте. Не методичка по CLI вообще, а **что делать пошагово**, чтобы не утонуть в переделках.

Источники правды для CLI:
- `CLAUDE.md` — архитектурные правила, нюансы Unity/VContainer, workflow оркестратора, ссылки на скилы.
- `docs/game-design.md` — ТЗ вертикального среза GodHand.
- Этот файл — регламент промптинга.

---

## Базовые принципы

### 1. Один шаг — один промпт — одна проверка в Unity

Не «реализуй весь срез», не «сделай несколько шагов сразу». Один шаг из Implementation Order (§19 `game-design.md`) = одна сессия CLI = один Unity-checkpoint. После каждого шага запускаешь Unity, проверяешь acceptance-критерий, коммитишь.

### 2. План всегда перед кодом

В любом промпте, требующем нового кода, **обязательно**: «Сначала покажи план файлов, жди моего "ок" перед началом написания». Прочитать план — 5 минут, откат плохого кода — час.

### 3. Источник правды — документы, не «общие знания»

Всегда явная ссылка на конкретный раздел `game-design.md` или `CLAUDE.md`. «Сделай как в Black & White 2» или «используй best practices» — под запретом. Только: «по §X документа Y».

### 4. Запрет на расширение скоупа

В каждом промпте: «не выдумывай новые сервисы или конфиги сверх плана, если хочется добавить — спрашивай». Особая зона риска на этом проекте: CLI будет тянуть в срез симуляцию деревни (нужды «сами по себе», расписания жителей, экономику деревни) и логику существа — всё это **вне среза** (§14.3 и §15 game-design).

### 5. После каждых 3–4 шагов — архитектурный аудит

Долг в AI-сгенерированном коде накапливается тихо. Ловить еженедельно, не разгребать в конце.

---

## Сессия 0 — Первичная инициализация CLI

Запускается **один раз** при старте проекта, либо при возврате после долгого перерыва.

### Промпт сессии 0

```
Прочитай CLAUDE.md и docs/game-design.md в корне проекта.
Это твои единственные источники правды по архитектуре и геймдизайну.
Если что-то в твоих действиях будет противоречить им — приоритет
у этих документов.

Также просмотри структуру Assets/ командой ls/tree (особенно
Assets/Development/ и Assets/ThirdParty/), чтобы понять что уже есть.
Не читай содержимое файлов в ThirdParty — это покупные ассеты,
не наш код.

После этого ответь ТОЛЬКО на следующие вопросы, ничего не делая:
1. Какие шаги из Implementation Order (§19 game-design.md) уже
   выполнены судя по структуре проекта? Какие пусты?
2. Какие сторонние ассеты ты видишь в Assets/ThirdParty/ и как они
   мапятся на потребности проекта (деревня/жители/VFX/UI/audio)?
3. Установлен ли VContainer? Если да — какая версия. Если нет — это
   блокер, ставить через Package Manager до начала работы.
4. Перечисли предохранители из §15 game-design.md и объясни своими
   словами, зачем каждый нужен.
5. Какие правила из CLAUDE.md и .claude/rules/ ты считаешь наиболее
   критичными для первой задачи (Implementation Order шаг 1: каркас)?

НЕ начинай писать код, пока я не дам команду. Сначала ответы
на вопросы.
```

### Что проверять в ответе

- Claude понимает, что срез — **level-based**, деревня — скриптованный таймлайн из `LevelConfig`, а не симуляция.
- Понимает три ключевые системы: рука (Grab/Miracle режимы), Worship (Awe+Fear в одну шкалу), Alignment (отдельный счёт, только визуал + аналитика стиля).
- Знает предохранители §15: пустые `Services/Progression/` и `Services/Creature/`, `IPlayerProfile`, `RewardEarnedEvent.xpAmount`, push-pop FSM, `IProfileStorage`, `ITimeProvider`, `creatureSpawnPoint`.
- Упоминает критичные нюансы CLAUDE.md: `RegisterComponent` vs `RegisterInstance`, kinematic Rigidbody на триггерах, инъекция объектов из пула, запрет `??`/`?.` с Unity-объектами.
- Не предлагает «общую структуру проекта» от себя — опирается на `Assets/Development/Scripts/` из CLAUDE.md.

Если ответ слабый — уточняющие вопросы до адекватного понимания. Только потом сессия 1.

---

## Сессия 1 — Implementation Order шаг 1 (Каркас)

### Промпт

```
Реализуй шаг 1 из Implementation Order (§19 game-design.md):
«Каркас».

Конкретно: GameLifetimeScope, GameBootstrap, IInputProvider +
PointerInputProvider (единый для touch и mouse; pinch/scroll
инкапсулированы внутри), IEventBus, generic FSM с push-pop
(Paused и WelcomeBack будут push-состояниями), базовые enum/const
(GameState, MiracleType, NeedType, HandMode, VillagerState,
RewardSource, UpgradeCategory, LevelId, Tags, AnimatorHashes),
IProfileStorage + PlayerPrefsProfileStorage, ITimeProvider +
SystemTimeProvider, и пустые папки Services/Progression/ и
Services/Creature/ (см. §15).

Регламент работы:
- Сначала покажи план: список файлов, порядок создания, интерфейсы
  каждого сервиса. Жди моего «ок» перед написанием кода.
- Используй скилы из CLAUDE.md: основная работа через code-writer,
  по завершении автоматически прогоняй code-reviewer.
- Если нужен Editor-скрипт — помечай EDITOR_SCRIPT_NEEDED: и
  параллельно делегируй editor-script-creator.
- Не лезь в задачи последующих шагов (никакой руки, чудес, жителей,
  Worship) — только каркас.
- Не выдумывай новые сервисы или конфиги сверх перечисленных.
  Хочется добавить — сначала спрашивай меня.

Обязательно применяй предохранители §15 game-design.md:
- Services/Progression/ и Services/Creature/ созданы пустыми.
- MetaProgressService будет работать через IPlayerProfile
  абстракцию (сейчас — только интерфейс-заглушка).
- GameFlowService поддерживает push-pop с самого начала,
  не «pause через флаг».
- Save/load — через IProfileStorage (PlayerPrefsProfileStorage).
- Всё время — через ITimeProvider, нигде не DateTime.UtcNow напрямую.

После завершения шага 1 жди от меня результат проверки в Unity,
прежде чем переходить дальше.
```

### Что проверять в Unity после шага 1

- Пустая сцена с `GameBootstrap` стартует без ошибок в Console.
- Лог показывает: `GameLifetimeScope` инициализирован, `IEventBus` зарегистрирован.
- `GameFlowService` стартует в `MainMenu`; push/pop тестового состояния работает (временный лог-тест).
- Структура папок соответствует CLAUDE.md; `Services/Progression/` и `Services/Creature/` существуют и пусты.

---

## Сессии 2–10 — Шаблон промпта для остальных шагов

### Универсальный шаблон

```
Шаг <N-1> проверен и работает. Переходим к шагу <N> из
Implementation Order (§19 game-design.md): «<название шага>».

Что должно появиться:
<список классов/конфигов из §19 и Architecture Mapping §12>

Цель шага по game-design: <цель из §19>.

Регламент тот же:
1. Сначала план файлов, жди ок.
2. code-writer для нового кода, code-reviewer параллельно.
3. EDITOR_SCRIPT_NEEDED: помечай и делегируй editor-script-creator.
4. Не лезь в последующие шаги.
5. После завершения — жди моей проверки.

Дополнительные ограничения для этого шага:
- <специфика шага, см. ниже>
```

### Конкретные дополнительные ограничения по шагам

**Шаг 2 (Камера + рука):**
- `GodHandController` — только Unity API (raycast, физика захвата); правила и расчёт импульса броска — в `HandInteractionService` (pure C#).
- Захват: реализовать velocity-follow (Rigidbody следует за точкой под pointer'ом); joint-вариант не делать без моей команды (Open Question #1).
- Импульс броска — из истории позиций pointer'а за `ThrowVelocitySampleWindow` (из `HandConfig`), кламп `MaxThrowForce`.
- Pan только если raycast не попал в `IGrabbable`; конфликт «начал pan — под пальцем оказался объект» решать по первому касанию.
- Все параметры — в `HandConfig` и `IsometricCameraConfig`, никаких чисел в контроллерах.
- Тестовая сцена `_TestHand` (подчёркивание = временная) с plane и кубами.

**Шаг 3 (Жители + события):**
- Жители — НЕ симуляция: FSM из §5 (Idle/Panic/Worship/Tossed/Stunned), блуждание по случайным точкам в радиусе. Никаких потребностей «сами по себе», расписаний, работы.
- `LevelEventService` — pure C#: читает `eventTimeline` из `LevelConfig`, триггерит по времени. Никакой генерации случайных событий — только скриптованный таймлайн.
- `NeedSiteController` — триггер-зоны с **kinematic Rigidbody** (см. CLAUDE.md).
- Жители — pooled через `VillagerFactory`: `pool.Get()` → `InjectGameObject` → явный `Launch(...)`.
- Tossed: Rigidbody + случайный angular velocity из `VillagerConfig`; полный ragdoll НЕ делать (Scalability Hook).

**Шаг 4 (Чудеса + Faith):**
- Только Rain и Fireball; Heal и Lightning — шаг 6.
- Каждый каст — `CastMiracleCommand : ICommand` с `CanExecute()` (хватает Faith, цель валидна).
- Эффекты чудес применяются через события (`MiracleCastEvent` → подписчики `NeedSiteController`/`LevelEventService`), не прямыми ссылками чуда на объекты сцены.
- Накачка hold: расход `HoldCostPerSecond`, радиус растёт до `MaxRadius` — всё из `MiracleConfig`.
- VFX — только через `VfxFactory` (пул), enum `VfxEffectType`.

**Шаг 5 (Worship + win):**
- `WorshipService` принимает и Awe, и Fear в одну шкалу; раздельный учёт aweTotal/fearTotal — для `LevelCompletedEvent` и экрана итогов.
- `AlignmentService` — отдельный сервис, НЕ внутри WorshipService.
- Soft-fail: `LevelStalledEvent` при (Faith == 0 && нет активных нужд && Worship < порога); rewarded-кнопка — заглушка с пометкой // ADS_STUB, никаких SDK.
- Worship-попапы (`+N Awe`) — pooled через `WorshipPopupFactory`.

**Шаг 6 (Оставшиеся чудеса + alignment-визуал):**
- Heal и Lightning + нужды Disease и Hunger по таблицам §5 и §7.
- `AlignmentVisualController` применяет стейдж из `AlignmentVisualConfig`; пустые поля игнорируются — никаких if/else по «активной стратегии» (тот же контракт, что описан в §11.1).
- В SO-инстансах заполнять материалы для Neutral + по одному стейджу в каждую сторону (Kind, Cruel); Saint/Demon — пустые заготовки.
- EDITOR_SCRIPT_NEEDED: GodHand/Setup/Setup Alignment Visual.

**Шаг 7 (Уровни 2–5):**
- Сборка уровней ТОЛЬКО через editor-скрипт `GodHand/Levels/Build Village Level` — никакой ручной расстановки в сцене.
- 4 новых `LevelConfig` + `VillageLayoutConfig`: прогрессия чистыми данными (§6), код не трогается.
- EDITOR_SCRIPT_NEEDED: GodHand/Validation/Validate Level Configs.
- Если для прогрессии «хочется» новый тип события или механику — стоп, это расширение скоупа, спрашивай.

**Шаг 8 (Meta + idle):**
- `MetaProgressService` — за `IPlayerProfile`; save/load только через `IProfileStorage`.
- `EconomyService` публикует `RewardEarnedEvent` с `xpAmount = 0` — предохранитель §15.
- `IdleIncomeService`: время только через `ITimeProvider`; формула min(elapsed, cap) * rate; `MinOfflineMinutes` из SO.
- `EffectiveStatsService` собирает effective-параметры на старте уровня; базовые SO не мутируются в рантайме.
- `CreatureTeaserView` — disabled-карточка, ноль логики.

**Шаг 9 (Mobile polish):**
- Safe area через отдельный компонент на корневых Canvas.
- Хит-зоны панели чудес ≥ 88px; проверить перекрытия с системными жестами.
- Хаптика — интерфейс `IHapticsService` + заглушка (реализация — после среза).
- Профилирование на устройстве: пулы VFX/попапов, отсутствие аллокаций в Update-петлях руки.

**Шаг 10 (Polish):**
- Не «делаем фичи», а закрываем acceptance criteria §16.
- Онбординг уровня 1 — подсказки поверх HUD (последовательность из SO, не хардкод).
- Баланс — только правки SO (если архитектура сделана правильно, аудиты это контролировали).

---

## Архитектурный аудит — каждые 3–4 шага

После шагов 3, 6 и 9 (или когда больше двух «странных» решений CLI подряд).

### Промпт аудита

```
Прогон skill audit-architecture по всему текущему коду проекта
(Assets/Development/Scripts/).

Особое внимание:
- Дубликаты строковых ключей (теги, ключи пулов, animator-параметры).
  Должны быть в enum/const.
- MonoBehaviour с бизнес-логикой. GodHandController и
  MiracleTargetingController — зона риска: расчёты импульса,
  стоимости, валидации целей должны быть в сервисах.
- Прямые обращения к IPoolManager вне фабрик.
- RegisterInstance там, где должен быть RegisterComponent.
- Resources.Load — запрещён.
- Magic numbers в Controllers — должны быть в SO.
- Объекты из пула без InjectGameObject + явного Launch().
- Триггер-зоны без kinematic Rigidbody.
- DateTime.UtcNow напрямую вместо ITimeProvider.
- Прямые ссылки эффектов чудес на объекты сцены вместо событий.
- Логика существа или прогрессии, просочившаяся в срез
  (папки Services/Creature/ и Services/Progression/ должны быть пусты).

Покажи отчёт с конкретными файлами и строками. Не исправляй
автоматически — я выберу, что фиксить.
```

### Что делать с отчётом

- Тривиальные нарушения — точечные промпты «исправь нарушение X в файле Y».
- Системные нарушения (например, бизнес-логика во всех Controllers) — откат к последнему коммиту и **пересмотр своего промпта**: что в нём дало Claude свободу так делать.

---

## Обработка ошибок Unity Console

Не пиши «исправь». Пиши так:

```
Получил ошибку в Unity Console:
<paste полный stacktrace>

Используй unity-error-analyzer скил для разбора. Прежде чем писать
фикс — расскажи мне:
1. Причину одной фразой.
2. Где именно нарушено (файл:строка).
3. План фикса.

Не правь код до моего «ок».
```

Частые причины ошибок на этом проекте (Claude должен искать сначала здесь):

- `NullReferenceException` в `OnEnable`/`Start`: `Construct()` ещё не вызван — флаг `_initialized` или перенос в `Construct()`. См. CLAUDE.md.
- `OnTriggerEnter` молчит (NeedSite, зоны попадания чудес): ни у кого нет Rigidbody → kinematic Rigidbody на триггер-зону.
- VContainer не инжектит: `RegisterInstance` вместо `RegisterComponent`.
- Житель из пула «не инициализируется»: забыт `InjectGameObject` + `Launch(...)`.
- Захват рукой «дёргается»/пробивает объекты: velocity-follow в `Update` вместо `FixedUpdate`, или input читается в `FixedUpdate` (см. CLAUDE.md про буферизацию input).
- Idle-доход «скачет» в Editor: `ITimeProvider` подменён/не подменён в тестовой конфигурации.

---

## Между сессиями — синхронизация контекста

Каждая новая сессия CLI начинается с синхронизирующего префикса.

### Префикс новой сессии

```
Я закончил шаги <X-Y> Implementation Order. Сейчас работаем
над шагом <Z>.

Прочитай docs/game-design.md §19 и подтверди, что шаги <X-Y>
действительно реализованы в коде (быстрый осмотр
Assets/Development/Scripts/, не полный аудит). Если видишь
расхождение с моим утверждением — скажи сразу.

Затем переходим к промпту шага <Z>.
```

---

## Что НЕ делать

- **Не писать** «реализуй весь вертикальный срез» одним промптом.
- **Не писать** «сделай как в Black & White 2» — Claude в неё не играл, а референс уже декомпозирован в game-design.md.
- **Не позволять** CLI самостоятельно переходить к следующему шагу.
- **Не пропускать** проверку плана файлов перед кодом.
- **Не пропускать** проверку в Unity между шагами.
- **Не пропускать** архитектурные аудиты.
- **Не игнорировать** EDITOR_SCRIPT_NEEDED пометки.
- **Не разрешать** CLI «оживлять» деревню сверх скриптованного таймлайна и не подпускать его к Services/Creature/ — самые вероятные направления расползания скоупа на этом проекте.

---

## Чек-лист перед запуском CLI впервые

- [ ] Проект Unity 6 (6000.3.x LTS) открыт, URP включён, Active Input Handling = Both.
- [ ] VContainer установлен через Package Manager.
- [ ] Cinemachine 3.1 установлен.
- [ ] TextMeshPro Essentials импортирован.
- [ ] `Assets/ThirdParty/` содержит выбранные арт-паки (деревня/жители/VFX/UI) — или зафиксировано решение стартовать на примитивах до шага 3 (Open Question #8).
- [ ] `CLAUDE.md` и `docs/game-design.md` присутствуют в корне.
- [ ] `.claude/rules/editor-files.md` присутствует.
- [ ] Скилы `code-writer`, `code-reviewer`, `editor-script-creator`, `audit-architecture`, `unity-error-analyzer`, `add-vfx-effect`, `add-editor-script`, `update-claude-md` зарегистрированы в Claude Code.
- [ ] Папка `docs/` существует, в ней `game-design.md` и этот `cli-workflow.md`.
- [ ] Git-репозиторий создан — коммит после каждого шага.

После прохождения чек-листа — открывай CLI и начинай с **Сессии 0**.
