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
    private bool hasMovementTarget;
    private Vector2 movementTarget;
    private Quaternion[] legStartingRotations;
    private Vector3[] legPivotLocalOffsets;
    private Vector3 torsoStartingLocalPosition;
    private bool loggedMissingMapManagerForAvoidance;

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

    private void OnValidate()
    {
        if (legs != null && legs.Length != 2)
        {
            Debug.LogError("Character has a different number of legs than expected! Expected 2, but found " + legs.Length + ". Check the Mover component on " + gameObject.name + ".");
        }
    }

    private void Awake()
    {
        defaultMovementSpeed = movementSpeed;
        InitializeRuntimeState();
        UpdateFacingScale();
    }

    protected virtual void Update()
    {
        Vector2 currentPosition = transform.position;
        Vector2 intendedDirection = movementDirection;
        float intendedDistance = movementSpeed * Time.deltaTime;

        if (hasMovementTarget)
        {
            Vector2 targetDelta = movementTarget - currentPosition;
            float targetDistance = targetDelta.magnitude;
            intendedDirection = targetDistance > Mathf.Epsilon
                ? targetDelta / targetDistance
                : Vector2.zero;
            intendedDistance = Mathf.Min(targetDistance, intendedDistance);
        }

        if (intendedDirection.sqrMagnitude > 0f
            && movementSpeed > 0f
            && ShouldAvoidResourceObjects())
        {
            MapManager mapManager = MapManager.Instance;
            if (mapManager == null)
            {
                if (!loggedMissingMapManagerForAvoidance)
                {
                    Debug.LogError(
                        "Resource avoidance requires an active MapManager.",
                        this);
                    loggedMissingMapManagerForAvoidance = true;
                }
            }
            else
            {
                loggedMissingMapManagerForAvoidance = false;
                Vector2 checkDirection = intendedDirection;
                float gridCellSize = mapManager.GridCellSize;
                float checkDistance = hasMovementTarget
                    ? Vector2.Distance(currentPosition, movementTarget)
                    : Mathf.Max(intendedDistance, gridCellSize * 1.5f);
                Vector3 pathEnd = currentPosition + checkDirection * checkDistance;

                if (mapManager.TryGetResourceObjectOnPath(
                        currentPosition,
                        pathEnd,
                        out _))
                {
                    if (TryFindClearResourceAvoidanceDirection(
                            mapManager,
                            currentPosition,
                            checkDirection,
                            Mathf.Max(gridCellSize * 1.5f, movementSpeed * 0.5f),
                            out Vector2 avoidanceDirection))
                    {
                        movementDirection = avoidanceDirection;
                        hasMovementTarget = false;
                        intendedDirection = avoidanceDirection;
                    }
                    else
                    {
                        movementDirection = Vector2.zero;
                        hasMovementTarget = false;
                        intendedDirection = Vector2.zero;
                        intendedDistance = 0f;
                    }
                }
            }
        }

        if (hasMovementTarget)
        {
            Vector2 targetDelta = movementTarget - currentPosition;
            float targetDistance = targetDelta.magnitude;
            float movementDistance = movementSpeed * Time.deltaTime;
            if (targetDistance <= movementDistance || targetDistance <= Mathf.Epsilon)
            {
                transform.position = new Vector3(
                    movementTarget.x,
                    movementTarget.y,
                    transform.position.z);
                movementDirection = Vector2.zero;
                hasMovementTarget = false;
            }
            else
            {
                movementDirection = targetDelta / targetDistance;
                transform.position += (Vector3)(movementDirection * movementDistance);
            }
        }
        else
        {
            transform.position += (Vector3)(intendedDirection * movementSpeed * Time.deltaTime);
        }

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
        hasMovementTarget = false;
        float radians = direction * Mathf.Deg2Rad;
        movementDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

        UpdateFacingFromDirection(movementDirection);
    }

    protected virtual bool ShouldAvoidResourceObjects()
    {
        return false;
    }

    private bool TryFindClearResourceAvoidanceDirection(
        MapManager mapManager,
        Vector2 currentPosition,
        Vector2 originalDirection,
        float checkDistance,
        out Vector2 avoidanceDirection)
    {
        const int directionAttempts = 16;
        for (int attempt = 0; attempt < directionAttempts; attempt++)
        {
            float directionAngle = Random.Range(0f, 360f);
            float radians = directionAngle * Mathf.Deg2Rad;
            Vector2 candidateDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector3 candidateEnd = currentPosition + candidateDirection * checkDistance;
            if (!mapManager.TryGetResourceObjectOnPath(
                    currentPosition,
                    candidateEnd,
                    out _))
            {
                avoidanceDirection = candidateDirection;
                UpdateFacingFromDirection(candidateDirection);
                return true;
            }
        }

        avoidanceDirection = originalDirection;
        return false;
    }

    private void UpdateFacingFromDirection(Vector2 direction)
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
        hasMovementTarget = false;
        movementDirection = Vector2.zero;
    }

    protected void MoveTo(Vector3 targetPosition)
    {
        movementTarget = new Vector2(targetPosition.x, targetPosition.y);
        hasMovementTarget = true;

        Vector2 direction = movementTarget - (Vector2)transform.position;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            movementDirection = Vector2.zero;
            hasMovementTarget = false;
            return;
        }

        movementDirection = direction.normalized;
        UpdateFacingFromDirection(movementDirection);
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
