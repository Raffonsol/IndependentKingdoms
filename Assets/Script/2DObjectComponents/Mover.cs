using System.Collections.Generic;
using UnityEngine;

public class Mover : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 2f;
    [SerializeField] private Vector2 movementDurationRange = new Vector2(1f, 3f);
    [SerializeField] private Vector2 idleDurationRange = new Vector2(1f, 3f);
    
    [SerializeField] private float legSwingAngle = 30f;
    [SerializeField] private float legSwingSpeed = 6f;
    [SerializeField] private Vector2 legPivotOffset = new Vector2(0.45f, 0.45f);
    [SerializeField] private float idleHeaveHeight = 0.05f;
    [SerializeField] private float idleHeaveSpeed = 1.5f;

    [SerializeField] private GameObject[] legs;
    [SerializeField] private GameObject torso;

    private float defaultMovementSpeed;
    private Vector2 movementDirection;
    private bool facingLeft = true;
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

    protected virtual bool PrioritizeRoadPaths
    {
        get { return true; }
    }

    protected bool HasFailedPathRequest
    {
        get { return hasPathRequest && !pathCompleted && pathWaypoints.Count == 0; }
    }

    private void OnValidate()
    {
        if (legs != null && legs.Length != 2)
        {
            Debug.LogError("Character has a different number of legs than expected! Expected 2, but found " + legs.Length + ". Check the Mover component on " + gameObject.name + ".");
        }
    }

    protected virtual void Awake()
    {
        defaultMovementSpeed = movementSpeed;
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

    public void Configure(Body bodyData, GameObject bodyTorso, GameObject[] bodyLegs)
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
        legSwingAngle = bodyData.legSwingAngle;
        legSwingSpeed = bodyData.legSwingSpeed;
        legPivotOffset = new Vector2(bodyData.legPivotOffsetX, bodyData.legPivotOffsetY);
        idleHeaveHeight = bodyData.idleHeaveHeight;
        idleHeaveSpeed = bodyData.idleHeaveSpeed;
        torso = bodyTorso;
        legs = bodyLegs;

        InitializeRuntimeState();
    }

    private void InitializeRuntimeState()
    {
        torsoStartingLocalPosition = torso != null ? torso.transform.localPosition : Vector3.zero;

        legStartingRotations = new Quaternion[2];
        legPivotLocalOffsets = new Vector3[2];
        if (legs == null)
        {
            return;
        }

        for (int index = 0; index < Mathf.Min(legs.Length, 2); index++)
        {
            if (legs[index] == null)
            {
                continue;
            }

            Transform legTransform = legs[index].transform;
            legStartingRotations[index] = legTransform.localRotation;

            SpriteRenderer spriteRenderer = legs[index].GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Sprite sprite = spriteRenderer.sprite;
                Vector2 pivotInPixels = Vector2.Scale(sprite.rect.size, legPivotOffset);
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
        if (direction.x < -0.0001f)
        {
            facingLeft = true;
            UpdateFacingScale();
        }
        else if (direction.x > 0.0001f)
        {
            facingLeft = false;
            UpdateFacingScale();
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

    /// <summary>Swings the two legs in opposite phases to imitate walking.</summary>
    public void AnimateLegs()
    {
        if (legs == null || legStartingRotations == null)
        {
            return;
        }

        float swing = Mathf.Sin(Time.time * legSwingSpeed) * legSwingAngle;
        SetLegRotation(0, swing);
        SetLegRotation(1, -swing);
    }

    private void SetLegRotation(int index, float angle)
    {
        if (legs == null || legStartingRotations == null || legPivotLocalOffsets == null
            || index < 0 || index >= legs.Length || index >= legStartingRotations.Length)
        {
            return;
        }

        if (legs[index] != null)
        {
            Transform legTransform = legs[index].transform;
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

        for (int index = 0; index < Mathf.Min(legs.Length, 2); index++)
        {
            SetLegRotation(index, 0f);
        }
    }

    private void UpdateFacingScale()
    {
        Vector3 scale = transform.localScale;
        scale.x = facingLeft ? 1f : -1f;
        transform.localScale = scale;
    }

}
