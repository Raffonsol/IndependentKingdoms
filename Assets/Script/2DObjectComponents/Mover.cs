using System.Collections.Generic;
using UnityEngine;

public class Mover : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 2f;
    [SerializeField] private Vector2 movementDurationRange = new Vector2(1f, 3f);
    [SerializeField] private Vector2 idleDurationRange = new Vector2(1f, 3f);
    
    [SerializeField] private float idleHeaveHeight = 0.05f;
    [SerializeField] private float idleHeaveSpeed = 1.5f;

    private float runningSpeed;
    [SerializeField] private LegDefinitions[] legs;
    [SerializeField] private GameObject torso;

    private float defaultMovementSpeed;
    private Vector2 movementDirection;
    private bool facingLeft = true;
    private Vector2Int facingDirection = Vector2Int.left;
    private readonly List<Vector3> pathWaypoints = new List<Vector3>();
    private int nextWaypointIndex;
    private Vector3 requestedDestination;
    private Vector2Int requestedDestinationCell;
    private float requestedArrivalDistance;
    private int pathfindingRevision = -1;
    private bool pathCompleted;
    private bool hasPathRequest;
    private bool loggedMissingMapManagerForPathfinding;
    private Quaternion[] legStartingRotations;
    private Vector3[] legPivotLocalOffsets;
    private Vector3 torsoStartingLocalPosition;

    protected Vector2 MovementDurationRange
    {
        get { return movementDurationRange; }
    }

    protected Vector2 IdleDurationRange
    {
        get { return idleDurationRange; }
    }

    protected float MovementSpeed
    {
        get { return movementSpeed; }
    }

    protected float DefaultMovementSpeed
    {
        get { return defaultMovementSpeed; }
    }

    protected float RunningSpeed
    {
        get { return runningSpeed; }
    }

    protected Vector2Int FacingDirection
    {
        get { return facingDirection; }
    }

    protected virtual bool PrioritizeRoadPaths
    {
        get { return true; }
    }

    protected bool HasFailedPathRequest
    {
        get { return hasPathRequest && !pathCompleted && pathWaypoints.Count == 0; }
    }

    protected virtual void Awake()
    {
        defaultMovementSpeed = movementSpeed;
        CombatSettings combatSettings = GameData.Instance.combatSettings;
        if (combatSettings == null)
        {
            Debug.LogError("Mover running speed could not be loaded from GameData.", this);
            enabled = false;
            return;
        }

        runningSpeed = combatSettings.runningSpeed;
        InitializeRuntimeState();
        UpdateFacingScale();
    }

    protected virtual void Update()
    {
        FollowPath(Time.deltaTime);

        if (movementDirection.sqrMagnitude > 0f && movementSpeed > 0f)
        {
            AnimateLegs();
        }
        else
        {
            ResetLegs();
            AnimateIdleHeaving();
        }
    }

    public void Configure(Body bodyData, GameObject bodyTorso, LegDefinitions[] bodyLegs)
    {
        if (bodyData == null)
        {
            Debug.LogError("Cannot configure Mover without body data.", this);
            return;
        }

        movementSpeed = bodyData.movementSpeed;
        defaultMovementSpeed = bodyData.movementSpeed;
        movementDurationRange = new Vector2(bodyData.movementDurationRangeX, bodyData.movementDurationRangeY);
        idleDurationRange = new Vector2(bodyData.idleDurationRangeX, bodyData.idleDurationRangeY);
        idleHeaveHeight = bodyData.idleHeaveHeight;
        idleHeaveSpeed = bodyData.idleHeaveSpeed;
        torso = bodyTorso;
        legs = bodyLegs;

        InitializeRuntimeState();
    }

    protected void ConfigureMovement(
        float speed,
        float configuredRunningSpeed,
        Vector2 movementDurations,
        Vector2 idleDurations)
    {
        movementSpeed = speed;
        defaultMovementSpeed = speed;
        runningSpeed = configuredRunningSpeed;
        movementDurationRange = movementDurations;
        idleDurationRange = idleDurations;
    }

    protected void ConfigureLegDefinitions(LegDefinitions[] bodyLegs)
    {
        legs = bodyLegs;
        InitializeRuntimeState();
    }

    private void InitializeRuntimeState()
    {
        torsoStartingLocalPosition = torso != null ? torso.transform.localPosition : Vector3.zero;

        int legCount = legs != null ? legs.Length : 0;
        legStartingRotations = new Quaternion[legCount];
        legPivotLocalOffsets = new Vector3[legCount];
        if (legs == null)
        {
            return;
        }

        for (int index = 0; index < legs.Length; index++)
        {
            LegDefinitions definition = legs[index];
            if (definition == null || definition.leg == null)
            {
                continue;
            }

            Transform legTransform = definition.leg.transform;
            legStartingRotations[index] = legTransform.localRotation;

            SpriteRenderer spriteRenderer = definition.leg.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Sprite sprite = spriteRenderer.sprite;
                Vector2 pivotInPixels = Vector2.Scale(sprite.rect.size, definition.legPivotOffset);
                Vector2 pivotOffsetInPixels = pivotInPixels - sprite.pivot;
                legPivotLocalOffsets[index] = pivotOffsetInPixels / sprite.pixelsPerUnit;
            }
        }
    }

    private void AnimateIdleHeaving()
    {
        if (torso == null)
        {
            return;
        }

        Vector3 localPosition = torsoStartingLocalPosition;
        localPosition.y += Mathf.Sin(Time.time * idleHeaveSpeed) * idleHeaveHeight;
        torso.transform.localPosition = localPosition;
    }

    /// <summary>Sets the movement heading in degrees, where 0 points right and 90 points up.</summary>
    public void Move(float direction)
    {
        float radians = direction * Mathf.Deg2Rad;
        Vector2 heading = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        MapManager mapManager = MapManager.Instance;
        if (mapManager == null || mapManager.GridCellSize <= 0f)
        {
            LogMissingMapManager();
            StopMoving();
            return;
        }

        float travelDistance = Mathf.Max(
            mapManager.GridCellSize,
            movementSpeed * Mathf.Max(1f, movementDurationRange.y));
        Vector2 destination = (Vector2)transform.position + heading * travelDistance;
        Vector2 mapMinimum = (Vector2)mapManager.MapCenter - mapManager.MapSize * 0.5f;
        Vector2 mapMaximum = (Vector2)mapManager.MapCenter + mapManager.MapSize * 0.5f;
        float mapInset = mapManager.GridCellSize * 0.5f;
        destination.x = Mathf.Clamp(destination.x, mapMinimum.x + mapInset, mapMaximum.x - mapInset);
        destination.y = Mathf.Clamp(destination.y, mapMinimum.y + mapInset, mapMaximum.y - mapInset);
        if (PrioritizeRoadPaths
            && mapManager.TryGetClosestRoadCellPosition(destination, out Vector3 roadDestination))
        {
            destination = roadDestination;
        }

        MoveTo(new Vector3(destination.x, destination.y, transform.position.z));
    }

    protected void UpdateFacingFromDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.00000001f)
        {
            return;
        }

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            facingDirection = direction.x < 0f ? Vector2Int.left : Vector2Int.right;
            facingLeft = direction.x < 0f;
            UpdateFacingScale();
        }
        else
        {
            facingDirection = direction.y < 0f ? Vector2Int.down : Vector2Int.up;
        }
    }

    protected void StopMoving()
    {
        hasPathRequest = false;
        pathWaypoints.Clear();
        nextWaypointIndex = 0;
        movementDirection = Vector2.zero;
    }

    protected void MoveTo(Vector3 targetPosition)
    {
        MoveTo(targetPosition, 0f);
    }

    protected void MoveTo(Vector3 targetPosition, float arrivalDistance)
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager == null)
        {
            LogMissingMapManager();
            StopMoving();
            return;
        }

        arrivalDistance = Mathf.Max(0f, arrivalDistance);
        Vector2Int destinationCell = WorldPositionToGridCell(targetPosition, mapManager.GridCellSize);
        bool destinationChanged = !hasPathRequest
            || destinationCell != requestedDestinationCell
            || !Mathf.Approximately(requestedArrivalDistance, arrivalDistance)
            || (pathCompleted
                && Vector2.Distance(transform.position, targetPosition) > arrivalDistance);
        bool mapChanged = pathfindingRevision != mapManager.PathfindingRevision;
        if (!destinationChanged && !mapChanged)
        {
            return;
        }

        requestedDestination = targetPosition;
        requestedDestinationCell = destinationCell;
        requestedArrivalDistance = arrivalDistance;
        pathfindingRevision = mapManager.PathfindingRevision;
        hasPathRequest = true;
        pathCompleted = false;
        RebuildPath(mapManager);
    }

    private void RebuildPath(MapManager mapManager)
    {
        pathWaypoints.Clear();
        nextWaypointIndex = 0;

        if (!mapManager.TryFindPath(
                transform.position,
                requestedDestination,
                requestedArrivalDistance,
                out List<Vector3> waypoints,
                PrioritizeRoadPaths))
        {
            movementDirection = Vector2.zero;
            return;
        }

        pathWaypoints.AddRange(waypoints);
        if (pathWaypoints.Count == 0)
        {
            movementDirection = Vector2.zero;
            return;
        }

        UpdateFacingFromDirection(pathWaypoints[0] - transform.position);
    }

    private void FollowPath(float deltaTime)
    {
        movementDirection = Vector2.zero;
        if (!hasPathRequest)
        {
            return;
        }

        MapManager mapManager = MapManager.Instance;
        if (mapManager != null && pathfindingRevision != mapManager.PathfindingRevision)
        {
            pathfindingRevision = mapManager.PathfindingRevision;
            RebuildPath(mapManager);
        }

        if (movementSpeed <= 0f || pathWaypoints.Count == 0)
        {
            return;
        }

        float currentMovementSpeed = movementSpeed;
        if (mapManager != null
            && mapManager.GridCellSize > 0f
            && mapManager.IsRoadCell(WorldPositionToGridCell(transform.position, mapManager.GridCellSize)))
        {
            currentMovementSpeed *= mapManager.RoadMovementSpeedMultiplier;
        }

        float remainingDistance = currentMovementSpeed * deltaTime;
        while (nextWaypointIndex < pathWaypoints.Count)
        {
            Vector3 waypoint = pathWaypoints[nextWaypointIndex];
            Vector2 offset = (Vector2)(waypoint - transform.position);
            float distance = offset.magnitude;
            if (distance <= remainingDistance || distance <= Mathf.Epsilon)
            {
                transform.position = new Vector3(waypoint.x, waypoint.y, transform.position.z);
                remainingDistance = Mathf.Max(0f, remainingDistance - distance);
                nextWaypointIndex++;
                if (remainingDistance <= 0f)
                {
                    break;
                }

                continue;
            }

            movementDirection = offset / distance;
            transform.position += (Vector3)(movementDirection * remainingDistance);
            UpdateFacingFromDirection(movementDirection);
            break;
        }

        if (nextWaypointIndex >= pathWaypoints.Count)
        {
            movementDirection = Vector2.zero;
            pathWaypoints.Clear();
            pathCompleted = true;
        }
    }

    private static Vector2Int WorldPositionToGridCell(Vector3 position, float gridCellSize)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / gridCellSize),
            Mathf.FloorToInt(position.y / gridCellSize));
    }

    private void LogMissingMapManager()
    {
        if (!loggedMissingMapManagerForPathfinding)
        {
            Debug.LogError("Movement pathfinding requires an active MapManager.", this);
            loggedMissingMapManagerForPathfinding = true;
        }
    }

    protected void SetMovementSpeed(float speed)
    {
        movementSpeed = Mathf.Max(0f, speed);
    }

    /// <summary>Swings opposing legs in alternating phases to imitate walking.</summary>
    public void AnimateLegs()
    {
        if (legs == null || legStartingRotations == null)
        {
            return;
        }

        bool isRunning = movementSpeed > defaultMovementSpeed + 0.0001f;
        for (int index = 0; index < legs.Length; index++)
        {
            LegDefinitions definition = legs[index];
            if (definition == null)
            {
                continue;
            }

            float angle = isRunning
                ? definition.legSwingRunningAngle
                : definition.legSwingAngle;
            float speed = isRunning
                ? definition.legSwingRunningSpeed
                : definition.legSwingSpeed;
            float swing = Mathf.Sin(Time.time * speed) * angle;
            SetLegRotation(index, swing * GetLegSwingDirection(index));
        }
    }

    private float GetLegSwingDirection(int index)
    {
        if (legs.Length == 4)
        {
            return index == 0 || index == 3 ? 1f : -1f;
        }

        return index % 2 == 0 ? 1f : -1f;
    }

    private void SetLegRotation(int index, float angle)
    {
        if (legs == null || legStartingRotations == null || legPivotLocalOffsets == null
            || index < 0 || index >= legs.Length || index >= legStartingRotations.Length)
        {
            return;
        }

        LegDefinitions definition = legs[index];
        if (definition != null && definition.leg != null)
        {
            Transform legTransform = definition.leg.transform;
            Vector3 pivotPosition = legTransform.TransformPoint(legPivotLocalOffsets[index]);
            legTransform.localRotation = legStartingRotations[index] * Quaternion.Euler(0f, 0f, angle);
            legTransform.position += pivotPosition - legTransform.TransformPoint(legPivotLocalOffsets[index]);
        }
    }

    private void ResetLegs()
    {
        if (legs == null || legStartingRotations == null)
        {
            return;
        }

        for (int index = 0; index < legs.Length; index++)
        {
            SetLegRotation(index, 0f);
        }
    }

    private void UpdateFacingScale()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (facingLeft ? 1f : -1f);
        transform.localScale = scale;
    }

}
