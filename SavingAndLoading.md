# Save and load maintenance guide

This guide records how the current Unity save/load flow works and how to extend it safely. Keep it updated whenever the snapshot schema, capture/restore order, or known persistence bugs change.

## Current flow

- The in-game **Save Game** action calls `GameSaveManager.TrySaveCurrentGame("default")`.
- Saves are JSON files at `Application.persistentDataPath/Saves/default.json`.
- The Main Menu enables **Load Game** only when `GameSaveManager.HasLoadableSave()` can read and validate the default slot. Selecting it stages the snapshot, then loads `MainGame`.
- `MapManager.Start()` creates its normal initial world, then waits one frame before asking `GameSaveManager` to restore a staged save. Restoration replaces the generated world in this order:
  1. `MapManager.RestoreSavedWorld`: roads, map resources and their remaining yields, and community storage.
  2. `Player.RestoreSavedGame`: player resource balances and recreated villagers.
  3. `GameSaveManager.RestoreHomes`: personal houses, then each villager's home-based or separate job workplace.
- A new game clears any staged load before entering `MainGame`.

### WebGL

The save code already uses `Application.persistentDataPath` and `System.IO`; Unity maps `persistentDataPath` to browser IndexedDB storage for Web builds. This means the default JSON slot can use the existing file-based flow without a separate save format or backend.

WebGL saves are local to the browser's site storage, not files automatically downloaded to the player's computer. Unity derives the storage path from the URL directory, so keep the game's deployed origin and URL path stable if users should see the same saves after updates. Browser storage can be cleared or unavailable in private/restricted browsing modes.

Validate a WebGL build from its deployed URL by saving, refreshing, confirming Load Game is enabled, and loading the save. Also close and reopen the page to check persistence across sessions. If the deployed build does not retain writes across refresh, investigate the Unity version/template's IndexedDB filesystem synchronization before changing the snapshot format.

Key implementation files:

| File | Responsibility |
| --- | --- |
| `Assets/Script/Global/GameSaveManager.cs` | Snapshot DTOs, file I/O, version migration, validation, capture and restoration orchestration |
| `Assets/Script/Global/Player.cs` | Player balances and villager recreation/restoration |
| `Assets/Script/Global/MapManager.cs` | World/map restoration, plot registration, workplace reservations and construction sites |
| `Assets/Script/2DObjectComponents/Villager.cs` | Villager state, home restoration, inventory/equipment and deferred initialization |
| `Assets/Script/2DObjectComponents/VillagerJobController.cs` | Job input, workplace stages/timers and production work |
| `Assets/Script/2DObjectComponents/HouseWorkplace.cs` | Workplace inventory and production progress |
| `Assets/Script/UI/GamePanelController.cs` | In-game Save Game button |
| `Assets/Script/UI/MainMenuController.cs` | Load availability and scene transition |

## State currently saved

- Player resource balances.
- Villager identity and appearance, position/scale, health, hunger/fatigue, job assignment, personal inventories, equipment, and job input stock.
- Personal home plot and house definition, construction state and elapsed/hammer timers, stored materials, annex state, and workplace inventory/production progress.
- A job workplace that is either the villager's home or a separate house. Separate workplace plots preserve their selected definition, stored materials/items, completion and annex status, workplace stage, construction elapsed/hammer timers, and production progress.
- Resource-object definition, grid cell/position, rotation/local scale, and each resource's remaining and bare-hand amounts.
- Community-storage location, capacity, and stored resources.
- Road keypoint paths.

House visuals and map registrations are rebuilt from saved IDs, grid data, current definitions, and prefab references. The save does not serialize Unity object references.

## Extending the save format

When adding a runtime feature, decide which component owns its live state first. Keep configuration in `GameData`/JSON and save only mutable runtime state.

1. Add serializable fields/records to the `GameSave*` DTOs in `GameSaveManager.cs`. `JsonUtility` requires serializable fields and works best here with arrays/wrapper records; store enum values as exact member-name strings.
2. Add capture logic that reads state from its owner. Keep derived/configuration data out of the snapshot where a stable ID is sufficient.
3. Add validation for missing records, enum names, numeric ranges, duplicate entries, and referenced definitions/assets. Invalid data should fail with a clear error rather than silently defaulting.
4. Restore through the owning component/service after its dependencies are initialized. Restore world/map registrations and reservations as well as visuals; restoring only the GameObject does not restore pathfinding blockers or ownership.
5. Preserve the correct initialization order. If a component may not have run `Start()`, defer restoration with an explicit pending state, as `Villager` does.
6. If the serialized shape or meaning changes, increment `CurrentSaveVersion` and add a migration for the immediately previous version. Migrations should be sequential and idempotent for their input version. Validate migrated snapshots with the current validator.
7. Update this guide's coverage list, version/migration notes, and bug/fix log.

Current schema: **version 5**. Version 2 snapshots are upgraded sequentially to versions 3, 4, and 5; version 3 snapshots are upgraded to versions 4 and 5; version 4 snapshots are upgraded to version 5. Other unsupported versions are rejected. Version 5 stores resource-object rotation and local scale. Version 4 did not store these values, so migrated saves use the prefab's default transform for those missing values.

For ongoing work, test both a fresh save and an older supported save. Include edge states (for example, before construction starts, during each construction stage/break, during annex renovation, and with partially completed production) rather than testing only completed buildings.

## Bug and fix log

| Status / version | Bug or gap | Fix / notes |
| --- | --- | --- |
| Resolved, save/load flow | Load availability and world restoration were incomplete. | The menu validates the default save before enabling Load Game; the save is staged and restored after map startup. |
| Resolved, versions 2–3 | Construction and production could not resume from the exact in-progress point. | Homes, workplace construction stages/timers, job input stock, and production elapsed time/duration are captured and restored. Version 2's fractional production progress is migrated to seconds. |
| Resolved, version 4 | Separate job workplaces and annex renovations were not restored with their active stage or plot ownership. | Reused `GameSaveHome` for workplace building data; added workplace stage/timer state and map registration for unstarted/started workplace plots. |
| Resolved, version 4 | Unity JSON deserialization represented null nested home/workplace records as empty objects, making valid saves fail validation after returning to the menu. | Normalize empty placeholders after reading, and validate newly captured snapshots before writing. Regression case: save a world with villagers who have no separate workplace, then load it. |
| Resolved, house restoration | Finished houses retained the green claimed-plot outline after loading. | `HouseVisual.ShowFinishedHouse` now disables the plot `LineRenderer`, matching construction-stage rendering. Regression case: load a save containing completed homes and separate workplaces. |
| Resolved, versions 4–5 | On loaded games, a resource object's first hit could snap its artwork to a different position because load applied the visual offset after initialization, unlike new-game placement. Rotation and scale were also absent from saves. | Both paths now share resource instantiation ordering (transform, visual offset, then initialization); version 5 saves rotation and local scale. Regression case: load trees and rocks with different saved rotations/scales, then hit each once and confirm artwork and sorting remain stable. |

When a new bug is found, append an entry with its affected version, reproducible state, expected/actual result, fix, and regression scenario. If it is still open, mark it **Open** and record a workaround only if one is known.

## Known gaps and validation

- Villager path/current routine, animation, and transient decision timers are not restored. Loading resumes persistent simulation state, not the exact visual frame or path.
- Resource-object rotation and scale, camera state, and UI selection state are not currently in the snapshot.
- Saved IDs and grid state are interpreted using the current game definitions/settings. Changing house definitions, job definitions, or balance settings can affect old saves; add explicit migrations when a data change requires one.
- There is no dedicated automated save/load test suite. Unity compilation, scene loading, and gameplay round-trip tests have not been performed in this environment.

Before calling a persistence change complete, check source/JSON consistency without invoking Unity, then test in the Unity Editor: save a world with the feature both idle and mid-progress, return to the menu, load, and verify state, ownership, visuals, blockers, and continued progress.
