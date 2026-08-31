# GodHand — Game Design (вертикальный срез)

> Рабочее название проекта — **GodHand** (заменяемо). Архитектурные правила, паттерны и запреты — в `CLAUDE.md`. Этот документ их не дублирует, а ссылается. Все параметры геймплея — только ScriptableObject; никаких magic numbers в Controllers.

---

## 1. Vision & Scope

**Референс:** Black & White 2 (Lionhead, 2005). Берём одно механическое ядро: **рука бога + чудеса + захват деревни через поклонение**, в формате hybrid-casual level-based игры.

**Жанр/аудитория:** hybrid-casual, mobile-first (Android/iOS основные, PC/Editor — для отладки). Сессия 2–5 минут на уровень.

**Что берём в срез:**
- **Рука бога** — палец игрока: drag/бросок жителей и физических объектов, замах-бросок с силой от скорости жеста.
- **Чудеса** — 4 штуки: Rain и Heal (добрые), Fireball и Lightning (злые). Панель выбора → тап по цели, удержание = сила/радиус. Ресурс — Faith (мана), генерируется верующими.
- **Захват деревни** — шкала Worship наполняется любым путём: благие дела (**Awe**) или террор (**Fear**). Worship ≥ порога = победа на уровне.
- **Alignment как core** — единый счёт `AlignmentScore` (−100…+100) сдвигается каждым действием; визуально мутирует руку, храм и UI через material swap.
- **5 уровней** — линейная кампания, каждый уровень = деревня со скриптованными событиями (пожары, болезни, голод) из SO-конфига.
- **Мета** — Coins за уровни, апгрейды (сила руки / ёмкость Faith / уровни чудес), idle-доход офлайн + Welcome Back попап, тизер-карточка существа "Coming soon".

**Что НЕ входит в срез (Scalability Hooks, см. §14):**
- Существо-питомец (в срезе только тизер + архитектурные предохранители, см. §15).
- Свободный city building, армии/RTS, непрерывная симуляция деревни.
- Жестовый каст чудес (рисование паттернов) — альтернативный input в будущем.
- Energy/lives, daily missions, battle pass, скины, PvP-дуэли, cloud save.
- Полный ragdoll жителей (в срезе — rigidbody-tumble, см. §4).

**Целевая длительность:** 2–5 минут на уровень, 7–10+ минут суммарного прохождения среза (5 уровней + мета).

**Платформы:** Android/iOS (touch) + PC/Editor (mouse) через единый `IInputProvider` (pointer-абстракция).

---

## 2. Core Gameplay Loop

```
[Main Menu]
   └─→ [Level Select] ──→ [Level: Village N]
             ↑                    │
             │        Worship ≥ порога (Awe и/или Fear)
             │                    ↓
             │             [Level Complete] ── Reward (coins + alignment shift)
             │                    ↓
             └────────── [Temple / Meta] ──→ Upgrades / Idle income / Creature teaser
```

Игрок проходит уровни, выбирая стиль (добро/зло/микс), копит Coins, прокачивает руку/Faith/чудеса, возвращается. При входе в игру после отсутствия — Welcome Back попап с накопленным idle-доходом.

---

## 3. Input

### Контракт `IInputProvider` (см. `Core/Input/`)
Pointer-абстракция: touch и mouse дают одинаковые события. Провайдер один — `PointerInputProvider`; платформенные различия (pinch vs колесо) инкапсулированы внутри.

| Action           | Type      | Mobile                          | PC (отладка)             |
| ---------------- | --------- | ------------------------------- | ------------------------ |
| `PointerDown/Up` | `Vector2` | Тап                             | LMB                      |
| `PointerDrag`    | `Vector2` | Ведение пальца                  | Ведение с зажатой LMB    |
| `PointerHold`    | `float`   | Удержание (накачка силы чуда)   | Удержание LMB            |
| `Pan`            | `Vector2` | Drag по пустому месту           | LMB-drag по пустому месту (паритет с тачем) **и** RMB-drag |
| `Zoom`           | `float`   | Pinch                           | Колесо мыши              |
| `Pause`          | `Trigger` | Pause-иконка                    | Esc                      |

### Режимы руки (Hand Modes)
Определяются выбором в панели чудес, переключает `HandModeService`:

- `Grab` (чудо не выбрано) — палец = рука: pointer down на interactable → захват; drag → перенос; резкий отпуск → бросок с импульсом от скорости жеста; отпуск над землёй → мягко поставить.
- `Miracle` (чудо выбрано) — тап по цели = каст; hold = накачка силы/радиуса (расход Faith пропорционален); повторный тап по иконке чуда = отмена, возврат в `Grab`.
- Pan/Zoom работают в обоих режимах: pan только с пустого места (raycast не попал в interactable/цель).

### Контексты ввода (Input Contexts)
Переключаются `InputRouter` по `GameState`:
- `Gameplay` — все действия руки/чудес + Pan/Zoom + Pause.
- `UI` — навигация по меню, Pause toggle.

---

## 4. Player

Игрок — бестелесный бог. «Тело» игрока = рука + чудеса + Faith.

### Рука бога
**MonoBehaviour:** `GodHandController` — Unity API only: raycast из pointer, физический захват (joint/velocity-follow), визуал руки (курсор-меш над точкой касания).
**Service:** `HandInteractionService` — pure C#: правила захвата (что можно брать — по `IGrabbable`), расчёт импульса броска из истории позиций pointer'а, лимиты силы из конфига.
**Config:** `HandConfig` (SO) — `MaxGrabMass`, `ThrowForceScale`, `MaxThrowForce`, `CarryHeight`, `GrabRadius`, `ThrowVelocitySampleWindow`.

Бросок жителя/объекта в дом или толпу = источник Fear (см. §7). Аккуратно поставить жителя = нейтрально.

**Жители при броске:** без полного ragdoll'а — `VillagerController` переключается в режим `Tossed`: Rigidbody + tumble (случайный angular velocity), при приземлении — оглушение и возврат в FSM. Полный ragdoll — Scalability Hook.

### Чудеса
**MonoBehaviour:** `MiracleTargetingController` — превью зоны действия (projector/decal) под pointer'ом, индикация накачки силы.
**Service:** `MiracleService` — pure C#: валидация каста (`CanCast`: хватает ли Faith, валидна ли цель), расчёт стоимости от накачки, публикация `MiracleCastEvent`. Каст — stateful-сессия внутри сервиса (`BeginCast` → `UpdateCast(dt)` накачка → `ReleaseCast` / `CancelCast`), симметрично `HandInteractionService.BeginHold/EndHold`; паттерн Command не применяется (решение 2026-08-28: обёртка `CanExecute/Execute` над сервисом не несла бы состояния и смысла — undo/очередь/множественные инвокеры касту не нужны).
**Config:** `MiracleConfig` (SO, один на чудо) — `MiracleType type`, `BaseFaithCost`, `HoldCostPerSecond`, `BaseRadius`, `MaxRadius`, `EffectPower`, `AlignmentShift` (знак определяет добро/зло; применяется по исходу — нужда реально разрешена/создана, шаг 5), `VfxEffectType[] vfx`, `Sprite icon`, `int[] upgradeLevelPowerBonus`.
**Каталог:** `MiracleCatalogConfig` (SO) — массив `MiracleConfig`, определяет состав панели.

Чудеса среза:

| Miracle    | Alignment | Эффект                                                        |
| ---------- | --------- | ------------------------------------------------------------- |
| `Rain`     | Good      | Тушит пожары в радиусе, ускоряет рост урожая (need `Hunger`)   |
| `Heal`     | Good      | Лечит больных жителей в радиусе (need `Disease`)               |
| `Fireball` | Evil      | Поджигает дома/объекты, разбрасывает жителей взрывом            |
| `Lightning`| Evil      | Точечный удар: мгновенно оглушает жителей в радиусе (стимул `LightningStruck`), поджигает здание как Fireball (`CreatesNeed=Fire`, решение 2026-09-01: ставка Fear меньше огня — молния может рикошетить), AoE-паника; на прочие объекты влияния пока нет |

### Faith (мана)
**Service:** `FaithService` — pure C#: текущий Faith, пассивная генерация от числа жителей в состоянии `Worship` (rate из конфига), потолок из `FaithConfig` + апгрейдов. Публикует `FaithChangedEvent`.
**Config:** `FaithConfig` (SO) — `BaseCapacity`, `RegenPerWorshipper`, `StartingFaithPercent`.

### Worship и Alignment
**Service:** `WorshipService` — pure C#: единая шкала 0…`WorshipTarget` (из `LevelConfig`). Источники: разрешённая нужда (Awe), акт террора (Fear) — величины только из `WorshipConfig` (Awe — по нужде `AwePerNeedResolved`, Fear — по акту `FearPerTerrorAct`; `MiracleConfig.WorshipYield` удалён на шаге 5 как дубль). Публикует `WorshipChangedEvent`; при достижении порога — `LevelCompletedEvent`.
**Service:** `AlignmentService` — pure C#: `AlignmentScore` −100…+100, сдвигается `AlignmentShift`'ами действий. Публикует `AlignmentChangedEvent` (на него подпишутся визуал §11.1 и будущее существо §15).
**Config:** `WorshipConfig` (SO) — `AwePerNeedResolved` (per need type), `FearPerTerrorAct` (per act type), `WorshipDecayPerSecond` (по умолчанию 0 — Open Question #2).

---

## 5. Villagers & Level Events (аналог Enemies)

Противник в срезе — не AI-соперник, а **таймлайн событий уровня** + инерция деревни.

### Жители
**MonoBehaviour:** `VillagerController` — Unity API only: NavMesh-free перемещение по waypoint'ам/рандом-блужданию, анимации, Rigidbody-tumble в `Tossed`.
**Service:** `VillagerStateService` — pure C#: FSM жителя `Idle → Panic → Worship → Tossed → Stunned → Idle`. Правила переходов: рядом пожар/взрыв → `Panic`; нужда разрешена или террор рядом → `Worship` (у страха и благодарности один поведенческий выход — молитва, различается только анимация/VFX); брошен рукой → `Tossed`.
**Config:** `VillagerConfig` (SO) — `MoveSpeed`, `PanicRadius`, `PanicDuration`, `WorshipDuration`, `StunDuration`, `TossAngularVelocityRange`.

Жители — pooled, спавн через `VillagerFactory` (см. `CLAUDE.md`: `InjectGameObject` + `Launch(...)`).

### События-нужды (Need Events)
**Service:** `LevelEventService` — pure C#: планировщик, читает таймлайн из `LevelConfig` и триггерит события; следит за их разрешением/истечением.
**Config-элемент:** `NeedEventEntry { NeedType type; float triggerTime; int targetSiteIndex; float duration; }` внутри `LevelConfig`.

| NeedType   | Что происходит                         | Разрешение (Awe)         | Игнор/террор            |
| ---------- | -------------------------------------- | ------------------------ | ----------------------- |
| `Fire`     | Дом загорается, жители паникуют        | `Rain` по дому           | Дом сгорает (Fear, если поджёг игрок; иначе просто потеря источника Awe) |
| `Disease`  | Группа жителей болеет (VFX + анимация) | `Heal` по группе         | Жители «выбывают» до конца уровня (шаг 6: жители в `PanicRadius` от сайта возвращаются в пул через `VillagerFactory.Despawn`; осознанная трактовка — если на шаге 7 появится респавн из пула, «выбывшие» смогут вернуться, тогда пересмотреть) |
| `Hunger`   | Поле «увядает», иконка голода          | `Rain` по полю           | Поле пустеет            |

**MonoBehaviour:** `NeedSiteController` — точка события (дом/поле/группа): визуальные состояния (нормально/горит/увяло), триггер-зона попадания чуда — **kinematic Rigidbody обязателен** (см. `CLAUDE.md`).

---

## 6. Level System

### Уровень = деревня
5 сцен не нужны: **одна сцена `Village_Base`**, наполнение собирается из `LevelConfig` editor-скриптом `Build Village Level` (§17) — расстановка домов, полей, спавн-точек жителей, NeedSite'ов, границ камеры.

**Config:** `LevelConfig` (SO, один на уровень) —
```csharp
public sealed class LevelConfig : ScriptableObject {
    public LevelId       id;
    public int           worshipTarget;          // порог победы
    public int           villagerCount;
    public VillageLayoutConfig layout;           // дома/поля/spawn points (позиции)
    public NeedEventEntry[] eventTimeline;       // скриптованные события
    public float         optionalTimerSeconds;   // 0 = выключен (по умолчанию)
    public int           coinsReward;
    public Vector3       creatureSpawnPosition;  // ПРЕДОХРАНИТЕЛЬ §15, см. ниже про hasCreatureSpawn
}
```
**Каталог:** `LevelCatalogConfig` (SO) — массив `LevelConfig` + `Sprite preview` + unlock-порядок (линейный).

Прогрессия сложности по уровням — чистые данные: больше `worshipTarget`, плотнее таймлайн, меньше жителей на старте (меньше Faith-регена).

### Temple / Meta
UI-сцена `Temple`. Экран между уровнями:
- Баланс Coins, кнопка сбора idle-дохода (если есть накопленное).
- Tabs: Upgrades (3 категории) / Creature (тизер-карточка "Coming soon" — аналог disabled-карточек, `CreatureTeaserView`).
- Визуал храма мутирует по `AlignmentScore` (см. §11.1).

### Level Select
UI-сцена `LevelSelect` (или панель в Main Menu): 5 карточек из `LevelCatalogConfig`, пройденные отмечены, следующая разблокирована, остальные — locked.

---

## 7. Weapons & Combat (Miracles & Interactions)

Классического combat нет. «Оружие» — рука и чудеса; «бой» — конверсия деревни.

### Матрица источников Worship
| Действие                                  | Источник | Worship | AlignmentShift |
| ----------------------------------------- | -------- | ------- | -------------- |
| Разрешить `Fire` (Rain по горящему дому)  | Awe      | +из SO  | +good          |
| Разрешить `Disease` (Heal по больным)     | Awe      | +из SO  | +good          |
| Разрешить `Hunger` (Rain по полю)         | Awe      | +из SO  | +good          |
| Fireball по дому                          | Fear     | +из SO  | −evil          |
| Lightning поджёг здание                   | Fear     | +из SO (`LightningStrike`, меньше `FireballOnStructure` — решение 2026-09-01) | −evil (по исходу, через `CauseMiracle`) |
| Lightning оглушил жителя                  | Fear     | +из SO (`VillagerStruckByLightning`, отдельная ставка за каждого — прецедент `VillagerBlasted`) | — (удар по жителям без структуры alignment не двигает — консистентно Fireball) |
| Бросок жителя (жёсткое приземление)       | Fear     | +из SO  | −evil (малый)  |
| Бросок объекта в дом (разрушение декора)  | Fear     | +из SO  | −evil (малый)  |
| Взрыв чуда: жёсткое приземление отброшенного жителя | Fear | +из SO (`VillagerBlasted`, отдельная ставка — решение 2026-08-29) | — (сдвиг только у самого чуда по исходу) |

Все величины — `WorshipConfig` + `MiracleConfig`. Balance-рычаг: Fear даёт Worship быстрее, но паника временно снижает число молящихся → падает Faith-реген (самобалансирующийся trade-off, кода не требует — следствие FSM жителей).

### Физические объекты
`ThrowableObjectController` (бочки, камни, деревья) — `IGrabbable`, Rigidbody. Спавн из `layout` уровня. Разрушаемый декор — `DestructibleObjectController` + `DestructibleConfig` (SO).

---

## 8. Economy

### Валюта
- `Coins` — единственная валюта, хранится в `MetaProgressService`.
- Источники: награда за уровень (`LevelConfig.coinsReward`, ×множитель за первый раз — `RewardConfig`), idle-доход (§8.3).

### Апгрейды
**Config:** `UpgradeCatalogConfig` (SO) — 3 категории × N уровней (`UpgradeLevelEntry { int level; float effectValue; int price; }`):

| Category       | Effect                                        |
| -------------- | --------------------------------------------- |
| `HandPower`    | `+X%` к `MaxThrowForce` и `MaxGrabMass`        |
| `FaithCapacity`| `+X` к `BaseCapacity` и `+Y%` к regen          |
| `MiraclePower` | `+X%` к `EffectPower`/`MaxRadius` всех чудес   |

Применение: на старте уровня `EffectiveStatsService` собирает effective-параметры из базовых SO + уровней апгрейдов из `MetaProgressService` (тот же паттерн, что `CarStatsService` в FlatOut-проекте).

### Idle-доход (в срезе, минимальная форма)
**Service:** `IdleIncomeService` + `IIdleIncomeService` — pure C#: при входе в игру читает `lastSeenUtc` из профиля через `ITimeProvider` (абстракция над `DateTime.UtcNow` — для тестов и защиты от подкрутки часов позже), считает `coins = min(elapsedHours, capHours) * ratePerHour`, публикует `IdleIncomeReadyEvent`. Сбор — по кнопке в Welcome Back попапе → `RewardEarnedEvent`.
**Config:** `IdleIncomeConfig` (SO) — `CoinsPerHour`, `CapHours`, `MinOfflineMinutes` (ниже — попап не показывается).
Никаких energy, ускорителей, пуш-уведомлений — Scalability Hooks.

### Награды — унифицированно
Все источники наград публикуют **`RewardEarnedEvent { RewardSource source; int coinAmount; int xpAmount; }`** — `xpAmount` пока всегда 0 (предохранитель §15).

### Сохранение
PlayerPrefs через `IProfileStorage` → `PlayerPrefsProfileStorage` (предохранитель §15): Coins, пройденные уровни, уровни апгрейдов, `AlignmentScore` (персистентен между уровнями), `lastSeenUtc`, лучшие результаты. Внутри уровня сохранений нет.

---

## 9. Game Flow & States

### Generic FSM (см. `CLAUDE.md`), push-pop с самого начала
Enum `GameState`:
```
MainMenu,
LevelSelect,
LevelPlaying,
LevelComplete,
TempleMeta,
Paused,        // push-pop поверх любого состояния
WelcomeBack    // push-pop попап поверх MainMenu/TempleMeta
```

### Ключевые переходы
| From            | Event                        | To              |
| --------------- | ---------------------------- | --------------- |
| `MainMenu`      | UI: Play                     | `LevelSelect`   |
| `MainMenu`      | UI: Temple                   | `TempleMeta`    |
| `MainMenu`/`TempleMeta` | `IdleIncomeReadyEvent` | `WelcomeBack` (push) |
| `WelcomeBack`   | UI: Collect / Close          | previous (pop)  |
| `LevelSelect`   | UI: Level card               | `LevelPlaying`  |
| `LevelPlaying`  | `LevelCompletedEvent`        | `LevelComplete` |
| `LevelComplete` | UI: Continue                 | `TempleMeta`    |
| `TempleMeta`    | UI: Next level               | `LevelSelect`   |
| any gameplay    | UI: Pause                    | `Paused` (push) |
| `Paused`        | UI: Resume                   | previous (pop)  |

`Paused`: `Time.timeScale = 0` + явный флаг для сервисов, которым нельзя полагаться на timeScale (idle-расчёты, UI-анимации).

### Soft-fail
Жёсткого fail нет. Если Faith = 0 и активных событий нет и Worship < порога → `LevelStalledEvent` → UI-предложение «Пополнить веру» (rewarded-заглушка: кнопка мгновенно даёт Faith, место для ads SDK помечено `// ADS_STUB`).

---

## 10. UI / HUD

MVP строго (см. `CLAUDE.md`): View — MonoBehaviour с UI-элементами; Presenter — pure C#; Model — сервисы.

### HUD: Level
**View:** `LevelHudView`.
- Worship-бар (крупный, сверху) с меткой порога.
- Faith-бар + числовое значение.
- Панель чудес (4 иконки): выбранное подсвечено, недоступные (не хватает Faith) — затемнены, cost-лейблы.
- Alignment-индикатор (компактный, крыло добро/зло).
- Pause-кнопка.
- Всплывающие оценки действий (`+12 Awe`, `+20 Fear`) — pooled `WorshipPopupView`.

### Welcome Back
**View:** `WelcomeBackView` — накопленные Coins, кнопка Collect.

### Temple / Meta
**View:** `TempleView` + `UpgradeRowView` (×3) + `CreatureTeaserView` (карточка "Coming soon", disabled).

### Level Select / Main Menu / Level Complete / Pause
`LevelSelectView` + `LevelCardView` (×5), `MainMenuView`, `LevelCompleteView` (Worship-итог, стиль прохождения: сколько Awe / сколько Fear, монеты), `PauseOverlayView`.

---

## 11. Camera

Фиксированная изометрия (~40–45° pitch), без вращения. `CameraRig` + `ICameraBehaviour`:
- `IsometricPanZoomBehaviour` — pan (drag по пустому месту), pinch/scroll zoom с лимитами, clamped к границам уровня (из `layout`).

**Ориентация экрана: только landscape** (обе стороны; решение от 2026-08-18). Определяет кадрирование камеры, раскладку HUD (§10) и safe area (§19.9). Лочится через `BlackWhiteLike/Setup/Apply Mobile Player Settings`.

Cinemachine 3.1 (см. `CLAUDE.md` про API). **Config:** `IsometricCameraConfig` (SO) — `Pitch`, `MinZoom`, `MaxZoom`, `PanSpeed`, `ZoomSpeed`, `BoundsPadding`.

### 11.1 Alignment-визуал (material swap)
**Config:** `AlignmentVisualConfig` (SO) — массив `AlignmentStage[]`:
```csharp
[Serializable] public struct AlignmentStage {
    public float      scoreThreshold;     // при каком AlignmentScore активируется
    public Material[] handMaterials;      // null = не менять
    public Material[] templeMaterials;    // null = не менять
    public GameObject[] decalPrefabsToSpawn;
    public VfxEffectType[] vfxToActivate; // ореол/пепел/искры
}
```
**Controller:** `AlignmentVisualController` (MonoBehaviour) — подписан на `AlignmentChangedEvent`, применяет стейдж; пустые поля игнорируются (тот же контракт, что `CarDamageVisualController` в FlatOut). Стейджей 5: Saint → Kind → Neutral → Cruel → Demon. UI-тинт — через `AlignmentUiPalette` внутри того же SO.

---

## 12. Architecture Mapping

> Все пути — относительно `Assets/Development/Scripts/` (структура и правила — `CLAUDE.md`).

### Core
- `Core/Events/Events.Hand.cs` — `ObjectGrabbedEvent`, `ObjectThrownEvent`, `ObjectLandedEvent`
- `Core/Events/Events.Miracles.cs` — `MiracleSelectedEvent`, `MiracleCastEvent`, `FaithChangedEvent`
- `Core/Events/Events.Village.cs` — `NeedTriggeredEvent`, `NeedResolvedEvent`, `NeedExpiredEvent`, `VillagerStateChangedEvent`
- `Core/Events/Events.Worship.cs` — `WorshipChangedEvent`, `AlignmentChangedEvent`, `LevelCompletedEvent`, `LevelStalledEvent`
- `Core/Events/Events.Meta.cs` — `RewardEarnedEvent`, `CurrencyChangedEvent`, `UpgradePurchasedEvent`, `IdleIncomeReadyEvent`, `IdleIncomeCollectedEvent`
- `Core/Events/Events.Flow.cs` — `GameStateChangedEvent`, `GamePausedEvent`, `GameResumedEvent`
- `Core/Input/IInputProvider.cs`, `PointerInputProvider.cs`, `InputRouter.cs`, `InputContext.cs`
- `Core/StateMachine/StateMachine.cs` + `IState.cs`

### Models
- `Models/GameState.cs`, `Models/LevelId.cs`, `Models/MiracleType.cs`, `Models/NeedType.cs`, `Models/HandMode.cs`, `Models/RewardSource.cs`, `Models/UpgradeCategory.cs`, `Models/VillagerState.cs`
- `Models/Tags.cs`, `Models/AnimatorHashes.cs` (правила — `CLAUDE.md`)

### Data / Configs (SO-определения; инстансы — `Assets/Development/Data/Configs/`)
- `Data/Configs/Hand/HandConfig.cs`
- `Data/Configs/Miracles/MiracleConfig.cs`, `Data/Configs/Miracles/MiracleCatalogConfig.cs`
- `Data/Configs/Faith/FaithConfig.cs`
- `Data/Configs/Worship/WorshipConfig.cs`
- `Data/Configs/Alignment/AlignmentVisualConfig.cs`
- `Data/Configs/Villagers/VillagerConfig.cs`
- `Data/Configs/Levels/LevelConfig.cs`, `Data/Configs/Levels/VillageLayoutConfig.cs`, `Data/Configs/Levels/LevelCatalogConfig.cs`
- `Data/Configs/Upgrades/UpgradeCatalogConfig.cs`
- `Data/Configs/Rewards/RewardConfig.cs`
- `Data/Configs/Idle/IdleIncomeConfig.cs`
- `Data/Configs/Camera/IsometricCameraConfig.cs`
- `Data/Configs/Destructible/DestructibleConfig.cs`

### Services (pure C#; каждый — интерфейс + реализация)
- `Services/Hand/HandInteractionService.cs`, `Services/Hand/HandModeService.cs`
- `Services/Miracles/MiracleService.cs`
- `Services/Faith/FaithService.cs`
- `Services/Worship/WorshipService.cs`, `Services/Worship/AlignmentService.cs`
- `Services/Village/LevelEventService.cs`, `Services/Village/VillagerStateService.cs`
- `Services/Level/LevelFlowService.cs` (жизненный цикл уровня: старт, win-check, награда)
- `Services/Meta/MetaProgressService.cs` (+ `IPlayerProfile`), `Services/Meta/EconomyService.cs`, `Services/Meta/EffectiveStatsService.cs`
- `Services/Idle/IdleIncomeService.cs`, `Services/Idle/ITimeProvider.cs` (+ `SystemTimeProvider`)
- `Services/Flow/GameFlowService.cs`
- `Services/Camera/CameraService.cs`
- `Services/Progression/` — **пустая папка** (предохранитель §15)
- `Services/Creature/` — **пустая папка** (предохранитель §15)

### Controllers (MonoBehaviour, Unity API only)
- `Controllers/Hand/GodHandController.cs`
- `Controllers/Miracles/MiracleTargetingController.cs`
- `Controllers/Village/VillagerController.cs`, `Controllers/Village/NeedSiteController.cs`, `Controllers/Village/ThrowableObjectController.cs`, `Controllers/Village/DestructibleObjectController.cs`
- `Controllers/Temple/AlignmentVisualController.cs`
- `Controllers/Camera/CameraRig.cs`, `Controllers/Camera/IsometricPanZoomBehaviour.cs`

### Views (MVP, только UI; Presenters рядом)
- `Views/UI/MainMenu/MainMenuView.cs`
- `Views/UI/LevelSelect/LevelSelectView.cs` + `LevelCardView.cs`
- `Views/UI/Hud/LevelHudView.cs` + `MiraclePanelView.cs` + `WorshipPopupView.cs`
- `Views/UI/Temple/TempleView.cs` + `UpgradeRowView.cs` + `CreatureTeaserView.cs`
- `Views/UI/Overlay/LevelCompleteView.cs`, `PauseOverlayView.cs`, `WelcomeBackView.cs`

### Factories (единственное место доступа к `IPoolManager`)
- `Factories/Village/VillagerFactory.cs`
- `Factories/Vfx/VfxFactory.cs`
- `Factories/Ui/WorshipPopupFactory.cs`

### Installers / Bootstrap
- `Installers/GameLifetimeScope.cs` — Composition Root (`RegisterComponent` для MonoBehaviour — см. `CLAUDE.md`).
- `GameBootstrap.cs` — точка входа.

---

## 13. EventBus Events

Все события — `readonly struct` через `IEventBus` (см. `CLAUDE.md`).

| Event                    | Payload                                                        | Publisher                |
| ------------------------ | -------------------------------------------------------------- | ------------------------ |
| `ObjectGrabbedEvent`     | `GrabbableId id, GrabbableKind kind`                            | `GodHandController`      |
| `ObjectThrownEvent`      | `GrabbableId id, Vector3 velocity`                              | `GodHandController` — **единственный** издатель (сервис pure C# и не держит `IEventBus`) |
| `ObjectLandedEvent`      | `GrabbableId id, float impactSpeed, bool hitStructure`          | `ThrowableObjectController` / `VillagerController` — реализации `IGrabbable` публикуют **только** это событие и глушат его, пока объект в руке |
| `MiracleSelectedEvent`   | `MiracleType type, bool selected`                               | `HandModeService`        |
| `MiracleCastEvent`       | `MiracleType type, Vector3 point, float power, float radius`    | `MiracleService`         |
| `FaithChangedEvent`      | `float oldValue, float newValue, float capacity`                | `FaithService`           |
| `NeedTriggeredEvent`     | `NeedType type, int siteIndex, bool byPlayer, MiracleType causeMiracle` | `LevelEventService` — `byPlayer = true`, если нужду создало чудо игрока (Fireball поджёг дом, §5), false — таймлайн |
| `NeedResolvedEvent`      | `NeedType type, int siteIndex, bool byPlayer, MiracleType causeMiracle` | `LevelEventService` — `causeMiracle` валиден только при `byPlayer` (AlignmentShift чуда по исходу, шаг 5) |
| `NeedExpiredEvent`       | `NeedType type, int siteIndex, bool byPlayer`                   | `LevelEventService` — тот же флаг, что у Triggered (шаг 5: Fear за сгоревший дом только при `byPlayer`) |
| `VillagerStateChangedEvent` | `VillagerId id, VillagerState from, VillagerState to, VillagerStimulus cause` (`None` — переход по таймеру; шаг 5: Fear за жёсткое приземление только при `cause == Grabbed`, не `Blasted`) | `VillagerStateService`   |
| `WorshipChangedEvent`    | `int oldValue, int newValue, int target, WorshipSource source`  | `WorshipService`         |
| `AlignmentChangedEvent`  | `float oldScore, float newScore`                                | `AlignmentService`       |
| `LevelCompletedEvent`    | `LevelId id, int aweTotal, int fearTotal, float duration`       | `LevelFlowService`       |
| `LevelStalledEvent`      | (empty)                                                         | `LevelFlowService`       |
| `RewardEarnedEvent`      | `RewardSource source, int coinAmount, int xpAmount`             | `EconomyService` / `IdleIncomeService` |
| `CurrencyChangedEvent`   | `int oldValue, int newValue, int delta`                         | `EconomyService`         |
| `UpgradePurchasedEvent`  | `UpgradeCategory category, int newLevel`                        | `EconomyService`         |
| `IdleIncomeReadyEvent`   | `int coinAmount, float offlineHours`                            | `IdleIncomeService`      |
| `IdleIncomeCollectedEvent` | `int coinAmount`                                              | `IdleIncomeService`      |
| `GameStateChangedEvent`  | `GameState from, GameState to`                                  | `GameFlowService`        |
| `GamePausedEvent` / `GameResumedEvent` | (empty)                                           | `GameFlowService`        |

---

## 14. Scalability Hooks

Три категории по стоимости (объём работы программиста, не сложность идеи).

### 14.1 Pure content — только SO и ассеты, кода не трогаем
- **Новые уровни** — `LevelConfig` + `VillageLayoutConfig` + entry в `LevelCatalogConfig`; сборка editor-скриптом.
- **Новые уровни апгрейдов** — entries в `UpgradeCatalogConfig`.
- **Балансировка** — `WorshipConfig`, `MiracleConfig`, `RewardConfig`, `IdleIncomeConfig`.
- **Новые alignment-стейджи / скины визуала** — entries в `AlignmentVisualConfig`.
- **Новые разрушаемые/бросаемые объекты** — prefab + `DestructibleConfig`.

### 14.2 Light code — 1 класс + конфиги + сцена/UI
- **Новое чудо в существующем шаблоне** (AoE-эффект по нуждам/объектам, напр. Meteor, Bless Field) — `MiracleType` value + `MiracleConfig` + один effect-handler в `MiracleService` (или новый `IMiracleEffect`).
- **Новый тип нужды** — `NeedType` value + `NeedEventEntry` поддержка + визуальные состояния `NeedSiteController` + маппинг разрешающего чуда.
- **Жестовый каст** — новый `IInputProvider`-слой распознавания поверх существующего `MiracleService` (каст-контракт не меняется).
- **Cloud save** — `CloudProfileStorage : IProfileStorage`, замена регистрации в `GameLifetimeScope`.

### 14.3 Heavy code — отдельная игровая система
- **Существо-питомец** — `Services/Creature/` стек: `CreatureBrainService`, `CreatureController` (навигация, взаимодействие с NeedSite), обучение поощрение/наказание, рост/статы, alignment-облики, UI. Подписывается на уже существующие `AlignmentChangedEvent`, `NeedTriggeredEvent`; спавнится в `LevelConfig.creatureSpawnPosition`. Оценка: 3–6 недель + самый дорогой арт проекта.
- **Idle-мета полного цикла** — energy/lives, ускорители, пуш-уведомления, оффлайн-симуляция деревни.
- **Daily missions / Player XP / Battle Pass** — `Services/Progression/`, подписки на `RewardEarnedEvent` / `LevelCompletedEvent` (см. §15).
- **PvP «божественные дуэли»** — сетевой стек, вне обсуждения среза.

Правило: **если в существующих сервисах надо менять интерфейс или логику — это 14.3, а не 14.1.**

---

## 15. Progression & Creature — архитектурные предохранители

В срез эти системы НЕ входят. Раздел фиксирует, что CLI обязан сделать сейчас (минуты работы), чтобы потом не переписывать.

- **`Services/Progression/` и `Services/Creature/` создаются пустыми** — будущие сервисы падают туда.
- **`MetaProgressService` инкапсулирует доступ через `IPlayerProfile`** — интерфейс описывает только данные среза (coins, пройденные уровни, апгрейды, alignment, lastSeenUtc).
- **`RewardEarnedEvent` содержит `xpAmount`** (пока всегда 0) — будущий Player Level просто подпишется.
- **`GameFlowService` — push-pop состояний с первого дня** (`Paused`, `WelcomeBack` уже используют; будущие модалки миссий/пасса — бесплатно).
- **Save/load через `IProfileStorage`** → `PlayerPrefsProfileStorage`; cloud save = новая реализация + смена регистрации.
- **`ITimeProvider` абстракция** — idle-расчёты тестируемы; позже — защита от подкрутки часов через серверное время.
- **`AlignmentChangedEvent` публикуется всегда** — существо подпишется на него без правок `AlignmentService`.
- **`LevelConfig.creatureSpawnPosition` + `hasCreatureSpawn`** — точка спавна существа есть с первого дня, в срезе игнорируется. Именно `Vector3`, а не `Transform`: ScriptableObject не имеет права держать сценную ссылку (она не переживает загрузку сцены). Флаг `hasCreatureSpawn` обязателен — без него `Vector3.zero` неотличим от осознанно выставленного центра карты, и существо на своём шаге молча заспавнится в мировом нуле, что будет выглядеть багом существа, а не незаполненным конфигом. Читать только через `TryGetCreatureSpawn(out Vector3)`.
- **`CreatureTeaserView`** в Temple — карточка "Coming soon" (только контент), заодно замер интереса на playtest'е.

---

## 16. Acceptance Criteria

Срез готов, когда **все** пункты выполняются.

### Рука бога
- [ ] Палец/мышь захватывает жителей и объекты (`IGrabbable`), переносит, бросает с импульсом от скорости жеста.
- [ ] Бросок жителя даёт `Tossed` (tumble) → `Stunned` → возврат в FSM; жёсткое приземление рядом с толпой даёт Fear.
- [ ] Pan по пустому месту и pinch/scroll zoom работают и не конфликтуют с захватом.

### Чудеса и Faith
- [ ] Панель из 4 чудес: выбор, отмена, затемнение при нехватке Faith, cost-лейблы.
- [ ] Тап = каст, hold = накачка силы/радиуса с растущим расходом Faith; превью зоны действия видно до каста.
- [ ] Rain тушит пожары и разрешает Hunger; Heal лечит Disease; Fireball поджигает дома; Lightning бьёт точечно с AoE-паникой.
- [ ] Faith генерируется от молящихся жителей, ограничен ёмкостью; бары обновляются.

### Worship / Alignment / Win
- [ ] Шкала Worship наполняется и от Awe, и от Fear; уровень проходится **обоими** стилями (проверить вручную оба пути на уровне 1).
- [ ] `AlignmentScore` сдвигается действиями, персистентен между уровнями; визуал руки/храма/UI мутирует по стейджам.
- [ ] Worship ≥ порога → Level Complete с итогами (Awe/Fear split, coins).
- [ ] Soft-fail: при Faith = 0 без активных событий появляется предложение «Пополнить веру» (rewarded-заглушка).

### Уровни и события
- [ ] 5 уровней с нарастающей сложностью, собраны editor-скриптом из `LevelConfig`, проходятся последовательно.
- [ ] События таймлайна (Fire/Disease/Hunger) срабатывают по расписанию, разрешаются и истекают корректно.
- [ ] Жители: Idle/блуждание, Panic от огня/взрывов, Worship после Awe/Fear, Tossed при броске.

### Meta / Idle
- [ ] Coins начисляются, баланс виден, сохраняется между сессиями.
- [ ] 3 апгрейда покупаются и реально влияют на следующий уровень (effective stats).
- [ ] После офлайна ≥ `MinOfflineMinutes` показывается Welcome Back с накопленными Coins; сбор работает; cap соблюдается.
- [ ] Creature-тизер виден в Temple как disabled-карточка.

### Поток состояний и UI
- [ ] MainMenu → LevelSelect → Level → LevelComplete → Temple → LevelSelect — без багов.
- [ ] Pause (push-pop) работает в геймплее, корректно резюмит; WelcomeBack — push-pop попап.
- [ ] HUD показывает релевантные данные; worship-попапы (`+N Awe/Fear`) пулятся.

### Платформы и input
- [ ] Играется на PC мышью и на мобиле/симуляторе тачем через один `PointerInputProvider`.
- [ ] Safe area учтена для HUD (вырезы/скругления).

### Тех-критерии (полный список правил — `CLAUDE.md`)
- [ ] Нет `Resources.Load`; идентификаторы — enum/const; параметры — в SO.
- [ ] Все триггер-зоны (`NeedSiteController` и др.) с kinematic Rigidbody.
- [ ] Пул: `InjectGameObject` + явный `Launch(...)`; MonoBehaviour в `GameLifetimeScope` — `RegisterComponent`.
- [ ] Пустые `Services/Progression/`, `Services/Creature/`; `RewardEarnedEvent.xpAmount` присутствует; `ITimeProvider` внедрён.

---

## 17. Editor Tooling

Любая рутина — через `[MenuItem("GodHand/<Category>/<Action>")]`. Правила — `CLAUDE.md` + skill `add-editor-script` + `.claude/rules/editor-files.md`.

- `EDITOR_SCRIPT_NEEDED: GodHand/Levels/Build Village Level` — по выбранному `LevelConfig` собрать сцену: дома/поля/NeedSite'ы из `layout`, spawn-точки жителей, границы камеры, kinematic Rigidbody на всех триггерах. Идемпотентен (повторный запуск пересобирает, не дублирует).
- `EDITOR_SCRIPT_NEEDED: GodHand/Configs/Create Empty SO Set` — пустые `.asset`-инстансы всех ключевых SO в правильной структуре папок.
- `EDITOR_SCRIPT_NEEDED: GodHand/Setup/Setup Villager Prefab` — из выбранной модели собрать префаб жителя: Rigidbody, коллайдер, `VillagerController`, Animator с хэшами из `AnimatorHashes`.
- `EDITOR_SCRIPT_NEEDED: GodHand/Setup/Setup Alignment Visual` — по выбранным рендерам руки/храма заполнить Neutral-стейдж `AlignmentVisualConfig` текущими материалами (заготовки под остальные стейджи — пустые).
- `EDITOR_SCRIPT_NEEDED: GodHand/Setup/Add Kinematic Rigidbody To Selected Triggers` — защита от забывания (см. `CLAUDE.md`).
- `EDITOR_SCRIPT_NEEDED: GodHand/Validation/Validate Composition Root` — все интерфейсы зарегистрированы; нет `RegisterInstance` для MonoBehaviour.
- `EDITOR_SCRIPT_NEEDED: GodHand/Validation/Find Magic Strings` — строковые литералы в Controllers, которые должны быть в enum/const.
- `EDITOR_SCRIPT_NEEDED: GodHand/Validation/Validate Level Configs` — у каждого `LevelConfig`: порог > 0, таймлайн отсортирован, `targetSiteIndex` в пределах layout, награда задана.

---

## 18. Open Questions

Решаются до имплементации соответствующего шага или по первому playtest'у; ответы = правки SO, не архитектуры.

1. **Захват рукой: joint или velocity-follow?** Velocity-follow (Rigidbody следует за точкой под пальцем) обычно стабильнее на мобиле; joint честнее физически. Решить на шаге 2 по ощущению.
2. **Worship decay.** Нужен ли спад шкалы со временем (давление на темп)? По умолчанию 0; поле в SO есть.
3. **Численный баланс.** `AwePerNeedResolved`/`FearPerTerrorAct`, стоимость чудес, Faith-реген, пороги уровней — стартовые значения задать «на глаз», крутить по playtest.
4. **Пороги alignment-стейджей.** 5 стейджей: ±30/±70? Подбор по визуалу.
5. **Fear-инфляция.** Нужен ли diminishing returns на повторный террор по одной цели (чтобы зло не было строго доминирующей стратегией)? Поле-множитель в `WorshipConfig`, по умолчанию 1.0.
6. **Таймер уровня.** Поле `optionalTimerSeconds` есть, выключено. Включать ли на уровнях 4–5 для темпа?
7. **Звук и хаптика.** Формально вне acceptance criteria; подключать `IAudioService`-заглушку сразу или отложить? Хаптика на бросок/каст — сильно добавляет «сочности» на мобиле, стоит дёшево.
8. **Арт-пайплайн.** Low-poly паки (деревня, жители, VFX) — подобрать конкретные к шагу 3; критерий: единый стиль + мобильный полигонаж; правило хранения — `Assets/ThirdParty/` (см. `CLAUDE.md`).

---

## 19. Implementation Order (рекомендация для Claude Code CLI)

Каждый шаг = играбельный/проверяемый Unity-checkpoint. Не переходить к следующему без проверки.

1. **Каркас**: `GameLifetimeScope`, `GameBootstrap`, `PointerInputProvider` + `IInputProvider`, `IEventBus`, generic FSM (push-pop), enum/const (`GameState`, `MiracleType`, `NeedType`, `HandMode`, `Tags`, `AnimatorHashes`), `IProfileStorage` + `PlayerPrefsProfileStorage`, `ITimeProvider`, пустые `Services/Progression/` и `Services/Creature/`. Цель: пустая сцена стартует без ошибок, FSM в `MainMenu`.
2. **Камера + рука**: `CameraRig` + `IsometricPanZoomBehaviour`, `GodHandController` + `HandInteractionService`, `ThrowableObjectController`. Тестовая сцена `_TestHand` с plane и кубами. Цель: pan/zoom, таскать и швырять кубы пальцем/мышью.
2.5. **Mobile smoke build** (вставлен 2026-08-18, вне исходного §19): первая сборка на устройство до накопления слоёв. `BlackWhiteLike/Setup/Apply Mobile Player Settings` (landscape-лок, bundle id, IL2CPP+ARM64, уровень качества Mobile) и `BlackWhiteLike/Build/Android Smoke Build` (сборка `_TestHand` в `Builds/Android/`). Цель: игра запускается на телефоне, рука хватается **пальцем** (захват идёт тонким лучом — `GrabRadius` удалён на шаге 2, палец толще курсора и закрывает цель собой). Полировка мобилки остаётся шагом 9.
3. **Жители + события**: `VillagerController` + `VillagerStateService` + `VillagerFactory`, `NeedSiteController`, `LevelEventService`, первый черновой `LevelConfig`. Цель: деревня живёт, по таймлайну загорается дом, жителей можно бросать (Panic/Tossed видны).
4. **Чудеса + Faith**: `MiracleService` (сессия каста, без Command — см. §4), `FaithService`, `MiracleTargetingController`, `MiraclePanelView`, чудеса Rain + Fireball. Цель: тушить пожар дождём / поджигать дом файрболом, Faith тратится и регенится.
5. **Worship + win**: `WorshipService`, `AlignmentService`, `LevelFlowService`, `LevelHudView` (бары, попапы), `LevelCompleteView`, soft-fail. Цель: уровень 1 проходится и добром, и злом.
6. **Оставшиеся чудеса + alignment-визуал**: Heal + Lightning, Disease/Hunger нужды, `AlignmentVisualController` + `AlignmentVisualConfig` (material swap). Цель: все 4 чуда работают, храм/рука мутируют.
7. **Уровни 2–5**: `LevelCatalogConfig`, `LevelSelectView`, editor-скрипт `Build Village Level`, сборка и настройка 4 конфигов. Цель: кампания из 5 уровней с прогрессией.
8. **Meta + idle**: `MetaProgressService` (+`IPlayerProfile`), `EconomyService`, `EffectiveStatsService`, `TempleView` + апгрейды, `IdleIncomeService` + `WelcomeBackView`, `CreatureTeaserView`. Цель: полный цикл «играй → копи → апай → играй», idle-доход собирается.
9. **Mobile polish**: safe area, размеры хит-зон панели, хаптика-заглушка, производительность (профилирование на устройстве, пулы VFX). Цель: комфортно играется на телефоне.
10. **Polish + баланс**: онбординг-подсказки (уровень 1), rewarded-заглушки, ответы на Open Questions через правки SO. Цель: acceptance criteria §16 закрыты.

После каждого шага — playable build, проверяемый по соответствующим пунктам §16.
