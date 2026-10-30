# Project Instructions

## Project direction

Independent Kingdoms is intended to be an unusual, mostly autonomous city builder inspired loosely by *Banished*. Villagers—not the player—should identify needs, choose work based on their personalities and the town's needs, gather resources, and build homes and other structures. The simulation is intended to include resource production and trade, aging, family formation and children, death, and monster attacks that encourage defense and equipment production.

The player is a town-level influence rather than a direct building manager: the player can inspect resources, spend mana to create roads, issue broad orders and warnings, and buy upgrades or skills that benefit the town. Villagers should generally navigate and establish structures around the player's roads. Treat these as design goals, not features that are already implemented.

## Current state at a glance

The active prototype is a Unity 2D project. `Assets/Scenes/MainGame.unity` is the primary scene; `Assets/Scenes/SampleScene.unity` is also present. The current gameplay foundation includes initial villager creation and visual customization, simple movement and road wandering, map resource placement, road drawing, mana regeneration, and some resource and villager UI.

Villagers now have an initial housing loop: they seek a road, enter a FIFO plot-claim queue, claim an available grid footprint beside a road, gather configured resources from registered map resources, and advance a timed construction state. Each villager starts with an empty inventory and `VillagerWeaponType.None`. The inventory/equipment API can add/remove item stacks and equip an axe when one is in inventory, but axe production and distribution are not implemented. A bare-handed villager can gather at most the configured `bareHandAmount` from a resource; an equipped axe permits gathering its remaining `fullCutAmount`. Gathering is a repeating hit-and-pause loop with a script-driven arm motion and resource shake. Harvest yields are in `ResourceDefinitions.json`; gathering timing/hand positions and resource shake tuning are in `GameSettings.json`. The scene currently assigns the road-line prefab as a temporary green plot outline; it is not a house. There is still no job/personality decision system, food production or trade, household/relationship/reproduction/aging simulation, actual house prefab/construction stages, or combat. `Monster` remains an empty `Mover` subclass. `Buildings.json` is not loaded by `GameData`; it describes a road entry and does not provide a working building-definition system.

## Code and asset layout

- `Assets/Script/Global/`
  - `GameData.cs`: static configuration/data loader.
  - `GameOverlord.cs`: scene-level singleton and road-tile prefab reference.
  - `Player.cs`: owns resource balances and villager registry, creates initial villagers, regenerates mana, and places the claimed-plot prefab.
  - `MapManager.cs`: map resource placement, grid queries, interactive road creation, plot reservations, and FIFO plot claiming.
  - `Struct.cs`: shared serializable records, settings classes, and enums.
- `Assets/Script/2DObjectComponents/`
  - `Mover.cs`: movement targets, facing, and simple leg/idle animation.
  - `Villager.cs`: road seeking followed by plot claiming, material gathering and timed house-construction states; owns personal inventory and equipped weapon state.
  - `ResourceObject.cs`: resource type, JSON-backed harvest amounts, remaining yields, and script-driven shake animation.
  - `VillagerHandMotion.cs`: script-driven gathering hit motion for a villager's right arm.
  - `VillagerNameGenerator.cs`: name selection from loaded name parts.
  - `Monster.cs`: currently an empty `Mover` subclass.
  - `Camera.cs`: camera pan and zoom input.
- `Assets/Script/UI/`: resource display, road/build UI actions, off-screen villager indicators, and hover tooltips.
- `Assets/Resources/Data/`: JSON consumed by the data loader, plus `Buildings.json` which is not consumed yet.
- `Assets/Resources/BodyParts/` and `Assets/Resources/ResourceParts/`: sprite assets used by the current JSON and prefabs.
- `Assets/Prefabs/`: road tile, mover/body, tree resource, and UI tooltip/road-preview assets.

## Data loading and configuration

`GameData` is a plain C# singleton (`GameData.Instance`), not a `MonoBehaviour`. Its static instance is created by `Load()` on first type access. It loads `TextAsset`s through Unity `Resources.Load` from `Assets/Resources/Data/`, parses them with `JsonUtility`, and resolves body-part sprite names from `Resources/BodyParts/`.

Currently loaded data:

- `Bodies.json`: a top-level JSON array; `GameData` wraps it in a `BodyCollection` shape before parsing. Body records hold sprite names, gender, movement/animation tuning, and offsets.
- `CharacterSettings.json`: color-code arrays used when creating villagers.
- `NamingParts.json`: a `nameParts` array; its `type` values correspond to `NamePartType` enum values (`0` is `FullName`, `1` is `FirstPart`, `2` is `LastPart`).
- `ResourceDefinitions.json`: a `resourceDefinitions` array. The loader expects exactly one valid entry per `ResourceType`; `type` is serialized as the enum's numeric value. It also checks icon indices, initial amounts, and full-cut/bare-hand harvest yields.
- `GameSettings.json`: `uiSettings`, `mapSettings`, `playerSettings`, `houseSettings`, `villagerSettings`, and `resourceSettings`, parsed into the matching classes in `Struct.cs`. House dimensions, material costs, construction time, villager behavior and hit motion, and resource shake tuning are JSON-backed.

`Player`, `MapManager`, and `UIManager` read their relevant settings from `GameData`; each `Villager` reads its AI settings there as well. `Player` creates separate runtime resource balances from resource definitions; keep that mutable simulation state distinct from static configuration. Missing or invalid JSON is generally reported with `Debug.LogError` and empty arrays, `null`, or default settings, so preserve clear validation and error reporting when extending the loader.

### Configuration migration rule

The project goal is for gameplay rules and tunable values to be JSON-backed and loaded through `GameData`. Inspector fields should be limited to prefab references and debug-only controls. When adding or changing systems:

- Put gameplay and balance values (for example map size and tree generation, resource type/amount, camera controls, villager behavior timings/speeds, button action/ID, and UI behavior settings) in a suitable JSON data model and expose loaded values through `GameData`.
- Keep runtime state (resource balances, current jobs, needs, households, road network state) in the owning simulation component/service; do not store live state in the static configuration singleton.
- Keep prefab references in Unity serialization where instantiation requires them. Use the existing JSON sprite-name-to-`Resources`-asset pattern where appropriate, and validate every referenced asset.
- Do not add a second settings source or leave an inspector fallback that silently overrides JSON. Validate ranges and required entries at load/initialization time.
- If adding JSON-backed definitions, add their record/collection types, loader, validation, and `GameData` property together. Update consumers to use the loaded definition rather than embedding values in a component.

Examples of settings still serialized as gameplay configuration include `MapManager` map/tree-generation values, `GameCamera` movement and zoom values, `ResourceObject` type, and `UIButton` action/ID. Resource harvest yields are in `ResourceDefinitions.json`; villager behavior and gathering animation settings and house dimensions/cost/time are in `GameSettings.json`. `Player` holds the base villager and claimed-space prefab references. Body movement and animation settings are already supplied from `Bodies.json`, although `Mover` retains serialized defaults. `UIManager` mixes prefab/asset references and debug controls with configuration; its debug grid options are appropriate inspector controls, while other tunable behavior should follow the JSON rule.

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
