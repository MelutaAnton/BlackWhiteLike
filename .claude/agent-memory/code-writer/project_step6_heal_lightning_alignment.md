---
name: project_step6_heal_lightning_alignment
description: Шаг 6 (Heal + Lightning, нужды Disease despawn, alignment-визуал) реализован — что уже есть и не нужно дублировать
type: project
---

Шаг 6 (cli-workflow.md) реализован 2026-09-01, в паре с editor-скриптами (контракт имён — в промпте
оркестратора, не в отдельном файле памяти). Heal — ноль нового кода (уже отрабатывается
`NeedSiteController.OnMiracleCast` через `ResolvesNeeds=[Disease]`). Lightning поджигает здание —
тоже чистые данные (`CreatesNeed=Fire` в конфиге), код не писался.

**Что уже есть (не дублировать)**:
- `Models/VillagerStimulus.LightningStruck` — мгновенный Stun, БЕЗ физического отброса (в отличие
  от `Blasted`, который ведёт в Tossed). `Models/TerrorAct.VillagerStruckByLightning` — отдельная
  ставка Fear от `LightningStrike` (поджог дома), тот же прецедент, что VillagerBlasted/
  FireballOnStructure. `Models/VfxEffectType.HealCast`/`LightningStrike`.
- `MiracleConfig.StrikesVillagers` (bool) — параллельно `BlastsVillagers`, но без Tossed/физики.
- `VillagerStateService.TryResolveTarget` — `LightningStruck`: из Tossed запрет (target=current,
  false), из Stunned self-loop (target=Stunned, true — продление таймера, WorshipService не
  начисляет Fear повторно, т.к. self-loop не публикует событие), из Idle/Panic/Worship → Stunned.
- `WorshipService.OnVillagerStateChanged` — ветка `evt.To == Stunned && evt.Cause ==
  LightningStruck` СРАЗУ после блока `To == Tossed`, ДО проверки `From != Tossed` (Lightning не
  проходит через Tossed вообще, это отдельный путь входа в Stunned).
- `MiracleImpactController.OnMiracleCast` — рефакторен: `if (!BlastsVillagers && !StrikesVillagers)
  return;`, отдельный `if (config.StrikesVillagers)` блок — OverlapSphere по evt.Radius →
  `ReportStimulus(LightningStruck)` (без `ApplyBlast`). AoE-паника (`ExplosionNearby` по
  PanicRadius) выполняется для ОБОИХ видов воздействия безусловно.
- `NeedSiteController` — новая зависимость `IVillagerFactory` в Construct; `OnExpired` для
  `NeedType.Disease` вызывает `DespawnVillagersNearby()` (OverlapSphere по PanicRadius, пропускает
  Tossed, `_villagerFactory.Despawn(villager)` для остальных) — жители выбывают до конца уровня
  (решение владельца 2026-09-01, ветка по NeedType — правило дизайна таблицы §5, не маппинг чуда).
- `Data/Configs/Alignment/AlignmentVisualConfig.cs` (namespace `BlackWhiteLike.Data.Configs.Alignment`)
  — `AlignmentStage` struct (ScoreThreshold, HandMaterials[], TempleMaterials[],
  DecalPrefabsToSpawn[] — задел, не обрабатывается; VfxToActivate[] — задел, не обрабатывается),
  `AlignmentUiPalette` struct (5 Color-тонов, дефолт White, `UiPalette` property на конфиге —
  **пока никем не читается**, применение к HUD отложено). `GetStageFor(score)` — ленивая сортировка
  по убыванию ScoreThreshold с кэшем (`_sortedCache`/`_cacheValid`, инвалидация в OnValidate),
  Debug.LogWarning если порядок в инспекторе был нарушен.
- `Controllers/Temple/AlignmentVisualController.cs` (НОВЫЙ под-неймспейс `Controllers.Temple`) —
  `_handRenderers`/`_templeRenderers` (Renderer[]), подписка на `AlignmentChangedEvent`, применяет
  стейдж через `renderer.sharedMaterial(s)` (НЕ `.material` — не плодит инстансы). Стартовый
  стейдж (score=0f, т.к. `AlignmentService._score` стартует с 0 и событие для старта не
  публикуется) применяется явно в Construct. No-op если стейдж не менялся (сравнение по
  ScoreThreshold). При несовпадении числа слотов рендерера и материалов стейджа — первый материал
  на все слоты + LogWarning.
- `GameLifetimeScope` — `_alignmentVisualConfig`/`_alignmentVisualController`, метод
  `RegisterAlignmentVisual` (условно, ТОЛЬКО если оба != null — `RegisterInstance` конфига +
  `RegisterComponent` контроллера), вызывается из `Configure` после `RegisterLevelFlow`.

**Why**: избежать дублирования при будущих шагах (декали/VFX по alignment-стейджу, UI-тинт по
AlignmentUiPalette, новые чудеса) — читать этот файл перед новым визуалом/чудом/нуждой.

**How to apply**: новый VFX/декали стейджа alignment — заполнить существующие поля
`DecalPrefabsToSpawn`/`VfxToActivate` в ассете + дописать обработку в `AlignmentVisualController`
(сейчас YAGNI-заглушка). UI-тинт по alignment — читать `AlignmentVisualConfig.UiPalette` из нового
HUD-потребителя, сам конфиг менять не нужно. Новый стимул с мгновенным входом в существующее
состояние (минуя промежуточное, как LightningStruck минует Tossed) — ветка в
`WorshipService`/`AlignmentService.OnVillagerStateChanged` СТАВИТСЯ ПЕРЕД проверкой `From !=
Tossed`, иначе будет молча проигнорирована (по построению того metod'а).

Связано: [[project_step5_worship_win]] (WorshipService/AlignmentService, FSM-переходы, тот же
`_tossedCauseByVillager` паттерн), [[project_step4_miracles_faith]] (MiracleConfig — маппинг нужд
как данные).
