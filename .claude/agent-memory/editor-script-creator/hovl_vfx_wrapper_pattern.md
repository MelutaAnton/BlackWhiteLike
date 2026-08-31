---
name: hovl-vfx-wrapper-pattern
description: How to safely wrap Hovl Studio (RPG VFX Bundle) ThirdParty prefabs as pooled Vfx_* prefabs, incl. known self-managing MonoBehaviours to disable
metadata:
  type: project
---

Шаг 6 (Heal + Lightning) добавил два VFX-обёрточных префаба поверх Hovl Studio "RPG VFX Bundle
v6.0.3" эффектов: `Vfx_HealCast.prefab` ← `RPG VFX Bundle/Random effect prefabs/Buff heal.prefab`,
`Vfx_LightningStrike.prefab` ← `AOE Magic spells Vol.1/Prefabs/Lightning strike.prefab`. Обёртка —
тот же приём, что `Vfx_FireballExplosion` (CFXR): `EditorPrefabUtility.InstantiateUnpacked` +
`VfxInstanceController` (`_particleSystem`/`_maxLifetime`), обобщённый в
`MiracleVfxPrefabSetupEditor.SetupWrappedThirdPartyVfxPrefab`.

Hovl-паки НЕ имеют единого `clearBehavior`-подобного поля (в отличие от CFXR). Вместо этого несут
вспомогательные `MonoBehaviour`, найденные на `Lightning strike.prefab` (namespace `Hovl`, не
подключён к `BlackWhiteLike.Editor` asmdef — искать по `GetType().Name`, тот же приём, что для
`CFXR_Effect`):
- `HS_EffectSound` (пакет "RPG VFX Bundle", `HSFiles/Scripts/HS_EffectSound.cs`) — `InvokeRepeating`
  зацикленный звук через собственный `AudioSource`, не контролируется нашей аудио-системой.
- `HS_ParticleCollisionInstance` (пакет "AAA Stylized Projectiles Vol.1", тот же `Hovl` namespace,
  переиспользуется другими Hovl-паками по guid) — спавнит доп. эффекты по столкновению частиц через
  собственный статический пул/`GameObject.Find("[PS_Effect_Pool]")`; `DestoyMainEffect` по умолчанию
  `false` (сам объект не самоуничтожается), но логика избыточна для нашего сценария, где жизненным
  циклом уже управляет `VfxInstanceController`.

Оба **отключаются** (`behaviour.enabled = false`), не удаляются (удаление сломало бы внутренние
ссылки самого ThirdParty-префаба, например `EffectsOnCollision`). Хелпер
`DisableKnownHovlBehaviours(GameObject, ownerLabel)` в `MiracleVfxPrefabSetupEditor.cs` — при
добавлении новых Hovl-эффектов проверять их компоненты на эти два типа (или новые из того же
паттерна) через `Debug.Log`, что нашёл/отключил, а не молчать.

`Buff heal.prefab` (Heal) не несёт ни одного MonoBehaviour — чистые ParticleSystem, обёртка без
доп. правок.
