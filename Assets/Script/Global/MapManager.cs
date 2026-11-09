using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MapManager : MonoBehaviour
{
    private sealed class AvailableHouseRecord
    {
        public HousePlotRecord housePlot;
        public GameObject houseObject;
    }

    [Serializable]
    private class RoadRecord
    {
        public List<Vector3> keyPoints = new List<Vector3>();
    }

    private static MapManager instance;

    public static MapManager Instance
    {
        get { return instance; }
    }

    private readonly List<Vector3> roadKeyPoints = new List<Vector3>();
    private readonly List<RoadRecord> roads = new List<RoadRecord>();
    private readonly HashSet<Vector2Int> roadCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, ResourceObject> resourceObjectsByCell =
        new Dictionary<Vector2Int, ResourceObject>();
    private readonly Dictionary<ResourceObject, Villager> resourceReservations =
        new Dictionary<ResourceObject, Villager>();
    private readonly HashSet<Vector2Int> claimedHouseCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> communityStorageCells = new HashSet<Vector2Int>();
    private readonly List<Villager> housePlotQueue = new List<Villager>();
    private readonly Dictionary<Villager, HousePlotRecord> claimedHousePlots =
        new Dictionary<Villager, HousePlotRecord>();
    private readonly Dictionary<Villager, HousePlotRecord> claimedWorkplacePlots =
        new Dictionary<Villager, HousePlotRecord>();
    private readonly Dictionary<Villager, GameObject> workplacePlotObjects =
        new Dictionary<Villager, GameObject>();
    private readonly HashSet<Villager> startedWorkplacePlots = new HashSet<Villager>();
    private readonly Dictionary<Villager, AvailableHouseRecord> claimedWorkplaceHouses =
        new Dictionary<Villager, AvailableHouseRecord>();
    private readonly Dictionary<Villager, GameObject> completedHouseObjects =
        new Dictionary<Villager, GameObject>();
    private readonly List<AvailableHouseRecord> availableHouses =
        new List<AvailableHouseRecord>();
    private readonly List<CommunityStorage> communityStorages = new List<CommunityStorage>();
    private readonly List<HouseConstructionSite> activeConstructionSites =
        new List<HouseConstructionSite>();
    private readonly Dictionary<CommunityStorage, Dictionary<ResourceType, List<GameObject>>>
        communityStorageIconInstances =
            new Dictionary<CommunityStorage, Dictionary<ResourceType, List<GameObject>>>();

    [SerializeField] private Vector2 mapSize = new Vector2(100f, 100f);
    [SerializeField] private ResourceObjectPrefabReference[] resourceObjectPrefabs;

    private MapSettings settings;
    private int houseStorageCapacity;
    private CommunityStorageSettings communityStorageSettings;
    private UnityEngine.Camera mapCamera;
    private GameObject startMarkerInstance;
    private LineRenderer roadPreviewInstance;
    private Vector3 roadStartPoint;
    private Vector2 pointerDownPosition;
    private bool isRoadBuilding;
    private bool hasRoadStartPoint;
    private bool isTrackingClick;
    private bool loggedMissingCommunityStoragePrefab;
    private bool loggedMissingCommunityStorageIconManager;
    private int communityStorageIconSortingLayerId;
    private int communityStorageIconSortingOrder;

    private void Start()
    {
        SpawnResourceObjects();
    }

    public IReadOnlyList<Vector3> RoadKeyPoints
    {
        get { return roadKeyPoints; }
    }

    public int RoadRevision { get; private set; }
    public int PathfindingRevision { get; private set; }
    public int ResourceAvailabilityRevision { get; private set; }

    public bool IsRoadBuilding
    {
        get { return isRoadBuilding; }
    }

    public float GridCellSize
    {
        get { return settings != null ? settings.gridCellSize : 0f; }
    }

    public float RoadMovementSpeedMultiplier
    {
        get { return settings != null ? settings.roadMovementSpeedMultiplier : 1f; }
    }

    public Vector2 MapSize
    {
        get { return mapSize; }
    }

    public Vector3 MapCenter
    {
        get { return transform.position; }
    }

    public IEnumerable<Vector2Int> OccupiedResourceCells
    {
        get { return resourceObjectsByCell.Keys; }
    }

    public IEnumerable<Vector2Int> RoadCells
    {
        get { return roadCells; }
    }

    public IEnumerable<Vector2Int> ClaimedHouseCells
    {
        get { return claimedHouseCells; }
    }

    public IReadOnlyList<CommunityStorage> CommunityStorages
    {
        get { return communityStorages; }
    }

    public bool HasCommunityStorageDepositCapacity(
        IReadOnlyList<VillagerResourceEntry> inventory)
    {
        return GetCommunityStorageDepositAmount(inventory) > 0;
    }

    public int GetCommunityStorageDepositAmount(
        IReadOnlyList<VillagerResourceEntry> inventory)
    {
        if (inventory == null || communityStorageSettings == null)
        {
            return 0;
        }

        int villagerAmount = 0;
        foreach (VillagerResourceEntry entry in inventory)
        {
            villagerAmount += entry != null ? Mathf.Max(0, entry.amount) : 0;
        }

        int availableCapacity = communityStorageSettings.maximumAmountPerStorage;
        foreach (CommunityStorage storage in communityStorages)
        {
            if (storage != null)
            {
                availableCapacity += storage.RemainingCapacity;
            }
        }

        return Mathf.Min(villagerAmount, Mathf.Max(0, availableCapacity));
    }

    public bool TryGetCommunityStorageEntrancePosition(
        CommunityStorage storage,
        out Vector3 position)
    {
        position = Vector3.zero;
        if (storage == null
            || !storage.IsInitialized
            || settings == null
            || settings.gridCellSize <= 0f)
        {
            return false;
        }

        position = GridCellToWorldPosition(storage.EntranceCell, settings.gridCellSize);
        position.z = transform.position.z;
        return true;
    }

    public bool HasActiveConstructionSiteFor(Villager helper)
    {
        if (helper == null)
        {
            return false;
        }

        foreach (HouseConstructionSite site in activeConstructionSites)
        {
            if (site != null && site.CanAcceptHelper(helper))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetNearestActiveConstructionSite(
        Villager helper,
        out HouseConstructionSite nearestSite)
    {
        nearestSite = null;
        if (helper == null)
        {
            return false;
        }

        float nearestDistanceSquared = float.PositiveInfinity;
        for (int index = activeConstructionSites.Count - 1; index >= 0; index--)
        {
            HouseConstructionSite candidate = activeConstructionSites[index];
            if (candidate == null || !candidate.IsActive)
            {
                activeConstructionSites.RemoveAt(index);
                continue;
            }

            if (!candidate.CanAcceptHelper(helper))
            {
                continue;
            }

            float distanceSquared =
                (candidate.WorkPosition - helper.transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestSite = candidate;
            }
        }

        return nearestSite != null;
    }

    public void RegisterActiveConstructionSite(HouseConstructionSite site)
    {
        if (site != null && site.IsActive && !activeConstructionSites.Contains(site))
        {
            activeConstructionSites.Add(site);
        }
    }

    public void UnregisterActiveConstructionSite(HouseConstructionSite site)
    {
        if (site != null)
        {
            activeConstructionSites.Remove(site);
        }
    }

    public bool TryClaimAvailableWorkplaceHouse(
        Villager villager,
        out HousePlotRecord housePlot,
        out GameObject houseObject)
    {
        housePlot = null;
        houseObject = null;
        if (villager == null || claimedWorkplaceHouses.ContainsKey(villager))
        {
            return false;
        }

        for (int annexedHousePass = 0; annexedHousePass < 2; annexedHousePass++)
        {
            for (int index = availableHouses.Count - 1; index >= 0; index--)
            {
                AvailableHouseRecord availableHouse = availableHouses[index];
                if (availableHouse.housePlot == null || availableHouse.houseObject == null)
                {
                    availableHouses.RemoveAt(index);
                    continue;
                }

                HouseWorkplace workplace = GetOrCreateHouseWorkplace(availableHouse.houseObject);
                if (workplace == null || workplace.HasAnnex != (annexedHousePass == 0))
                {
                    continue;
                }

                if (!workplace.TryReserve(villager))
                {
                    continue;
                }

                housePlot = availableHouse.housePlot;
                houseObject = availableHouse.houseObject;
                availableHouses.RemoveAt(index);
                claimedWorkplaceHouses.Add(villager, availableHouse);
                return true;
            }
        }

        return false;
    }

    public bool TryClaimWorkplaceHousePlot(
        Villager villager,
        Vector2Int dimensions,
        out HousePlotRecord housePlot)
    {
        housePlot = null;
        if (villager == null
            || dimensions.x <= 0
            || dimensions.y <= 0
            || GridCellSize <= 0f)
        {
            return false;
        }

        if (claimedWorkplacePlots.TryGetValue(villager, out housePlot))
        {
            return true;
        }

        housePlotQueue.RemoveAll(queuedVillager => queuedVillager == null);
        if (!housePlotQueue.Contains(villager))
        {
            housePlotQueue.Add(villager);
        }

        if (housePlotQueue.Count == 0 || housePlotQueue[0] != villager)
        {
            return false;
        }

        if (!TryFindAvailableHousePlot(dimensions, out housePlot))
        {
            return false;
        }

        housePlotQueue.RemoveAt(0);
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                claimedHouseCells.Add(new Vector2Int(
                    housePlot.originCell.x + x,
                    housePlot.originCell.y + y));
            }
        }

        claimedWorkplacePlots.Add(villager, housePlot);
        return true;
    }

    public void SetWorkplaceHouseObject(Villager villager, GameObject houseObject)
    {
        if (villager == null
            || houseObject == null
            || !claimedWorkplacePlots.ContainsKey(villager))
        {
            Debug.LogError("Cannot set a workplace house object without a claimed workplace plot.", this);
            return;
        }

        workplacePlotObjects[villager] = houseObject;
    }

    public void NotifyWorkplaceConstructionStarted(Villager villager)
    {
        if (villager != null
            && claimedWorkplacePlots.TryGetValue(villager, out HousePlotRecord housePlot)
            && housePlot != null)
        {
            startedWorkplacePlots.Add(villager);
            RemoveBuildableOverResourceObjectsInPlot(housePlot);
            PathfindingRevision++;
        }
    }

    public bool RegisterCompletedWorkplaceHouse(
        Villager villager,
        HousePlotRecord housePlot,
        GameObject houseObject)
    {
        if (villager == null
            || housePlot == null
            || houseObject == null
            || !claimedWorkplacePlots.TryGetValue(villager, out HousePlotRecord claimedPlot)
            || claimedPlot != housePlot)
        {
            Debug.LogError(
                "Cannot register a completed workplace without its villager, plot, and house.",
                this);
            return false;
        }

        claimedWorkplacePlots.Remove(villager);
        workplacePlotObjects.Remove(villager);
        startedWorkplacePlots.Remove(villager);
        claimedWorkplaceHouses[villager] = new AvailableHouseRecord
        {
            housePlot = housePlot,
            houseObject = houseObject
        };
        GetOrCreateHouseWorkplace(houseObject);
        CreateHouseRoadConnection(housePlot);
        return true;
    }

    public void ReleaseWorkplaceHouse(Villager villager)
    {
        if (villager == null)
        {
            return;
        }

        housePlotQueue.Remove(villager);
        if (claimedWorkplacePlots.TryGetValue(villager, out HousePlotRecord activePlot))
        {
            bool constructionStarted = startedWorkplacePlots.Contains(villager);
            if (activePlot != null)
            {
                RemoveHousePlotCells(activePlot);
            }

            if (workplacePlotObjects.TryGetValue(villager, out GameObject unfinishedHouse)
                && unfinishedHouse != null)
            {
                Destroy(unfinishedHouse);
            }

            claimedWorkplacePlots.Remove(villager);
            workplacePlotObjects.Remove(villager);
            startedWorkplacePlots.Remove(villager);
            if (constructionStarted)
            {
                PathfindingRevision++;
            }
        }

        if (claimedWorkplaceHouses.TryGetValue(villager, out AvailableHouseRecord house))
        {
            claimedWorkplaceHouses.Remove(villager);
            if (house.housePlot != null && house.houseObject != null)
            {
                GetOrCreateHouseWorkplace(house.houseObject).Release(villager);
                availableHouses.Add(house);
            }
        }
    }

    public bool TryReserveWorkplaceAnnex(GameObject houseObject, Villager villager)
    {
        HouseVisual houseVisual = houseObject != null
            ? houseObject.GetComponent<HouseVisual>()
            : null;
        if (houseVisual == null)
        {
            Debug.LogError("A workplace requires a house with a HouseVisual component.", this);
            return false;
        }

        HouseWorkplace workplace = GetOrCreateHouseWorkplace(houseObject);
        return workplace != null && workplace.TryReserve(villager);
    }

    public void ReleaseWorkplaceAnnex(GameObject houseObject, Villager villager)
    {
        if (houseObject == null || villager == null)
        {
            return;
        }

        HouseWorkplace workplace = houseObject.GetComponent<HouseWorkplace>();
        if (workplace != null)
        {
            workplace.Release(villager);
        }
    }

    public HouseWorkplace GetOrCreateHouseWorkplace(GameObject houseObject)
    {
        if (houseObject == null)
        {
            return null;
        }

        if (houseStorageCapacity <= 0)
        {
            Debug.LogError("House storage capacity could not be loaded from GameData.", this);
            return null;
        }

        HouseWorkplace workplace = houseObject.GetComponent<HouseWorkplace>();
        if (workplace == null)
        {
            workplace = houseObject.AddComponent<HouseWorkplace>();
        }

        if (!workplace.IsInitialized && !workplace.Initialize(houseStorageCapacity))
        {
            return null;
        }

        return workplace;
    }

    public HouseConstructionSite GetOrCreateHouseConstructionSite(GameObject houseObject)
    {
        if (houseObject == null)
        {
            return null;
        }

        HouseConstructionSite site = houseObject.GetComponent<HouseConstructionSite>();
        if (site == null)
        {
            site = houseObject.AddComponent<HouseConstructionSite>();
        }

        return site;
    }

    private void RemoveBuildableOverResourceObjectsOnRoadPath(
        IReadOnlyList<Vector3> roadPath,
        ISet<Vector2Int> roadCellsToAdd)
    {
        List<ResourceObject> resourcesToRemove = new List<ResourceObject>();
        foreach (KeyValuePair<Vector2Int, ResourceObject> occupiedCell in resourceObjectsByCell)
        {
            ResourceObject resourceObject = occupiedCell.Value;
            if (resourceObject == null || !resourceObject.CanBeRemovedByConstruction)
            {
                continue;
            }

            bool intersectsRoad = roadCellsToAdd.Contains(occupiedCell.Key);
            for (int index = 0; index < roadPath.Count && !intersectsRoad; index++)
            {
                intersectsRoad = WorldPositionToGridCell(
                    roadPath[index],
                    settings.gridCellSize) == occupiedCell.Key;
            }

            for (int index = 0; index < roadPath.Count - 1 && !intersectsRoad; index++)
            {
                intersectsRoad = SegmentIntersectsCell(
                    roadPath[index],
                    roadPath[index + 1],
                    occupiedCell.Key,
                    settings.gridCellSize);
            }

            if (intersectsRoad)
            {
                resourcesToRemove.Add(resourceObject);
            }
        }

        foreach (ResourceObject resourceObject in resourcesToRemove)
        {
            RemoveResourceObjectWithoutYield(resourceObject);
        }
    }

    public bool TryCreateCommunityStorage(
        Villager villager,
        Vector2Int dimensions,
        out CommunityStorage storage)
    {
        storage = null;
        if (villager == null
            || dimensions.x <= 0
            || dimensions.y <= 0
            || settings == null
            || settings.gridCellSize <= 0f)
        {
            return false;
        }

        GameOverlord gameOverlord = GameOverlord.Instance;
        if (gameOverlord == null || gameOverlord.RoadTilePrefab == null)
        {
            if (!loggedMissingCommunityStoragePrefab)
            {
                Debug.LogError(
                    "Community storage requires an active GameOverlord with a road tile prefab.",
                    this);
                loggedMissingCommunityStoragePrefab = true;
            }

            return false;
        }

        SpriteRenderer roadTileRenderer =
            gameOverlord.RoadTilePrefab.GetComponentInChildren<SpriteRenderer>();
        if (roadTileRenderer != null)
        {
            communityStorageIconSortingLayerId = roadTileRenderer.sortingLayerID;
            communityStorageIconSortingOrder = roadTileRenderer.sortingOrder + 1;
        }

        if (!TryFindAvailableHousePlot(dimensions, out HousePlotRecord availablePlot))
        {
            return false;
        }

        CommunityStorageRecord storageRecord = new CommunityStorageRecord
        {
            originCell = availablePlot.originCell,
            dimensions = availablePlot.dimensions,
            entranceCell = availablePlot.entranceCell
        };
        Vector3 firstCellPosition = GridCellToWorldPosition(
            storageRecord.originCell,
            settings.gridCellSize);
        Vector3 lastCellPosition = GridCellToWorldPosition(
            storageRecord.originCell + storageRecord.dimensions - Vector2Int.one,
            settings.gridCellSize);
        Vector3 storageCenter = (firstCellPosition + lastCellPosition) * 0.5f;
        storageCenter.z = transform.position.z;
        GameObject storageObject = new GameObject("Community Storage");
        storageObject.transform.SetParent(transform, false);
        storageObject.transform.position = storageCenter;
        storage = storageObject.AddComponent<CommunityStorage>();
        if (!storage.Initialize(
                storageRecord,
                communityStorageSettings.maximumAmountPerStorage))
        {
            Destroy(storageObject);
            storage = null;
            return false;
        }

        BoxCollider2D storageCollider = storageObject.AddComponent<BoxCollider2D>();
        storageCollider.isTrigger = true;
        storageCollider.size = new Vector2(
            dimensions.x * settings.gridCellSize,
            dimensions.y * settings.gridCellSize);

        RemoveBuildableOverResourceObjectsInPlot(availablePlot);
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                Vector2Int cell = new Vector2Int(
                    storageRecord.originCell.x + x,
                    storageRecord.originCell.y + y);
                communityStorageCells.Add(cell);
            }
        }

        Vector3 bottomLeft = GridCellToWorldPosition(
            storageRecord.originCell,
            settings.gridCellSize);
        bottomLeft.x -= settings.gridCellSize * 0.5f;
        bottomLeft.y -= settings.gridCellSize * 0.5f;
        int tileCountX = Mathf.Max(
            1,
            Mathf.CeilToInt(dimensions.x * settings.gridCellSize / settings.roadTileSpacing));
        int tileCountY = Mathf.Max(
            1,
            Mathf.CeilToInt(dimensions.y * settings.gridCellSize / settings.roadTileSpacing));
        for (int x = 0; x < tileCountX; x++)
        {
            for (int y = 0; y < tileCountY; y++)
            {
                Vector3 tilePosition = bottomLeft + new Vector3(
                    (x + 0.5f) * dimensions.x * settings.gridCellSize / tileCountX,
                    (y + 0.5f) * dimensions.y * settings.gridCellSize / tileCountY,
                    0f);
                tilePosition.z = transform.position.z;
                Instantiate(
                    gameOverlord.RoadTilePrefab,
                    tilePosition,
                    Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)),
                    storageObject.transform);
            }
        }

        communityStorages.Add(storage);
        communityStorageIconInstances.Add(
            storage,
            new Dictionary<ResourceType, List<GameObject>>());
        PathfindingRevision++;
        return true;
    }

    public bool TryGetCommunityStorageAtWorldPosition(
        Vector3 worldPosition,
        out CommunityStorage storage)
    {
        storage = null;
        if (settings == null || settings.gridCellSize <= 0f)
        {
            return false;
        }

        Vector2Int cell = WorldPositionToGridCell(worldPosition, settings.gridCellSize);
        foreach (CommunityStorage candidate in communityStorages)
        {
            if (candidate != null && candidate.ContainsCell(cell))
            {
                storage = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetHouseAtWorldPosition(
        Vector3 worldPosition,
        out GameObject houseObject)
    {
        houseObject = null;
        if (settings == null || settings.gridCellSize <= 0f)
        {
            return false;
        }

        Vector2Int cell = WorldPositionToGridCell(worldPosition, settings.gridCellSize);
        foreach (AvailableHouseRecord availableHouse in availableHouses)
        {
            if (TryGetFinishedHouseAtCell(
                    availableHouse.housePlot,
                    availableHouse.houseObject,
                    cell,
                    out houseObject))
            {
                return true;
            }
        }

        foreach (KeyValuePair<Villager, GameObject> residentHouse in completedHouseObjects)
        {
            if (claimedHousePlots.TryGetValue(residentHouse.Key, out HousePlotRecord housePlot)
                && TryGetFinishedHouseAtCell(
                    housePlot,
                    residentHouse.Value,
                    cell,
                    out houseObject))
            {
                return true;
            }
        }

        foreach (KeyValuePair<Villager, AvailableHouseRecord> workplace in claimedWorkplaceHouses)
        {
            if (workplace.Value != null
                && TryGetFinishedHouseAtCell(
                    workplace.Value.housePlot,
                    workplace.Value.houseObject,
                    cell,
                    out houseObject))
            {
                return true;
            }
        }

        return false;
    }

    public int GetCommunityStorageAmount(ResourceType resourceType)
    {
        if (!Enum.IsDefined(typeof(ResourceType), resourceType))
        {
            return 0;
        }

        int total = 0;
        foreach (CommunityStorage storage in communityStorages)
        {
            if (storage != null)
            {
                total += storage.GetAmount(resourceType);
            }
        }

        return total;
    }

    public int GetCommunityStorageTotalAmount()
    {
        int totalAmount = 0;
        foreach (CommunityStorage storage in communityStorages)
        {
            if (storage != null)
            {
                totalAmount += storage.GetTotalAmount();
            }
        }

        return totalAmount;
    }

    public bool TryDepositCommunityStorage(
        CommunityStorage storage,
        ResourceType resourceType,
        int requestedAmount,
        out int depositedAmount)
    {
        depositedAmount = 0;
        if (storage == null
            || !communityStorages.Contains(storage)
            || requestedAmount <= 0
            || !storage.TryDeposit(resourceType, requestedAmount, out depositedAmount))
        {
            return false;
        }

        RefreshCommunityStorageIcons(storage, resourceType);
        return true;
    }

    public bool TryWithdrawCommunityStorage(
        CommunityStorage storage,
        ResourceType resourceType,
        int requestedAmount,
        out int withdrawnAmount)
    {
        withdrawnAmount = 0;
        if (storage == null
            || !communityStorages.Contains(storage)
            || requestedAmount <= 0
            || !storage.TryWithdraw(resourceType, requestedAmount, out withdrawnAmount))
        {
            return false;
        }

        RefreshCommunityStorageIcons(storage, resourceType);
        return true;
    }

    public Bounds GetGridCellBounds(Vector2Int cell)
    {
        float gridCellSize = GridCellSize;
        Vector3 center = GridCellToWorldPosition(cell, gridCellSize);
        center.z = MapCenter.z;
        return new Bounds(center, new Vector3(gridCellSize, gridCellSize, 0f));
    }

    public bool TryGetRandomMapCellPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (settings == null || settings.gridCellSize <= 0f || mapSize.x <= 0f || mapSize.y <= 0f)
        {
            return false;
        }

        Vector2 mapMinimum = (Vector2)MapCenter - mapSize * 0.5f;
        Vector2 mapMaximum = (Vector2)MapCenter + mapSize * 0.5f;
        int minimumCellX = Mathf.FloorToInt(mapMinimum.x / settings.gridCellSize);
        int minimumCellY = Mathf.FloorToInt(mapMinimum.y / settings.gridCellSize);
        int maximumCellX = Mathf.CeilToInt(mapMaximum.x / settings.gridCellSize) - 1;
        int maximumCellY = Mathf.CeilToInt(mapMaximum.y / settings.gridCellSize) - 1;
        if (maximumCellX < minimumCellX || maximumCellY < minimumCellY)
        {
            return false;
        }

        Vector2Int cell = new Vector2Int(
            UnityEngine.Random.Range(minimumCellX, maximumCellX + 1),
            UnityEngine.Random.Range(minimumCellY, maximumCellY + 1));
        if (!IsCellInsideMap(cell))
        {
            return false;
        }

        position = GridCellToWorldPosition(cell, settings.gridCellSize);
        position.z = MapCenter.z;
        return true;
    }

    public bool TryFindPath(
        Vector3 startPosition,
        Vector3 destination,
        float arrivalDistance,
        out List<Vector3> waypoints,
        bool prioritizeRoads = false)
    {
        waypoints = null;
        if (settings == null || settings.gridCellSize <= 0f || arrivalDistance < 0f)
        {
            return false;
        }

        Vector2Int startCell = WorldPositionToGridCell(startPosition, settings.gridCellSize);
        Vector2Int destinationCell = WorldPositionToGridCell(destination, settings.gridCellSize);
        if (!IsCellBlocked(destinationCell))
        {
            if (!TryFindCellPath(
                    startCell,
                    destinationCell,
                    prioritizeRoads,
                    out List<Vector2Int> cellPath))
            {
                return false;
            }

            waypoints = CreateWorldPath(cellPath, destination);
            return true;
        }

        return TryFindApproachPath(
            startCell,
            destinationCell,
            destination,
            arrivalDistance,
            prioritizeRoads,
            out waypoints);
    }

    private bool TryFindApproachPath(
        Vector2Int startCell,
        Vector2Int blockedCell,
        Vector3 destination,
        float arrivalDistance,
        bool prioritizeRoads,
        out List<Vector3> waypoints)
    {
        waypoints = null;
        float gridCellSize = settings.gridCellSize;
        float minimumX = blockedCell.x * gridCellSize;
        float minimumY = blockedCell.y * gridCellSize;
        float maximumX = minimumX + gridCellSize;
        float maximumY = minimumY + gridCellSize;
        float inset = Mathf.Min(0.02f, gridCellSize * 0.01f);
        Vector2Int[] directions =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.up
        };
        List<Vector2Int> bestCellPath = null;
        Vector3 bestApproachPoint = Vector3.zero;
        float bestPathLength = float.PositiveInfinity;

        foreach (Vector2Int direction in directions)
        {
            Vector2Int approachCell = blockedCell + direction;
            if (!IsCellInsideMap(approachCell) || IsCellBlocked(approachCell))
            {
                continue;
            }

            Vector3 approachPoint = GridCellToWorldPosition(approachCell, gridCellSize);
            if (direction == Vector2Int.left)
            {
                approachPoint.x = minimumX - inset;
                approachPoint.y = Mathf.Clamp(destination.y, minimumY + inset, maximumY - inset);
            }
            else if (direction == Vector2Int.right)
            {
                approachPoint.x = maximumX + inset;
                approachPoint.y = Mathf.Clamp(destination.y, minimumY + inset, maximumY - inset);
            }
            else if (direction == Vector2Int.down)
            {
                approachPoint.y = minimumY - inset;
                approachPoint.x = Mathf.Clamp(destination.x, minimumX + inset, maximumX - inset);
            }
            else
            {
                approachPoint.y = maximumY + inset;
                approachPoint.x = Mathf.Clamp(destination.x, minimumX + inset, maximumX - inset);
            }

            if (Vector2.Distance(approachPoint, destination) > arrivalDistance
                || !TryFindCellPath(
                    startCell,
                    approachCell,
                    prioritizeRoads,
                    out List<Vector2Int> candidatePath))
            {
                continue;
            }

            float candidateLength = GetPathCost(candidatePath, prioritizeRoads) * gridCellSize
                + Vector2.Distance(GridCellToWorldPosition(approachCell, gridCellSize), approachPoint);
            if (candidateLength >= bestPathLength)
            {
                continue;
            }

            bestPathLength = candidateLength;
            bestCellPath = candidatePath;
            bestApproachPoint = approachPoint;
        }

        if (bestCellPath == null)
        {
            return false;
        }

        waypoints = CreateWorldPath(bestCellPath, bestApproachPoint);
        return true;
    }

    private bool TryFindCellPath(
        Vector2Int startCell,
        Vector2Int goalCell,
        bool prioritizeRoads,
        out List<Vector2Int> path)
    {
        path = null;
        if (!IsCellInsideMap(startCell)
            || !IsCellInsideMap(goalCell)
            || IsCellBlocked(goalCell))
        {
            return false;
        }

        List<Vector2Int> openCells = new List<Vector2Int> { startCell };
        HashSet<Vector2Int> closedCells = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> previousCells = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> movementCosts = new Dictionary<Vector2Int, float>
        {
            { startCell, 0 }
        };
        Vector2Int[] directions =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.up,
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(1, 1)
        };

        while (openCells.Count > 0)
        {
            int bestIndex = FindLowestCostCellIndex(
                openCells,
                movementCosts,
                goalCell,
                prioritizeRoads);
            Vector2Int currentCell = openCells[bestIndex];
            openCells.RemoveAt(bestIndex);
            if (currentCell == goalCell)
            {
                path = ReconstructCellPath(previousCells, startCell, goalCell);
                return path != null;
            }

            closedCells.Add(currentCell);
            foreach (Vector2Int direction in directions)
            {
                Vector2Int neighbor = currentCell + direction;
                bool isDiagonal = direction.x != 0 && direction.y != 0;
                if (closedCells.Contains(neighbor)
                    || !IsCellInsideMap(neighbor)
                    || (neighbor != startCell && IsCellBlocked(neighbor)))
                {
                    continue;
                }

                if (isDiagonal
                    && (IsCellBlocked(currentCell + new Vector2Int(direction.x, 0))
                        || IsCellBlocked(currentCell + new Vector2Int(0, direction.y))))
                {
                    continue;
                }

                float candidateCost = movementCosts[currentCell]
                    + GetCellTraversalCost(neighbor, prioritizeRoads, isDiagonal);
                if (movementCosts.TryGetValue(neighbor, out float knownCost)
                    && candidateCost >= knownCost)
                {
                    continue;
                }

                previousCells[neighbor] = currentCell;
                movementCosts[neighbor] = candidateCost;
                if (!openCells.Contains(neighbor))
                {
                    openCells.Add(neighbor);
                }
            }
        }

        return false;
    }

    private int FindLowestCostCellIndex(
        List<Vector2Int> openCells,
        Dictionary<Vector2Int, float> movementCosts,
        Vector2Int goalCell,
        bool prioritizeRoads)
    {
        int bestIndex = 0;
        float bestTotalCost = movementCosts[openCells[0]]
            + GetHeuristicCost(openCells[0], goalCell, prioritizeRoads);
        for (int index = 1; index < openCells.Count; index++)
        {
            Vector2Int cell = openCells[index];
            float totalCost = movementCosts[cell]
                + GetHeuristicCost(cell, goalCell, prioritizeRoads);
            if (totalCost < bestTotalCost)
            {
                bestIndex = index;
                bestTotalCost = totalCost;
            }
        }

        return bestIndex;
    }

    private float GetPathCost(IReadOnlyList<Vector2Int> path, bool prioritizeRoads)
    {
        float totalCost = 0f;
        for (int index = 1; index < path.Count; index++)
        {
            Vector2Int delta = path[index] - path[index - 1];
            totalCost += GetCellTraversalCost(
                path[index],
                prioritizeRoads,
                delta.x != 0 && delta.y != 0);
        }

        return totalCost;
    }

    private float GetCellTraversalCost(
        Vector2Int cell,
        bool prioritizeRoads,
        bool isDiagonal = false)
    {
        float stepLength = isDiagonal ? Mathf.Sqrt(2f) : 1f;
        Vector3 cellCenter = GridCellToWorldPosition(cell, settings.gridCellSize);
        bool receivesRoadPriority = prioritizeRoads
            && IsWithinRoadMovementRange(cellCenter);
        return stepLength
            * (receivesRoadPriority ? 1f / settings.roadMovementSpeedMultiplier : 1f);
    }

    private float GetHeuristicCost(Vector2Int currentCell, Vector2Int goalCell, bool prioritizeRoads)
    {
        float minimumStepCost = prioritizeRoads
            ? 1f / settings.roadMovementSpeedMultiplier
            : 1f;
        int deltaX = Mathf.Abs(currentCell.x - goalCell.x);
        int deltaY = Mathf.Abs(currentCell.y - goalCell.y);
        int diagonalSteps = Mathf.Min(deltaX, deltaY);
        int straightSteps = Mathf.Max(deltaX, deltaY) - diagonalSteps;
        return (diagonalSteps * Mathf.Sqrt(2f) + straightSteps) * minimumStepCost;
    }

    private static List<Vector2Int> ReconstructCellPath(
        Dictionary<Vector2Int, Vector2Int> previousCells,
        Vector2Int startCell,
        Vector2Int goalCell)
    {
        List<Vector2Int> path = new List<Vector2Int> { goalCell };
        Vector2Int currentCell = goalCell;
        while (currentCell != startCell)
        {
            if (path.Count > previousCells.Count
                || !previousCells.TryGetValue(currentCell, out Vector2Int previousCell)
                || previousCell == currentCell)
            {
                return null;
            }

            currentCell = previousCell;
            path.Add(currentCell);
        }

        path.Reverse();
        return path;
    }

    private List<Vector3> CreateWorldPath(IReadOnlyList<Vector2Int> cellPath, Vector3 destination)
    {
        List<Vector3> waypoints = new List<Vector3>();
        if (cellPath.Count > 1 && !IsCellBlocked(cellPath[0]))
        {
            Vector3 startCellCenter = GridCellToWorldPosition(cellPath[0], settings.gridCellSize);
            startCellCenter.z = destination.z;
            waypoints.Add(startCellCenter);
        }

        for (int index = 1; index < cellPath.Count; index++)
        {
            Vector3 waypoint = GridCellToWorldPosition(cellPath[index], settings.gridCellSize);
            waypoint.z = destination.z;
            waypoints.Add(waypoint);
        }

        if (waypoints.Count == 0
            || (waypoints[waypoints.Count - 1] - destination).sqrMagnitude > Mathf.Epsilon)
        {
            waypoints.Add(destination);
        }

        return waypoints;
    }

    private bool IsCellBlocked(Vector2Int cell)
    {
        return (resourceObjectsByCell.TryGetValue(cell, out ResourceObject resourceObject)
                && resourceObject != null)
            || communityStorageCells.Contains(cell)
            || IsOccupiedByStartedHouse(cell);
    }

    private void RefreshCommunityStorageIcons(
        CommunityStorage storage,
        ResourceType resourceType)
    {
        if (storage == null
            || !communityStorageIconInstances.TryGetValue(
                storage,
                out Dictionary<ResourceType, List<GameObject>> iconsByType))
        {
            return;
        }

        if (!iconsByType.TryGetValue(resourceType, out List<GameObject> instances))
        {
            instances = new List<GameObject>();
            iconsByType.Add(resourceType, instances);
        }

        foreach (GameObject iconInstance in instances)
        {
            if (iconInstance != null)
            {
                Destroy(iconInstance);
            }
        }

        instances.Clear();
        int resourceAmount = storage.GetAmount(resourceType);
        if (resourceAmount <= 0)
        {
            return;
        }

        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            if (!loggedMissingCommunityStorageIconManager)
            {
                Debug.LogError(
                    "Community storage contains resources, but UIManager is unavailable to provide resource icons.",
                    this);
                loggedMissingCommunityStorageIconManager = true;
            }

            return;
        }

        Sprite iconSprite = uiManager.GetResourceIcon(resourceType);
        if (iconSprite == null)
        {
            Debug.LogError(
                "No resource icon is configured for community storage resource " + resourceType + ".",
                this);
            return;
        }

        int iconCount = 1 + resourceAmount
            / communityStorageSettings.unitsPerAdditionalIcon;
        float iconWidth = settings.gridCellSize * 0.35f;
        float spriteWidth = iconSprite.bounds.size.x;
        float iconScale = spriteWidth > 0f ? iconWidth / spriteWidth : 1f;
        float iconRadius = iconWidth * 0.5f;
        Vector3 bottomLeft = GridCellToWorldPosition(
            storage.OriginCell,
            settings.gridCellSize);
        bottomLeft.x -= settings.gridCellSize * 0.5f;
        bottomLeft.y -= settings.gridCellSize * 0.5f;
        float width = storage.Dimensions.x * settings.gridCellSize;
        float height = storage.Dimensions.y * settings.gridCellSize;
        List<Vector2> occupiedPositions = new List<Vector2>();
        foreach (KeyValuePair<ResourceType, List<GameObject>> entry in iconsByType)
        {
            if (entry.Key == resourceType)
            {
                continue;
            }

            foreach (GameObject existingIcon in entry.Value)
            {
                if (existingIcon != null)
                {
                    occupiedPositions.Add(existingIcon.transform.position);
                }
            }
        }

        for (int index = 0; index < iconCount; index++)
        {
            Vector2 position = Vector2.zero;
            bool foundPosition = false;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                position = new Vector2(
                    UnityEngine.Random.Range(
                        bottomLeft.x + iconRadius,
                        bottomLeft.x + width - iconRadius),
                    UnityEngine.Random.Range(
                        bottomLeft.y + iconRadius,
                        bottomLeft.y + height - iconRadius));
                bool overlaps = false;
                foreach (Vector2 occupiedPosition in occupiedPositions)
                {
                    if ((position - occupiedPosition).sqrMagnitude < iconWidth * iconWidth)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    foundPosition = true;
                    break;
                }
            }

            if (!foundPosition)
            {
                position = new Vector2(
                    UnityEngine.Random.Range(bottomLeft.x + iconRadius, bottomLeft.x + width - iconRadius),
                    UnityEngine.Random.Range(bottomLeft.y + iconRadius, bottomLeft.y + height - iconRadius));
            }

            GameObject iconObject = new GameObject("Community Storage " + resourceType + " Icon");
            iconObject.transform.SetParent(storage.transform, true);
            iconObject.transform.position = new Vector3(
                position.x,
                position.y,
                storage.transform.position.z);
            iconObject.transform.localScale = Vector3.one * iconScale;
            SpriteRenderer spriteRenderer = iconObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = iconSprite;
            spriteRenderer.sortingLayerID = communityStorageIconSortingLayerId;
            spriteRenderer.sortingOrder = communityStorageIconSortingOrder;
            instances.Add(iconObject);
            occupiedPositions.Add(position);
        }
    }

    public void NotifyHouseConstructionStarted(Villager villager)
    {
        if (villager != null
            && claimedHousePlots.TryGetValue(villager, out HousePlotRecord housePlot)
            && housePlot != null)
        {
            RemoveBuildableOverResourceObjectsInPlot(housePlot);
            PathfindingRevision++;
        }
    }

    private void RemoveBuildableOverResourceObjectsInPlot(HousePlotRecord housePlot)
    {
        List<ResourceObject> resourcesToRemove = new List<ResourceObject>();
        for (int x = 0; x < housePlot.dimensions.x; x++)
        {
            for (int y = 0; y < housePlot.dimensions.y; y++)
            {
                Vector2Int cell = new Vector2Int(
                    housePlot.originCell.x + x,
                    housePlot.originCell.y + y);
                if (resourceObjectsByCell.TryGetValue(cell, out ResourceObject resourceObject)
                    && resourceObject != null
                    && resourceObject.CanBeRemovedByConstruction)
                {
                    resourcesToRemove.Add(resourceObject);
                }
            }
        }

        foreach (ResourceObject resourceObject in resourcesToRemove)
        {
            RemoveResourceObjectWithoutYield(resourceObject);
        }
    }

    private void RemoveResourceObjectWithoutYield(ResourceObject resourceObject)
    {
        if (resourceObject == null
            || !resourceObject.HasGridCell
            || !resourceObjectsByCell.TryGetValue(
                resourceObject.GridCell,
                out ResourceObject registeredObject)
            || registeredObject != resourceObject)
        {
            return;
        }

        resourceReservations.Remove(resourceObject);
        resourceObjectsByCell.Remove(resourceObject.GridCell);
        ResourceAvailabilityRevision++;
        PathfindingRevision++;
        Destroy(resourceObject.gameObject);
    }

    private bool IsCellInsideMap(Vector2Int cell)
    {
        Bounds cellBounds = GetGridCellBounds(cell);
        Vector2 mapMinimum = (Vector2)transform.position - mapSize * 0.5f;
        Vector2 mapMaximum = (Vector2)transform.position + mapSize * 0.5f;
        Vector2 cellMinimum = (Vector2)cellBounds.min;
        Vector2 cellMaximum = (Vector2)cellBounds.max;
        return cellMinimum.x >= mapMinimum.x
            && cellMinimum.y >= mapMinimum.y
            && cellMaximum.x <= mapMaximum.x
            && cellMaximum.y <= mapMaximum.y;
    }

    public bool TryGetResourceObjectAtCell(Vector2Int cell, out ResourceObject resourceObject)
    {
        return resourceObjectsByCell.TryGetValue(cell, out resourceObject);
    }

    public bool IsRoadCell(Vector2Int cell)
    {
        return roadCells.Contains(cell);
    }

    public bool IsWithinRoadMovementRange(Vector3 position)
    {
        if (settings == null || settings.gridCellSize <= 0f || roadCells.Count == 0)
        {
            return false;
        }

        float gridCellSize = settings.gridCellSize;
        float range = settings.roadMovementBonusRange;
        float rangeSquared = range * range;
        int minimumX = Mathf.CeilToInt((position.x - range) / gridCellSize) - 1;
        int maximumX = Mathf.FloorToInt((position.x + range) / gridCellSize);
        int minimumY = Mathf.CeilToInt((position.y - range) / gridCellSize) - 1;
        int maximumY = Mathf.FloorToInt((position.y + range) / gridCellSize);
        float halfCellSize = gridCellSize * 0.5f;

        for (int x = minimumX; x <= maximumX; x++)
        {
            for (int y = minimumY; y <= maximumY; y++)
            {
                if (!roadCells.Contains(new Vector2Int(x, y)))
                {
                    continue;
                }

                float centerX = x * gridCellSize + halfCellSize;
                float centerY = y * gridCellSize + halfCellSize;
                float distanceX = Mathf.Max(Mathf.Abs(position.x - centerX) - halfCellSize, 0f);
                float distanceY = Mathf.Max(Mathf.Abs(position.y - centerY) - halfCellSize, 0f);
                if (distanceX * distanceX + distanceY * distanceY <= rangeSquared)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public Vector3 GetHouseEntrancePosition(HousePlotRecord housePlot)
    {
        if (housePlot == null || GridCellSize <= 0f)
        {
            Debug.LogError("Cannot get a house entrance position without a valid house plot and grid.", this);
            return Vector3.zero;
        }

        Vector3 position = GridCellToWorldPosition(housePlot.entranceCell, GridCellSize);
        position.z = housePlot.center.z;
        return position;
    }

    public bool TryGetClosestRoadCellPosition(Vector3 position, out Vector3 roadPosition)
    {
        roadPosition = Vector3.zero;
        if (roadCells.Count == 0 || settings == null || settings.gridCellSize <= 0f)
        {
            return false;
        }

        float closestDistanceSquared = float.PositiveInfinity;
        foreach (Vector2Int roadCell in roadCells)
        {
            Vector3 candidate = GridCellToWorldPosition(roadCell, settings.gridCellSize);
            float distanceSquared = ((Vector2)(candidate - position)).sqrMagnitude;
            if (distanceSquared >= closestDistanceSquared)
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            roadPosition = candidate;
        }

        roadPosition.z = position.z;
        return true;
    }

    public bool TryGetResourceObjectAtWorldPosition(
        Vector3 worldPosition,
        out ResourceObject resourceObject)
    {
        resourceObject = null;
        if (settings == null || settings.gridCellSize <= 0f)
        {
            return false;
        }

        Vector2Int cell = WorldPositionToGridCell(worldPosition, settings.gridCellSize);
        return TryGetResourceObjectAtCell(cell, out resourceObject)
            && resourceObject != null;
    }

    public void NotifyResourceAvailabilityChanged(ResourceObject resourceObject)
    {
        if (resourceObject == null
            || !resourceObject.HasGridCell
            || !resourceObjectsByCell.TryGetValue(
                resourceObject.GridCell,
                out ResourceObject registeredObject)
            || registeredObject != resourceObject)
        {
            return;
        }

        ResourceAvailabilityRevision++;
    }

    public bool TryGetClosestResourceObject(
        Villager villager,
        ResourceType resourceType,
        Vector3 position,
        bool hasAxe,
        out ResourceObject closestResource)
    {
        return TryGetClosestResourceObject(
            villager,
            resourceType,
            position,
            hasAxe,
            null,
            false,
            0f,
            out closestResource);
    }

    public bool TryGetClosestReachableResourceObject(
        Villager villager,
        ResourceType resourceType,
        Vector3 position,
        bool hasAxe,
        ISet<ResourceObject> excludedResources,
        float arrivalDistance,
        out ResourceObject closestResource)
    {
        return TryGetClosestResourceObject(
            villager,
            resourceType,
            position,
            hasAxe,
            excludedResources,
            true,
            arrivalDistance,
            out closestResource);
    }

    public bool TryGetClosestReachableBareHandResourceObject(
        Villager villager,
        Vector3 position,
        ISet<ResourceObject> excludedResources,
        ISet<ResourceType> allowedResourceTypes,
        float arrivalDistance,
        out ResourceObject closestResource,
        out ResourceType closestResourceType)
    {
        closestResource = null;
        closestResourceType = default(ResourceType);
        if (villager == null)
        {
            return false;
        }

        List<KeyValuePair<float, ResourceObject>> reachableCandidates =
            new List<KeyValuePair<float, ResourceObject>>();
        foreach (ResourceObject resourceObject in resourceObjectsByCell.Values)
        {
            if (resourceObject == null
                || (excludedResources != null && excludedResources.Contains(resourceObject)))
            {
                continue;
            }

            if (resourceReservations.TryGetValue(resourceObject, out Villager reservingVillager))
            {
                if (reservingVillager != null && reservingVillager != villager)
                {
                    continue;
                }

                if (reservingVillager == null)
                {
                    resourceReservations.Remove(resourceObject);
                }
            }

            bool hasBareHandResource = false;
            foreach (ResourceType resourceType in resourceObject.AvailableResourceTypes)
            {
                if ((allowedResourceTypes == null || allowedResourceTypes.Contains(resourceType))
                    && resourceObject.CanGather(resourceType, false))
                {
                    hasBareHandResource = true;
                    break;
                }
            }

            if (hasBareHandResource)
            {
                float distanceSquared =
                    (resourceObject.transform.position - position).sqrMagnitude;
                reachableCandidates.Add(
                    new KeyValuePair<float, ResourceObject>(distanceSquared, resourceObject));
            }
        }

        reachableCandidates.Sort(
            (first, second) => first.Key.CompareTo(second.Key));
        foreach (KeyValuePair<float, ResourceObject> candidate in reachableCandidates)
        {
            ResourceObject resourceObject = candidate.Value;
            foreach (ResourceType resourceType in resourceObject.AvailableResourceTypes)
            {
                if ((allowedResourceTypes != null && !allowedResourceTypes.Contains(resourceType))
                    || !resourceObject.CanGather(resourceType, false))
                {
                    continue;
                }

                GetResourceInteractionTarget(
                    resourceObject,
                    resourceType,
                    arrivalDistance,
                    out Vector3 targetPosition,
                    out float targetArrivalDistance);
                if (!TryFindPath(
                        position,
                        targetPosition,
                        targetArrivalDistance,
                        out List<Vector3> waypoints)
                    || waypoints == null
                    || waypoints.Count == 0)
                {
                    continue;
                }

                closestResource = resourceObject;
                closestResourceType = resourceType;
                resourceReservations[closestResource] = villager;
                return true;
            }
        }

        return false;
    }

    private bool TryGetClosestResourceObject(
        Villager villager,
        ResourceType resourceType,
        Vector3 position,
        bool hasAxe,
        ISet<ResourceObject> excludedResources,
        bool requireReachable,
        float arrivalDistance,
        out ResourceObject closestResource)
    {
        closestResource = null;
        if (villager == null)
        {
            return false;
        }

        float closestDistanceSquared = float.PositiveInfinity;
        List<KeyValuePair<float, ResourceObject>> reachableCandidates =
            requireReachable
                ? new List<KeyValuePair<float, ResourceObject>>()
                : null;
        foreach (ResourceObject resourceObject in resourceObjectsByCell.Values)
        {
            if (resourceObject == null
                || (excludedResources != null && excludedResources.Contains(resourceObject))
                || !resourceObject.CanGather(resourceType, hasAxe))
            {
                continue;
            }

            if (resourceReservations.TryGetValue(resourceObject, out Villager reservingVillager))
            {
                if (reservingVillager != null && reservingVillager != villager)
                {
                    continue;
                }

                if (reservingVillager == null)
                {
                    resourceReservations.Remove(resourceObject);
                }
            }

            float distanceSquared = (resourceObject.transform.position - position).sqrMagnitude;
            if (requireReachable)
            {
                reachableCandidates.Add(
                    new KeyValuePair<float, ResourceObject>(distanceSquared, resourceObject));
                continue;
            }

            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestResource = resourceObject;
            }
        }

        if (requireReachable)
        {
            reachableCandidates.Sort(
                (first, second) => first.Key.CompareTo(second.Key));
            foreach (KeyValuePair<float, ResourceObject> candidate in reachableCandidates)
            {
                ResourceObject resourceObject = candidate.Value;
                GetResourceInteractionTarget(
                    resourceObject,
                    resourceType,
                    arrivalDistance,
                    out Vector3 targetPosition,
                    out float targetArrivalDistance);
                if (!TryFindPath(
                        position,
                        targetPosition,
                        targetArrivalDistance,
                        out List<Vector3> waypoints)
                    || waypoints == null
                    || waypoints.Count == 0)
                {
                    continue;
                }

                closestResource = resourceObject;
                break;
            }

            if (closestResource == null)
            {
                return false;
            }
        }

        if (closestResource == null)
        {
            return false;
        }

        resourceReservations[closestResource] = villager;
        return true;
    }

    private void GetResourceInteractionTarget(
        ResourceObject resourceObject,
        ResourceType resourceType,
        float arrivalDistance,
        out Vector3 targetPosition,
        out float targetArrivalDistance)
    {
        Vector2Int interactionOffset = resourceObject.GetInteractionOffset(resourceType);
        targetPosition = interactionOffset != Vector2Int.zero
            ? GetGridCellBounds(resourceObject.GridCell + interactionOffset).center
            : resourceObject.transform.position;
        targetArrivalDistance = interactionOffset != Vector2Int.zero
            ? 0.1f
            : arrivalDistance;
    }

    public void ReleaseResourceReservation(ResourceObject resourceObject, Villager villager)
    {
        if (ReferenceEquals(resourceObject, null)
            || !resourceReservations.TryGetValue(resourceObject, out Villager reservingVillager)
            || reservingVillager != villager)
        {
            return;
        }

        resourceReservations.Remove(resourceObject);
    }

    public bool TryGatherResource(
        Villager villager,
        ResourceObject resourceObject,
        int requestedAmount,
        bool hasAxe,
        out int gatheredAmount)
    {
        return TryGatherResource(
            villager,
            resourceObject,
            resourceObject != null ? resourceObject.Type : default(ResourceType),
            requestedAmount,
            hasAxe,
            out gatheredAmount);
    }

    public bool TryGatherResource(
        Villager villager,
        ResourceObject resourceObject,
        ResourceType resourceType,
        int requestedAmount,
        bool hasAxe,
        out int gatheredAmount)
    {
        gatheredAmount = 0;
        if (villager == null
            || resourceObject == null
            || requestedAmount <= 0
            || !resourceReservations.TryGetValue(resourceObject, out Villager reservingVillager)
            || reservingVillager != villager
            || !resourceObject.HasGridCell
            || !resourceObjectsByCell.TryGetValue(resourceObject.GridCell, out ResourceObject registeredObject)
            || registeredObject != resourceObject)
        {
            return false;
        }

        gatheredAmount = resourceObject.Take(resourceType, requestedAmount, hasAxe);
        if (!resourceObject.HasAnyResources)
        {
            ReleaseResourceReservation(resourceObject, villager);
            if (!resourceObject.CanRegenerateAnyResource)
            {
                resourceObjectsByCell.Remove(resourceObject.GridCell);
                PathfindingRevision++;
                Destroy(resourceObject.gameObject);
            }
        }

        return gatheredAmount > 0;
    }

    public bool TryClaimHousePlot(
        Villager villager,
        Vector2Int dimensions,
        out HousePlotRecord housePlot)
    {
        housePlot = null;
        if (villager == null || dimensions.x <= 0 || dimensions.y <= 0 || GridCellSize <= 0f)
        {
            return false;
        }

        if (claimedHousePlots.TryGetValue(villager, out housePlot))
        {
            return true;
        }

        housePlotQueue.RemoveAll(queuedVillager => queuedVillager == null);
        if (!housePlotQueue.Contains(villager))
        {
            housePlotQueue.Add(villager);
        }

        if (housePlotQueue.Count == 0 || housePlotQueue[0] != villager)
        {
            return false;
        }

        if (!TryFindAvailableHousePlot(dimensions, out housePlot))
        {
            return false;
        }

        housePlotQueue.RemoveAt(0);
        for (int x = 0; x < dimensions.x; x++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                claimedHouseCells.Add(new Vector2Int(
                    housePlot.originCell.x + x,
                    housePlot.originCell.y + y));
            }
        }

        claimedHousePlots.Add(villager, housePlot);
        return true;
    }

    public bool HasAvailableHouse(Vector2Int dimensions)
    {
        foreach (AvailableHouseRecord availableHouse in availableHouses)
        {
            if (availableHouse.housePlot != null
                && availableHouse.houseObject != null
                && availableHouse.housePlot.dimensions == dimensions)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryClaimAvailableHouse(
        Villager villager,
        Vector2Int dimensions,
        out HousePlotRecord housePlot,
        out GameObject houseObject)
    {
        housePlot = null;
        houseObject = null;
        if (villager == null || dimensions.x <= 0 || dimensions.y <= 0)
        {
            return false;
        }

        if (claimedHousePlots.ContainsKey(villager))
        {
            Debug.LogError(
                "A villager must release their unstarted house plot before claiming an available house.",
                villager);
            return false;
        }

        for (int index = availableHouses.Count - 1; index >= 0; index--)
        {
            AvailableHouseRecord availableHouse = availableHouses[index];
            if (availableHouse.housePlot == null || availableHouse.houseObject == null)
            {
                availableHouses.RemoveAt(index);
                continue;
            }

            if (availableHouse.housePlot.dimensions != dimensions)
            {
                continue;
            }

            HouseWorkplace workplace = GetOrCreateHouseWorkplace(availableHouse.houseObject);
            if (workplace == null || !workplace.TrySetResident(villager))
            {
                Debug.LogError(
                    "Could not register the villager as a house resident.",
                    availableHouse.houseObject);
                continue;
            }

            housePlot = availableHouse.housePlot;
            houseObject = availableHouse.houseObject;
            availableHouses.RemoveAt(index);
            housePlotQueue.Remove(villager);
            claimedHousePlots.Add(villager, housePlot);
            completedHouseObjects[villager] = houseObject;
            return true;
        }

        return false;
    }

    public void RegisterCompletedHouse(Villager villager, GameObject houseObject)
    {
        if (villager == null
            || houseObject == null
            || !claimedHousePlots.TryGetValue(villager, out HousePlotRecord housePlot)
            || housePlot == null)
        {
            Debug.LogError("Cannot register a completed house without its villager and plot.", this);
            return;
        }

        completedHouseObjects[villager] = houseObject;
        HouseWorkplace workplace = GetOrCreateHouseWorkplace(houseObject);
        if (workplace == null || !workplace.TrySetResident(villager))
        {
            Debug.LogError("Could not register the villager as a house resident.", houseObject);
        }

        CreateHouseRoadConnection(housePlot);
    }

    private void CreateHouseRoadConnection(HousePlotRecord housePlot)
    {
        GameOverlord gameOverlord = GameOverlord.Instance;
        if (gameOverlord == null || gameOverlord.RoadTilePrefab == null)
        {
            Debug.LogError(
                "A completed house requires an active GameOverlord with a road tile prefab "
                + "to create its entrance path.",
                this);
            return;
        }

        Vector3 entrancePosition = GridCellToWorldPosition(
            housePlot.entranceCell,
            settings.gridCellSize);
        entrancePosition.z = housePlot.center.z;
        PlaceRoadTilesAlongSegment(
            gameOverlord.RoadTilePrefab,
            housePlot.center,
            entrancePosition);
    }

    public void ReleaseCompletedHouse(Villager villager)
    {
        if (villager == null
            || !claimedHousePlots.TryGetValue(villager, out HousePlotRecord housePlot)
            || housePlot == null
            || !completedHouseObjects.TryGetValue(villager, out GameObject houseObject)
            || houseObject == null)
        {
            return;
        }

        claimedHousePlots.Remove(villager);
        completedHouseObjects.Remove(villager);
        HouseWorkplace workplace = houseObject.GetComponent<HouseWorkplace>();
        if (workplace != null)
        {
            workplace.ReleaseResident(villager);
        }

        availableHouses.Add(new AvailableHouseRecord
        {
            housePlot = housePlot,
            houseObject = houseObject
        });
    }

    public void ReleaseHousePlot(Villager villager)
    {
        if (villager == null)
        {
            return;
        }

        housePlotQueue.Remove(villager);
        if (!claimedHousePlots.TryGetValue(villager, out HousePlotRecord housePlot))
        {
            return;
        }

        RemoveHousePlotCells(housePlot);

        claimedHousePlots.Remove(villager);
        completedHouseObjects.Remove(villager);
    }

    private void RemoveHousePlotCells(HousePlotRecord housePlot)
    {
        if (housePlot == null)
        {
            return;
        }

        for (int x = 0; x < housePlot.dimensions.x; x++)
        {
            for (int y = 0; y < housePlot.dimensions.y; y++)
            {
                claimedHouseCells.Remove(new Vector2Int(
                    housePlot.originCell.x + x,
                    housePlot.originCell.y + y));
            }
        }
    }

    private void CancelUnstartedHousePlotsIntersecting(HashSet<Vector2Int> roadCellsToAdd)
    {
        List<Villager> villagersToCancel = new List<Villager>();
        List<Villager> workplacePlotsToCancel = new List<Villager>();
        foreach (KeyValuePair<Villager, HousePlotRecord> claimedPlot in claimedHousePlots)
        {
            Villager villager = claimedPlot.Key;
            HousePlotRecord housePlot = claimedPlot.Value;
            if (villager == null
                || housePlot == null
                || IsHouseConstructionStarted(villager.HousingState))
            {
                continue;
            }

            bool intersectsRoad = false;
            for (int x = 0; x < housePlot.dimensions.x && !intersectsRoad; x++)
            {
                for (int y = 0; y < housePlot.dimensions.y; y++)
                {
                    if (roadCellsToAdd.Contains(new Vector2Int(
                            housePlot.originCell.x + x,
                            housePlot.originCell.y + y)))
                    {
                        intersectsRoad = true;
                        break;
                    }
                }
            }

            if (intersectsRoad)
            {
                villagersToCancel.Add(villager);
            }
        }

        foreach (Villager villager in villagersToCancel)
        {
            villager.CancelHousePlotForRoad();
        }

        foreach (KeyValuePair<Villager, HousePlotRecord> workplacePlot in claimedWorkplacePlots)
        {
            Villager villager = workplacePlot.Key;
            if (villager != null
                && !startedWorkplacePlots.Contains(villager)
                && IntersectsRoad(workplacePlot.Value, roadCellsToAdd))
            {
                workplacePlotsToCancel.Add(villager);
            }
        }

        foreach (Villager villager in workplacePlotsToCancel)
        {
            villager.CancelWorkplacePlotForRoad();
        }
    }

    private bool IsOccupiedByStartedHouse(Vector2Int cell)
    {
        foreach (AvailableHouseRecord availableHouse in availableHouses)
        {
            if (ContainsHouseCell(availableHouse.housePlot, cell))
            {
                return true;
            }
        }

        foreach (KeyValuePair<Villager, HousePlotRecord> claimedPlot in claimedHousePlots)
        {
            Villager villager = claimedPlot.Key;
            HousePlotRecord housePlot = claimedPlot.Value;
            if (villager == null
                || housePlot == null
                || !IsHouseConstructionStarted(villager.HousingState))
            {
                continue;
            }

            if (ContainsHouseCell(housePlot, cell))
            {
                return true;
            }
        }

        foreach (KeyValuePair<Villager, AvailableHouseRecord> workplace in claimedWorkplaceHouses)
        {
            if (ContainsHouseCell(workplace.Value.housePlot, cell))
            {
                return true;
            }
        }

        foreach (KeyValuePair<Villager, HousePlotRecord> workplacePlot in claimedWorkplacePlots)
        {
            if (startedWorkplacePlots.Contains(workplacePlot.Key)
                && ContainsHouseCell(workplacePlot.Value, cell))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IntersectsRoad(HousePlotRecord housePlot, HashSet<Vector2Int> roadCellsToAdd)
    {
        if (housePlot == null)
        {
            return false;
        }

        for (int x = 0; x < housePlot.dimensions.x; x++)
        {
            for (int y = 0; y < housePlot.dimensions.y; y++)
            {
                if (roadCellsToAdd.Contains(new Vector2Int(
                        housePlot.originCell.x + x,
                        housePlot.originCell.y + y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsHouseCell(HousePlotRecord housePlot, Vector2Int cell)
    {
        return housePlot != null
            && cell.x >= housePlot.originCell.x
            && cell.x < housePlot.originCell.x + housePlot.dimensions.x
            && cell.y >= housePlot.originCell.y
            && cell.y < housePlot.originCell.y + housePlot.dimensions.y;
    }

    private static bool TryGetFinishedHouseAtCell(
        HousePlotRecord housePlot,
        GameObject candidate,
        Vector2Int cell,
        out GameObject houseObject)
    {
        houseObject = null;
        if (candidate == null || !ContainsHouseCell(housePlot, cell))
        {
            return false;
        }

        HouseVisual houseVisual = candidate.GetComponent<HouseVisual>();
        if (houseVisual == null || houseVisual.Definition == null)
        {
            return false;
        }

        houseObject = candidate;
        return true;
    }

    private static bool IsHouseConstructionStarted(VillagerHousingState housingState)
    {
        return housingState == VillagerHousingState.BuildingHouseStageOne
            || housingState == VillagerHousingState.HouseConstructionBreak
            || housingState == VillagerHousingState.BuildingHouseStageTwo
            || housingState == VillagerHousingState.Housed;
    }

    private bool TryFindAvailableHousePlot(Vector2Int dimensions, out HousePlotRecord housePlot)
    {
        foreach (Vector2Int roadCell in roadCells)
        {
            Vector2Int centeredHorizontalOrigin = new Vector2Int(
                roadCell.x - (dimensions.x - 1) / 2,
                roadCell.y);
            Vector2Int centeredVerticalOrigin = new Vector2Int(
                roadCell.x,
                roadCell.y - (dimensions.y - 1) / 2);

            Vector2Int[] candidateOrigins =
            {
                new Vector2Int(centeredHorizontalOrigin.x, roadCell.y - dimensions.y),
                new Vector2Int(centeredHorizontalOrigin.x, roadCell.y + 1),
                new Vector2Int(roadCell.x - dimensions.x, centeredVerticalOrigin.y),
                new Vector2Int(roadCell.x + 1, centeredVerticalOrigin.y)
            };

            foreach (Vector2Int origin in candidateOrigins)
            {
                if (!IsHousePlotAvailable(origin, dimensions)
                    || !TrySelectHouseEntranceCell(origin, dimensions, out Vector2Int entranceCell))
                {
                    continue;
                }

                float gridCellSize = GridCellSize;
                Vector3 bottomLeft = GridCellToWorldPosition(origin, gridCellSize);
                Vector3 center = bottomLeft + new Vector3(
                    (dimensions.x - 1) * gridCellSize * 0.5f,
                    (dimensions.y - 1) * gridCellSize * 0.5f,
                    0f);
                center.z = transform.position.z;
                housePlot = new HousePlotRecord
                {
                    originCell = origin,
                    dimensions = dimensions,
                    entranceCell = entranceCell,
                    center = center
                };
                return true;
            }
        }

        housePlot = null;
        return false;
    }

    private bool TrySelectHouseEntranceCell(
        Vector2Int origin,
        Vector2Int dimensions,
        out Vector2Int entranceCell)
    {
        List<Vector2Int> adjacentRoadCells = new List<Vector2Int>();

        for (int x = origin.x; x < origin.x + dimensions.x; x++)
        {
            Vector2Int southCell = new Vector2Int(x, origin.y - 1);
            Vector2Int northCell = new Vector2Int(x, origin.y + dimensions.y);
            if (roadCells.Contains(southCell))
            {
                adjacentRoadCells.Add(southCell);
            }

            if (roadCells.Contains(northCell))
            {
                adjacentRoadCells.Add(northCell);
            }
        }

        for (int y = origin.y; y < origin.y + dimensions.y; y++)
        {
            Vector2Int westCell = new Vector2Int(origin.x - 1, y);
            Vector2Int eastCell = new Vector2Int(origin.x + dimensions.x, y);
            if (roadCells.Contains(westCell))
            {
                adjacentRoadCells.Add(westCell);
            }

            if (roadCells.Contains(eastCell))
            {
                adjacentRoadCells.Add(eastCell);
            }
        }

        if (adjacentRoadCells.Count == 0)
        {
            entranceCell = Vector2Int.zero;
            return false;
        }

        entranceCell = adjacentRoadCells[UnityEngine.Random.Range(0, adjacentRoadCells.Count)];
        return true;
    }

    private bool IsHousePlotAvailable(Vector2Int origin, Vector2Int dimensions)
    {
        float gridCellSize = GridCellSize;
        Vector2 mapMinimum = (Vector2)transform.position - mapSize * 0.5f;
        Vector2 mapMaximum = (Vector2)transform.position + mapSize * 0.5f;
        float halfCellSize = gridCellSize * 0.5f;

        for (int x = 0; x < dimensions.x; x++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                Vector2Int cell = new Vector2Int(origin.x + x, origin.y + y);
                Vector3 cellCenter = GridCellToWorldPosition(cell, gridCellSize);
                Vector2 cellMinimum = (Vector2)cellCenter - Vector2.one * halfCellSize;
                Vector2 cellMaximum = (Vector2)cellCenter + Vector2.one * halfCellSize;
                if (cellMinimum.x < mapMinimum.x
                    || cellMinimum.y < mapMinimum.y
                    || cellMaximum.x > mapMaximum.x
                    || cellMaximum.y > mapMaximum.y
                    || roadCells.Contains(cell)
                    || claimedHouseCells.Contains(cell)
                    || communityStorageCells.Contains(cell)
                    || IsBlockingResourceObject(cell))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsBlockingResourceObject(Vector2Int cell)
    {
        return resourceObjectsByCell.TryGetValue(cell, out ResourceObject resourceObject)
            && (resourceObject == null || !resourceObject.CanBeRemovedByConstruction);
    }

    public bool TryGetResourceObjectOnPath(
        Vector3 startPosition,
        Vector3 endPosition,
        out ResourceObject resourceObject)
    {
        resourceObject = null;
        if (settings == null || settings.gridCellSize <= 0f)
        {
            return false;
        }

        float gridCellSize = settings.gridCellSize;
        Vector2 start = startPosition;
        Vector2 end = endPosition;
        Vector2Int startCell = WorldPositionToGridCell(startPosition, gridCellSize);
        foreach (KeyValuePair<Vector2Int, ResourceObject> occupiedCell in resourceObjectsByCell)
        {
            if (occupiedCell.Key == startCell || occupiedCell.Value == null)
            {
                continue;
            }

            if (SegmentIntersectsCell(start, end, occupiedCell.Key, gridCellSize))
            {
                resourceObject = occupiedCell.Value;
                return true;
            }
        }

        resourceObject = null;
        return false;
    }

    private static bool SegmentIntersectsCell(
        Vector2 start,
        Vector2 end,
        Vector2Int cell,
        float gridCellSize)
    {
        float minimumX = cell.x * gridCellSize;
        float minimumY = cell.y * gridCellSize;
        float maximumX = minimumX + gridCellSize;
        float maximumY = minimumY + gridCellSize;
        Vector2 direction = end - start;
        float minimumParameter = 0f;
        float maximumParameter = 1f;

        return ClipSegmentAxis(
                start.x,
                direction.x,
                minimumX,
                maximumX,
                ref minimumParameter,
                ref maximumParameter)
            && ClipSegmentAxis(
                start.y,
                direction.y,
                minimumY,
                maximumY,
                ref minimumParameter,
                ref maximumParameter);
    }

    private static bool ClipSegmentAxis(
        float origin,
        float direction,
        float minimum,
        float maximum,
        ref float minimumParameter,
        ref float maximumParameter)
    {
        const float tolerance = 0.0001f;
        if (Mathf.Abs(direction) <= tolerance)
        {
            return origin >= minimum - tolerance && origin <= maximum + tolerance;
        }

        float firstIntersection = (minimum - origin) / direction;
        float secondIntersection = (maximum - origin) / direction;
        if (firstIntersection > secondIntersection)
        {
            float swap = firstIntersection;
            firstIntersection = secondIntersection;
            secondIntersection = swap;
        }

        minimumParameter = Mathf.Max(minimumParameter, firstIntersection);
        maximumParameter = Mathf.Min(maximumParameter, secondIntersection);
        return minimumParameter <= maximumParameter + tolerance;
    }

    private bool TryGetResourceObjectOnRoadPath(
        IReadOnlyList<Vector3> roadPath,
        out ResourceObject resourceObject)
    {
        resourceObject = null;
        if (roadPath == null
            || roadPath.Count == 0
            || settings == null
            || settings.gridCellSize <= 0f)
        {
            return false;
        }

        foreach (Vector3 pathPoint in roadPath)
        {
            Vector2Int cell = WorldPositionToGridCell(pathPoint, settings.gridCellSize);
            if (TryGetResourceObjectAtCell(cell, out resourceObject)
                && resourceObject != null
                && !resourceObject.CanBeRemovedByConstruction)
            {
                return true;
            }
        }

        for (int index = 0; index < roadPath.Count - 1; index++)
        {
            if (TryGetResourceObjectOnRoadSegment(
                    roadPath[index],
                    roadPath[index + 1],
                    out resourceObject))
            {
                return true;
            }
        }

        resourceObject = null;
        return false;
    }

    private bool TryGetResourceObjectOnRoadSegment(
        Vector3 startPosition,
        Vector3 endPosition,
        out ResourceObject resourceObject)
    {
        resourceObject = null;
        if (settings == null || settings.gridCellSize <= 0f)
        {
            return false;
        }

        Vector2 start = startPosition;
        Vector2 end = endPosition;
        float gridCellSize = settings.gridCellSize;
        foreach (KeyValuePair<Vector2Int, ResourceObject> occupiedCell in resourceObjectsByCell)
        {
            if (occupiedCell.Value == null)
            {
                continue;
            }

            if (!occupiedCell.Value.CanBeRemovedByConstruction
                && SegmentIntersectsCell(start, end, occupiedCell.Key, gridCellSize))
            {
                resourceObject = occupiedCell.Value;
                return true;
            }
        }

        return false;
    }

    public bool TryGetClosestRoadPoint(
        Vector3 position,
        out Vector3 closestPoint,
        out float distance)
    {
        bool foundSegment = TryGetClosestRoadSegment(
            position,
            out _,
            out _,
            out closestPoint,
            out distance);
        return foundSegment;
    }

    public bool TryGetRoadWanderTarget(
        Vector3 position,
        float maxTravelDistance,
        out Vector3 target)
    {
        target = Vector3.zero;
        if (maxTravelDistance <= 0f)
        {
            return false;
        }

        if (!TryGetClosestRoadSegment(
                position,
                out Vector3 closestSegmentStart,
                out Vector3 closestSegmentEnd,
                out Vector3 closestPoint,
                out _))
        {
            return false;
        }

        Vector2 segmentDirection = (Vector2)(closestSegmentEnd - closestSegmentStart);
        float segmentLength = segmentDirection.magnitude;
        if (segmentLength <= Mathf.Epsilon)
        {
            return false;
        }

        segmentDirection /= segmentLength;
        float distanceAlongSegment = Vector2.Dot(
            (Vector2)(closestPoint - closestSegmentStart),
            segmentDirection);
        bool moveForward = UnityEngine.Random.value >= 0.5f;
        float availableDistance = moveForward
            ? segmentLength - distanceAlongSegment
            : distanceAlongSegment;
        if (availableDistance <= 0.01f)
        {
            moveForward = !moveForward;
            availableDistance = moveForward
                ? segmentLength - distanceAlongSegment
                : distanceAlongSegment;
        }

        float travelDistance = Mathf.Min(maxTravelDistance, availableDistance);
        if (travelDistance <= 0.01f)
        {
            target = closestPoint;
            return true;
        }

        float directionSign = moveForward ? 1f : -1f;
        Vector2 target2D = (Vector2)closestSegmentStart
            + segmentDirection * (distanceAlongSegment + directionSign * travelDistance);
        target = new Vector3(target2D.x, target2D.y, closestPoint.z);
        return true;
    }

    private bool TryGetClosestRoadSegment(
        Vector3 position,
        out Vector3 closestSegmentStart,
        out Vector3 closestSegmentEnd,
        out Vector3 closestPoint,
        out float distance)
    {
        closestSegmentStart = Vector3.zero;
        closestSegmentEnd = Vector3.zero;
        closestPoint = Vector3.zero;
        distance = float.PositiveInfinity;
        float closestDistanceSquared = float.PositiveInfinity;
        Vector2 position2D = position;

        foreach (RoadRecord road in roads)
        {
            if (road == null || road.keyPoints == null)
            {
                continue;
            }

            for (int index = 0; index < road.keyPoints.Count - 1; index++)
            {
                Vector3 segmentStart = road.keyPoints[index];
                Vector3 segmentEnd = road.keyPoints[index + 1];
                Vector2 segment = (Vector2)(segmentEnd - segmentStart);
                float segmentLengthSquared = segment.sqrMagnitude;
                if (segmentLengthSquared <= Mathf.Epsilon)
                {
                    continue;
                }

                float interpolation = Mathf.Clamp01(
                    Vector2.Dot(position2D - (Vector2)segmentStart, segment)
                    / segmentLengthSquared);
                Vector3 candidate = Vector3.Lerp(segmentStart, segmentEnd, interpolation);
                float distanceSquared = (position2D - (Vector2)candidate).sqrMagnitude;
                if (distanceSquared < closestDistanceSquared - 0.0001f)
                {
                    closestDistanceSquared = distanceSquared;
                    closestSegmentStart = segmentStart;
                    closestSegmentEnd = segmentEnd;
                    closestPoint = candidate;
                }
            }
        }

        if (float.IsPositiveInfinity(closestDistanceSquared))
        {
            return false;
        }

        distance = Mathf.Sqrt(closestDistanceSquared);
        return true;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Only one MapManager can be active at a time.", this);
            enabled = false;
            return;
        }

        instance = this;
        roadKeyPoints.Clear();
        roads.Clear();
        roadCells.Clear();
        claimedHouseCells.Clear();
        communityStorageCells.Clear();
        communityStorages.Clear();
        activeConstructionSites.Clear();
        communityStorageIconInstances.Clear();
        loggedMissingCommunityStoragePrefab = false;
        housePlotQueue.Clear();
        claimedHousePlots.Clear();
        claimedWorkplacePlots.Clear();
        workplacePlotObjects.Clear();
        startedWorkplacePlots.Clear();
        claimedWorkplaceHouses.Clear();
        completedHouseObjects.Clear();
        availableHouses.Clear();

        GameData gameData = GameData.Instance;
        settings = gameData.mapSettings;
        houseStorageCapacity = gameData.houseSettings != null
            ? gameData.houseSettings.storageCapacity
            : 0;
        communityStorageSettings = gameData.communityStorageSettings;
        if (settings == null)
        {
            Debug.LogError("Map settings could not be loaded from GameData.", this);
            enabled = false;
            return;
        }

        if (communityStorageSettings == null
            || communityStorageSettings.maximumAmountPerStorage <= 0
            || communityStorageSettings.unitsPerAdditionalIcon <= 0)
        {
            Debug.LogError(
                "Community storage settings must define a positive per-storage capacity and icon threshold.",
                this);
            enabled = false;
            return;
        }

        if (houseStorageCapacity <= 0)
        {
            Debug.LogError("House settings must define a positive storage capacity.", this);
            enabled = false;
            return;
        }

        loggedMissingCommunityStorageIconManager = false;
        mapCamera = UnityEngine.Camera.main;
    }

    private void Update()
    {
        if (!isRoadBuilding)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            StopRoadBuilding();
            return;
        }

        if (TryGetMouseWorldPosition(Input.mousePosition, out Vector3 mouseWorldPosition))
        {
            UpdateRoadPreview(ResolveRoadEndpoint(mouseWorldPosition));
        }

        if (Input.GetMouseButtonDown(0))
        {
            pointerDownPosition = Input.mousePosition;
            isTrackingClick = !IsPointerOverUI();
        }

        if (Input.GetMouseButtonUp(0))
        {
            bool isClick = isTrackingClick
                && Vector2.Distance(pointerDownPosition, Input.mousePosition) <= settings.clickDragThresholdPixels
                && !IsPointerOverUI();
            isTrackingClick = false;

            if (isClick && TryGetMouseWorldPosition(Input.mousePosition, out mouseWorldPosition))
            {
                HandleRoadClick(ResolveRoadEndpoint(mouseWorldPosition), mouseWorldPosition);
            }
        }
    }

    private void SpawnResourceObjects()
    {
        ResourceObjectDefinition[] definitions = GameData.Instance.resourceObjectDefinitions;
        if (definitions == null)
        {
            Debug.LogError("Resource object definitions could not be loaded from GameData.", this);
            return;
        }

        foreach (ResourceObjectDefinition definition in definitions)
        {
            SpawnResourceObjects(definition);
        }
    }

    private void SpawnResourceObjects(ResourceObjectDefinition definition)
    {
        if (definition == null || definition.spawnSettings == null)
        {
            Debug.LogError("A resource object definition or its spawn settings are missing.", this);
            return;
        }

        ResourceObjectSpawnSettings spawnSettings = definition.spawnSettings;
        if (spawnSettings.count == 0)
        {
            return;
        }

        if (mapSize.x <= 0f
            || mapSize.y <= 0f
            || spawnSettings.edgePadding < 0f
            || spawnSettings.edgePadding * 2f >= mapSize.x
            || spawnSettings.edgePadding * 2f >= mapSize.y
            || spawnSettings.minimumSpacing < 0f
            || spawnSettings.placementAttemptsPerObject < 1
            || spawnSettings.scaleRange.x <= 0f
            || spawnSettings.scaleRange.y < spawnSettings.scaleRange.x)
        {
            Debug.LogError("Resource object spawn settings are invalid for '" + definition.id + "'.", this);
            return;
        }

        GameObject prefab = GetResourceObjectPrefab(definition.id);
        if (prefab == null)
        {
            Debug.LogError(
                "No Inspector prefab is assigned for resource object definition '" + definition.id + "'.",
                this);
            return;
        }

        if (prefab.GetComponent<ResourceObject>() == null)
        {
            Debug.LogError(
                "The prefab for resource object '" + definition.id
                + "' must have a ResourceObject component.",
                this);
            return;
        }

        if (settings == null || settings.gridCellSize <= 0f)
        {
            Debug.LogError("A positive gridCellSize is required to place resource objects on map cells.", this);
            return;
        }

        SpriteRenderer[] spriteRenderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasSprite = false;
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                hasSprite = true;
                break;
            }
        }

        if (!hasSprite)
        {
            Debug.LogError(
                "The prefab for resource object '" + definition.id
                + "' must contain a SpriteRenderer with a Sprite.",
                this);
            return;
        }

        System.Random random = new System.Random(
            spawnSettings.randomSeed == 0 ? Environment.TickCount : spawnSettings.randomSeed);
        float gridCellSize = settings.gridCellSize;
        float halfCellSize = gridCellSize * 0.5f;
        Vector2 halfMapSize = mapSize * 0.5f;
        Vector2 spawnCenter = transform.position;
        float minX = spawnCenter.x - halfMapSize.x + spawnSettings.edgePadding;
        float maxX = spawnCenter.x + halfMapSize.x - spawnSettings.edgePadding;
        float minY = spawnCenter.y - halfMapSize.y + spawnSettings.edgePadding;
        float maxY = spawnCenter.y + halfMapSize.y - spawnSettings.edgePadding;
        int minCellX = Mathf.CeilToInt((minX - halfCellSize) / gridCellSize);
        int maxCellX = Mathf.FloorToInt((maxX - halfCellSize) / gridCellSize);
        int minCellY = Mathf.CeilToInt((minY - halfCellSize) / gridCellSize);
        int maxCellY = Mathf.FloorToInt((maxY - halfCellSize) / gridCellSize);

        if (minCellX > maxCellX || minCellY > maxCellY)
        {
            Debug.LogError(
                "The map is too small to contain grid cells for resource object '" + definition.id + "'.",
                this);
            return;
        }

        float minimumDistanceSquared = spawnSettings.minimumSpacing * spawnSettings.minimumSpacing;
        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>(resourceObjectsByCell.Keys);
        int spawnedCount = 0;

        for (int objectIndex = 0; objectIndex < spawnSettings.count; objectIndex++)
        {
            bool foundPosition = false;
            Vector2Int cell = Vector2Int.zero;
            Vector3 cellPosition = Vector3.zero;

            for (int attempt = 0; attempt < spawnSettings.placementAttemptsPerObject; attempt++)
            {
                cell = new Vector2Int(
                    random.Next(minCellX, maxCellX + 1),
                    random.Next(minCellY, maxCellY + 1));

                if (occupiedCells.Contains(cell))
                {
                    continue;
                }

                cellPosition = GridCellToWorldPosition(cell, gridCellSize);
                cellPosition.z = transform.position.z;
                bool isTooClose = false;
                foreach (Vector2Int existingCell in occupiedCells)
                {
                    Vector3 existingPosition = GridCellToWorldPosition(existingCell, gridCellSize);
                    if ((cellPosition - existingPosition).sqrMagnitude < minimumDistanceSquared)
                    {
                        isTooClose = true;
                        break;
                    }
                }

                if (!isTooClose)
                {
                    foundPosition = true;
                    break;
                }
            }

            if (!foundPosition)
            {
                continue;
            }

            float rotation = spawnSettings.randomizeRotation
                ? RandomRange(random, 0f, 360f)
                : 0f;
            float scale = RandomRange(random, spawnSettings.scaleRange.x, spawnSettings.scaleRange.y);
            GameObject resourceObjectInstance = Instantiate(
                prefab,
                cellPosition,
                Quaternion.Euler(0f, 0f, rotation),
                transform);
            ResourceObject resourceObject = resourceObjectInstance.GetComponent<ResourceObject>();
            if (resourceObject == null)
            {
                Debug.LogError(
                    "Resource object prefab '" + definition.id + "' is missing ResourceObject.",
                    resourceObjectInstance);
                Destroy(resourceObjectInstance);
                continue;
            }

            resourceObjectInstance.transform.localScale *= scale;
            if (!ApplyResourceObjectVisualOffset(resourceObject, spawnSettings.visualOffset))
            {
                Destroy(resourceObjectInstance);
                continue;
            }

            if (!resourceObject.Initialize(definition))
            {
                Destroy(resourceObjectInstance);
                continue;
            }

            resourceObject.SetGridCell(cell);
            resourceObjectsByCell.Add(cell, resourceObject);
            occupiedCells.Add(cell);
            PathfindingRevision++;
            spawnedCount++;
        }

        if (spawnedCount < spawnSettings.count)
        {
            Debug.LogWarning(
                "Placed " + spawnedCount + " of " + spawnSettings.count
                + " requested '" + definition.id
                + "' resource objects. Increase the map size or placement attempts, or reduce minimum spacing.",
                this);
        }
    }

    private GameObject GetResourceObjectPrefab(string id)
    {
        if (resourceObjectPrefabs == null)
        {
            return null;
        }

        GameObject matchedPrefab = null;
        bool foundReference = false;
        foreach (ResourceObjectPrefabReference reference in resourceObjectPrefabs)
        {
            if (reference != null && string.Equals(reference.id, id, StringComparison.OrdinalIgnoreCase))
            {
                if (foundReference)
                {
                    Debug.LogError(
                        "More than one Inspector prefab is assigned for resource object definition '"
                        + id + "'.",
                        this);
                    return null;
                }

                foundReference = true;
                matchedPrefab = reference.prefab;
            }
        }

        return matchedPrefab;
    }

    private static bool ApplyResourceObjectVisualOffset(
        ResourceObject resourceObject,
        Vector2 visualOffset)
    {
        if (visualOffset == Vector2.zero)
        {
            return true;
        }

        if (resourceObject == null || resourceObject.VisualRoot == null)
        {
            Debug.LogError("Resource object prefab requires a SortingAnchor/Visuals hierarchy.", resourceObject);
            return false;
        }

        Vector3 worldOffset = new Vector3(visualOffset.x, visualOffset.y, 0f);
        resourceObject.VisualRoot.position += worldOffset;
        return true;
    }

    private static float RandomRange(System.Random random, float minimum, float maximum)
    {
        return minimum + (float)random.NextDouble() * (maximum - minimum);
    }

    public void ToggleRoadBuilding()
    {
        if (isRoadBuilding)
        {
            StopRoadBuilding();
            return;
        }

        StartRoadBuilding();
    }

    private void StartRoadBuilding()
    {
        GameOverlord gameOverlord = GameOverlord.Instance;
        UIManager uiManager = UIManager.Instance;
        if (gameOverlord == null || gameOverlord.RoadTilePrefab == null)
        {
            Debug.LogError("Road building requires an active GameOverlord with a road tile prefab.", this);
            return;
        }

        if (uiManager == null
            || uiManager.RoadEndpointPrefab == null
            || uiManager.RoadPreviewPrefab == null)
        {
            Debug.LogError("Road building requires UIManager road endpoint and preview references.", this);
            return;
        }

        if (settings.gridCellSize <= 0f
            || settings.roadMovementSpeedMultiplier < 1f
            || settings.keyPointSelectionRadius <= 0f
            || settings.roadEndpointKeyPointTolerance < 0f
            || settings.clickDragThresholdPixels < 0f
            || settings.maxRoadKeyPointDistance <= 0f
            || settings.roadTileSpacing <= 0f
            || settings.manaCostPerKeyPoint < 0)
        {
            Debug.LogError("Map settings contain an invalid road distance, grid distance, or mana cost.", this);
            return;
        }

        if (mapCamera == null)
        {
            mapCamera = UnityEngine.Camera.main;
        }

        if (mapCamera == null)
        {
            Debug.LogError("Road building requires a MainCamera-tagged camera.", this);
            return;
        }

        isRoadBuilding = true;
        hasRoadStartPoint = false;
        isTrackingClick = false;
        uiManager.SetRoadBuildingCursor(true);
    }

    private void HandleRoadClick(Vector3 endpoint, Vector3 mouseWorldPosition)
    {
        if (!hasRoadStartPoint)
        {
            Vector3 candidateStartPoint;
            if (roadKeyPoints.Count == 0)
            {
                candidateStartPoint = SnapToGrid(mouseWorldPosition);
            }
            else if (!TryGetRoadKeyPoint(mouseWorldPosition, settings.keyPointSelectionRadius, out roadStartPoint))
            {
                Debug.LogWarning(
                    "A new road must start within " + settings.keyPointSelectionRadius
                    + " world units of an existing road keyPoint.",
                    this);
                return;
            }
            else
            {
                candidateStartPoint = roadStartPoint;
            }

            Vector2Int startCell = WorldPositionToGridCell(candidateStartPoint, settings.gridCellSize);
            if (TryGetResourceObjectAtCell(startCell, out ResourceObject blockingResource)
                && blockingResource != null)
            {
                Debug.LogWarning(
                    "Road start rejected: grid cell " + startCell
                    + " is occupied by " + blockingResource.Type + ".",
                    this);
                return;
            }

            roadStartPoint = candidateStartPoint;
            if (roadKeyPoints.Count == 0)
            {
                startMarkerInstance = Instantiate(
                    UIManager.Instance.RoadEndpointPrefab,
                    roadStartPoint,
                    Quaternion.identity);
            }

            BeginRoadSegment(roadStartPoint);
            return;
        }

        if ((endpoint - roadStartPoint).sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        if (!BuildRoad(roadStartPoint, endpoint, out List<Vector3> addedKeyPoints))
        {
            return;
        }

        foreach (Vector3 keyPoint in addedKeyPoints)
        {
            if (startMarkerInstance != null && (keyPoint - roadStartPoint).sqrMagnitude <= Mathf.Epsilon)
            {
                continue;
            }

            Instantiate(UIManager.Instance.RoadEndpointPrefab, keyPoint, Quaternion.identity);
        }

        ResetRoadPlacement();
        BeginRoadSegment(endpoint);
    }

    private void BeginRoadSegment(Vector3 startPoint)
    {
        roadStartPoint = startPoint;
        roadPreviewInstance = Instantiate(UIManager.Instance.RoadPreviewPrefab);
        roadPreviewInstance.useWorldSpace = true;
        roadPreviewInstance.textureMode = LineTextureMode.Tile;
        roadPreviewInstance.positionCount = 2;
        hasRoadStartPoint = true;
        UpdateRoadPreview(startPoint);
    }

    private void UpdateRoadPreview(Vector3 endpoint)
    {
        if (roadPreviewInstance == null)
        {
            return;
        }

        if (TryGetRoadKeyPointPath(roadStartPoint, endpoint, out List<Vector3> roadPath))
        {
            roadPreviewInstance.positionCount = roadPath.Count;
            for (int index = 0; index < roadPath.Count; index++)
            {
                roadPreviewInstance.SetPosition(index, roadPath[index]);
            }

            SetRoadPreviewColor(TryValidateRoad(roadStartPoint, endpoint, out _, out _));
        }
        else
        {
            roadPreviewInstance.positionCount = 2;
            roadPreviewInstance.SetPosition(0, roadStartPoint);
            roadPreviewInstance.SetPosition(1, endpoint);
            SetRoadPreviewColor(false);
        }
    }

    private void SetRoadPreviewColor(bool isValid)
    {
        Color color = isValid ? settings.validRoadPreviewColor : settings.invalidRoadPreviewColor;
        roadPreviewInstance.startColor = color;
        roadPreviewInstance.endColor = color;
    }

    private Vector3 ResolveRoadEndpoint(Vector3 mouseWorldPosition)
    {
        float tolerance = Mathf.Max(
            settings.roadEndpointKeyPointTolerance,
            settings.roadEndpointKeyPointClearance);
        if (TryGetRoadKeyPoint(mouseWorldPosition, tolerance, out Vector3 matchedKeyPoint))
        {
            return matchedKeyPoint;
        }

        return SnapToGrid(mouseWorldPosition);
    }

    private bool TryGetRoadKeyPoint(Vector3 position, float selectionRadius, out Vector3 keyPoint)
    {
        keyPoint = Vector3.zero;
        float closestDistanceSquared = selectionRadius * selectionRadius;
        bool foundKeyPoint = false;

        foreach (Vector3 candidate in roadKeyPoints)
        {
            float distanceSquared = (new Vector2(candidate.x, candidate.y)
                - new Vector2(position.x, position.y)).sqrMagnitude;
            if (distanceSquared <= closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                keyPoint = candidate;
                foundKeyPoint = true;
            }
        }

        return foundKeyPoint;
    }

    private bool BuildRoad(Vector3 startPoint, Vector3 endPoint, out List<Vector3> addedKeyPoints)
    {
        addedKeyPoints = new List<Vector3>();
        if (!TryValidateRoad(startPoint, endPoint, out List<Vector3> roadPath, out string failureReason))
        {
            Debug.LogWarning("Road build rejected: " + failureReason, this);
            return false;
        }

        int newKeyPointCount = 0;
        foreach (Vector3 point in roadPath)
        {
            if (!ContainsRoadKeyPoint(point))
            {
                newKeyPointCount++;
            }
        }

        long manaCost = (long)newKeyPointCount * settings.manaCostPerKeyPoint;
        if (manaCost > int.MaxValue)
        {
            Debug.LogError("Road build mana cost exceeds the supported resource amount.", this);
            return false;
        }

        if (manaCost > 0)
        {
            Player player = Player.Instance;
            if (player == null)
            {
                Debug.LogError("Road building requires an active Player to spend mana.", this);
                return false;
            }

            int manaCostAmount = (int)manaCost;
            if (!player.TrySpendResourceCosts(new[]
            {
                new ResourceCost
                {
                    resourceType = ResourceType.Mana,
                    amount = manaCostAmount
                }
            }))
            {
                Debug.LogWarning(
                    "Road build rejected: building " + newKeyPointCount + " KeyPoint(s) requires "
                    + manaCostAmount + " Mana.",
                    this);
                return false;
            }
        }

        HashSet<Vector2Int> cellsToAdd = new HashSet<Vector2Int>();
        for (int pathIndex = 0; pathIndex < roadPath.Count - 1; pathIndex++)
        {
            List<Vector2Int> cells = CreateGridCellPath(
                WorldPositionToGridCell(roadPath[pathIndex], settings.gridCellSize),
                WorldPositionToGridCell(roadPath[pathIndex + 1], settings.gridCellSize),
                false);
            foreach (Vector2Int cell in cells)
            {
                cellsToAdd.Add(cell);
            }
        }

        RemoveBuildableOverResourceObjectsOnRoadPath(roadPath, cellsToAdd);
        CancelUnstartedHousePlotsIntersecting(cellsToAdd);

        GameOverlord gameOverlord = GameOverlord.Instance;
        for (int segmentIndex = 0; segmentIndex < roadPath.Count - 1; segmentIndex++)
        {
            PlaceRoadTilesAlongSegment(
                gameOverlord.RoadTilePrefab,
                roadPath[segmentIndex],
                roadPath[segmentIndex + 1]);
        }

        foreach (Vector3 point in roadPath)
        {
            if (!ContainsRoadKeyPoint(point))
            {
                roadKeyPoints.Add(point);
                addedKeyPoints.Add(point);
            }
        }

        foreach (Vector2Int cell in cellsToAdd)
        {
            roadCells.Add(cell);
        }

        roads.Add(new RoadRecord { keyPoints = new List<Vector3>(roadPath) });
        RoadRevision++;
        return true;
    }

    private void PlaceRoadTilesAlongSegment(
        GameObject roadTilePrefab,
        Vector3 segmentStart,
        Vector3 segmentEnd)
    {
        float segmentLength = Vector3.Distance(segmentStart, segmentEnd);
        int tileCount = Mathf.Max(1, Mathf.CeilToInt(segmentLength / settings.roadTileSpacing));
        for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            float interpolation = (tileIndex + 0.5f) / tileCount;
            Vector3 tilePosition = Vector3.Lerp(segmentStart, segmentEnd, interpolation);
            Quaternion tileRotation =
                Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            Instantiate(roadTilePrefab, tilePosition, tileRotation, transform);
        }
    }

    private bool TryGetRoadKeyPointPath(Vector3 startPoint, Vector3 endPoint, out List<Vector3> roadPath)
    {
        roadPath = CreateRoadKeyPointPath(startPoint, endPoint);
        return roadPath != null;
    }

    private bool TryValidateRoad(
        Vector3 startPoint,
        Vector3 endPoint,
        out List<Vector3> roadPath,
        out string failureReason)
    {
        roadPath = null;
        failureReason = null;
        if ((endPoint - startPoint).sqrMagnitude <= Mathf.Epsilon)
        {
            failureReason = "The road must have distinct start and end points.";
            return false;
        }

        if (!TryGetRoadKeyPointPath(startPoint, endPoint, out roadPath))
        {
            failureReason = "Could not create a grid-aligned path within maxRoadKeyPointDistance.";
            return false;
        }

        if (TryGetResourceObjectOnRoadPath(roadPath, out ResourceObject blockingResource))
        {
            failureReason = "The road intersects an occupied resource cell ("
                + blockingResource.Type + ").";
            return false;
        }

        foreach (Vector3 pathPoint in roadPath)
        {
            Vector2Int pathCell = WorldPositionToGridCell(pathPoint, settings.gridCellSize);
            if (communityStorageCells.Contains(pathCell))
            {
                failureReason = "The road intersects community storage.";
                return false;
            }

            if (IsOccupiedByStartedHouse(pathCell))
            {
                failureReason = "The road intersects a claimed house plot.";
                return false;
            }
        }

        for (int pathIndex = 0; pathIndex < roadPath.Count - 1; pathIndex++)
        {
            List<Vector2Int> cells = CreateGridCellPath(
                WorldPositionToGridCell(roadPath[pathIndex], settings.gridCellSize),
                WorldPositionToGridCell(roadPath[pathIndex + 1], settings.gridCellSize),
                false);
            foreach (Vector2Int cell in cells)
            {
                if (communityStorageCells.Contains(cell))
                {
                    failureReason = "The road intersects community storage.";
                    return false;
                }

                if (IsOccupiedByStartedHouse(cell))
                {
                    failureReason = "The road intersects a claimed house plot.";
                    return false;
                }
            }
        }

        if (!IsEndpointClearOfKeyPoints(endPoint))
        {
            failureReason = "The road endpoint is too close to an existing keyPoint.";
            return false;
        }

        if (IntersectsExistingRoad(roadPath))
        {
            failureReason = "The road crosses or overlaps an existing road away from a shared keyPoint.";
            return false;
        }

        return true;
    }

    private bool IsEndpointClearOfKeyPoints(Vector3 endpoint)
    {
        float clearance = Mathf.Max(0f, settings.roadEndpointKeyPointClearance);
        float clearanceSquared = clearance * clearance;
        foreach (Vector3 keyPoint in roadKeyPoints)
        {
            Vector2 delta = new Vector2(endpoint.x - keyPoint.x, endpoint.y - keyPoint.y);
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return true;
            }
        }

        foreach (Vector3 keyPoint in roadKeyPoints)
        {
            Vector2 delta = new Vector2(endpoint.x - keyPoint.x, endpoint.y - keyPoint.y);
            if (delta.sqrMagnitude < clearanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private bool IntersectsExistingRoad(List<Vector3> proposedPath)
    {
        for (int proposedIndex = 0; proposedIndex < proposedPath.Count - 1; proposedIndex++)
        {
            Vector2 proposedStart = proposedPath[proposedIndex];
            Vector2 proposedEnd = proposedPath[proposedIndex + 1];
            foreach (RoadRecord existingRoad in roads)
            {
                if (existingRoad == null || existingRoad.keyPoints == null)
                {
                    continue;
                }

                for (int existingIndex = 0; existingIndex < existingRoad.keyPoints.Count - 1; existingIndex++)
                {
                    Vector2 existingStart = existingRoad.keyPoints[existingIndex];
                    Vector2 existingEnd = existingRoad.keyPoints[existingIndex + 1];
                    if (!TryGetSegmentIntersection(
                        proposedStart,
                        proposedEnd,
                        existingStart,
                        existingEnd,
                        out Vector2 intersection,
                        out bool overlapping))
                    {
                        continue;
                    }

                    if (overlapping || !ContainsRoadKeyPoint(intersection))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryGetSegmentIntersection(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd,
        out Vector2 intersection,
        out bool overlapping)
    {
        const float tolerance = 0.0001f;
        intersection = Vector2.zero;
        overlapping = false;
        Vector2 firstDirection = firstEnd - firstStart;
        Vector2 secondDirection = secondEnd - secondStart;
        float denominator = Cross(firstDirection, secondDirection);
        Vector2 startOffset = secondStart - firstStart;

        if (Mathf.Abs(denominator) <= tolerance)
        {
            if (Mathf.Abs(Cross(startOffset, firstDirection)) > tolerance)
            {
                return false;
            }

            float lengthSquared = firstDirection.sqrMagnitude;
            if (lengthSquared <= tolerance * tolerance)
            {
                return (firstStart - secondStart).sqrMagnitude <= tolerance * tolerance
                    || (firstStart - secondEnd).sqrMagnitude <= tolerance * tolerance;
            }

            float firstProjection = Vector2.Dot(secondStart - firstStart, firstDirection) / lengthSquared;
            float secondProjection = Vector2.Dot(secondEnd - firstStart, firstDirection) / lengthSquared;
            float overlapStart = Mathf.Max(0f, Mathf.Min(firstProjection, secondProjection));
            float overlapEnd = Mathf.Min(1f, Mathf.Max(firstProjection, secondProjection));
            if (overlapEnd < overlapStart - tolerance)
            {
                return false;
            }

            overlapping = overlapEnd - overlapStart > tolerance;
            intersection = firstStart + firstDirection * ((overlapStart + overlapEnd) * 0.5f);
            return true;
        }

        float firstParameter = Cross(startOffset, secondDirection) / denominator;
        float secondParameter = Cross(startOffset, firstDirection) / denominator;
        if (firstParameter < -tolerance || firstParameter > 1f + tolerance
            || secondParameter < -tolerance || secondParameter > 1f + tolerance)
        {
            return false;
        }

        intersection = firstStart + firstDirection * firstParameter;
        return true;
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }

    private List<Vector3> CreateRoadKeyPointPath(Vector3 startPoint, Vector3 endPoint)
    {
        float gridCellSize = settings.gridCellSize;
        Vector2Int startCell = WorldPositionToGridCell(startPoint, gridCellSize);
        Vector2Int endCell = WorldPositionToGridCell(endPoint, gridCellSize);
        bool allowDiagonalSteps = settings.maxRoadKeyPointDistance >= gridCellSize * Mathf.Sqrt(2f);
        List<Vector2Int> cells = CreateGridCellPath(startCell, endCell, allowDiagonalSteps);
        List<Vector3> path = new List<Vector3> { startPoint };
        int currentCellIndex = 0;

        while (currentCellIndex < cells.Count - 1)
        {
            int nextCellIndex = currentCellIndex + 1;
            Vector3 currentPoint = GridCellToWorldPosition(cells[currentCellIndex], gridCellSize);
            Vector3 firstCandidate = GridCellToWorldPosition(cells[nextCellIndex], gridCellSize);
            if ((firstCandidate - currentPoint).sqrMagnitude
                > settings.maxRoadKeyPointDistance * settings.maxRoadKeyPointDistance + Mathf.Epsilon)
            {
                return null;
            }

            while (nextCellIndex + 1 < cells.Count)
            {
                Vector3 candidate = GridCellToWorldPosition(cells[nextCellIndex + 1], gridCellSize);
                if ((candidate - currentPoint).sqrMagnitude
                    > settings.maxRoadKeyPointDistance * settings.maxRoadKeyPointDistance + Mathf.Epsilon)
                {
                    break;
                }

                nextCellIndex++;
            }

            Vector3 nextPoint = GridCellToWorldPosition(cells[nextCellIndex], gridCellSize);
            if (nextCellIndex == cells.Count - 1)
            {
                nextPoint = endPoint;
            }

            if ((nextPoint - path[path.Count - 1]).sqrMagnitude > Mathf.Epsilon)
            {
                path.Add(nextPoint);
            }

            currentCellIndex = nextCellIndex;
        }

        if ((endPoint - path[path.Count - 1]).sqrMagnitude > Mathf.Epsilon)
        {
            if ((endPoint - path[path.Count - 1]).sqrMagnitude
                > settings.maxRoadKeyPointDistance * settings.maxRoadKeyPointDistance + Mathf.Epsilon)
            {
                return null;
            }

            path.Add(endPoint);
        }

        return path;
    }

    private List<Vector2Int> CreateGridCellPath(
        Vector2Int startCell,
        Vector2Int endCell,
        bool allowDiagonalSteps)
    {
        List<Vector2Int> cells = new List<Vector2Int> { startCell };
        int x = startCell.x;
        int y = startCell.y;
        int deltaX = Mathf.Abs(endCell.x - x);
        int deltaY = Mathf.Abs(endCell.y - y);
        int stepX = x < endCell.x ? 1 : -1;
        int stepY = y < endCell.y ? 1 : -1;

        if (allowDiagonalSteps)
        {
            int error = deltaX - deltaY;
            while (x != endCell.x || y != endCell.y)
            {
                int doubledError = 2 * error;
                if (doubledError >= -deltaY)
                {
                    error -= deltaY;
                    x += stepX;
                }

                if (doubledError <= deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }

                cells.Add(new Vector2Int(x, y));
            }

            return cells;
        }

        int stepsX = 0;
        int stepsY = 0;
        while (stepsX < deltaX || stepsY < deltaY)
        {
            if (stepsX == deltaX)
            {
                y += stepY;
                stepsY++;
            }
            else if (stepsY == deltaY)
            {
                x += stepX;
                stepsX++;
            }
            else if ((1 + 2 * stepsX) * deltaY <= (1 + 2 * stepsY) * deltaX)
            {
                x += stepX;
                stepsX++;
            }
            else
            {
                y += stepY;
                stepsY++;
            }

            cells.Add(new Vector2Int(x, y));
        }

        return cells;
    }

    private static Vector2Int WorldPositionToGridCell(Vector3 position, float gridCellSize)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / gridCellSize),
            Mathf.FloorToInt(position.y / gridCellSize));
    }

    private static Vector3 GridCellToWorldPosition(Vector2Int cell, float gridCellSize)
    {
        float halfCellSize = gridCellSize * 0.5f;
        return new Vector3(
            cell.x * gridCellSize + halfCellSize,
            cell.y * gridCellSize + halfCellSize,
            0f);
    }

    private Vector3 SnapToGrid(Vector3 worldPosition)
    {
        float gridCellSize = settings.gridCellSize;
        float halfCellSize = gridCellSize * 0.5f;
        return new Vector3(
            Mathf.Floor(worldPosition.x / gridCellSize) * gridCellSize + halfCellSize,
            Mathf.Floor(worldPosition.y / gridCellSize) * gridCellSize + halfCellSize,
            0f);
    }

    private bool ContainsRoadKeyPoint(Vector3 point)
    {
        foreach (Vector3 existingPoint in roadKeyPoints)
        {
            if ((existingPoint - point).sqrMagnitude <= Mathf.Epsilon)
            {
                return true;
            }
        }

        return false;
    }

    private bool ContainsRoadKeyPoint(Vector2 point)
    {
        foreach (Vector3 existingPoint in roadKeyPoints)
        {
            Vector2 delta = new Vector2(existingPoint.x - point.x, existingPoint.y - point.y);
            if (delta.sqrMagnitude <= 0.0000001f)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetMouseWorldPosition(Vector3 screenPosition, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (mapCamera == null)
        {
            return false;
        }

        Ray pointerRay = mapCamera.ScreenPointToRay(screenPosition);
        Plane mapPlane = new Plane(Vector3.forward, Vector3.zero);
        if (!mapPlane.Raycast(pointerRay, out float distance))
        {
            return false;
        }

        worldPosition = pointerRay.GetPoint(distance);
        return true;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void ResetRoadPlacement()
    {
        hasRoadStartPoint = false;
        isTrackingClick = false;
        if (roadPreviewInstance != null)
        {
            Destroy(roadPreviewInstance.gameObject);
            roadPreviewInstance = null;
        }

        startMarkerInstance = null;
    }

    private void StopRoadBuilding()
    {
        isRoadBuilding = false;
        hasRoadStartPoint = false;
        isTrackingClick = false;
        if (startMarkerInstance != null)
        {
            Destroy(startMarkerInstance);
            startMarkerInstance = null;
        }

        if (roadPreviewInstance != null)
        {
            Destroy(roadPreviewInstance.gameObject);
            roadPreviewInstance = null;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetRoadBuildingCursor(false);
        }
    }

    private void OnDisable()
    {
        StopRoadBuilding();
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            StopRoadBuilding();
            instance = null;
        }
    }
}
