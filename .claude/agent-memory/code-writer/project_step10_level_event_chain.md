---
name: project_step10_level_event_chain
description: Шаг 10 (продолжение) — событийная цепочка таймлайна уровня 1 (NeedTriggerMode), общий VillagerTossRules; что уже есть, куда расширять новым режимом взведения
type: project
---

Реализовано 2026-09-11 (решение владельца): уровень 1 — здание загорается только после первого
броска жителя рукой, нужда без ограничения времени пока игрок не разрешит, следующая запись
таймлайна взводится по разрешению предыдущей. 407/407 EditMode тестов зелёные (397 было + 10 новых).

**Что уже есть (не дублировать)**:
- `Models/NeedTriggerMode.cs` — enum `AtTime` (0, дефолт)/`AfterVillagerTossed`/`AfterPreviousResolved`.
- `Data/Configs/Levels/NeedEventEntry.cs` — единственное инкапсулированное поле структуры (остальные
  публичные без `_`, контракт game-design §6): `[SerializeField] private NeedTriggerMode _triggerMode;`
  + свойство `TriggerMode` (get/set — сеттер публичный специально, чтобы тесты собирали record через
  object initializer без reflection). `triggerTime` валиден только при `AtTime`; `duration <= 0` —
  нужда не истекает НИКОГДА, для любого `TriggerMode` (проверяется общим `if (duration > 0f && ...)`
  в `LevelEventService.Tick`, не по режиму — работает и для рантайм-записей `TriggerNeed`, бонусом).
- `Services/Village/VillagerTossRules.cs` — статический хелпер без состояния (прецедент
  `VillagerPoseRules`), единственный источник предиката "брошен именно рукой":
  `IsTossedByHand(VillagerStateChangedEvent) => To == Tossed && Cause == Grabbed`. Раньше жил только
  как приватная проверка в `TutorialService.OnVillagerStateChanged` — вынесен, когда понадобился
  ВТОРОЙ потребитель (`LevelEventService`). Namespace `Services.Village` (не `Models`!) — предикат
  завязан на `VillagerStateChangedEvent` из `Core.Events`, а `Core.Events` уже зависит от `Models`
  (Events.Village.cs `using BlackWhiteLike.Models;`) — класть хелпер в `Models` означало бы обратную
  зависимость Models→Core.Events, хоть и физически компилируется (один asmdef на весь Development/Scripts).
  `TutorialService` (в `Services.Tutorial`) уже импортировал `Services.Village` ради
  `INeedSiteRegistry` — направление зависимости не новое.
- `LevelEventService` теперь `IDisposable` (раньше не был), подписан на `VillagerStateChangedEvent` +
  `NeedResolvedEvent` (паттерн подписки как у `WorshipService`) — регистрация в `GameLifetimeScope`
  обновлена на `.As<ILevelEventService, ITickable, IDisposable>()`. Общая точка активации записи —
  приватный `ActivateEntry(EntryState, float initialTimeSinceTriggered = 0f)`: Tick(AtTime) передаёт
  реальный лаг `_elapsed - triggerTime` (Н1 ревью шага 3), событийные режимы — 0 (взведение и есть
  "сейчас"). `EntryState.PreviousType`/`PreviousSiteIndex`/`HasPreviousKey` — "предыдущая" запись по
  ПОРЯДКУ В ИСХОДНОМ МАССИВЕ (`timeline[i-1]`), вычисляется один раз в конструкторе (не индексным
  поиском в рантайме) — рантайм-записи `TriggerNeed` добавляются в хвост `_entries` и не должны
  сдвигать чужие "предыдущие". Первая запись таймлайна с `AfterPreviousResolved` — конфигурационная
  ошибка, `Debug.LogWarning` + запись остаётся Pending навсегда (не exception).

**Why**: избежать повторного дублирования предиката VillagerTossedByHand при третьем потребителе
(следующий уровень с похожим онбордингом/скриптовкой) и не изобретать новый TriggerMode, если нужный
уже есть.

**How to apply**: новый режим взведения (например, "по количеству разрешённых нужд определённого
типа") — новое значение `NeedTriggerMode` + ветка в конструкторе `LevelEventService` (если нужно
что-то посчитать один раз, как `PreviousType`/`PreviousSiteIndex`) + новый обработчик подписки (или
расширение существующего `OnNeedResolved`/`OnVillagerStateChanged`) + `ActivateEntry` переиспользуется
как есть. Третий потребитель `VillagerTossRules.IsTossedByHand` — просто новый `using
BlackWhiteLike.Services.Village;`, класс без интерфейса и без DI-регистрации.

Связано: [[project_step10_tutorial_localization]] (тот же шаг, откуда исходно взят предикат
VillagerTossedByHand — `TutorialService.OnVillagerStateChanged` теперь тоже зовёт `VillagerTossRules`).
