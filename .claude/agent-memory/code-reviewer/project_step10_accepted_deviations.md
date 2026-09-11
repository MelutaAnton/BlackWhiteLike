---
name: step10-accepted-deviations
description: Owner-preapproved design decisions for step 10 (NeedTriggerMode event chain in LevelEventService, tutorial, localization) — assess risk only, never re-litigate as violations
metadata:
  type: project
---

Owner pre-accepted (2026-09-11, step 10 fix review) — do NOT report these as violations, only as drift risk:

- **`LevelEventService.Tick` НЕ гейтуется флагом `_levelFinished`.** Гейт стоит только в `OnVillagerStateChanged`/`OnNeedResolved`. Значит, после `LevelCompletedEvent` AtTime-записи (уровни 2–5 целиком) продолжают взводиться и истекать. **Why:** владелец ограничил фикс событийными путями. **How to apply:** упоминать максимум как незначительную асимметрию.
- **Условие `LevelStalled` в `Services/Flow|Level/LevelFlowService.cs` и семантика `HasPendingNeeds` — отдельное решение владельца.** С цепочкой уровня 1 (`duration = 0`, последняя нужда Active вечно) стал недостижим и stalled, и «все нужды отработали». **Why:** владелец явно вывел LevelFlowService из скоупа правок шага 10. **How to apply:** не трогать и не предлагать правку; только предупреждать, если diff туда полез.
- **`NeedTriggerMode`-семантика:** `duration <= 0` = бесконечная нужда для ЛЮБОГО режима; `triggerTime` читается только при `AtTime`; все Pending-записи `AfterVillagerTossed` взводятся ОДНОВРЕМЕННО первым броском (валидатор предупреждает, если их >1 или единственная не первая); `EntryState.Previous` — ссылка на объект (i-1 в исходном timeline), а не копия ключа (type, siteIndex) — это фикс блокера, ключевое сопоставление больше не считается корректным.
- **`VillagerTossRules`/`VillagerPoseRules`/`PinchGestureRules` — статические предикаты без интерфейса** (см. [[false-positive-static-rules-tables]]).
- **Реентерантность в `LevelEventService`:** обработчики шины и `Tick` обязаны идти индексным `for` по снимку `_entries.Count`; `ResolveNeed`/`TriggerNeed` остаются на `foreach` и безопасны только потому, что `Publish` там стоит непосредственно перед `return`/в конце метода — при любой правке этих методов проверять заново.
