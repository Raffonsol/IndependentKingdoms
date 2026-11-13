using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public class GameSaveSnapshot
{
    public int version;
    public string savedAtUtc;
    public GameSaveResourceAmount[] playerResources;
    public GameSaveVillager[] villagers;
    public GameSaveResourceObject[] resourceObjects;
    public GameSaveCommunityStorage[] communityStorages;
    public GameSaveRoad[] roads;
    [NonSerialized] public bool hasLegacyResourceTransforms;
}

[Serializable]
public class GameSaveVillager
{
    public string id;
    public GameSavePerson person;
    public GameSavePosition position;
    public GameSavePosition localScale;
    public float health;
    public float hunger;
    public float fatigue;
    public string housingState;
    public float constructionProgressSeconds;
    public float constructionHammerTimer;
    public string jobType;
    public GameSaveAmount[] jobInputStock;
    public bool hasJobProductionProgress;
    public string jobProductionItemType;
    public float jobProductionProgressSeconds;
    public float jobProductionDurationSeconds;
    public bool jobWorkplaceIsHome;
    public bool jobWorkplaceIsSeparate;
    public bool jobWorkplaceBuildingNewHouse;
    public string jobWorkplaceStage;
    public float jobWorkplaceConstructionProgressSeconds;
    public float jobWorkplaceHammeringTimer;
    public GameSaveHome jobWorkplace;
    public string equippedItem;
    public GameSaveAmount[] itemInventory;
    public GameSaveAmount[] resourceInventory;
    public GameSaveHome home;
}

[Serializable]
public class GameSavePerson
{
    public string name;
    public string familyName;
    public int age;
    public bool isMale;
    public float reactionTime;
    public string[] personalityTypes;
    public string skinColor;
    public string hairColor;
    public string eyeColor;
    public string clothingTopColor;
    public string clothingBottomColor;
    public string leggingColor;
    public string bootColor;
    public string hairAssetName;
    public string faceAssetName;
    public string earAssetName;
    public string baseClothingTopAssetName;
    public string baseClothingBottomAssetName;
    public string baseLeggingAssetName;
    public string baseBootAssetName;
    public string baseSleeveAssetName;
}

[Serializable]
public class GameSaveHome
{
    public GameSaveCell originCell;
    public GameSaveCell dimensions;
    public GameSaveCell entranceCell;
    public GameSavePosition position;
    public string houseDefinitionId;
    public bool isComplete;
    public bool hasAnnex;
    public GameSaveResourceAmount[] storedMaterials;
    public int workplaceCapacity;
    public GameSaveAmount[] storedItems;
    public bool hasProductionProgress;
    public string productionItemType;
    public float productionProgressSeconds;
    public float productionDurationSeconds;
    // Version 2 saves stored production progress as a fraction.
    public float productionProgress;
}

[Serializable]
public class GameSaveResourceObject
{
    public string definitionId;
    public bool hasGridCell;
    public GameSaveCell gridCell;
    public GameSavePosition position;
    public bool hasSavedTransform;
    public float rotationZ;
    public GameSavePosition localScale;
    public GameSaveResourceAmount[] remainingResources;
}

[Serializable]
public class GameSaveCommunityStorage
{
    public GameSaveCell originCell;
    public GameSaveCell dimensions;
    public GameSaveCell entranceCell;
    public GameSavePosition position;
    public int capacity;
    public GameSaveResourceAmount[] resources;
}

[Serializable]
public class GameSaveRoad
{
    public GameSavePosition[] keyPoints;
}

[Serializable]
public class GameSaveAmount
{
    public string type;
    public int amount;
}

[Serializable]
public class GameSaveResourceAmount
{
    public string resourceType;
    public int amount;
    public int bareHandAmount;
}

[Serializable]
public class GameSavePosition
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class GameSaveCell
{
    public int x;
    public int y;
}

public static class GameSaveManager
{
    private const int CurrentSaveVersion = 5;
    private const string SaveDirectoryName = "Saves";
    public const string DefaultSlotName = "default";

    private static GameSaveSnapshot pendingSnapshot;

    public static bool HasPendingLoad
    {
        get { return pendingSnapshot != null; }
    }

    public static bool HasLoadableSave()
    {
        if (!TryGetSavePath(DefaultSlotName, out string savePath)
            || !File.Exists(savePath))
        {
            return false;
        }

        return TryLoadSnapshot(DefaultSlotName, out _);
    }

    public static bool TryPrepareLoad()
    {
        if (!TryLoadSnapshot(DefaultSlotName, out GameSaveSnapshot snapshot))
        {
            return false;
        }

        pendingSnapshot = snapshot;
        return true;
    }

    public static void ClearPendingLoad()
    {
        pendingSnapshot = null;
    }

    public static bool TryRestorePendingGame()
    {
        if (pendingSnapshot == null)
        {
            return true;
        }

        Player player = Player.Instance;
        MapManager mapManager = MapManager.Instance;
        if (player == null || mapManager == null)
        {
            Debug.LogError("Cannot load a game without an active Player and MapManager.");
            pendingSnapshot = null;
            return false;
        }

        GameSaveSnapshot snapshot = pendingSnapshot;
        if (!mapManager.RestoreSavedWorld(snapshot)
            || !player.RestoreSavedGame(snapshot))
        {
            pendingSnapshot = null;
            return false;
        }

        if (!RestoreHomes(snapshot, player, mapManager))
        {
            pendingSnapshot = null;
            return false;
        }

        pendingSnapshot = null;
        return true;
    }

    public static bool TryCaptureCurrentGame(out GameSaveSnapshot snapshot)
    {
        snapshot = null;
        Player player = Player.Instance;
        MapManager mapManager = MapManager.Instance;
        if (player == null || mapManager == null)
        {
            Debug.LogError("Cannot capture a game save without an active Player and MapManager.");
            return false;
        }

        GameSaveSnapshot capturedSnapshot = new GameSaveSnapshot
        {
            version = CurrentSaveVersion,
            savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            playerResources = CapturePlayerResources(player),
            villagers = CaptureVillagers(player),
            resourceObjects = CaptureResourceObjects(mapManager),
            communityStorages = CaptureCommunityStorages(mapManager),
            roads = CaptureRoads(mapManager)
        };

        if (capturedSnapshot.villagers == null
            || capturedSnapshot.resourceObjects == null
            || capturedSnapshot.communityStorages == null
            || capturedSnapshot.roads == null)
        {
            return false;
        }

        snapshot = capturedSnapshot;
        return true;
    }

    public static bool TrySaveCurrentGame(string slotName)
    {
        if (!TryGetSavePath(slotName, out string savePath)
            || !TryCaptureCurrentGame(out GameSaveSnapshot snapshot))
        {
            return false;
        }

        if (!IsValidSnapshot(snapshot))
        {
            Debug.LogError("Captured game save failed validation; no save was written.");
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            string json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(savePath, json, new UTF8Encoding(false));
            return true;
        }
        catch (IOException exception)
        {
            Debug.LogError("Could not write game save: " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.LogError("Could not write game save: " + exception.Message);
        }
        catch (SecurityException exception)
        {
            Debug.LogError("Could not write game save: " + exception.Message);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("Could not serialize game save: " + exception.Message);
        }

        return false;
    }

    public static bool TryLoadSnapshot(string slotName, out GameSaveSnapshot snapshot)
    {
        snapshot = null;
        if (!TryGetSavePath(slotName, out string savePath))
        {
            return false;
        }

        if (!File.Exists(savePath))
        {
            Debug.LogError("Game save does not exist: " + slotName);
            return false;
        }

        try
        {
            string json = File.ReadAllText(savePath, Encoding.UTF8);
            GameSaveSnapshot loadedSnapshot = JsonUtility.FromJson<GameSaveSnapshot>(json);
            NormalizeEmptyHomePlaceholders(loadedSnapshot);
            if (loadedSnapshot != null && loadedSnapshot.version == 2
                && !UpgradeVersion2Snapshot(loadedSnapshot))
            {
                Debug.LogError("Could not upgrade version 2 game save: " + slotName);
                return false;
            }

            if (loadedSnapshot != null && loadedSnapshot.version == 3
                && !UpgradeVersion3Snapshot(loadedSnapshot))
            {
                Debug.LogError("Could not upgrade version 3 game save: " + slotName);
                return false;
            }

            if (loadedSnapshot != null && loadedSnapshot.version == 4
                && !UpgradeVersion4Snapshot(loadedSnapshot))
            {
                Debug.LogError("Could not upgrade version 4 game save: " + slotName);
                return false;
            }

            if (loadedSnapshot == null)
            {
                Debug.LogError("Game save is empty or invalid: " + slotName);
                return false;
            }

            if (loadedSnapshot.version != CurrentSaveVersion)
            {
                Debug.LogError(
                    "Game save uses unsupported version "
                    + loadedSnapshot.version
                    + ": "
                    + slotName);
                return false;
            }

            if (!IsValidSnapshot(loadedSnapshot))
            {
                Debug.LogError("Game save contains invalid data: " + slotName);
                return false;
            }

            snapshot = loadedSnapshot;
            return true;
        }
        catch (IOException exception)
        {
            Debug.LogError("Could not read game save: " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.LogError("Could not read game save: " + exception.Message);
        }
        catch (SecurityException exception)
        {
            Debug.LogError("Could not read game save: " + exception.Message);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("Could not deserialize game save: " + exception.Message);
        }

        return false;
    }

    private static void NormalizeEmptyHomePlaceholders(GameSaveSnapshot snapshot)
    {
        if (snapshot == null || snapshot.villagers == null)
        {
            return;
        }

        foreach (GameSaveVillager villager in snapshot.villagers)
        {
            if (villager == null)
            {
                continue;
            }

            if (IsEmptyHomePlaceholder(villager.home))
            {
                villager.home = null;
            }

            if (IsEmptyHomePlaceholder(villager.jobWorkplace))
            {
                villager.jobWorkplace = null;
            }
        }
    }

    private static bool IsEmptyHomePlaceholder(GameSaveHome home)
    {
        return home != null
            && string.IsNullOrWhiteSpace(home.houseDefinitionId)
            && IsZeroCell(home.originCell)
            && IsZeroCell(home.dimensions)
            && IsZeroCell(home.entranceCell)
            && IsZeroPosition(home.position)
            && !home.isComplete
            && !home.hasAnnex
            && IsEmpty(home.storedMaterials)
            && home.workplaceCapacity == 0
            && IsEmpty(home.storedItems)
            && !home.hasProductionProgress
            && string.IsNullOrEmpty(home.productionItemType)
            && home.productionProgressSeconds == 0f
            && home.productionDurationSeconds == 0f
            && home.productionProgress == 0f;
    }

    private static bool IsZeroCell(GameSaveCell cell)
    {
        return cell == null || (cell.x == 0 && cell.y == 0);
    }

    private static bool IsZeroPosition(GameSavePosition position)
    {
        return position == null
            || (position.x == 0f && position.y == 0f && position.z == 0f);
    }

    private static bool IsEmpty<T>(T[] values)
    {
        return values == null || values.Length == 0;
    }

    private static bool UpgradeVersion2Snapshot(GameSaveSnapshot snapshot)
    {
        if (snapshot.villagers == null)
        {
            return false;
        }

        GameData gameData = GameData.Instance;
        foreach (GameSaveVillager villager in snapshot.villagers)
        {
            if (villager == null)
            {
                return false;
            }

            villager.jobInputStock = new GameSaveAmount[0];
            if (villager.home == null)
            {
                continue;
            }

            GameSaveHome home = villager.home;
            if (!string.IsNullOrWhiteSpace(home.houseDefinitionId))
            {
                home.isComplete = true;
                villager.housingState = VillagerHousingState.Housed.ToString();
                villager.constructionProgressSeconds = 0f;
                villager.constructionHammerTimer = 0f;
            }

            if (!home.hasProductionProgress)
            {
                continue;
            }

            if (!Enum.TryParse(home.productionItemType, out VillagerItemType itemType)
                || !Enum.IsDefined(typeof(VillagerItemType), itemType)
                || float.IsNaN(home.productionProgress)
                || float.IsInfinity(home.productionProgress)
                || home.productionProgress < 0f
                || home.productionProgress > 1f)
            {
                return false;
            }

            float productionDurationSeconds = 0f;
            foreach (JobType jobType in Enum.GetValues(typeof(JobType)))
            {
                if (gameData.TryGetJobDefinition(jobType, out JobDefinition jobDefinition)
                    && jobDefinition.workType == JobWorkType.Production
                    && jobDefinition.outputItemType == itemType)
                {
                    productionDurationSeconds = jobDefinition.productionDurationSeconds;
                    break;
                }
            }

            if (!IsFinitePositive(productionDurationSeconds))
            {
                return false;
            }

            home.productionDurationSeconds = productionDurationSeconds;
            home.productionProgressSeconds =
                home.productionProgress * home.productionDurationSeconds;
        }

        snapshot.version = 3;
        return true;
    }

    private static bool UpgradeVersion3Snapshot(GameSaveSnapshot snapshot)
    {
        if (snapshot.villagers == null)
        {
            return false;
        }

        foreach (GameSaveVillager villager in snapshot.villagers)
        {
            if (villager == null)
            {
                return false;
            }

            if (villager.jobWorkplaceIsHome
                && (villager.home == null
                    || string.IsNullOrWhiteSpace(villager.home.houseDefinitionId)))
            {
                villager.jobWorkplaceIsHome = false;
            }

            villager.jobWorkplaceStage = villager.jobWorkplaceIsHome
                ? villager.home != null && villager.home.hasAnnex
                    ? "Ready"
                    : "GatheringMaterials"
                : villager.jobWorkplace != null
                    ? "Ready"
                    : "None";
            villager.jobWorkplaceIsSeparate = villager.jobWorkplace != null;
            villager.jobWorkplaceBuildingNewHouse = false;
            villager.jobWorkplaceConstructionProgressSeconds = 0f;
            villager.jobWorkplaceHammeringTimer = 0f;
        }

        snapshot.version = 4;
        return true;
    }

    private static bool UpgradeVersion4Snapshot(GameSaveSnapshot snapshot)
    {
        if (snapshot.resourceObjects == null)
        {
            return false;
        }

        foreach (GameSaveResourceObject resourceObject in snapshot.resourceObjects)
        {
            if (resourceObject == null)
            {
                return false;
            }

            resourceObject.hasSavedTransform = false;
            resourceObject.rotationZ = 0f;
            resourceObject.localScale = null;
        }

        snapshot.hasLegacyResourceTransforms = true;
        snapshot.version = CurrentSaveVersion;
        return true;
    }

    private static bool RestoreHomes(
        GameSaveSnapshot snapshot,
        Player player,
        MapManager mapManager)
    {
        HouseDefinition[] definitions = GameData.Instance.houseDefinitions;
        for (int index = 0; index < snapshot.villagers.Length; index++)
        {
            GameSaveVillager savedVillager = snapshot.villagers[index];
            if (savedVillager == null || savedVillager.home == null)
            {
                continue;
            }

            if (index >= player.Villagers.Count
                || player.Villagers[index] == null
                || player.Villagers[index].Villager == null)
            {
                Debug.LogError("Could not find the loaded villager for a saved home.");
                return false;
            }

            GameSaveHome savedHome = savedVillager.home;
            if (!TryParseCell(savedHome.originCell, out Vector2Int origin)
                || !TryParseCell(savedHome.dimensions, out Vector2Int dimensions)
                || !TryParseCell(savedHome.entranceCell, out Vector2Int entrance)
                || dimensions.x <= 0
                || dimensions.y <= 0
                || !TryParsePosition(savedHome.position, out Vector3 position))
            {
                Debug.LogError("A saved home has invalid plot data.");
                return false;
            }

            HouseDefinition definition = FindHouseDefinition(definitions, savedHome.houseDefinitionId);
            ResourceCost[] storedMaterials = ParseResourceCosts(savedHome.storedMaterials);
            if (string.IsNullOrWhiteSpace(savedHome.houseDefinitionId))
            {
                Debug.LogWarning(
                    "A saved home was still unstarted; its villager will claim a plot again after loading.");
                continue;
            }

            if (definition == null || storedMaterials == null)
            {
                Debug.LogError(
                    "Could not restore the saved home for villager '" + savedVillager.person.name + "'.");
                return false;
            }

            if (!Enum.TryParse(savedVillager.housingState, out VillagerHousingState housingState)
                || !IsValidSavedHomeProgress(savedHome, savedVillager, housingState))
            {
                Debug.LogError(
                    "A saved home has invalid or unsupported construction progress.",
                    player.Villagers[index].Villager);
                return false;
            }

            HousePlotRecord plot = new HousePlotRecord
            {
                originCell = origin,
                dimensions = dimensions,
                entranceCell = entrance,
                center = position,
                storedMaterials = storedMaterials
            };
            Villager villager = player.Villagers[index].Villager;
            GameObject houseObject = player.PlaceClaimedSpace(plot);
            if (houseObject == null)
            {
                return false;
            }

            houseObject.transform.position = position;
            HouseVisual houseVisual = houseObject.GetComponent<HouseVisual>();
            if (houseVisual == null)
            {
                houseVisual = houseObject.AddComponent<HouseVisual>();
            }

            float visualWidth = dimensions.x * mapManager.GridCellSize * definition.visualScale;
            bool isUnderConstruction = housingState != VillagerHousingState.Housed;
            if (isUnderConstruction)
            {
                bool isFirstStage =
                    housingState == VillagerHousingState.BuildingHouseStageOne;
                houseVisual.ShowConstructionStage(
                    isFirstStage
                        ? GameData.Instance.houseBuildStageOneSprites
                        : GameData.Instance.houseBuildStageTwoSprites,
                    isFirstStage
                        ? GameData.Instance.houseBuildStageOneColors
                        : GameData.Instance.houseBuildStageTwoColors,
                    visualWidth,
                    definition.visualOffset,
                    definition.sortingAnchorOffset,
                    !isFirstStage);
            }
            else
            {
                houseVisual.ShowFinishedHouse(definition, visualWidth);
                if (savedHome.hasAnnex)
                {
                    houseVisual.CompleteAnnexRenovation();
                }
            }

            HouseWorkplace workplace = mapManager.GetOrCreateHouseWorkplace(houseObject);
            if (workplace == null || !RestoreWorkplace(savedHome, workplace))
            {
                return false;
            }

            bool registered = isUnderConstruction
                ? mapManager.TryRegisterSavedHouseInProgress(villager, plot, houseObject)
                : mapManager.TryRegisterSavedHouse(villager, plot, houseObject);
            if (!registered
                || !villager.RestoreHome(
                    plot,
                    houseObject,
                    housingState,
                    savedVillager.constructionProgressSeconds,
                    savedVillager.constructionHammerTimer,
                    definition))
            {
                return false;
            }

            if (isUnderConstruction
                && (housingState == VillagerHousingState.BuildingHouseStageOne
                    || housingState == VillagerHousingState.BuildingHouseStageTwo))
            {
                HouseConstructionSite site =
                    mapManager.GetOrCreateHouseConstructionSite(houseObject);
                if (site == null
                    || !site.BeginWorkStep(
                        villager,
                        mapManager.GetHouseEntrancePosition(plot)))
                {
                    Debug.LogError("Could not resume the saved house construction work step.", houseObject);
                    return false;
                }
            }
        }

        for (int index = 0; index < snapshot.villagers.Length; index++)
        {
            GameSaveVillager savedVillager = snapshot.villagers[index];
            if (savedVillager == null
                || (!savedVillager.jobWorkplaceIsHome && savedVillager.jobWorkplace == null))
            {
                continue;
            }

            if (index >= player.Villagers.Count
                || player.Villagers[index] == null
                || player.Villagers[index].Villager == null
                || !RestoreSavedJobWorkplace(
                    savedVillager,
                    player.Villagers[index].Villager,
                    player,
                    mapManager))
            {
                Debug.LogError("Could not restore a saved villager job workplace.");
                return false;
            }
        }

        return true;
    }

    private static bool RestoreSavedJobWorkplace(
        GameSaveVillager savedVillager,
        Villager villager,
        Player player,
        MapManager mapManager)
    {
        GameSaveHome savedWorkplace = savedVillager.jobWorkplaceIsHome
            ? savedVillager.home
            : savedVillager.jobWorkplace;
        if (savedWorkplace == null)
        {
            return false;
        }

        if (!TryParseCell(savedWorkplace.originCell, out Vector2Int origin)
            || !TryParseCell(savedWorkplace.dimensions, out Vector2Int dimensions)
            || !TryParseCell(savedWorkplace.entranceCell, out Vector2Int entrance)
            || dimensions.x <= 0
            || dimensions.y <= 0
            || !TryParsePosition(savedWorkplace.position, out Vector3 position))
        {
            return false;
        }

        HousePlotRecord plot;
        GameObject houseObject;
        HouseDefinition definition;
        if (savedVillager.jobWorkplaceIsHome)
        {
            plot = villager.HomeHousePlot;
            houseObject = villager.HomeHouseObject;
            definition = villager.SelectedHouseDefinition;
            if (plot == null
                || houseObject == null
                || definition == null
                || !mapManager.TryReserveWorkplaceAnnex(houseObject, villager))
            {
                return false;
            }
        }
        else
        {
            definition = FindHouseDefinition(
                GameData.Instance.houseDefinitions,
                savedWorkplace.houseDefinitionId);
            ResourceCost[] storedMaterials = ParseResourceCosts(savedWorkplace.storedMaterials);
            if (definition == null || storedMaterials == null)
            {
                return false;
            }

            plot = new HousePlotRecord
            {
                originCell = origin,
                dimensions = dimensions,
                entranceCell = entrance,
                center = position,
                storedMaterials = storedMaterials
            };
            houseObject = player.PlaceClaimedSpace(plot);
            if (houseObject == null)
            {
                return false;
            }

            houseObject.transform.position = position;
            HouseVisual houseVisual = houseObject.GetComponent<HouseVisual>();
            if (houseVisual == null)
            {
                houseVisual = houseObject.AddComponent<HouseVisual>();
            }

            RestoreJobWorkplaceVisual(
                savedVillager,
                savedWorkplace,
                houseVisual,
                plot,
                definition,
                mapManager);

            HouseWorkplace workplace = mapManager.GetOrCreateHouseWorkplace(houseObject);
            if (workplace == null || !RestoreWorkplace(savedWorkplace, workplace))
            {
                return false;
            }

            bool registered = savedWorkplace.isComplete
                ? mapManager.TryRegisterSavedWorkplaceHouse(villager, plot, houseObject)
                : mapManager.TryRegisterSavedWorkplacePlot(
                    villager,
                    plot,
                    houseObject,
                    IsWorkplaceConstructionStarted(savedVillager.jobWorkplaceStage));
            if (!registered)
            {
                return false;
            }
        }

        HouseVisual restoredVisual = houseObject.GetComponent<HouseVisual>();
        if (savedVillager.jobWorkplaceIsHome)
        {
            RestoreJobWorkplaceVisual(
                savedVillager,
                savedWorkplace,
                restoredVisual,
                plot,
                definition,
                mapManager);
        }

        return villager.RestoreJobWorkplace(plot, houseObject, definition, savedVillager);
    }

    private static void RestoreJobWorkplaceVisual(
        GameSaveVillager savedVillager,
        GameSaveHome savedWorkplace,
        HouseVisual houseVisual,
        HousePlotRecord plot,
        HouseDefinition definition,
        MapManager mapManager)
    {
        float visualWidth = plot.dimensions.x * mapManager.GridCellSize * definition.visualScale;
        if (!savedWorkplace.isComplete)
        {
            if (savedVillager.jobWorkplaceStage == "BuildingHouseStageOne")
            {
                houseVisual.ShowConstructionStage(
                    GameData.Instance.houseBuildStageOneSprites,
                    GameData.Instance.houseBuildStageOneColors,
                    visualWidth,
                    definition.visualOffset,
                    definition.sortingAnchorOffset,
                    false);
            }
            else if (IsWorkplaceStageTwoOrLater(savedVillager.jobWorkplaceStage))
            {
                houseVisual.ShowConstructionStage(
                    GameData.Instance.houseBuildStageTwoSprites,
                    GameData.Instance.houseBuildStageTwoColors,
                    visualWidth,
                    definition.visualOffset,
                    definition.sortingAnchorOffset,
                    true);
            }

            return;
        }

        houseVisual.ShowFinishedHouse(definition, visualWidth);
        if (savedWorkplace.hasAnnex)
        {
            houseVisual.CompleteAnnexRenovation();
        }
        else if (savedVillager.jobWorkplaceStage == "RenovatingAnnex")
        {
            houseVisual.ShowRenovationProgress(
                GameData.Instance.houseBuildStageTwoSprites,
                GameData.Instance.houseBuildStageTwoColors,
                visualWidth,
                definition.visualOffset,
                definition.sortingAnchorOffset);
        }
    }

    private static bool IsWorkplaceConstructionStarted(string stage)
    {
        return stage == "BuildingHouseStageOne"
            || stage == "HouseConstructionBreak"
            || stage == "BuildingHouseStageTwo";
    }

    private static bool IsWorkplaceStageTwoOrLater(string stage)
    {
        return stage == "HouseConstructionBreak"
            || stage == "BuildingHouseStageTwo";
    }

    private static bool RestoreWorkplace(GameSaveHome savedHome, HouseWorkplace workplace)
    {
        int capacity = savedHome.workplaceCapacity > 0
            ? savedHome.workplaceCapacity
            : GameData.Instance.houseSettings.storageCapacity;
        if (!workplace.Initialize(capacity))
        {
            return false;
        }

        if (savedHome.storedItems != null)
        {
            foreach (GameSaveAmount savedItem in savedHome.storedItems)
            {
                if (savedItem == null
                    || savedItem.amount <= 0
                    || !Enum.TryParse(savedItem.type, out VillagerItemType itemType)
                    || !Enum.IsDefined(typeof(VillagerItemType), itemType)
                    || !workplace.TryStoreItem(itemType, savedItem.amount))
                {
                    Debug.LogError("A saved house contains an invalid or over-capacity item stack.", workplace);
                    return false;
                }
            }
        }

        if (savedHome.hasProductionProgress)
        {
            if (!Enum.TryParse(savedHome.productionItemType, out VillagerItemType productionItemType)
                || !Enum.IsDefined(typeof(VillagerItemType), productionItemType)
                || savedHome.productionDurationSeconds <= 0f
                || savedHome.productionProgressSeconds < 0f
                || savedHome.productionProgressSeconds > savedHome.productionDurationSeconds)
            {
                Debug.LogError("A saved house contains invalid production progress.", workplace);
                return false;
            }

            return workplace.RestoreProductionProgress(
                productionItemType,
                savedHome.productionDurationSeconds,
                savedHome.productionProgressSeconds);
        }

        return true;
    }

    private static HouseDefinition FindHouseDefinition(HouseDefinition[] definitions, string id)
    {
        if (definitions == null || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (HouseDefinition definition in definitions)
        {
            if (definition != null && string.Equals(definition.id, id, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private static ResourceCost[] ParseResourceCosts(GameSaveResourceAmount[] savedCosts)
    {
        if (savedCosts == null)
        {
            return new ResourceCost[0];
        }

        List<ResourceCost> costs = new List<ResourceCost>();
        foreach (GameSaveResourceAmount savedCost in savedCosts)
        {
            if (savedCost == null
                || savedCost.amount <= 0
                || !Enum.TryParse(savedCost.resourceType, out ResourceType resourceType)
                || !Enum.IsDefined(typeof(ResourceType), resourceType))
            {
                Debug.LogError("A saved house contains an invalid stored-material entry.");
                return null;
            }

            costs.Add(new ResourceCost { resourceType = resourceType, amount = savedCost.amount });
        }

        return costs.ToArray();
    }

    private static bool TryParseCell(GameSaveCell savedCell, out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        if (savedCell == null)
        {
            return false;
        }

        cell = new Vector2Int(savedCell.x, savedCell.y);
        return true;
    }

    private static bool TryParsePosition(GameSavePosition savedPosition, out Vector3 position)
    {
        position = Vector3.zero;
        if (savedPosition == null
            || float.IsNaN(savedPosition.x)
            || float.IsNaN(savedPosition.y)
            || float.IsNaN(savedPosition.z)
            || float.IsInfinity(savedPosition.x)
            || float.IsInfinity(savedPosition.y)
            || float.IsInfinity(savedPosition.z))
        {
            return false;
        }

        position = new Vector3(savedPosition.x, savedPosition.y, savedPosition.z);
        return true;
    }

    private static GameSaveResourceAmount[] CapturePlayerResources(Player player)
    {
        List<GameSaveResourceAmount> resources = new List<GameSaveResourceAmount>();
        foreach (ResourceType resourceType in Enum.GetValues(typeof(ResourceType)))
        {
            if (GameData.Instance.IsCommunityStorageResource(resourceType)
                || !player.TryGetResourceAmount(resourceType, out int amount))
            {
                continue;
            }

            resources.Add(new GameSaveResourceAmount
            {
                resourceType = resourceType.ToString(),
                amount = amount
            });
        }

        return resources.ToArray();
    }

    private static GameSaveVillager[] CaptureVillagers(Player player)
    {
        List<GameSaveVillager> villagers = new List<GameSaveVillager>();
        for (int index = 0; index < player.Villagers.Count; index++)
        {
            VillagerRecord record = player.Villagers[index];
            Villager villager = record != null ? record.Villager : null;
            if (villager == null)
            {
                continue;
            }

            if (villager.Person == null)
            {
                Debug.LogError("Cannot save villager " + villager.name + " because its Person data is missing.", villager);
                return null;
            }

            villagers.Add(CaptureVillager(villager, index));
        }

        return villagers.ToArray();
    }

    private static GameSaveVillager CaptureVillager(Villager villager, int index)
    {
        GameSaveVillager savedVillager = new GameSaveVillager
        {
            id = "villager_" + index.ToString(CultureInfo.InvariantCulture),
            person = CapturePerson(villager.Person),
            position = CapturePosition(villager.transform.position),
            localScale = CapturePosition(villager.transform.localScale),
            health = villager.Health,
            hunger = villager.Hunger,
            fatigue = villager.Fatigue,
            housingState = villager.HousingState.ToString(),
            constructionProgressSeconds = IsHouseConstructionStarted(villager.HousingState)
                ? villager.ConstructionProgressSeconds
                : 0f,
            constructionHammerTimer = IsHouseConstructionStarted(villager.HousingState)
                ? villager.ConstructionHammerTimer
                : 0f,
            jobType = villager.JobAssignment.ToString(),
            jobWorkplaceStage = "None",
            jobInputStock = villager.CaptureJobInputStock(),
            jobProductionItemType = string.Empty,
            equippedItem = villager.EquippedItem.HasValue
                ? villager.EquippedItem.Value.ToString()
                : string.Empty,
            itemInventory = CaptureItemInventory(villager.Inventory),
            resourceInventory = CaptureResourceInventory(villager.ResourceInventory)
        };

        HousePlotRecord homePlot = villager.HomeHousePlot;
        if (homePlot != null)
        {
            savedVillager.home = CaptureHome(villager, homePlot);
        }

        GameObject jobWorkplaceHouse = villager.JobWorkplaceHouse;
        HousePlotRecord jobWorkplacePlot = villager.JobWorkplacePlot;
        if (jobWorkplaceHouse != null && jobWorkplaceHouse == villager.HomeHouseObject)
        {
            savedVillager.jobWorkplaceIsHome = true;
            villager.CaptureJobWorkplaceState(savedVillager);
        }
        else if (jobWorkplaceHouse != null && jobWorkplacePlot != null)
        {
            HouseVisual workplaceVisual = jobWorkplaceHouse.GetComponent<HouseVisual>();
            HouseDefinition workplaceDefinition = villager.JobWorkplaceDefinition;
            if (workplaceDefinition == null && workplaceVisual != null)
            {
                workplaceDefinition = workplaceVisual.Definition;
            }

            if (workplaceDefinition != null)
            {
                villager.CaptureJobWorkplaceState(savedVillager);
                savedVillager.jobWorkplace = CaptureHouseWorkplace(
                    jobWorkplaceHouse,
                    jobWorkplacePlot,
                    workplaceDefinition,
                    workplaceVisual,
                    !villager.IsBuildingNewJobWorkplaceHouse);
            }
        }

        villager.CaptureJobProductionProgress(savedVillager);
        return savedVillager;
    }

    private static GameSavePerson CapturePerson(Person person)
    {
        string[] personalityTypes = new string[person.personalityTypes != null
            ? person.personalityTypes.Length
            : 0];
        for (int index = 0; index < personalityTypes.Length; index++)
        {
            personalityTypes[index] = person.personalityTypes[index].ToString();
        }

        return new GameSavePerson
        {
            name = person.name,
            familyName = person.familyName,
            age = person.age,
            isMale = person.isMale,
            reactionTime = person.reactionTime,
            personalityTypes = personalityTypes,
            skinColor = person.skinColor,
            hairColor = person.hairColor,
            eyeColor = person.eyeColor,
            clothingTopColor = person.clothingTopColor,
            clothingBottomColor = person.clothingBottomColor,
            leggingColor = person.leggingColor,
            bootColor = person.bootColor,
            hairAssetName = person.hairAssetName,
            faceAssetName = person.faceAssetName,
            earAssetName = person.earAssetName,
            baseClothingTopAssetName = person.baseClothingTopAssetName,
            baseClothingBottomAssetName = person.baseClothingBottomAssetName,
            baseLeggingAssetName = person.baseLeggingAssetName,
            baseBootAssetName = person.baseBootAssetName,
            baseSleeveAssetName = person.baseSleeveAssetName
        };
    }

    private static GameSaveHome CaptureHome(Villager villager, HousePlotRecord plot)
    {
        GameObject homeObject = villager.HomeHouseObject;
        HouseVisual houseVisual = homeObject != null
            ? homeObject.GetComponent<HouseVisual>()
            : null;
        HouseDefinition definition = houseVisual != null ? houseVisual.Definition : null;
        HouseWorkplace workplace = homeObject != null
            ? homeObject.GetComponent<HouseWorkplace>()
            : null;
        if (definition == null)
        {
            definition = villager.SelectedHouseDefinition;
        }

        return new GameSaveHome
        {
            originCell = CaptureCell(plot.originCell),
            dimensions = CaptureCell(plot.dimensions),
            entranceCell = CaptureCell(plot.entranceCell),
            position = CapturePosition(homeObject != null ? homeObject.transform.position : plot.center),
            houseDefinitionId = definition != null ? definition.id : string.Empty,
            isComplete = villager.HousingState == VillagerHousingState.Housed,
            hasAnnex = houseVisual != null && houseVisual.HasAnnex,
            storedMaterials = CaptureResourceCosts(plot.storedMaterials),
            workplaceCapacity = workplace != null ? workplace.Capacity : 0,
            storedItems = CaptureWorkplaceItems(workplace),
            hasProductionProgress = workplace != null && workplace.HasProductionProgress,
            productionItemType = workplace != null && workplace.HasProductionProgress
                ? workplace.ProductionItemType.ToString()
                : string.Empty,
            productionProgressSeconds = workplace != null
                ? workplace.ProductionProgressSeconds
                : 0f,
            productionDurationSeconds = workplace != null
                ? workplace.ProductionDurationSeconds
                : 0f
        };
    }

    private static GameSaveHome CaptureHouseWorkplace(
        GameObject houseObject,
        HousePlotRecord plot,
        HouseDefinition definition,
        HouseVisual houseVisual,
        bool isComplete)
    {
        HouseWorkplace workplace = houseObject.GetComponent<HouseWorkplace>();
        return new GameSaveHome
        {
            originCell = CaptureCell(plot.originCell),
            dimensions = CaptureCell(plot.dimensions),
            entranceCell = CaptureCell(plot.entranceCell),
            position = CapturePosition(houseObject.transform.position),
            houseDefinitionId = definition.id,
            isComplete = isComplete,
            hasAnnex = houseVisual != null && houseVisual.HasAnnex,
            storedMaterials = CaptureResourceCosts(plot.storedMaterials),
            workplaceCapacity = workplace != null ? workplace.Capacity : 0,
            storedItems = CaptureWorkplaceItems(workplace),
            hasProductionProgress = workplace != null && workplace.HasProductionProgress,
            productionItemType = workplace != null && workplace.HasProductionProgress
                ? workplace.ProductionItemType.ToString()
                : string.Empty,
            productionProgressSeconds = workplace != null
                ? workplace.ProductionProgressSeconds
                : 0f,
            productionDurationSeconds = workplace != null
                ? workplace.ProductionDurationSeconds
                : 0f
        };
    }

    private static GameSaveAmount[] CaptureItemInventory(
        IReadOnlyList<VillagerInventoryEntry> inventory)
    {
        List<GameSaveAmount> items = new List<GameSaveAmount>();
        if (inventory != null)
        {
            foreach (VillagerInventoryEntry entry in inventory)
            {
                if (entry != null && entry.amount > 0)
                {
                    items.Add(new GameSaveAmount
                    {
                        type = entry.itemType.ToString(),
                        amount = entry.amount
                    });
                }
            }
        }

        return items.ToArray();
    }

    private static GameSaveAmount[] CaptureResourceInventory(
        IReadOnlyList<VillagerResourceEntry> inventory)
    {
        List<GameSaveAmount> resources = new List<GameSaveAmount>();
        if (inventory != null)
        {
            foreach (VillagerResourceEntry entry in inventory)
            {
                if (entry != null && entry.amount > 0)
                {
                    resources.Add(new GameSaveAmount
                    {
                        type = entry.resourceType.ToString(),
                        amount = entry.amount
                    });
                }
            }
        }

        return resources.ToArray();
    }

    private static GameSaveResourceAmount[] CaptureResourceCosts(ResourceCost[] costs)
    {
        List<GameSaveResourceAmount> savedCosts = new List<GameSaveResourceAmount>();
        if (costs != null)
        {
            foreach (ResourceCost cost in costs)
            {
                if (cost != null && cost.amount > 0)
                {
                    savedCosts.Add(new GameSaveResourceAmount
                    {
                        resourceType = cost.resourceType.ToString(),
                        amount = cost.amount
                    });
                }
            }
        }

        return savedCosts.ToArray();
    }

    private static GameSaveAmount[] CaptureWorkplaceItems(HouseWorkplace workplace)
    {
        List<GameSaveAmount> items = new List<GameSaveAmount>();
        if (workplace == null)
        {
            return items.ToArray();
        }

        foreach (VillagerItemType itemType in Enum.GetValues(typeof(VillagerItemType)))
        {
            int amount = workplace.GetStoredItemAmount(itemType);
            if (amount > 0)
            {
                items.Add(new GameSaveAmount
                {
                    type = itemType.ToString(),
                    amount = amount
                });
            }
        }

        return items.ToArray();
    }

    private static GameSaveResourceObject[] CaptureResourceObjects(MapManager mapManager)
    {
        ResourceObject[] resourceObjects = mapManager.GetRegisteredResourceObjects();
        Array.Sort(resourceObjects, CompareResourceObjects);
        GameSaveResourceObject[] savedObjects = new GameSaveResourceObject[resourceObjects.Length];
        for (int index = 0; index < resourceObjects.Length; index++)
        {
            ResourceObject resourceObject = resourceObjects[index];
            List<GameSaveResourceAmount> remainingResources =
                new List<GameSaveResourceAmount>();
            for (int resourceIndex = 0;
                resourceIndex < resourceObject.AvailableResourceTypes.Count;
                resourceIndex++)
            {
                ResourceType resourceType = resourceObject.AvailableResourceTypes[resourceIndex];
                remainingResources.Add(new GameSaveResourceAmount
                {
                    resourceType = resourceType.ToString(),
                    amount = resourceObject.GetAmount(resourceType),
                    bareHandAmount = resourceObject.GetBareHandAmount(resourceType)
                });
            }

            savedObjects[index] = new GameSaveResourceObject
            {
                definitionId = resourceObject.DefinitionId,
                hasGridCell = resourceObject.HasGridCell,
                gridCell = CaptureCell(resourceObject.GridCell),
                position = CapturePosition(resourceObject.transform.position),
                hasSavedTransform = true,
                rotationZ = resourceObject.transform.eulerAngles.z,
                localScale = CapturePosition(resourceObject.transform.localScale),
                remainingResources = remainingResources.ToArray()
            };
        }

        return savedObjects;
    }

    private static int CompareResourceObjects(ResourceObject left, ResourceObject right)
    {
        if (left == null)
        {
            return right == null ? 0 : 1;
        }

        if (right == null)
        {
            return -1;
        }

        int xComparison = left.GridCell.x.CompareTo(right.GridCell.x);
        return xComparison != 0
            ? xComparison
            : left.GridCell.y.CompareTo(right.GridCell.y);
    }

    private static GameSaveCommunityStorage[] CaptureCommunityStorages(MapManager mapManager)
    {
        CommunityStorage[] storages = mapManager.GetCommunityStorages();
        List<GameSaveCommunityStorage> savedStorages =
            new List<GameSaveCommunityStorage>();
        foreach (CommunityStorage storage in storages)
        {
            if (storage == null || !storage.IsInitialized)
            {
                continue;
            }

            List<GameSaveResourceAmount> resources = new List<GameSaveResourceAmount>();
            foreach (ResourceType resourceType in Enum.GetValues(typeof(ResourceType)))
            {
                if (!GameData.Instance.IsCommunityStorageResource(resourceType))
                {
                    continue;
                }

                int amount = storage.GetAmount(resourceType);
                if (amount > 0)
                {
                    resources.Add(new GameSaveResourceAmount
                    {
                        resourceType = resourceType.ToString(),
                        amount = amount
                    });
                }
            }

            savedStorages.Add(new GameSaveCommunityStorage
            {
                originCell = CaptureCell(storage.OriginCell),
                dimensions = CaptureCell(storage.Dimensions),
                entranceCell = CaptureCell(storage.EntranceCell),
                position = CapturePosition(storage.transform.position),
                capacity = storage.Capacity,
                resources = resources.ToArray()
            });
        }

        return savedStorages.ToArray();
    }

    private static GameSaveRoad[] CaptureRoads(MapManager mapManager)
    {
        Vector3[][] roadPaths = mapManager.GetRoadKeyPointPaths();
        GameSaveRoad[] savedRoads = new GameSaveRoad[roadPaths.Length];
        for (int roadIndex = 0; roadIndex < roadPaths.Length; roadIndex++)
        {
            Vector3[] keyPoints = roadPaths[roadIndex];
            GameSavePosition[] savedKeyPoints = new GameSavePosition[keyPoints.Length];
            for (int pointIndex = 0; pointIndex < keyPoints.Length; pointIndex++)
            {
                savedKeyPoints[pointIndex] = CapturePosition(keyPoints[pointIndex]);
            }

            savedRoads[roadIndex] = new GameSaveRoad
            {
                keyPoints = savedKeyPoints
            };
        }

        return savedRoads;
    }

    private static GameSavePosition CapturePosition(Vector3 position)
    {
        return new GameSavePosition
        {
            x = position.x,
            y = position.y,
            z = position.z
        };
    }

    private static GameSaveCell CaptureCell(Vector2Int cell)
    {
        return new GameSaveCell
        {
            x = cell.x,
            y = cell.y
        };
    }

    private static bool TryGetSavePath(string slotName, out string savePath)
    {
        savePath = null;
        if (string.IsNullOrWhiteSpace(slotName)
            || !Regex.IsMatch(slotName, "^[A-Za-z0-9_-]+$"))
        {
            Debug.LogError("Save slot names may contain only letters, numbers, underscores, and hyphens.");
            return false;
        }

        savePath = Path.Combine(
            Application.persistentDataPath,
            SaveDirectoryName,
            slotName + ".json");
        return true;
    }

    private static bool IsValidSnapshot(GameSaveSnapshot snapshot)
    {
        if (snapshot == null
            || snapshot.version != CurrentSaveVersion
            || snapshot.playerResources == null
            || snapshot.villagers == null
            || snapshot.resourceObjects == null
            || snapshot.communityStorages == null
            || snapshot.roads == null)
        {
            return false;
        }

        GameData gameData = GameData.Instance;
        if (gameData == null)
        {
            return false;
        }

        foreach (GameSaveResourceAmount resource in snapshot.playerResources)
        {
            if (!IsValidResourceAmount(resource, false)
                || !Enum.TryParse(resource.resourceType, out ResourceType resourceType)
                || gameData.IsCommunityStorageResource(resourceType))
            {
                return false;
            }
        }

        foreach (GameSaveVillager villager in snapshot.villagers)
        {
            if (villager == null
                || !IsValidPerson(villager.person)
                || !IsValidPosition(villager.position)
                || !IsValidPosition(villager.localScale)
                || !IsValidAmountArray(villager.itemInventory, typeof(VillagerItemType))
                || !IsValidAmountArray(villager.resourceInventory, typeof(ResourceType))
                || !IsValidJobInputStock(villager.jobInputStock)
                || !IsFiniteNonnegative(villager.constructionProgressSeconds)
                || !IsFiniteNonnegative(villager.constructionHammerTimer)
                || villager.health <= 0f
                || float.IsNaN(villager.health)
                || float.IsInfinity(villager.health)
                || villager.hunger < 0f
                || villager.hunger > Villager.MaximumHunger
                || float.IsNaN(villager.hunger)
                || float.IsInfinity(villager.hunger)
                || villager.fatigue < 0f
                || villager.fatigue > Villager.MaximumFatigue
                || float.IsNaN(villager.fatigue)
                || float.IsInfinity(villager.fatigue)
                || !Enum.TryParse(villager.jobType, out JobType jobType)
                || !Enum.IsDefined(typeof(JobType), jobType)
                || (jobType != JobType.None
                    && !gameData.CanAssignJob(jobType, out _))
                || !Enum.TryParse(villager.housingState, out VillagerHousingState housingState)
                || !Enum.IsDefined(typeof(VillagerHousingState), housingState)
                || (!string.IsNullOrEmpty(villager.equippedItem)
                    && (!Enum.TryParse(villager.equippedItem, out VillagerItemType equippedItem)
                        || !Enum.IsDefined(typeof(VillagerItemType), equippedItem))))
            {
                return false;
            }

            if (villager.hasJobProductionProgress)
            {
                if (!Enum.TryParse(
                        villager.jobProductionItemType,
                        out VillagerItemType jobProductionItemType)
                    || !Enum.IsDefined(typeof(VillagerItemType), jobProductionItemType)
                    || !gameData.TryGetJobDefinition(jobType, out JobDefinition jobDefinition)
                    || jobDefinition.workType != JobWorkType.Production
                    || jobDefinition.outputItemType != jobProductionItemType
                    || !IsFinitePositive(villager.jobProductionDurationSeconds)
                    || !IsFiniteNonnegative(villager.jobProductionProgressSeconds)
                    || villager.jobProductionProgressSeconds
                        > villager.jobProductionDurationSeconds)
                {
                    return false;
                }
            }

            if (villager.home != null
                && (!IsValidCell(villager.home.originCell)
                    || !IsValidCell(villager.home.dimensions)
                    || !IsValidCell(villager.home.entranceCell)
                    || villager.home.dimensions.x <= 0
                    || villager.home.dimensions.y <= 0
                    || !IsValidPosition(villager.home.position)
                    || villager.home.storedMaterials == null
                    || villager.home.storedItems == null
                    || !IsValidResourceAmountArray(villager.home.storedMaterials, false, true)
                    || !IsValidAmountArray(villager.home.storedItems, typeof(VillagerItemType))))
            {
                return false;
            }

            if (villager.home != null)
            {
                if (!IsValidSavedHomeProgress(villager.home, villager, housingState))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(villager.home.houseDefinitionId)
                    && FindHouseDefinition(
                        gameData.houseDefinitions,
                        villager.home.houseDefinitionId) == null)
                {
                    return false;
                }

                if (villager.home.hasProductionProgress
                    && (!Enum.TryParse(
                            villager.home.productionItemType,
                            out VillagerItemType productionItemType)
                        || !Enum.IsDefined(typeof(VillagerItemType), productionItemType)
                        || !IsFinitePositive(villager.home.productionDurationSeconds)
                        || !IsFiniteNonnegative(villager.home.productionProgressSeconds)
                        || villager.home.productionProgressSeconds
                            > villager.home.productionDurationSeconds))
                {
                    return false;
                }
            }

            if (!IsValidSavedJobWorkplace(villager, jobType, gameData))
            {
                return false;
            }
        }

        foreach (GameSaveResourceObject resourceObject in snapshot.resourceObjects)
        {
            if (resourceObject == null
                || string.IsNullOrWhiteSpace(resourceObject.definitionId)
                || !resourceObject.hasGridCell
                || !IsValidCell(resourceObject.gridCell)
                || !IsValidPosition(resourceObject.position)
                || !IsValidResourceObjectTransform(
                    resourceObject,
                    snapshot.hasLegacyResourceTransforms)
                || resourceObject.remainingResources == null
                || !IsValidResourceAmountArray(resourceObject.remainingResources, true, false))
            {
                return false;
            }

            ResourceObjectDefinition definition =
                FindResourceObjectDefinition(gameData.resourceObjectDefinitions, resourceObject.definitionId);
            if (definition == null
                || definition.outputs == null
                || definition.outputs.Length != resourceObject.remainingResources.Length)
            {
                return false;
            }

            HashSet<ResourceType> savedTypes = new HashSet<ResourceType>();
            foreach (GameSaveResourceAmount savedAmount in resourceObject.remainingResources)
            {
                if (!Enum.TryParse(savedAmount.resourceType, out ResourceType resourceType)
                    || !savedTypes.Add(resourceType))
                {
                    return false;
                }
            }

            foreach (ResourceOutputDefinition output in definition.outputs)
            {
                if (output == null || !savedTypes.Contains(output.type))
                {
                    return false;
                }
            }
        }

        foreach (GameSaveCommunityStorage storage in snapshot.communityStorages)
        {
            if (storage == null
                || storage.capacity <= 0
                || !IsValidCell(storage.originCell)
                || !IsValidCell(storage.dimensions)
                || storage.dimensions.x <= 0
                || storage.dimensions.y <= 0
                || !IsValidCell(storage.entranceCell)
                || !IsValidPosition(storage.position)
                || storage.resources == null
                || !IsValidResourceAmountArray(storage.resources, false, true))
            {
                return false;
            }

            foreach (GameSaveResourceAmount storedResource in storage.resources)
            {
                if (!Enum.TryParse(storedResource.resourceType, out ResourceType resourceType)
                    || !gameData.IsCommunityStorageResource(resourceType))
                {
                    return false;
                }
            }
        }

        foreach (GameSaveRoad road in snapshot.roads)
        {
            if (road == null || road.keyPoints == null || road.keyPoints.Length < 2)
            {
                return false;
            }

            foreach (GameSavePosition point in road.keyPoints)
            {
                if (!IsValidPosition(point))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsValidSavedHomeProgress(
        GameSaveHome savedHome,
        GameSaveVillager savedVillager,
        VillagerHousingState housingState)
    {
        if (string.IsNullOrWhiteSpace(savedHome.houseDefinitionId))
        {
            return !savedHome.isComplete
                && housingState != VillagerHousingState.Housed
                && !IsHouseConstructionStarted(housingState)
                && !savedHome.hasProductionProgress
                && savedVillager.constructionProgressSeconds == 0f
                && savedVillager.constructionHammerTimer == 0f;
        }

        if (savedHome.isComplete != (housingState == VillagerHousingState.Housed))
        {
            return false;
        }

        if (housingState == VillagerHousingState.Housed)
        {
            return savedVillager.constructionProgressSeconds == 0f
                && savedVillager.constructionHammerTimer == 0f;
        }

        if (!IsHouseConstructionStarted(housingState))
        {
            return false;
        }

        float duration = housingState == VillagerHousingState.BuildingHouseStageOne
            ? GameData.Instance.houseSettings.firstConstructionDurationSeconds
            : housingState == VillagerHousingState.HouseConstructionBreak
                ? GameData.Instance.houseSettings.constructionBreakDurationSeconds
                : GameData.Instance.houseSettings.secondConstructionDurationSeconds;
        return IsFiniteNonnegative(duration)
            && IsFiniteNonnegative(savedVillager.constructionProgressSeconds)
            && savedVillager.constructionProgressSeconds <= duration
            && IsFiniteNonnegative(savedVillager.constructionHammerTimer);
    }

    private static bool IsValidSavedWorkplace(
        GameSaveHome savedWorkplace,
        GameData gameData)
    {
        if (string.IsNullOrWhiteSpace(savedWorkplace.houseDefinitionId)
            || FindHouseDefinition(gameData.houseDefinitions, savedWorkplace.houseDefinitionId) == null
            || !IsValidCell(savedWorkplace.originCell)
            || !IsValidCell(savedWorkplace.dimensions)
            || savedWorkplace.dimensions.x <= 0
            || savedWorkplace.dimensions.y <= 0
            || !IsValidCell(savedWorkplace.entranceCell)
            || !IsValidPosition(savedWorkplace.position)
            || savedWorkplace.storedMaterials == null
            || !IsValidResourceAmountArray(savedWorkplace.storedMaterials, false, true)
            || savedWorkplace.storedItems == null
            || !IsValidAmountArray(savedWorkplace.storedItems, typeof(VillagerItemType)))
        {
            return false;
        }

        if (savedWorkplace.hasAnnex && !savedWorkplace.isComplete)
        {
            return false;
        }

        return !savedWorkplace.hasProductionProgress
            || (Enum.TryParse(
                    savedWorkplace.productionItemType,
                    out VillagerItemType productionItemType)
                && Enum.IsDefined(typeof(VillagerItemType), productionItemType)
                && IsFinitePositive(savedWorkplace.productionDurationSeconds)
                && IsFiniteNonnegative(savedWorkplace.productionProgressSeconds)
                && savedWorkplace.productionProgressSeconds
                    <= savedWorkplace.productionDurationSeconds);
    }

    private static bool IsValidSavedJobWorkplace(
        GameSaveVillager villager,
        JobType jobType,
        GameData gameData)
    {
        if (!Enum.TryParse(
                villager.jobWorkplaceStage,
                out VillagerJobController.WorkplaceStage stage)
            || !Enum.IsDefined(typeof(VillagerJobController.WorkplaceStage), stage)
            || !IsFiniteNonnegative(villager.jobWorkplaceConstructionProgressSeconds)
            || !IsFiniteNonnegative(villager.jobWorkplaceHammeringTimer))
        {
            return false;
        }

        bool hasHomeWorkplace = villager.jobWorkplaceIsHome;
        bool hasSeparateWorkplace = villager.jobWorkplace != null;
        if (stage == VillagerJobController.WorkplaceStage.None)
        {
            return !hasHomeWorkplace
                && !hasSeparateWorkplace
                && !villager.jobWorkplaceIsSeparate
                && !villager.jobWorkplaceBuildingNewHouse
                && villager.jobWorkplaceConstructionProgressSeconds == 0f
                && villager.jobWorkplaceHammeringTimer == 0f;
        }

        if (hasHomeWorkplace == hasSeparateWorkplace
            || jobType == JobType.None
            || !gameData.TryGetJobDefinition(jobType, out JobDefinition jobDefinition)
            || !jobDefinition.needsAnnex)
        {
            return false;
        }

        if (hasHomeWorkplace)
        {
            if (villager.jobWorkplaceIsSeparate
                || villager.jobWorkplaceBuildingNewHouse
                || villager.home == null
                || !IsValidSavedWorkplace(villager.home, gameData))
            {
                return false;
            }

            if (stage == VillagerJobController.WorkplaceStage.Ready)
            {
                return villager.home.isComplete && villager.home.hasAnnex
                    && HasNoWorkplaceConstructionProgress(villager);
            }

            if (stage == VillagerJobController.WorkplaceStage.GatheringMaterials)
            {
                return !villager.home.hasAnnex
                    && HasNoWorkplaceConstructionProgress(villager);
            }

            if (stage == VillagerJobController.WorkplaceStage.RenovatingAnnex)
            {
                return villager.home.isComplete
                    && !villager.home.hasAnnex
                    && IsValidWorkplaceConstructionProgress(villager, stage);
            }

            return false;
        }

        if (!villager.jobWorkplaceIsSeparate
            || !IsValidSavedWorkplace(villager.jobWorkplace, gameData))
        {
            return false;
        }

        GameSaveHome savedWorkplace = villager.jobWorkplace;
        if (!savedWorkplace.isComplete)
        {
            return villager.jobWorkplaceBuildingNewHouse
                && !savedWorkplace.hasAnnex
                && (stage == VillagerJobController.WorkplaceStage.GatheringMaterials
                    ? HasNoWorkplaceConstructionProgress(villager)
                    : IsNewWorkplaceConstructionStage(stage)
                        && IsValidWorkplaceConstructionProgress(villager, stage));
        }

        if (villager.jobWorkplaceBuildingNewHouse)
        {
            return false;
        }

        if (stage == VillagerJobController.WorkplaceStage.Ready)
        {
            return savedWorkplace.hasAnnex && HasNoWorkplaceConstructionProgress(villager);
        }

        if (stage == VillagerJobController.WorkplaceStage.GatheringMaterials)
        {
            return !savedWorkplace.hasAnnex
                && HasNoWorkplaceConstructionProgress(villager);
        }

        return stage == VillagerJobController.WorkplaceStage.RenovatingAnnex
            && !savedWorkplace.hasAnnex
            && IsValidWorkplaceConstructionProgress(villager, stage);
    }

    private static bool HasNoWorkplaceConstructionProgress(GameSaveVillager villager)
    {
        return villager.jobWorkplaceConstructionProgressSeconds == 0f
            && villager.jobWorkplaceHammeringTimer == 0f;
    }

    private static bool IsNewWorkplaceConstructionStage(
        VillagerJobController.WorkplaceStage stage)
    {
        return stage == VillagerJobController.WorkplaceStage.BuildingHouseStageOne
            || stage == VillagerJobController.WorkplaceStage.HouseConstructionBreak
            || stage == VillagerJobController.WorkplaceStage.BuildingHouseStageTwo;
    }

    private static bool IsValidWorkplaceConstructionProgress(
        GameSaveVillager villager,
        VillagerJobController.WorkplaceStage stage)
    {
        float duration = stage == VillagerJobController.WorkplaceStage.BuildingHouseStageOne
            ? GameData.Instance.houseSettings.firstConstructionDurationSeconds
            : stage == VillagerJobController.WorkplaceStage.HouseConstructionBreak
                ? GameData.Instance.houseSettings.constructionBreakDurationSeconds
                : GameData.Instance.houseSettings.secondConstructionDurationSeconds;
        return IsFinitePositive(duration)
            && villager.jobWorkplaceConstructionProgressSeconds <= duration;
    }

    private static bool IsHouseConstructionStarted(VillagerHousingState housingState)
    {
        return housingState == VillagerHousingState.BuildingHouseStageOne
            || housingState == VillagerHousingState.HouseConstructionBreak
            || housingState == VillagerHousingState.BuildingHouseStageTwo;
    }

    private static bool IsFiniteNonnegative(float value)
    {
        return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinitePositive(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static ResourceObjectDefinition FindResourceObjectDefinition(
        ResourceObjectDefinition[] definitions,
        string id)
    {
        if (definitions == null || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (ResourceObjectDefinition definition in definitions)
        {
            if (definition != null && string.Equals(definition.id, id, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private static bool IsValidAmountArray(GameSaveAmount[] amounts, Type enumType)
    {
        if (amounts == null)
        {
            return false;
        }

        foreach (GameSaveAmount amount in amounts)
        {
            bool validType = enumType == typeof(VillagerItemType)
                ? Enum.TryParse(amount != null ? amount.type : null, out VillagerItemType itemType)
                    && Enum.IsDefined(typeof(VillagerItemType), itemType)
                : enumType == typeof(ResourceType)
                    && Enum.TryParse(amount != null ? amount.type : null, out ResourceType resourceType)
                    && Enum.IsDefined(typeof(ResourceType), resourceType);
            if (amount == null
                || amount.amount <= 0
                || !validType)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidJobInputStock(GameSaveAmount[] amounts)
    {
        if (!IsValidAmountArray(amounts, typeof(ResourceType)))
        {
            return false;
        }

        HashSet<ResourceType> savedTypes = new HashSet<ResourceType>();
        foreach (GameSaveAmount amount in amounts)
        {
            if (!Enum.TryParse(amount.type, out ResourceType resourceType)
                || !savedTypes.Add(resourceType))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidResourceAmountArray(
        GameSaveResourceAmount[] amounts,
        bool includeBareHandAmount,
        bool requirePositiveAmount)
    {
        if (amounts == null)
        {
            return false;
        }

        foreach (GameSaveResourceAmount amount in amounts)
        {
            if (!IsValidResourceAmount(amount, includeBareHandAmount)
                || (requirePositiveAmount && amount.amount <= 0))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidResourceAmount(
        GameSaveResourceAmount amount,
        bool includeBareHandAmount)
    {
        return amount != null
            && amount.amount >= 0
            && amount.bareHandAmount >= 0
            && (!includeBareHandAmount || amount.bareHandAmount <= amount.amount)
            && Enum.TryParse(amount.resourceType, out ResourceType resourceType)
            && Enum.IsDefined(typeof(ResourceType), resourceType);
    }

    private static bool IsValidPosition(GameSavePosition position)
    {
        return position != null
            && !float.IsNaN(position.x)
            && !float.IsNaN(position.y)
            && !float.IsNaN(position.z)
            && !float.IsInfinity(position.x)
            && !float.IsInfinity(position.y)
            && !float.IsInfinity(position.z);
    }

    private static bool IsValidResourceObjectTransform(
        GameSaveResourceObject resourceObject,
        bool allowLegacyTransform)
    {
        if (!resourceObject.hasSavedTransform)
        {
            return allowLegacyTransform;
        }

        return !float.IsNaN(resourceObject.rotationZ)
            && !float.IsInfinity(resourceObject.rotationZ)
            && IsValidPosition(resourceObject.localScale)
            && resourceObject.localScale.x != 0f
            && resourceObject.localScale.y != 0f;
    }

    private static bool IsValidPerson(GameSavePerson person)
    {
        if (person == null
            || string.IsNullOrWhiteSpace(person.name)
            || person.personalityTypes == null
            || person.reactionTime < 0f
            || float.IsNaN(person.reactionTime)
            || float.IsInfinity(person.reactionTime))
        {
            return false;
        }

        foreach (string personalityTypeName in person.personalityTypes)
        {
            if (!Enum.TryParse(personalityTypeName, out VillagerPersonalityType personalityType)
                || !Enum.IsDefined(typeof(VillagerPersonalityType), personalityType))
            {
                return false;
            }
        }

        return Player.CanRestorePerson(person);
    }

    private static bool IsValidCell(GameSaveCell cell)
    {
        return cell != null;
    }
}
