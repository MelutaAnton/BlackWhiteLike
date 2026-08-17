---
name: review-format-and-scope
description: How the owner wants code reviews delivered on this project — triage levels, no self-fixing, honest verification over checklist ticking
metadata:
  type: feedback
---

Deliver findings as a flat list ordered by descending severity, tagged **блокер / важно / на будущее**, each with file:line. Never fix anything during a review — the owner routes fixes back through `code-writer`.

**Why:** the project runs an orchestrator workflow (CLAUDE.md) where `code-reviewer` and `code-writer` are separate roles; a reviewer that edits files breaks the hand-off and the owner loses the diff boundary. He also explicitly asks to "проверь ЧЕСТНО" — i.e. he wants the reasoning that a rule actually holds in the given code path, not a restatement that the code contains the required pattern (e.g. "flag is reset before early returns" is only half the check; the other half is whether a down+up pair inside one physics tick survives).

**How to apply:** for every checklist item, trace the concrete failing input before declaring it safe or broken. When the owner pre-labels something as a conscious deviation, assess the *risk and its blast radius in later steps* — do not re-litigate it as a violation. See [[step2-accepted-deviations]].
