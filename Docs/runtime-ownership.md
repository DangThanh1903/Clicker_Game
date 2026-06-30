# Runtime Ownership

Phase 1 decisions for `Assets/DevWork`.

## State owners

- `StatsManager`
  - runtime owner for `clicks`, `diamonds`, `totalBlockBreaked`, `totalDamageDealed`, `totalTimePlayed`
  - gameplay code reads/writes lifetime counters here during play

- `DataSaver`
  - persisted owner for player save data
  - caches and restores the lifetime stat snapshot
  - persisted owner for craft node states by biome

- `CraftNodeManager`
  - runtime owner for the currently active crafting tree
  - does not own cross-biome persistence

## Allowed globals

These are acceptable app-level globals for now:

- `DataSaver`
- `StatsManager`
- `LocationLoader`
- `TopNotificationManager`
- `PopupController`
- `LocalizationManager`

## Legacy globals

These still exist, but new code should not add more direct singleton coupling to them:

- `PlayerController`
- `QuestManager`
- `BlockManager`
- `InventoryController`
- `UIManager`

## Craft scope rule

When a biome crafting scope has no saved data yet, the tree must reset to that biome's default runtime state first. It must never inherit node states from the previously bound biome.
