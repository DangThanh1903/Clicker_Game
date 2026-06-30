# Save Persistence

Phase 2 decisions for `Assets/DevWork`.

## Main path

- `SaveCoordinator`
  - the only local JSON file gateway in runtime code
  - owns path resolution, JSON read/write, and delete

- `PlayerProfileRepository`
  - repository for `local_save.json`
  - used by `DataSaver`

## DataSaver role

- remains the temporary facade via `DataSaver.Ins`
- owns runtime-to-save orchestration for:
  - gameplay state
  - player profile
  - inventory payload
  - craft state by biome
  - biome progress

## Section helpers

- `LifetimeStatsSection`
- `InventorySaveSection`
- `CraftSection`
- `BiomeProgressSection`

These helpers only map section data. They do not touch files directly.

## Rule

Runtime systems must not call `File.ReadAllText`, `File.WriteAllText`, `File.Delete`, or build `Application.persistentDataPath` paths directly.
They must go through `SaveCoordinator`.
