---
path: "**/Scripts/{Controllers,Core/Input}/**"
---

## Известные нюансы ввода и физики

### One-shot ввод сгорает при потреблении из FixedUpdate (КРИТИЧНО)

Покадровое событие ввода истинно ровно один render-кадр: в новом Input System это
`wasPressedThisFrame` / `wasReleasedThisFrame` (в legacy было `Input.GetKeyDown`,
он у нас запрещён — Active Input Handling = New, см. CLAUDE.md). При высоком FPS
(рендер 200+ Гц, физика 50 Гц) между двумя кадрами часто выполняется **ноль**
`FixedUpdate`-тиков — нажатие, прочитанное в `Update`/`Tick()` и проверяемое
из `FixedUpdate`, безвозвратно сгорает. Симптом: кнопка «иногда не срабатывает»,
тем чаще, чем мощнее машина пользователя. Continuous-ввод (`isPressed`, оси)
проблемы не имеет — только Down/Up-события.

В нашем коде такие сигналы отдаёт `IInputProvider` (`PointerDown`, `PointerUp`,
`PausePressed`); их one-shot-природа зафиксирована в xml-doc интерфейса.

Правило: любое одноразовое нажатие, которое потребляет физика (импульс, прыжок,
буст), буферизуется флагом в `Update` и сбрасывается в `FixedUpdate` после
потребления:

```csharp
private void Update()
{
    // Копим нажатие до ближайшего физического тика.
    if (_input.JumpPressed) _jumpQueued = true;
}

private void FixedUpdate()
{
    bool jump = _jumpQueued;
    _jumpQueued = false;                 // сброс сразу, до ранних return
    if (jump && CanJump()) ApplyJump();  // потребление — только если действие легально
}
```

Осторожно с очередью: сбрасывать флаг, пока действие невозможно (например,
персонаж ещё не в нужном состоянии), иначе нажатие «до» прожжёт
лимитированный ресурс «после».
