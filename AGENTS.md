# Project Instructions

## Project direction

Independent Kingdoms is intended to be an unusual, mostly autonomous city builder inspired loosely by *Banished*. Villagers—not the player—should identify needs, choose work based on their personalities and the town's needs, gather resources, and build homes and other structures. The simulation is intended to include resource production and trade, aging, family formation and children, death, and monster attacks that encourage defense and equipment production.

The player is a town-level influence rather than a direct building manager: the player can inspect resources, spend mana to create roads, issue broad orders and warnings, and buy upgrades or skills that benefit the town. Villagers should generally navigate and establish structures around the player's roads. Treat these as design goals, not features that are already implemented.

## Current state at a glance

The active prototype is a Unity 2D project. `Assets/Scenes/MainGame.unity` is the primary scene; `Assets/Scenes/SampleScene.unity` is also present. The current gameplay foundation includes initial villager creation and visual customization, simple movement and road wandering, map resource placement, road drawing, mana regeneration, and some resource and villager UI.

Villagers have an initial housing loop: they seek a road, enter a FIFO plot-claim queue, claim an available grid footprint beside a road, gather configured resources from registered map resources into their personal resource inventory, carry the required materials back to the plot, and deposit them before construction starts. `buildStage1` and `buildStage2` show the two construction phases; villagers hammer at the plot, take a configured break between phases, then finish a randomly selected house using its base and low-roof sprites. Per-house visual scale and x/y offset, plus per-stage/per-part color palettes, are configurable in `HouseDefinitions.json`. Plots block pathfinding and reject roads from the start of construction through completion; claims that have not reached stage one can still be canceled by roads. House sprites are assembled at runtime from `BuildingParts`, so no house prefab is required. House visuals use the same `LowObjects` SortingGroup pattern as the mover body prefab, with internal sprite orders for layered parts. Each villager starts with an empty inventory and `VillagerWeaponType.None`. The item inventory/equipment API can add/remove item stacks and equip an axe when one is in inventory, but axe production and distribution are not implemented. A bare-handed villager can gather at most the configured `bareHandAmount` from a resource; an equipped axe permits gathering its remaining `fullCutAmount`. Gathering is a repeating hit-and-pause loop with a script-driven arm motion and resource shake. `ResourceDefinitions.json` configures harvest amounts and each resource's grid-cell `interactionOffset`; for example, Wood currently uses `(0, 1)` so villagers approach the cell above the tree. Gathering timing/hand positions, staged house construction timings/hammer motion, and resource shake tuning are in `GameSettings.json`. House part definitions, house scale, offsets, colors, and construction sprite-sheet names are in `HouseDefinitions.json`. Each initial villager's `Person.reactionTime` is randomly set from the configured 0.5–1.3 second range. Reaction delays should delay a decided routine transition, not freeze a villager that is already moving: preserve the current routine/action until the delay expires. A villager waiting to claim a plot after reaching a road is already at its decision point and should stand still during that claim delay. New roads only trigger a delayed reconsideration for villagers currently seeking a road; they should not interrupt other routines. If a routine ends while the next routine is being decided, switch immediately to idle while waiting.

`Mover` uses simple cardinal grid A* through `MapManager` for targeted movement and wandering. Registered resource-object cells (currently trees) block movement; house footprints become blockers only after materials are deposited and construction begins. Unstarted claimed plots, roads, villagers, and physics/collision queries do not block movement. Keep this simple until a separate obstacle system is requested. Resource approaches use their configured interaction offset when nonzero. Road-seeking villagers choose and retain a road target while traveling, rather than switching to whichever road is momentarily closest; a new road may be considered after its reaction delay. Road rendering follows the original road keypoint path. The separately tracked road occupancy cells use orthogonal grid steps, and the optional `UIManager` debug grid highlights those occupied road cells. The scene assigns the road-line prefab as the temporary green outline for claimed plots; runtime visuals replace it with construction sprites and a finished house. There is still no job/personality decision system, food production or trade, household/relationship/reproduction/aging simulation, or combat. `Monster` remains an empty `Mover` subclass. `Buildings.json` is not loaded by `GameData`; it describes a road entry and does not provide a working building-definition system.

## Code and asset layout

- `Assets/Script/Global/`
  - `GameData.cs`: static configuration/data loader.
  - `GameOverlord.cs`: scene-level singleton and road-tile prefab reference.
  - `Player.cs`: owns resource balances and villager registry, creates initial villagers, regenerates mana, and places the claimed-plot prefab.
  - `MapManager.cs`: map resource placement, grid-based pathfinding, grid queries, interactive road creation, plot reservations, and FIFO plot claiming.
  - `Struct.cs`: shared serializable records, settings classes, and enums.
- `Assets/Script/2DObjectComponents/`
  - `Mover.cs`: cardinal-grid path following, facing, and simple leg/idle animation.
  - `Villager.cs`: road seeking followed by plot claiming, material gathering/delivery, and staged house construction; owns personal inventory and equipped weapon state.
  - `HouseVisual.cs`: assembles construction-stage sprites and finished house-part sprites on a claimed plot.
  - `ResourceObject.cs`: resource type, JSON-backed harvest amounts and interaction offset, remaining yields, and script-driven shake animation.
  - `VillagerHandMotion.cs`: script-driven gathering hit motion for a villager's right arm.
  - `VillagerNameGenerator.cs`: name selection from loaded name parts.
  - `Monster.cs`: currently an empty `Mover` subclass.
  - `Camera.cs`: camera pan and zoom input.
- `Assets/Script/UI/`: resource display, road/build UI actions, off-screen villager indicators, and hover tooltips.
- `Assets/Resources/Data/`: JSON consumed by the data loader, including `HouseDefinitions.json`; `Buildings.json` is not consumed yet.
- `Assets/Resources/BodyParts/`, `Assets/Resources/ResourceParts/`, and `Assets/Resources/BuildingParts/`: sprite assets used by the current JSON and prefabs/runtime visuals.
- `Assets/Prefabs/`: road tile, mover/body, tree resource, and UI tooltip/road-preview assets.

## Data loading and configuration

`GameData` is a plain C# singleton (`GameData.Instance`), not a `MonoBehaviour`. Its static instance is created by `Load()` on first type access. It loads `TextAsset`s through Unity `Resources.Load` from `Assets/Resources/Data/`, parses them with `JsonUtility`, and resolves body/building sprite names from their corresponding `Resources` folders.

Currently loaded data:

- `Bodies.json`: a top-level JSON array; `GameData` wraps it in a `BodyCollection` shape before parsing. Body records hold sprite names, gender, movement/animation tuning, and offsets.
- `CharacterSettings.json`: color-code arrays used when creating villagers.
- `NamingParts.json`: a `nameParts` array; its `type` values correspond to `NamePartType` enum values (`0` is `FullName`, `1` is `FirstPart`, `2` is `LastPart`).
- `ResourceDefinitions.json`: a `resourceDefinitions` array. The loader expects exactly one valid entry per `ResourceType`; `type` is serialized as the enum's numeric value. It also checks icon indices, initial amounts, and full-cut/bare-hand harvest yields. Each entry's integer `interactionOffset` is added to the resource's grid cell to choose its interaction cell; `(0, 0)` retains the regular approach behavior.
- `HouseDefinitions.json`: a `houseDefinitions` array with IDs, per-house visual scale and world-space x/y visual offset, sprite-name arrays, and HTML color-code palettes for each house-part category. The loader validates referenced sprites and colors in `Resources/BuildingParts/`; each definition must include a base and low roof. The two construction stages each have a separate color palette and are loaded as sliced sprite groups from the same folder.
- `GameSettings.json`: `uiSettings`, `mapSettings`, `playerSettings`, `houseSettings`, `villagerSettings`, and `resourceSettings`, parsed into the matching classes in `Struct.cs`. House dimensions, material costs, both construction durations, the between-stage break, hammer timing/positions, villager behavior and hit motion, and resource shake tuning are JSON-backed.

`Player`, `MapManager`, and `UIManager` read their relevant settings from `GameData`; each `Villager` reads its AI settings there as well. `Player` creates separate runtime resource balances from resource definitions; keep that mutable simulation state distinct from static configuration. Missing or invalid JSON is generally reported with `Debug.LogError` and empty arrays, `null`, or default settings, so preserve clear validation and error reporting when extending the loader.

### Configuration migration rule

The project goal is for gameplay rules and tunable values to be JSON-backed and loaded through `GameData`. Inspector fields should be limited to prefab references and debug-only controls. When adding or changing systems:

- Put gameplay and balance values (for example map size and tree generation, resource type/amount, camera controls, villager behavior timings/speeds, button action/ID, and UI behavior settings) in a suitable JSON data model and expose loaded values through `GameData`.
- Keep runtime state (resource balances, current jobs, needs, households, road network state) in the owning simulation component/service; do not store live state in the static configuration singleton.
- Keep prefab references in Unity serialization where instantiation requires them. Use the existing JSON sprite-name-to-`Resources`-asset pattern where appropriate, and validate every referenced asset.
- Do not add a second settings source or leave an inspector fallback that silently overrides JSON. Validate ranges and required entries at load/initialization time.
- If adding JSON-backed definitions, add their record/collection types, loader, validation, and `GameData` property together. Update consumers to use the loaded definition rather than embedding values in a component.

Examples of settings still serialized as gameplay configuration include `MapManager` map/tree-generation values, `GameCamera` movement and zoom values, `ResourceObject` type, and `UIButton` action/ID. Resource harvest yields and interaction offsets are in `ResourceDefinitions.json`; villager reaction-time range, behavior and gathering animation settings, and house dimensions/cost/stage timings/hammer motion are in `GameSettings.json`. House art is defined in `HouseDefinitions.json`. `Player` holds the base villager and claimed-space prefab references. Body movement and animation settings are already supplied from `Bodies.json`, although `Mover` retains serialized defaults. `UIManager` mixes prefab/asset references and debug controls with configuration; its debug grid options are appropriate inspector controls, while other tunable behavior should follow the JSON rule.

## Singleton and communication patterns

There are two distinct patterns:

- `GameData` is a process-wide, lazily initialized static data object.
- `GameOverlord`, `Player`, `MapManager`, and `UIManager` are scene `MonoBehaviour`s with static `Instance` accessors and duplicate-instance checks in `Awake`. They are scene objects, not persistent `DontDestroyOnLoad` services.

The current dependency flow includes `Player` reading `GameData`, creating and registering initial villagers, and exposing runtime resources; `MapManager` reading map settings and coordinating map resources, road cells, and plot reservations; `Villager` reading housing configuration and querying `MapManager`/`Player`; and UI components querying `Player`, `MapManager`, and `UIManager`. `Player.Start()` creates villagers after scene `Awake()` calls, so it relies on its own configuration/resources being initialized in `Awake()`.

For new systems, prefer explicit initialization/dependencies and narrow interfaces over adding more global `Instance` lookups. A small scene bootstrap/composition root can validate required services and initialize them in a known order. Keep ownership clear (for example, a resource service owns balances; villagers own individual state; the map owns roads and spatial queries). Use events/signals for state changes such as resource balance updates instead of making every UI element poll global state each frame. Retain the current singleton pattern only when there is a clear project-wide need, and make scene unload, duplicate instances, and static-reference cleanup predictable.

## Working conventions

- Follow the existing Unity C# style and keep scripts in the existing `Assets/Script` feature folders.
- Preserve prefab references and the hierarchy/component contracts relied on by code (for example, the villager body-part child names and the immediate `Canvas` child expected by UI code).
- Update JSON and C# data types together. `JsonUtility` expects serializable fields and does not provide general-purpose dictionary or polymorphic serialization; use wrapper records/arrays as the current data files do.
- Treat enum numeric values written to JSON as schema: changing enum order can reinterpret existing content. Prefer explicit stable IDs or a deliberate migration if the data format grows.
- **Do not run Unity compilation or builds.** Do not launch Unity in batch mode, invoke a Unity compile/build command, or otherwise try to compile the project. Leave Unity compilation and runtime verification to the user. Report what source/JSON checks were performed and clearly state when Unity validation remains for the user.
- For performance investigations, prefer a Windows Development Build with **Development Build** and **Autoconnect Profiler** enabled, then inspect a selected CPU Usage spike in Timeline. Recent testing found the observed choppiness in Editor Play Mode was gone in a Development Build; EditorLoop-heavy Play Mode captures did not indicate significant villager script time. Do not treat Editor-only timing as proof of a gameplay bottleneck.
- Keep changes focused. No dedicated automated test suite was found in the current `Assets/Script` tree. Validate JSON/source consistency without invoking Unity, and leave script compilation, scene loading, and gameplay checks to the user.
