---
name: step2-accepted-deviations
description: Two deviations from docs/game-design.md that the owner consciously accepted during step 2 (Камера + рука) — do not re-flag as violations
metadata:
  type: project
---

Owner accepted (2026-08-18, step 2 review) two departures from `docs/game-design.md`:

1. `ObjectGrabbedEvent` / `ObjectThrownEvent` are published by `GodHandController`, although the §13 event table names the service as publisher. Reason: `HandInteractionService` is deliberately pure C# without `IEventBus`.
2. `ThrowableObjectController.Construct` takes `(IEventBus, HandConfig)` instead of `(IEventBus)`, so the landing threshold can come from the config.

**Why:** both follow from the project's own harder rule — services stay pure C# and testable without Unity, and no magic numbers in controllers. The doc was written before that constraint was applied to the hand stack.

**How to apply:** treat as settled. If a review touches them, report only *drift risk* — e.g. a second `IGrabbable` implementation appearing in step 3 that would need its own threshold (HandConfig turning into a grab-bag), or a second grabber that would duplicate the publish site. Do not report them as CLAUDE.md/game-design violations. Review-delivery conventions: [[review-format-and-scope]].
