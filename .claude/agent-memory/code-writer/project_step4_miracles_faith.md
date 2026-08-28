---
name: project_step4_miracles_faith
description: Шаг 4 (Чудеса + Faith) реализован — сервисы/конфиги/контроллеры/View, что уже существует и не нужно дублировать
type: project
---

Шаг 4 «Чудеса + Faith» (cli-workflow.md) реализован целиком (2026-08-28). Не путать со State
шагов 5-6: Heal/Lightning и Worship/Alignment ещё НЕ реализованы — их конфигов и веток в
`MiracleConfig`/`MiracleCatalogConfig` намеренно нет.

**Что уже есть (не дублировать)**:
- `Services/Hand/IHandModeService.cs` + `HandModeService.cs` — Grab/Miracle режим, публикует `MiracleSelectedEvent`.
- `Services/Faith/IFaithService.cs` + `FaithService.cs` — pure C#, реген от `IVillagerStateService.CountInState(Worship)`.
- `Services/Miracles/IMiracleService.cs` + `MiracleService.cs` — сессия каста (BeginCast/UpdateCast/ReleaseCast/CancelCast), НЕ Command.
- `Data/Configs/Miracles/MiracleConfig.cs` (маппинг нужд как данные: `ResolvesNeeds`, `CreatesNeed`, `CreatedNeed`, `BlastsVillagers`) + `MiracleCatalogConfig.cs`.
- `Data/Configs/Faith/FaithConfig.cs`, `Data/Configs/Vfx/VfxConfig.cs` + `VfxEntry.cs`.
- `Factories/Vfx/IVfxFactory.cs` + `VfxFactory.cs`, `Controllers/Vfx/VfxInstanceController.cs` (пул VFX, ключ — VfxEffectType).
- `Controllers/Miracles/MiracleTargetingController.cs` (превью+ввод каста), `MiracleImpactController.cs` (Fireball разбрасывает жителей через `VillagerController.ApplyBlast`), `Controllers/Vfx/MiracleVfxController.cs`.
- `Views/UI/Hud/*` — первый MVP UI проекта: `IMiraclePanelView`/`MiraclePanelView`/`MiracleButtonView`/`MiraclePanelPresenter`, `IFaithBarView`/`FaithBarView`/`FaithBarPresenter`.
- `IInputProvider.IsPointerOverUi` — единый источник "pointer над HUD", реализован в `PointerInputProvider` через `EventSystem.current.IsPointerOverGameObject(...)`.
- `VillagerStimulus.Blasted` → Tossed (та же ветка, что Grabbed, в `VillagerStateService.TryResolveTarget`).
- `IVillagerStateService.CountInState(state)`, `ILevelEventService.TriggerNeed(type, siteIndex, duration, byPlayer)` — рантайм-нужда поверх таймлайна.
- `NeedTriggeredEvent`/`NeedExpiredEvent` теперь несут `bool ByPlayer` (третий параметр конструктора) — `NeedResolvedEvent` уже имел его с шага 3.
- `VillagerMovementRules.ComputeBlastVelocity(origin, position, power, upwardBias)` — направление от origin, вертикальная примесь через upwardBias.

**Why**: избежать дублирования при шагах 5-6 (Worship/Alignment, Heal/Lightning) — читать этот файл
и существующий код перед добавлением новых чудес/нужд.

**How to apply**: новое чудо (Heal/Lightning, шаг 6) — новый `MiracleConfig`-инстанс + запись в
каталог, НЕ новый код в `MiracleService` (game-design §14.2, "новое чудо в существующем шаблоне —
1 конфиг"). Новый тип нужды — так же, через данные `MiracleConfig`, не ветки в `NeedSiteController`.

Связано: [[project_dedicated_editor_agent_contract]] (если будет заведена) — контракт имён между
code-writer и editor-script-creator в этом проекте передаётся через промпт оркестратора дословно,
без отдельного файла памяти на каждый шаг.
