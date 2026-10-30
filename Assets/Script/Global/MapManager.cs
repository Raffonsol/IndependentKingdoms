using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MapManager : MonoBehaviour
{
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
    private readonly List<Villager> housePlotQueue = new List<Villager>();
    private readonly Dictionary<Villager, HousePlotRecord> claimedHousePlots =
        new Dictionary<Villager, HousePlotRecord>();

    [SerializeField] private Vector2 mapSize = new Vector2(100f, 100f);
    [SerializeField] private GameObject treePrefab;
    [SerializeField, Min(0)] private int treeCount;
    [SerializeField, Min(0f)] private float treeEdgePadding = 2f;
    [SerializeField, Min(0f)] private float minimumTreeSpacing = 3f;
    [SerializeField, Min(1)] private int treePlacementAttemptsPerTree = 20;
    [SerializeField] private bool randomizeTreeRotation = true;
    [SerializeField] private Vector2 treeVisualOffset;
    [SerializeField] private Vector2 treeScaleRange = new Vector2(0.9f, 1.1f);
    [SerializeField] private int treeRandomSeed;

    private MapSettings settings;
    private UnityEngine.Camera mapCamera;
    private GameObject startMarkerInstance;
    private LineRenderer roadPreviewInstance;
    private Vector3 roadStartPoint;
    private Vector2 pointerDownPosition;
    private bool isRoadBuilding;
    private bool hasRoadStartPoint;
    private bool isTrackingClick;

    private void Start()
    {
        SpawnTrees();
    }

    public IReadOnlyList<Vector3> RoadKeyPoints
    {
        get { return roadKeyPoints; }
    }

    public int RoadRevision { get; private set; }
    public int PathfindingRevision { get; private set; }

    public float GridCellSize
    {
        get { return settings != null ? settings.gridCellSize : 0f; }
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

    public Bounds GetGridCellBounds(Vector2Int cell)
    {
        float gridCellSize = GridCellSize;
        Vector3 center = GridCellToWorldPosition(cell, gridCellSize);
        center.z = MapCenter.z;
        return new Bounds(center, new Vector3(gridCellSize, gridCellSize, 0f));
    }

    public bool TryFindPath(
        Vector3 startPosition,
        Vector3 destination,
        float arrivalDistance,
        out List<Vector3> waypoints)
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
            if (!TryFindCellPath(startCell, destinationCell, out List<Vector2Int> cellPath))
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
            out waypoints);
    }

    private bool TryFindApproachPath(
        Vector2Int startCell,
        Vector2Int blockedCell,
        Vector3 destination,
        float arrivalDistance,
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
                || !TryFindCellPath(startCell, approachCell, out List<Vector2Int> candidatePath))
            {
                continue;
            }

            float candidateLength = (candidatePath.Count - 1) * gridCellSize
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
        Dictionary<Vector2Int, int> movementCosts = new Dictionary<Vector2Int, int>
        {
            { startCell, 0 }
        };
        Vector2Int[] directions =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.up
        };

        while (openCells.Count > 0)
        {
            int bestIndex = FindLowestCostCellIndex(openCells, movementCosts, goalCell);
            Vector2Int currentCell = openCells[bestIndex];
            openCells.RemoveAt(bestIndex);
            if (currentCell == goalCell)
            {
                path = ReconstructCellPath(previousCells, startCell, goalCell);
                return true;
            }

            closedCells.Add(currentCell);
            foreach (Vector2Int direction in directions)
            {
                Vector2Int neighbor = currentCell + direction;
                if (closedCells.Contains(neighbor)
                    || !IsCellInsideMap(neighbor)
                    || (neighbor != startCell && IsCellBlocked(neighbor)))
                {
                    continue;
                }

                int candidateCost = movementCosts[currentCell] + 1;
                if (movementCosts.TryGetValue(neighbor, out int knownCost)
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
        Dictionary<Vector2Int, int> movementCosts,
        Vector2Int goalCell)
    {
        int bestIndex = 0;
        int bestTotalCost = movementCosts[openCells[0]] + GetManhattanDistance(openCells[0], goalCell);
        for (int index = 1; index < openCells.Count; index++)
        {
            Vector2Int cell = openCells[index];
            int totalCost = movementCosts[cell] + GetManhattanDistance(cell, goalCell);
            if (totalCost < bestTotalCost)
            {
                bestIndex = index;
                bestTotalCost = totalCost;
            }
        }

        return bestIndex;
    }

    private static int GetManhattanDistance(Vector2Int first, Vector2Int second)
    {
        return Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y);
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
            currentCell = previousCells[currentCell];
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
        return resourceObjectsByCell.TryGetValue(cell, out ResourceObject resourceObject)
            && resourceObject != null;
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

    public bool TryGetClosestResourceObject(
        Villager villager,
        ResourceType resourceType,
        Vector3 position,
        bool hasAxe,
        out ResourceObject closestResource)
    {
        closestResource = null;
        if (villager == null)
        {
            return false;
        }

        float closestDistanceSquared = float.PositiveInfinity;
        foreach (ResourceObject resourceObject in resourceObjectsByCell.Values)
        {
            if (resourceObject == null
                || resourceObject.Type != resourceType
                || !resourceObject.CanGather(hasAxe))
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
            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestResource = resourceObject;
            }
        }

        if (closestResource == null)
        {
            return false;
        }

        resourceReservations[closestResource] = villager;
        return true;
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

        gatheredAmount = resourceObject.Take(requestedAmount, hasAxe);
        if (resourceObject.Amount == 0)
        {
            resourceObjectsByCell.Remove(resourceObject.GridCell);
            ReleaseResourceReservation(resourceObject, villager);
            PathfindingRevision++;
            Destroy(resourceObject.gameObject);
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

        for (int x = 0; x < housePlot.dimensions.x; x++)
        {
            for (int y = 0; y < housePlot.dimensions.y; y++)
            {
                claimedHouseCells.Remove(new Vector2Int(
                    housePlot.originCell.x + x,
                    housePlot.originCell.y + y));
            }
        }

        claimedHousePlots.Remove(villager);
    }

    private void CancelUnstartedHousePlotsIntersecting(HashSet<Vector2Int> roadCellsToAdd)
    {
        List<Villager> villagersToCancel = new List<Villager>();
        foreach (KeyValuePair<Villager, HousePlotRecord> claimedPlot in claimedHousePlots)
        {
            Villager villager = claimedPlot.Key;
            HousePlotRecord housePlot = claimedPlot.Value;
            if (villager == null
                || housePlot == null
                || villager.HousingState == VillagerHousingState.BuildingHouse
                || villager.HousingState == VillagerHousingState.Housed)
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
    }

    private bool IsOccupiedByStartedHouse(Vector2Int cell)
    {
        foreach (KeyValuePair<Villager, HousePlotRecord> claimedPlot in claimedHousePlots)
        {
            Villager villager = claimedPlot.Key;
            HousePlotRecord housePlot = claimedPlot.Value;
            if (villager == null
                || housePlot == null
                || (villager.HousingState != VillagerHousingState.BuildingHouse
                    && villager.HousingState != VillagerHousingState.Housed))
            {
                continue;
            }

            if (cell.x >= housePlot.originCell.x
                && cell.x < housePlot.originCell.x + housePlot.dimensions.x
                && cell.y >= housePlot.originCell.y
                && cell.y < housePlot.originCell.y + housePlot.dimensions.y)
            {
                return true;
            }
        }

        return false;
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
                if (!IsHousePlotAvailable(origin, dimensions))
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
                    center = center
                };
                return true;
            }
        }

        housePlot = null;
        return false;
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
                    || resourceObjectsByCell.ContainsKey(cell))
                {
                    return false;
                }
            }
        }

        return true;
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
            if (TryGetResourceObjectAtCell(cell, out resourceObject) && resourceObject != null)
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

            if (SegmentIntersectsCell(start, end, occupiedCell.Key, gridCellSize))
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
        housePlotQueue.Clear();
        claimedHousePlots.Clear();

        settings = GameData.Instance.mapSettings;
        if (settings == null)
        {
            Debug.LogError("Map settings could not be loaded from GameData.", this);
            enabled = false;
            return;
        }

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

    private void SpawnTrees()
    {
        if (treeCount == 0)
        {
            return;
        }

        if (treeCount < 0
            || mapSize.x <= 0f
            || mapSize.y <= 0f
            || treeEdgePadding < 0f
            || treeEdgePadding * 2f >= mapSize.x
            || treeEdgePadding * 2f >= mapSize.y
            || minimumTreeSpacing < 0f
            || treePlacementAttemptsPerTree < 1
            || treeScaleRange.x <= 0f
            || treeScaleRange.y < treeScaleRange.x)
        {
            Debug.LogError("Tree spawning settings are invalid.", this);
            return;
        }

        if (treePrefab == null)
        {
            Debug.LogError("A tree prefab must be assigned when treeCount is greater than zero.", this);
            return;
        }

        if (treePrefab.GetComponent<ResourceObject>() == null)
        {
            Debug.LogError("The tree prefab must have a ResourceObject component.", this);
            return;
        }

        if (settings == null || settings.gridCellSize <= 0f)
        {
            Debug.LogError("A positive gridCellSize is required to place trees on map cells.", this);
            return;
        }

        SpriteRenderer[] spriteRenderers = treePrefab.GetComponentsInChildren<SpriteRenderer>(true);
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
            Debug.LogError("The tree prefab must contain a SpriteRenderer with a Sprite.", this);
            return;
        }

        System.Random random = new System.Random(
            treeRandomSeed == 0 ? Environment.TickCount : treeRandomSeed);
        float gridCellSize = settings.gridCellSize;
        float halfCellSize = gridCellSize * 0.5f;
        Vector2 halfMapSize = mapSize * 0.5f;
        Vector2 spawnCenter = transform.position;
        float minX = spawnCenter.x - halfMapSize.x + treeEdgePadding;
        float maxX = spawnCenter.x + halfMapSize.x - treeEdgePadding;
        float minY = spawnCenter.y - halfMapSize.y + treeEdgePadding;
        float maxY = spawnCenter.y + halfMapSize.y - treeEdgePadding;
        int minCellX = Mathf.CeilToInt((minX - halfCellSize) / gridCellSize);
        int maxCellX = Mathf.FloorToInt((maxX - halfCellSize) / gridCellSize);
        int minCellY = Mathf.CeilToInt((minY - halfCellSize) / gridCellSize);
        int maxCellY = Mathf.FloorToInt((maxY - halfCellSize) / gridCellSize);

        if (minCellX > maxCellX || minCellY > maxCellY)
        {
            Debug.LogError("The map is too small to contain any grid cells for tree placement.", this);
            return;
        }

        float minimumDistanceSquared = minimumTreeSpacing * minimumTreeSpacing;
        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>(resourceObjectsByCell.Keys);
        int spawnedTreeCount = 0;

        for (int treeIndex = 0; treeIndex < treeCount; treeIndex++)
        {
            bool foundPosition = false;
            Vector2Int cell = Vector2Int.zero;
            Vector3 cellPosition = Vector3.zero;

            for (int attempt = 0; attempt < treePlacementAttemptsPerTree; attempt++)
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

            float rotation = randomizeTreeRotation
                ? RandomRange(random, 0f, 360f)
                : 0f;
            float scale = RandomRange(random, treeScaleRange.x, treeScaleRange.y);
            GameObject tree = Instantiate(
                treePrefab,
                cellPosition,
                Quaternion.Euler(0f, 0f, rotation),
                transform);
            tree.transform.localScale *= scale;
            ApplyTreeVisualOffset(tree.transform);

            ResourceObject resourceObject = tree.GetComponent<ResourceObject>();
            resourceObject.SetGridCell(cell);
            resourceObjectsByCell.Add(cell, resourceObject);
            occupiedCells.Add(cell);
            PathfindingRevision++;
            spawnedTreeCount++;
        }

        if (spawnedTreeCount < treeCount)
        {
            Debug.LogWarning(
                "Placed " + spawnedTreeCount + " of " + treeCount
                + " requested trees. Increase the map size or placement attempts, or reduce minimum tree spacing.",
                this);
        }
    }

    private void ApplyTreeVisualOffset(Transform treeTransform)
    {
        if (treeVisualOffset == Vector2.zero)
        {
            return;
        }

        Vector3 worldOffset = new Vector3(treeVisualOffset.x, treeVisualOffset.y, 0f);
        for (int childIndex = 0; childIndex < treeTransform.childCount; childIndex++)
        {
            Transform visualChild = treeTransform.GetChild(childIndex);
            if (visualChild.GetComponentInChildren<SpriteRenderer>(true) != null)
            {
                visualChild.position += worldOffset;
            }
        }
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
            || settings.keyPointSelectionRadius <= 0f
            || settings.roadEndpointKeyPointTolerance < 0f
            || settings.clickDragThresholdPixels < 0f
            || settings.maxRoadKeyPointDistance <= 0f
            || settings.roadTileSpacing <= 0f)
        {
            Debug.LogError("Map settings contain an invalid road or grid distance.", this);
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

        CancelUnstartedHousePlotsIntersecting(cellsToAdd);

        GameOverlord gameOverlord = GameOverlord.Instance;
        for (int segmentIndex = 0; segmentIndex < roadPath.Count - 1; segmentIndex++)
        {
            Vector3 segmentStart = roadPath[segmentIndex];
            Vector3 segmentEnd = roadPath[segmentIndex + 1];
            float segmentLength = Vector3.Distance(segmentStart, segmentEnd);
            int tileCount = Mathf.Max(1, Mathf.CeilToInt(segmentLength / settings.roadTileSpacing));
            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                float interpolation = (tileIndex + 0.5f) / tileCount;
                Vector3 tilePosition = Vector3.Lerp(segmentStart, segmentEnd, interpolation);
                Quaternion tileRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                Instantiate(gameOverlord.RoadTilePrefab, tilePosition, tileRotation, transform);
            }
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
            if (IsOccupiedByStartedHouse(WorldPositionToGridCell(pathPoint, settings.gridCellSize)))
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
