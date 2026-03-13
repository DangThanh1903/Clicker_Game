# ARealClickerGame - Engineering Guide (Current State)

## Scope
- This guide is scoped mainly to `Assets/DevWork` gameplay/runtime code unless otherwise stated.
- Scene/runtime wiring is considered only as needed to explain `DevWork` behavior.
- Third-party package internals are out of scope.

## Current Architecture (Observed)
- Runtime is manager-centric with many singletons (`Ins`/`Instance`), several using `DontDestroyOnLoad`.
- Content/balance is ScriptableObject-driven (blocks, items, buffs, monsters, weather, quests, dungeons).
- Combat input flow is centralized in `PlayerController`:
  - pointer/hold raycast + dispatch lives in `ProcessPointerInput`,
  - while holding input, hold dispatch can retarget to the currently raycast damage receiver,
  - non-pointer target selection (state tick) is resolved in `LateUpdate` through `IDamageTargetSelectionService` + `ITargetRegistry` (default runtime adapter wraps static `DamageTargetRegistry`),
  - state (`Normal`/`Hold`/`Idle`) dispatches via `IDamageReceiver.ApplyDamageInput(...)`,
  - damage target metadata lives in `IDamageReceiver` (`InputPriority`, `CanReceiveDamage`),
  - pointer-hit context is a separate capability `IPointerHitContext` (`SetPointerHit(...)`).
- `PlayerController` delegates combat resources to `PlayerCombatResourceService` (mana usage/regen, stamina usage/regen, idle stack/multiplier).
- `PlayerController` delegates hold-beam + idle-pet lifecycle to `PlayerCombatVfxService`.
- `PlayerController` owns `ClickPerTickService` as the single writer of `StatType.ClickPerTick` (`NotifyDamageHit()` is used for click/hold hit reporting only; idle damage uses `NotifyIdleDamageDealt(...)` for VFX feedback and does not increment click-per-tick).
- Target->player combat feedback is routed through `CombatFeedbackRuntime` (`ICombatFeedbackSink`) instead of direct `PlayerController.Instance` calls in block/monster/boss targets.
- Run fail signaling is routed through `PlayerRunLifecycleService` (reason-based events like boss timeout / dungeon fail), decoupled from player HP.
- Manager->player run fail notify is routed through `RunFailNotifierRuntime` (`IRunFailNotifier`) instead of direct `PlayerController.Instance` calls in `BlockManager` / `DungeonRunManager`.
- Idle stack now resets on state change via resource service (`SetState(...)` -> `OnStateChanged()`).
- Player HP regen/death subscription flow has been removed from `PlayerController`; player fail-state is no longer driven by `CurrentHP`.
- Pointer target resolve/camera raycast is separated behind `IPointerDamageTargetResolver` (default `PhysicsPointerDamageTargetResolver`).
- `PlayerController` runtime seam setters (`SetTargetRegistry`, `SetTargetSelectionService`, `SetPointerTargetResolver`) are non-null strict (invalid injection logs error in dev build).
- Runtime targets now register/unregister through `DamageTargetRegistrant` (component-level lifecycle hook) to static `DamageTargetRegistry`.
- `DamageTargetRegistrant` auto-resolves `IDamageReceiver` from the same GameObject (no serialized target source mapping).
- Target-side behavior remains mostly local in `ClickableObject`, `MonsterClickable`, `Boss` (damage side effects, VFX, analytics), while item grant side effects are shared through `DropGrantService`.
- Boss spawn ownership is in `BlockManager.Summon(...)` (inventory item use -> `InventoryController` -> `BlockManager`); legacy standalone `BossSpawner` has been removed.
- Boss encounter is now time-limited (`BossEntry.timeLimitSeconds`) and timeout fail is handled in `BlockManager` (despawn boss, unlock navigation, return block view). Boss timer setup is strict to `BossEntry` data (no local fallback time-limit path in `BlockManager`).
- Boss rewards are data-driven in `BossEntry.drops` and passed to runtime boss via `Boss.SetSpawnContext(...)`.
- Dungeon run is time-limited for the whole run and currently uses real-time countdown (`Time.unscaledDeltaTime`) for fail.
- Shared damage helpers now exist:
  - `DamageInputPowerResolver` (click/hold/idle power resolution),
  - `DamageTickAccumulator` (hold tick timing),
  - `DamageStatsRecorder` (damage/click stat writes).
- Shared reward helper now exists:
  - `DropRollService` (shared luck-aware roll evaluation from `ItemDrop` lists for block/monster/boss data),
  - `DropGrantService` (inventory add + quest signal + toast + optional addressable item resolve from `ItemDrop`).
- Runtime block drop flow uses `GetDropResultsByName(...)` / `GetDropResults(...)` path; older tuple-style block drop API was removed from the runtime path.
- Shared click-rate helper now exists:
  - `ClickRateTracker` (rolling hit-count window utility),
  - `ClickPerTickService` (single runtime owner writing `StatType.ClickPerTick`).
- `DamageInputPowerResolver` now reads combat resource data through `ICombatResourceReadModel` via `CombatResourceReadModelRuntime` binding (not direct `PlayerController.Instance` calls).
- If no read-model is bound, `DamageInputPowerResolver` now logs and returns zero-impact values (no hidden gameplay fallback path).
- UniRx is heavily used for reactive state; LeanPool is used in multiple hot paths.
- `Assets/DevWork` still has weak module boundaries (no local asmdef partitioning).

## Key Systems
- Combat loop: `PlayerController`, `ClickerState` variants, `ClickableObject`, `MonsterClickable`, `Boss`.
- Combat/shared utilities: `DamageInputPowerResolver`, `DamageTickAccumulator`, `DamageStatsRecorder`, `DropRollService`, `DropGrantService`, `ClickRateTracker`, `ClickPerTickService`.
- Block/content: `BlockUVDatabase`, block discovery/drop flow, block animation/fragment systems.
- Progression/meta: inventory, crafting, quests, dungeon run flow.
- World simulation: location/time/weather managers.
- Presentation: UI managers, VFX/SFX systems, toast/popup flows.
- Persistence: mainly `DataSaver` + quest-related persistence paths.
- Boss flow: `BossSO` data lookup + `BlockManager` spawn/despawn + location unlock on boss death.

## Current Technical Debt (Observed)
- God classes with mixed concerns remain (`PlayerController`, `ClickableObject`, `DataSaver`).
- High singleton coupling limits testability and lifecycle control.
- Damage formulas and side-effect orchestration are still duplicated across Block/Monster/Boss (despite unified dispatch entrypoint).
- Drop roll + grant are now split into two shared helpers (`DropRollService`, `DropGrantService`), but source-specific wrappers still remain in data classes (`BlockUVEntry`, `MonsterDef`, `BossEntry`).
- `DamageTargetRegistry` is still a static global list with manual compacting; lifecycle correctness depends on target `OnEnable/OnDisable` discipline.
- `PlayerController` now depends on `ITargetRegistry`, but default binding is still static-global via runtime adapter (seam exists, ownership/lifecycle still global).
- `DamageTargetRegistrant` is a dedicated lifecycle component, but registry storage itself remains static-global.
- `CombatResourceReadModelRuntime` is still a static runtime bridge; binding ownership is coupled to `PlayerController` lifecycle.
- `CombatFeedbackRuntime` / `RunFailNotifierRuntime` are static runtime bridges; they remove direct singleton calls but still keep lifecycle coupling at runtime bind/unbind boundaries.
- `PlayerCombatResourceService` is extracted but currently owned directly by `PlayerController` (not yet reusable/injectable across callers).
- `PlayerCombatVfxService` is extracted but currently owned directly by `PlayerController` (not yet shared/injected).
- `PlayerRunLifecycleService` is extracted but currently owned directly by `PlayerController`; fail dispatch still depends on managers calling `PlayerController.Instance`.
- `ClickPerTickService` is centralized under `PlayerController`; this removes target race writes, but it still depends on `PlayerController` lifecycle/binding.
- Target feedback still depends on `PlayerController.Instance` (`NotifyDamageHit`, `NotifyIdleDamageDealt`) from `ClickableObject` / `MonsterClickable` / `Boss` (runtime singleton coupling remains).
- Resolver unbound logs are one-shot but now available outside dev builds too; missing bind is visible but still runtime-coupled to lifecycle timing.
- Legacy `StatType.HP/CurrentHP` still exists in stats/UI ecosystem, but player combat fail no longer depends on that path.
- Spawn paths are strict about prefab contracts (`MonsterClickable`/`Boss` required on spawned prefabs), so prefab correctness is required.
- `DungeonRunManager` still contains legacy fallback run/stage/reward path mixed with profile-driven flow.
- Module boundaries are weak (limited namespaces, no `DevWork` asmdef segmentation).

## Preferred Direction (Incremental)
- Preserve current gameplay behavior while refactoring behind narrow contracts.
- Keep `PlayerController` as the only pointer input dispatcher (do not reintroduce per-target click detection).
- Continue extracting shared damage pipeline pieces, but keep target-specific side effects local.
- Keep using `DropGrantService` for inventory/toast/quest grant side effects; avoid reintroducing duplicated grant code in targets.
- Keep using `DropRollService` for luck/chance/amount roll evaluation; avoid reintroducing manual roll loops in data classes.
- Keep `ClickPerTickService` as the only writer of `StatType.ClickPerTick`; targets should only report hits.
- Keep click-per-tick semantics strict: only click/hold should report `NotifyDamageHit`; idle should stay on `NotifyIdleDamageDealt` feedback path.
- Keep target/manager dispatch through runtime bridge seams (`CombatFeedbackRuntime`, `RunFailNotifierRuntime`) and avoid reintroducing direct `PlayerController.Instance` calls.
- Consider unifying drop roll evaluation behind a narrow contract after parity checks (`BlockUVEntry`/`MonsterDef`/`BossEntry` currently each roll independently).
- Keep registry-based target tracking, then gradually reduce static-global coupling (move from static registry toward owned runtime registry service).
- Keep capability-based dispatch and avoid reintroducing concrete target checks in controller.
- Keep target lifecycle registration centralized in `DamageTargetRegistrant`; avoid reintroducing per-target registry glue.
- Keep `ICombatResourceReadModel` read path for damage resolution; later replace static bridge (`CombatResourceReadModelRuntime`) with explicit bootstrap/injection ownership when practical.
- Keep boss spawn logic centralized in `BlockManager` (do not reintroduce parallel boss spawner flows).
- Split `DataSaver` by responsibility (local storage vs cloud sync vs runtime apply) in small steps.

## Safe Refactoring Rules
- No full rewrite. Use staged, reversible refactors.
- One change-set should target one responsibility slice.
- Preserve save compatibility and existing keys unless a migration is explicitly implemented.
- Preserve gameplay invariants unless a task explicitly changes design:
  - click/hold/idle semantics,
  - stamina/mana interactions,
  - click-per-tick buff trigger semantics,
  - timer fail semantics for boss/dungeon,
  - luck/drop outcomes,
  - quest progression signals,
  - dungeon reward/exit flow.
- High-risk files (`PlayerController`, `ClickableObject`, `DataSaver`, `QuestManager`) require focused manual regression before merge.

## Recommended Patterns (Fit This Project)
- State pattern for click/hold/idle combat modes.
- Capability-style interface contracts (`IDamageReceiver`, `IPointerHitContext`) for dispatch and optional pointer context.
- Strategy for pluggable movement/animation behaviors.
- Observer/reactive streams (UniRx/events) for UI and progression updates.
- Facade boundaries around manager APIs before internal splits.
- Object pooling for frequent spawn/despawn objects.

## Known Uncertainties / Must Verify
- Actual boot/init ordering per build scene.
- Scene/prefab overrides for critical serialized flags (e.g., timer limits, layer/raycast setup).
- Scene/prefab coverage for `DamageTargetRegistrant` on all live `IDamageReceiver` objects.
- Persistence ownership boundaries between `DataSaver` and quest systems.
- Registry behavior under pooled object churn (duplicate register/unregister miss, stale targets).
- Whether `PlayerController` seam setters are exercised in runtime/tests or currently only present for testability.
- Whether "idle stack resets on state change" matches all design intents (newly enforced behavior).
- Whether all boss entries and active dungeon profiles now have intended time-limit values configured in content data (boss timer now has no local fallback in `BlockManager`).
- Whether all active boss entries have intended `drops` configured (or intentionally empty).
- Whether all click/hold damage paths call `PlayerController.NotifyDamageHit()` and idle paths remain intentionally excluded from CPT.
- Whether resolver unbound fallback path ever occurs during scene transitions/runtime disable-enable sequences.
- Remaining instantiate/destroy hotspots that should be pooled.
