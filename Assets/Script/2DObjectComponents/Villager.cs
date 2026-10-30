using UnityEngine;

public class Villager : Mover
{
    [SerializeField] private VillagerBehaviorStage currentBehavior;
    [SerializeField, Min(0f)] private float behaviorTimer;

    public Person Person { get; private set; }
    public Transform HoverAnchor { get; private set; }
    public VillagerHousingState HousingState { get; private set; }
    public VillagerBehaviorStage CurrentBehavior
    {
        get { return currentBehavior; }
    }

    public float BehaviorTimer
    {
        get { return behaviorTimer; }
    }

    public bool IsLookingForStreet { get; private set; }

    private VillagerSettings settings;
    private HouseSettings houseSettings;
    private HousePlotRecord housePlot;
    private GameObject claimedSpaceObject;
    private ResourceObject gatheringResource;
    private float movementPhaseTimer;
    private float gatheringTimer;
    private float constructionTimer;
    private bool isInMovementPhase;
    private bool loggedMissingMapManager;
    private bool loggedMissingPlayer;

    protected override bool ShouldAvoidResourceObjects()
    {
        return false;
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
        GameData gameData = GameData.Instance;
        settings = gameData.villagerSettings;
        houseSettings = gameData.houseSettings;
        if (settings == null || houseSettings == null)
        {
            Debug.LogError("Villager and house settings could not be loaded from GameData.", this);
            enabled = false;
            return;
        }

        HousingState = VillagerHousingState.FindingRoad;
        EnterBehavior(VillagerBehaviorMode.Wander, settings.idleBehaviorDuration);
    }

    protected override void Update()
    {
        if (settings == null || houseSettings == null)
        {
            return;
        }

        behaviorTimer += Time.deltaTime;
        switch (HousingState)
        {
            case VillagerHousingState.FindingRoad:
                UpdateFindingRoad();
                break;
            case VillagerHousingState.ClaimingHousePlot:
                UpdateClaimingHousePlot();
                break;
            case VillagerHousingState.GatheringHouseResources:
                UpdateGatheringHouseResources();
                break;
            case VillagerHousingState.BuildingHouse:
                UpdateBuildingHouse();
                break;
            case VillagerHousingState.Housed:
                StopMoving();
                break;
        }

        base.Update();
    }

    private void UpdateFindingRoad()
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager == null)
        {
            if (!loggedMissingMapManager)
            {
                Debug.LogError("Villager housing requires an active MapManager.", this);
                loggedMissingMapManager = true;
            }

            StopMoving();
            return;
        }

        loggedMissingMapManager = false;
        bool foundRoad = mapManager.TryGetClosestRoadPoint(
            transform.position,
            out Vector3 closestRoadPoint,
            out float roadDistance);
        if (foundRoad && roadDistance <= settings.roadDetectionDistance)
        {
            IsLookingForStreet = false;
            StopMoving();
            HousingState = VillagerHousingState.ClaimingHousePlot;
            EnterBehavior(VillagerBehaviorMode.Work, settings.idleBehaviorDuration);
            return;
        }

        IsLookingForStreet = true;
        if (foundRoad)
        {
            isInMovementPhase = false;
            SetMovementSpeed(roadDistance <= settings.roadSearchDistance
                ? DefaultMovementSpeed
                : settings.runningSpeed);
            MoveTo(closestRoadPoint);
            return;
        }

        SetMovementSpeed(settings.runningSpeed);
        MoveAround();
    }

    private void UpdateClaimingHousePlot()
    {
        StopMoving();
        Player player = Player.Instance;
        MapManager mapManager = MapManager.Instance;
        if (player == null || mapManager == null)
        {
            if (!loggedMissingPlayer)
            {
                Debug.LogError("Villager house claiming requires active Player and MapManager components.", this);
                loggedMissingPlayer = true;
            }

            return;
        }

        loggedMissingPlayer = false;
        if (!player.HasClaimedSpacePrefab)
        {
            return;
        }

        if (!mapManager.TryClaimHousePlot(this, houseSettings.defaultHouseDimensions, out housePlot))
        {
            return;
        }

        claimedSpaceObject = player.PlaceClaimedSpace(housePlot);
        if (claimedSpaceObject == null)
        {
            mapManager.ReleaseHousePlot(this);
            housePlot = null;
            return;
        }

        HousingState = VillagerHousingState.GatheringHouseResources;
        EnterBehavior(VillagerBehaviorMode.Work, settings.idleBehaviorDuration);
    }

    private void UpdateGatheringHouseResources()
    {
        StopMoving();
        Player player = Player.Instance;
        MapManager mapManager = MapManager.Instance;
        if (player == null || mapManager == null)
        {
            return;
        }

        if (HasHouseResources(player))
        {
            if (!player.TrySpendResourceCosts(houseSettings.baseHouseCosts))
            {
                return;
            }

            HousingState = VillagerHousingState.BuildingHouse;
            constructionTimer = 0f;
            gatheringResource = null;
            EnterBehavior(VillagerBehaviorMode.Work, settings.idleBehaviorDuration);
            return;
        }

        ResourceCost missingCost = FindMissingHouseCost(player);
        if (missingCost == null)
        {
            return;
        }

        if (gatheringResource == null
            || gatheringResource.Amount <= 0
            || gatheringResource.Type != missingCost.resourceType)
        {
            if (!mapManager.TryGetClosestResourceObject(
                    missingCost.resourceType,
                    transform.position,
                    out gatheringResource))
            {
                StopMoving();
                return;
            }
        }

        float distance = Vector2.Distance(transform.position, gatheringResource.transform.position);
        if (distance > settings.gatheringDistance)
        {
            gatheringTimer = 0f;
            MoveTo(gatheringResource.transform.position);
            return;
        }

        StopMoving();
        gatheringTimer += Time.deltaTime;
        if (gatheringTimer < settings.gatheringIntervalSeconds)
        {
            return;
        }

        gatheringTimer = 0f;
        if (mapManager.TryGatherResource(gatheringResource, 1, out int gatheredAmount))
        {
            player.TryAddResourceAmount(missingCost.resourceType, gatheredAmount);
        }
        else
        {
            gatheringResource = null;
        }
    }

    private void UpdateBuildingHouse()
    {
        StopMoving();
        constructionTimer += Time.deltaTime;
        if (constructionTimer < houseSettings.constructionDurationSeconds)
        {
            return;
        }

        HousingState = VillagerHousingState.Housed;
        EnterBehavior(VillagerBehaviorMode.Idle, settings.idleBehaviorDuration);
        StopMoving();
    }

    private bool HasHouseResources(Player player)
    {
        return FindMissingHouseCost(player) == null;
    }

    private ResourceCost FindMissingHouseCost(Player player)
    {
        foreach (ResourceCost cost in houseSettings.baseHouseCosts)
        {
            if (cost != null
                && (!player.TryGetResourceAmount(cost.resourceType, out int amount)
                    || amount < cost.amount))
            {
                return cost;
            }
        }

        return null;
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
            Move(Random.Range(0f, 360f));
            isInMovementPhase = true;
            movementPhaseTimer = Random.Range(MovementDurationRange.x, MovementDurationRange.y);
        }
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
        StopMoving();
    }

    private void OnDestroy()
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager != null && HousingState != VillagerHousingState.Housed)
        {
            mapManager.ReleaseHousePlot(this);
        }

        if (HousingState != VillagerHousingState.Housed && claimedSpaceObject != null)
        {
            Destroy(claimedSpaceObject);
        }
    }
}
