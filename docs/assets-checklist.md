# Assets Checklist — GodHand

> Чеклист закупки и подготовки визуального контента перед запуском Claude Code CLI.
> Сопровождает [`game-design.md`](./game-design.md) и [`CLAUDE.md`](../CLAUDE.md).
>
> **Стиль:** low-poly stylized (Synty-like), тёплая «сказочная деревня» как нейтральная база — alignment-мутации уводят её в светлую/тёмную сторону материалами и VFX, а не отдельными ассетами.
> **Камера:** фиксированная изометрия ~40–45° — ассеты должны читаться с этого ракурса, детализация низа/тыла не нужна.
> **Бюджет на старте:** ≈ $60–130 платных ассетов (большая часть — переиспользование уже купленного).

---

## 1. Решения по стилю (фиксируется ДО закупки)

- [ ] Зафиксировать **базовую палитру** (8–10 цветов) + **две alignment-палитры**: Good (тёплое золото/белый/зелень) и Evil (пепел/багровый/фиолет). Сохранить как `palette.md` рядом с этим файлом или SO `Assets/Development/Art/ColorPalette.asset`. Alignment-материалы (§11.1 game-design) приводятся к этим палитрам.
- [ ] Определить **референсные изображения** (3–5 шт) стиля деревни + по 2 референса Good/Evil мутации храма. Положить в `Assets/Development/Art/StyleReferences/` (исключить из билда).
- [ ] Решение по **руке бога**: полноценный меш кисти vs стилизованный «курсор-перчатка» vs светящийся сгусток с силуэтом ладони. Рекомендация: стилизованный меш ладони с эмиссией — дёшево, читаемо на мобиле, легко мутирует материалом (Saint = золотое свечение, Demon = когти/багровая эмиссия через отдельные материалы, не отдельные меши).

---

## 2. 3D — Деревня (КРИТИЧНО)

Центральный экранный объект — деревня целиком. Всё низкополигональное, один пак = единый стиль.

- [ ] **Synty POLYGON Fantasy Kingdom** (~$60–70, часто в Humble Bundle / Fanatical за меньше) — Unity Asset Store / syntystore.com
  - Дома (несколько типов — нужны варианты под 5 уровней), поля/огороды, храм/церковь (база под Temple), заборы, телеги, бочки, ящики, деревья, колодец, рыночные лотки.
  - Персонажи-жители входят в пак (см. §3).
  - Импортировать → `Assets/ThirdParty/Synty_FantasyKingdom/`. **Не редактировать содержимое** (правило CLAUDE.md).
- [ ] (опционально, если Fantasy Kingdom не окажется в бандлах) альтернативы: **POLYGON Vikings** / **POLYGON Farm** (поля лучше) — оценить по превью, критерий: дома + поля + храмоподобное здание в одном паке.
- [ ] Проверить наличие **горящих/разрушенных вариантов домов** в паке. Если нет — состояние Fire делаем VFX-ом (огонь + дым поверх целого меша) и материалом «обугленный» (стратегия material swap, меши не нужны). Состояние «дом сгорел» = обугленный материал + VFX тления.
- [ ] Создать в `Assets/Development/Prefabs/Village/` префабы поверх ThirdParty-мешей:
  - `House_A.prefab`, `House_B.prefab`, `House_C.prefab` — с `NeedSiteController`, триггер-зоной попадания чуда (**kinematic Rigidbody**), точками крепления VFX огня/дыма (empty GameObject-маркеры).
  - `Field_Crops.prefab` — поле с двумя визуальными состояниями (материал «цветёт»/«увяло») + `NeedSiteController`.
  - `Temple.prefab` — храм с рендерами, зарегистрированными под `AlignmentVisualController` (SerializeField пустые — заполнит editor-скрипт `Setup Alignment Visual`).
  - `Throwable_Barrel.prefab`, `Throwable_Crate.prefab`, `Throwable_Rock.prefab` — `ThrowableObjectController`, Rigidbody.
  - `Destructible_Fence.prefab` — `DestructibleObjectController` + `DestructibleConfig`.
  - Pivot у бросаемых объектов — в центре масс; у домов — на земле.

---

## 3. 3D — Жители

- [ ] Персонажи из **Synty POLYGON Fantasy Kingdom** (крестьяне, горожане — 3–5 разных внешностей достаточно для среза).
- [ ] Создать `Assets/Development/Prefabs/Village/Villager_A.prefab` (+B, C):
  - Rigidbody (kinematic в обычных состояниях, non-kinematic в `Tossed`), CapsuleCollider.
  - `VillagerController`, Animator (контроллер — наш, в `Assets/Development/Art/Animations/`).
  - Сборка — через editor-скрипт `GodHand/Setup/Setup Villager Prefab` (§17 game-design), руками не собирать.
- [ ] Проверить **Humanoid-риг** Synty-персонажей для Mixamo-ретаргета (Synty обычно совместим, но проверить пропорции — см. каveат по chibi из Pirates-проекта: при конфликте корректировать через Avatar Configuration).

---

## 4. 3D — Рука бога

Готовых «рук бога» паками не продают — собираем из простого.

- [ ] Вариант 1 (рекомендуемый): **низкополигональная кисть** — бесплатные CC-модели (Sketchfab / Polyhaven / Quaternius), выбрать читаемую с изометрии, ~500–2000 tris.
- [ ] Вариант 2: до шага 6 жить на **примитиве** (сфера/капсула с эмиссией) — рука нужна с шага 2, а красивый меш можно подменить позже без правок кода (визуал — child-объект `GodHandController`).
- [ ] Создать `Assets/Development/Prefabs/Hand/GodHand.prefab`:
  - Меш + 5 материалов под alignment-стейджи (Saint/Kind/Neutral/Cruel/Demon) — в `Assets/Development/Art/Materials/Hand/`.
  - Точка крепления VFX ореола (empty-маркер).
  - Рендеры зарегистрированы под `AlignmentVisualController` (SerializeField заполнит editor-скрипт).

---

## 5. VFX

Чудеса — главный «сок» игры, на VFX не экономить по времени настройки.

- [ ] **JMO Assets — Cartoon FX Remaster Free** (бесплатно) — база: взрывы, искры, дым, sparkles.
- [ ] **Hovl Studio Magic Effects Pack** (~$25) — лучи, лечение, молнии, ауры — закрывает Heal, Lightning и alignment-ореолы почти целиком.
- [ ] (опционально) **JMO Cartoon FX Pack 1–4** (~$10–15 каждый) — если Free-версии не хватит на огонь/дождь.
- [ ] Импортировать в `Assets/ThirdParty/<PackName>/`.
- [ ] Создать **наши VFX-префабы** в `Assets/Development/Prefabs/VFX/` поверх ThirdParty-эмиттеров:
  - [ ] `VFX_RainCloud.prefab` — локальное облако + струи дождя (зона действия Rain)
  - [ ] `VFX_MiraclePreview.prefab` — decal/projector круга зоны действия (растёт при hold)
  - [ ] `VFX_Fireball_Projectile.prefab`, `VFX_Fireball_Explosion.prefab`
  - [ ] `VFX_Lightning_Strike.prefab`
  - [ ] `VFX_Heal_Sparkles.prefab`
  - [ ] `VFX_HouseFire_Loop.prefab`, `VFX_HouseSmolder_Loop.prefab` (тление сгоревшего)
  - [ ] `VFX_Disease_Cloud.prefab` (зелёная дымка над больными)
  - [ ] `VFX_Worship_Glow.prefab` (молящийся житель)
  - [ ] `VFX_Panic_Marker.prefab` (восклицание/пыль от бега)
  - [ ] `VFX_ObjectImpact.prefab` (приземление брошенного)
  - [ ] `VFX_HandAura_Good.prefab`, `VFX_HandAura_Evil.prefab` (alignment-стейджи)
  - [ ] `VFX_CoinBurst.prefab` (награда, сбор idle-дохода)
- [ ] Все типы зарегистрировать в `VfxEffectType` enum + `VfxConfig.asset` — один источник правды (правило CLAUDE.md, skill `add-vfx-effect`). Спавн — только через `VfxFactory` (пул).

---

## 6. UI

- [ ] **Переиспользовать уже купленные паки**: MegaGUIPack2 / Cartoon GUI Pack (Layer Lab) из предыдущих проектов — стиль подходит под low-poly stylized. Импортировать в `Assets/ThirdParty/<PackName>/`.
- [ ] Иконки чудес (4 шт: капля/дождь, сердце/крест, огненный шар, молния) — источники по убыванию предпочтения:
  - **Game-icons.net** (free, CC BY 3.0) — есть все четыре мотива, единый автор → консистентно;
  - AI-генерация в едином стиле (см. §7);
  - иконки из купленных UI-паков.
- [ ] Иконки UI: монета, шкала Worship (крыло/нимб + рог/пламя для Awe/Fear-попапов), апгрейды (ладонь / сосуд веры / звезда чуда), замок для locked-уровней.
- [ ] Скопировать используемые спрайты в `Assets/Development/Art/Sprites/UI/` (только реально нужные — не тащить пак целиком в билд).

---

## 7. 2D — AI-генерация (Midjourney / Flux)

- [ ] Единый style reference + промпт-шаблон (как в Pirates-пайплайне).
- [ ] Сгенерировать (по 2–3 варианта, выбрать лучший):
  - [ ] `LevelPreview_01..05.png` — превью 5 уровней для карточек Level Select (вид деревни, нарастающая «сложность» в композиции)
  - [ ] `CreatureTeaser_EggIdol.png` — арт яйца/идола для тизер-карточки существа (Coming soon)
  - [ ] `MainMenu_Background.png` — фон главного меню (деревня с высоты, нейтральный alignment)
  - [ ] `WelcomeBack_Header.png` — шапка попапа idle-дохода (храм, копящий свет)
  - [ ] (опц.) иконки чудес, если Game-icons не устроит стилистически
- [ ] Photoshop: привести к палитре §1, оставить место под UI, экспорт PNG → `Assets/Development/Art/Sprites/Backgrounds/` и `.../UI/`.

---

## 8. Анимации (Mixamo)

- [ ] **Mixamo** — free, mixamo.com. Скачать в `Assets/Development/Art/Animations/`:
  - [ ] `Idle_01.fbx`, `Idle_02.fbx` (разнообразие жителей)
  - [ ] `Walk.fbx`
  - [ ] `Run_Panic.fbx` (Mixamo: "Running" + вариант с руками вверх — "Run In Panic"/"Frantic Run")
  - [ ] `Praying.fbx` или `Kneeling.fbx` (состояние Worship)
  - [ ] `Stunned.fbx` / `Dizzy.fbx` (после Tossed-приземления)
  - [ ] `Scared_Reaction.fbx` (вход в Panic)
  - [ ] `Cheering.fbx` (реакция на разрешённую нужду — Awe-момент)
- [ ] Humanoid retarget на Synty-риг; при конфликте пропорций — Avatar Configuration.
- [ ] Полный ragdoll НЕ настраивать — в срезе Tossed = rigidbody-tumble (§4 game-design). Ragdoll — Scalability Hook.
- [ ] Имена animator-параметров — только через `AnimatorHashes.cs` (`Animator.StringToHash`), никаких string-литералов (правило CLAUDE.md).

---

## 9. Шрифты

- [ ] **Cinzel** — Google Fonts (free) — декоративный «божественный» для заголовков (Worship, Level Complete).
- [ ] **Nunito** — Google Fonts (free) — тело UI (уже используется в других проектах, TMP-пайплайн знаком).
- [ ] `.ttf` → `Assets/Development/Art/Fonts/`; Font Asset Creator:
  - [ ] `Cinzel_TMP.asset`
  - [ ] `Nunito_Bold_TMP.asset`, `Nunito_Regular_TMP.asset`
  - [ ] Кириллица: custom character set (U+0400–U+04FF), если UI будет локализован на русский.

---

## 10. Аудио

### Music
- [ ] **Sonniss GDC Bundle / Pixabay Music / FreePD** (free) — минимум 2 трека:
  - [ ] `Music_Village_Calm.ogg` — геймплей (нейтрально-пасторальная)
  - [ ] `Music_Menu.ogg` — меню/храм
  - [ ] (опц., приятный бонус) тёмный вариант геймплейного трека или интенсивный слой для Evil-alignment — Scalability Hook, не блокер.
- [ ] → `Assets/Development/Audio/Music/`.

### SFX
- [ ] **Sonniss GDC Bundle**, **freesound.org** (CC), **Kenney Audio Packs** (CC0). Минимальный набор → `Assets/Development/Audio/SFX/`:
  - [ ] `SFX_Grab.ogg`, `SFX_Throw_Whoosh.ogg`, `SFX_Impact_Soft.ogg`, `SFX_Impact_Wood.ogg`
  - [ ] `SFX_Rain_Loop.ogg`, `SFX_Fireball_Launch.ogg`, `SFX_Fireball_Explode.ogg`, `SFX_Lightning_Thunder.ogg`, `SFX_Heal_Chime.ogg`
  - [ ] `SFX_Fire_Loop.ogg` (горящий дом)
  - [ ] `SFX_Crowd_Panic.ogg`, `SFX_Crowd_Cheer.ogg`, `SFX_Pray_Chant_Short.ogg`
  - [ ] `SFX_Worship_Tick.ogg` (прирост шкалы), `SFX_LevelComplete.ogg`
  - [ ] `SFX_Coin.ogg`, `SFX_CoinPile.ogg` (idle-сбор), `SFX_Upgrade.ogg`
  - [ ] `SFX_UI_Tap.ogg`, `SFX_UI_Confirm.ogg`, `SFX_UI_Cancel.ogg`, `SFX_UI_Popup.ogg`
  - [ ] `SFX_Village_Ambience_Loop.ogg` (птицы, ветер)
- [ ] Звук формально вне acceptance criteria (Open Question #7 game-design), но набор дешёвый — закупить сразу, подключение через `IAudioService`-заглушку решается на шаге 1 CLI.

---

## 11. Подготовка проекта Unity ДО запуска CLI

### 11.1 Установка пакетов
- [ ] Unity 6 (6000.3.x LTS), URP template.
- [ ] **Git-репозиторий + Git LFS настроить ДО импорта первого ассета.**
- [ ] **Active Input Handling = Both** (Project Settings → Player).
- [ ] **VContainer** через Package Manager (`https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer`).
- [ ] **Cinemachine 3.1** через Package Manager.
- [ ] **TextMesh Pro Essentials**.

### 11.2 Структура папок
- [ ] `Assets/Development/Art/` (Models/, Animations/, Materials/Hand/, Materials/Temple/, Textures/, Sprites/UI/, Sprites/Backgrounds/, Fonts/, Audio/Music/, Audio/SFX/, StyleReferences/).
- [ ] `Assets/Development/Prefabs/` (Village/, Hand/, VFX/, UI/, Systems/).
- [ ] `Assets/Development/Data/Configs/` (пустая, под `.asset`).
- [ ] `Assets/Development/Scenes/`.
- [ ] `Assets/ThirdParty/`.

### 11.3 Конвертация материалов под URP
- [ ] После импорта **каждого** пака: Edit → Rendering → Materials → Convert Selected Built-in Materials to URP. Проверить scale (Synty часто ×100).

### 11.4 Boot-сцена
- [ ] `Assets/Development/Scenes/Bootstrap.unity`: `[Systems]` (заглушка `GameLifetimeScope` от VContainer `LifetimeScope`) + `[Bootstrap]` (`GameBootstrap`). Префаб `[Systems]` → `Prefabs/Systems/SystemsRoot.prefab`.

### 11.5 Базовые префабы-заглушки
- [ ] `GodHand.prefab` (хотя бы примитив, §4), `House_A.prefab`, `Villager_A.prefab`, `Throwable_Barrel.prefab` — SerializeField пустые, заполнят editor-скрипты после генерации классов CLI.

---

## 12. Что НЕ делать ДО CLI

- [ ] ❌ Не настраивать вручную SO-конфиги — классы сгенерит CLI, инстансы создаст editor-скрипт `Create Empty SO Set`.
- [ ] ❌ Не писать Controllers/Services с заглушками — CLI начнёт с чистого листа.
- [ ] ❌ Не закупать ассеты «на всякий случай» вне списка — пострадает консистентность стиля.
- [ ] ❌ Не редактировать `Assets/ThirdParty/<PackName>/` — доработка = копия в `Assets/Development/Art/` (правило CLAUDE.md).
- [ ] ❌ Не настраивать ragdoll жителям — в срезе его нет.

---

## 13. Чеклист «готов к запуску CLI»

- [ ] Unity 6 проект создан, URP, Input = Both, Git LFS работает.
- [ ] VContainer + Cinemachine 3.1 установлены.
- [ ] Структура `Assets/Development/` и `Assets/ThirdParty/` создана.
- [ ] Synty Fantasy Kingdom (или альтернатива) импортирован, материалы → URP.
- [ ] VFX-паки (JMO Free + Hovl Magic) импортированы.
- [ ] UI-пак импортирован, иконки 4 чудес найдены/сгенерированы.
- [ ] TMP Font Assets (Cinzel + Nunito) созданы.
- [ ] Mixamo-набор (7 анимаций) скачан, ретаргет на Synty проверен на одном персонаже.
- [ ] Сгенерированы: 5 превью уровней, тизер существа, фон меню.
- [ ] Музыка (2 трека) + базовый SFX-набор на месте.
- [ ] `Bootstrap.unity` + префабы-заглушки созданы.
- [ ] Палитры (база + Good + Evil) зафиксированы.
- [ ] `game-design.md`, `cli-workflow.md` лежат в `docs/`, `CLAUDE.md` — в корне.

✅ Все галочки — запускай CLI с Сессии 0 по `cli-workflow.md`.

---

## 14. Бюджет (итог)

| Категория | Минимум | Полный |
|---|---|---|
| **Деревня + жители** (Synty Fantasy Kingdom) | $30–40 (Humble/Fanatical) | $60–70 (Asset Store) |
| **VFX** (JMO Free + Hovl Magic) | $0 (только JMO Free) | $25–40 |
| **UI** | $0 (переиспользование купленного) | $0 |
| **Иконки** | $0 (Game-icons.net) | $0–15 (AI/пак) |
| **Рука бога** | $0 (CC-модель / примитив) | $0 |
| **Music + SFX** | $0 (Sonniss + Pixabay + Kenney) | $0–20 |
| **AI-подписка** (Midjourney/Flux, уже есть) | — | $10–30/мес |
| **Итого** | **~$30–40** | **~$85–130** |

Самый дешёвый проект в портфеле — почти всё критичное закрывается одним Synty-паком и переиспользованием UI/аудио-базы. Главная статья расходов времени (не денег) — настройка 13 VFX-префабов, закладывай на неё день-полтора.
