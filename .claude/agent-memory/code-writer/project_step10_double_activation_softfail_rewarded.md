---
name: project_step10_double_activation_softfail_rewarded
description: Шаг 10 (продолжение) — фикс двойной активации ключа (EntryStatus.Armed), новая семантика soft-fail (HasBlockingNeeds), окно rewarded-согласия LevelStalled с кулдауном пере-показа
type: project
---

Реализовано 2026-09-11 (решения владельца, три связанные задачи одной сессии). 427/427 EditMode
тестов зелёные (411 было + 16 новых: 10 LevelEventServiceTests, 3 LevelFlowServiceTests net,
3 LevelStalledPresenterTests).

**1. Двойная активация ключа — `Services/Village/LevelEventService.cs`**:
- Новый `EntryStatus.Armed` (между Pending и Active) — условие взведения (время/событие) наступило,
  но ключ (type, targetSiteIndex) занят ДРУГОЙ Active-записью. `Tick` её не трогает (тот же
  `default: continue`, что Resolved/Expired) — взводится только через освобождение ключа, не время.
- `ActivateEntry` теперь ПЕРЕД активацией проверяет занятость ключа
  (`IsKeyOccupiedByAnotherActiveEntry`, foreach по `_entries` кроме себя) — если занят, ставит Armed
  без публикации события, вместо создания второй Active-записи на тот же ключ. Метод переиспользуется
  и для повторной попытки — если ключ занят ДРУГОЙ Armed-записью, только что активированной в том же
  проходе, корректно остаётся Armed (никакого спецкода для этого случая не понадобилось).
- Новый `TryActivateArmedEntries(type, siteIndex)` — вызывается СРАЗУ после перевода блокирующей
  записи в терминальный статус: из `ResolveNeed` (после `Publish(NeedResolvedEvent)`) и из `Tick`
  (после `Publish(NeedExpiredEvent)` в ветке истечения). Гейт `_levelFinished` действует и здесь.
  Индексный for по снимку `_entries.Count` — та же реентерантная защита, что у `Tick`/OnVillagerStateChanged/OnNeedResolved.
- **Why**: сценарий блокера — Fireball создаёт рантайм-нужду Fire@0 ДО того, как скриптованная
  AfterVillagerTossed-запись на тот же ключ взводится броском; без фикса бросок создавал бы ВТОРУЮ
  Active-запись на тот же (type,siteIndex), что ломает инвариант "максимум одна Active на ключ",
  на котором держится `ResolveNeed`/`TriggerNeed`.
- **How to apply**: новый путь взведения (будущий TriggerMode) ДОЛЖЕН идти через `ActivateEntry` (не
  напрямую ставить `Status = Active`), иначе конфликт ключа не проверяется. Освобождение ключа =
  любая новая точка, где запись становится Resolved/Expired, должна звать `TryActivateArmedEntries`.

**2. Soft-fail семантика — `ILevelEventService.HasBlockingNeeds` + `LevelFlowService.Tick`**:
- Новое свойство (интерфейс + реализация): true только для Active-записи с `duration > 0` (истечёт
  сама) ИЛИ Pending-записи с `TriggerMode == AtTime` (наступит сама). НЕ считает блокирующими вечную
  Active-нужду (`duration <= 0`) и событийные Pending (AfterVillagerTossed/AfterPreviousResolved) —
  их разрешение зависит только от игрока/другого события, не от времени.
- `HasActiveNeeds`/`HasPendingNeeds` СОХРАНЕНЫ (не удалены) — `LevelEventServiceTests.cs` активно их
  тестирует напрямую (30+ ассертов), это легитимный потребитель по правилу оркестратора "оставь, если
  читает кто-то ещё". Только `LevelFlowService.Tick` переведён на `HasBlockingNeeds`.
- `LevelFlowServiceTests.cs`: `FakeLevelEventService` получил третье поле `HasBlockingNeeds { get; set; }`
  (старые два оставлены для совместимости с интерфейсом, но тесты стали выставлять только новое).
  Тест `Tick_HasPendingNeeds_DoesNotStall` УДАЛЁН (дублировал `Tick_HasActiveNeeds_DoesNotStall` после
  унификации в один флаг) — та переименована в `Tick_HasBlockingNeeds_DoesNotStall`.
- **Why**: на L01 (вечная нужда после первого броска + следующая запись ждёт разрешения предыдущей)
  старое условие `!HasActiveNeeds && !HasPendingNeeds` никогда не становится true — soft-fail
  недостижим навсегда. Уровни 2-5 (все записи AtTime, duration 15) не затронуты — зафиксировано
  тестом `HasBlockingNeeds_AtTimeEntryWithLevel2To5Duration_MatchesHasActiveNeeds`.

**3. Окно rewarded-согласия — `ILevelStalledView`/`LevelStalledView`/`LevelStalledPresenter` + `ILevelFlowService.AcknowledgeStallDeclined`**:
- View: новые `_questionText` (TMP_Text), `_declineButton` (Button), метод `SetQuestionText(string)`,
  событие `DeclineClicked`. Существующие `_refillButton`/`RefillClicked` — роль "согласие" сохранена
  как есть (не переименована). Editor-проводка (для параллельного editor-агента, контракт зафиксирован
  заранее): узлы `LevelStalledOverlay/Panel/QuestionText`, `.../DeclineButton`.
  Text: `"Вера иссякла! Посмотреть рекламу, чтобы пополнить веру?"` — const в презентере,
  TODO-заглушка на англ. локализацию через будущий `ILocalizationService`.
- Presenter: конструктор получил 4-й параметр `ILevelFlowService levelFlowService` (VContainer
  резолвит автоматически, GameLifetimeScope НЕ трогали — `LevelFlowService`/`LevelStalledPresenter`
  регистрируются в одном и том же гейте `_initialState == GameState.LevelPlaying`, см.
  `RegisterLevelFlow`/`RegisterLevelOverlays`). `OnDeclineClicked` → `_levelFlowService.AcknowledgeStallDeclined()` + `_view.Hide()`, БЕЗ рефила Faith.
- **Пере-показ после Decline**: `LevelFlowService` получил `AcknowledgeStallDeclined()` (сбрасывает
  `_isStalled = false`) + приватный кулдаун `_stallReshowCooldownRemaining` (`StallReshowCooldownAfterDecline = 5f`
  секунд, декрементируется каждый Tick). Гейт публикации `LevelStalledEvent` в `Tick` — `isStalledNow && !_isStalled && _stallReshowCooldownRemaining <= 0f`.
  Выбран этот вариант (не "ждать Faith>0→0"), потому что без кулдауна сброс защёлки на Decline
  мгновенно переоткрывал бы то же окно на СЛЕДУЮЩЕМ кадре (Faith обычно не меняется сразу после
  отказа) — кулдаун сохраняет "не спамить каждый кадр", позволяя честный пере-показ позже. Обычный
  сброс через Faith>epsilon (кнопка "Пополнить"/сторонний источник Faith) кулдаун НЕ трогает —
  `_stallReshowCooldownRemaining` остаётся 0, следующий Tick публикует немедленно (старое тестируемое
  поведение `Tick_FaithRestoredAboveEpsilon_ResetsStalled_AllowingStallAgain` не сломано).
- **Why**: без явного сброса защёлки `_isStalled` навсегда оставался бы true после Decline (если
  игрок больше никак не поднимет Faith выше эпсилона) — окно "Посмотреть рекламу?" никогда не
  показалось бы снова, даже если игрок передумает.
- **How to apply**: новая причина скрыть/показать LevelStalled-оверлей — расширять
  `OnFaithChanged`/`OnLevelStalled`/`OnDeclineClicked` в презентере, не трогая View напрямую
  (MVP — View только реагирует на явные вызовы Show/Hide/SetQuestionText).

Связано: [[project_step10_level_event_chain]] (тот же таймлайн-механизм, EntryState/TriggerMode),
[[project_step5_worship_win]] (LevelFlowService/LevelStalledEvent впервые появились).
