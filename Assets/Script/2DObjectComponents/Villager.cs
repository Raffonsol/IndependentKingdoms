using UnityEngine;

public class Villager : Mover
{
    [SerializeField, Min(0.1f)] private float idleBehaviorDuration = 30f;
    [SerializeField, Min(0f)] private float roadDetectionDistance = 1.5f;
    [SerializeField, Min(0f)] private float roadSearchDistance = 6f;
    [SerializeField, Min(0f)] private float runningSpeed = 4f;
    [SerializeField] private VillagerBehaviorStage currentBehavior;
    [SerializeField, Min(0f)] private float behaviorTimer;

    public Person Person { get; private set; }
    public Transform HoverAnchor { get; private set; }

    private bool isInMovementPhase;
    private bool isRoadMovementPhase;
    private float movementPhaseTimer;
    private bool loggedMissingMapManager;

    public VillagerBehaviorStage CurrentBehavior
    {
        get { return currentBehavior; }
    }

    public float BehaviorTimer
    {
        get { return behaviorTimer; }
    }

    public bool IsLookingForStreet { get; private set; }

    protected override bool ShouldAvoidResourceObjects()
    {
        return currentBehavior != null && currentBehavior.mode == VillagerBehaviorMode.Idle;
    }

    public void ConfigureIdentity(Person person, Transform hoverAnchor)
    {
        if (person == null)
        {
            Debug.LogError("Cannot configure a villager without Person data.", this);
            return;
        }

        if (hoverAnchor == null)
        {
            Debug.LogError("Cannot configure a villager without a hover anchor.", this);
            return;
        }

        Person = person;
        HoverAnchor = hoverAnchor;
    }

    private void Start()
    {
        VillagerBehaviorMode initialMode = currentBehavior == null
            ? VillagerBehaviorMode.Idle
            : currentBehavior.mode;
        float initialDuration = currentBehavior == null
            ? idleBehaviorDuration
            : currentBehavior.duration;
        EnterBehavior(initialMode, initialDuration);
    }

    protected override void Update()
    {
        if (currentBehavior == null)
        {
            EnterBehavior(VillagerBehaviorMode.Idle, idleBehaviorDuration);
        }

        behaviorTimer += Time.deltaTime;
        if (behaviorTimer >= currentBehavior.duration)
        {
            EnterBehavior(DecideNextBehavior(), idleBehaviorDuration);
        }

        UpdateRoadBehavior();
        base.Update();
    }

    private void EnterBehavior(VillagerBehaviorMode mode, float duration)
    {
        currentBehavior = new VillagerBehaviorStage
        {
            mode = mode,
            duration = Mathf.Max(0.1f, duration),
            isPrimary = true
        };
        behaviorTimer = 0f;
        isInMovementPhase = false;
        movementPhaseTimer = 0f;

        if (mode == VillagerBehaviorMode.Idle)
        {
            BeginMovementPhase();
        }
        else
        {
            isRoadMovementPhase = false;
            StopMoving();
        }
    }

    private void MoveAround()
    {
        movementPhaseTimer -= Time.deltaTime;
        if (movementPhaseTimer > 0f)
        {
            return;
        }

        if (isInMovementPhase)
        {
            StopMoving();
            isInMovementPhase = false;
            movementPhaseTimer = Random.Range(IdleDurationRange.x, IdleDurationRange.y);
        }
        else
        {
            BeginMovementPhase();
        }
    }

    private void UpdateRoadBehavior()
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager == null)
        {
            if (!loggedMissingMapManager)
            {
                Debug.LogError(
                    "Villager road behavior requires an active MapManager.",
                    this);
                loggedMissingMapManager = true;
            }

            IsLookingForStreet = true;
            isInMovementPhase = false;
            isRoadMovementPhase = false;
            movementPhaseTimer = 0f;
            StopMoving();
            return;
        }

        loggedMissingMapManager = false;
        Vector3 position = transform.position;
        bool foundRoad = mapManager.TryGetClosestRoadPoint(
            position,
            out Vector3 closestRoadPoint,
            out float roadDistance);
        if (foundRoad && roadDistance <= roadDetectionDistance)
        {
            if (IsLookingForStreet)
            {
                isInMovementPhase = false;
                isRoadMovementPhase = false;
                movementPhaseTimer = 0f;
                StopMoving();
            }

            IsLookingForStreet = false;
            SetMovementSpeed(DefaultMovementSpeed);
            if (currentBehavior.mode == VillagerBehaviorMode.Idle)
            {
                if (!isRoadMovementPhase)
                {
                    isInMovementPhase = false;
                    movementPhaseTimer = 0f;
                    StopMoving();
                    isRoadMovementPhase = true;
                }

                MoveAroundOnRoad(mapManager);
            }
            else
            {
                isInMovementPhase = false;
                isRoadMovementPhase = false;
                StopMoving();
            }

            return;
        }

        if (!IsLookingForStreet)
        {
            isInMovementPhase = false;
            isRoadMovementPhase = false;
            movementPhaseTimer = 0f;
            StopMoving();
        }

        IsLookingForStreet = true;
        if (foundRoad && roadDistance <= roadSearchDistance)
        {
            SetMovementSpeed(DefaultMovementSpeed);
            isInMovementPhase = false;
            isRoadMovementPhase = false;
            movementPhaseTimer = 0f;
            MoveTo(closestRoadPoint);
            return;
        }

        SetMovementSpeed(runningSpeed);
        isRoadMovementPhase = false;
        MoveAround();
    }

    private void MoveAroundOnRoad(MapManager mapManager)
    {
        movementPhaseTimer -= Time.deltaTime;
        if (movementPhaseTimer > 0f)
        {
            return;
        }

        if (isInMovementPhase)
        {
            StopMoving();
            isInMovementPhase = false;
            movementPhaseTimer = Random.Range(IdleDurationRange.x, IdleDurationRange.y);
            return;
        }

        float desiredTravelDistance = MovementSpeed
            * Random.Range(MovementDurationRange.x, MovementDurationRange.y);
        if (!mapManager.TryGetRoadWanderTarget(
                transform.position,
                desiredTravelDistance,
                out Vector3 target))
        {
            StopMoving();
            isRoadMovementPhase = true;
            movementPhaseTimer = Random.Range(IdleDurationRange.x, IdleDurationRange.y);
            return;
        }

        float travelDistance = Vector2.Distance(transform.position, target);
        if (travelDistance <= 0.01f)
        {
            StopMoving();
            isRoadMovementPhase = true;
            movementPhaseTimer = Random.Range(IdleDurationRange.x, IdleDurationRange.y);
            return;
        }

        MoveTo(target);
        isInMovementPhase = true;
        isRoadMovementPhase = true;
        movementPhaseTimer = travelDistance / Mathf.Max(MovementSpeed, 0.01f);
    }

    private void BeginMovementPhase()
    {
        Move(Random.Range(0f, 360f));
        isInMovementPhase = true;
        isRoadMovementPhase = false;
        movementPhaseTimer = Random.Range(MovementDurationRange.x, MovementDurationRange.y);
    }

    private VillagerBehaviorMode DecideNextBehavior()
    {
        return VillagerBehaviorMode.Idle;
    }
}
