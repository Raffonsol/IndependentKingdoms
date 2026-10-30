using System.Collections.Generic;
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
    private ResourceSettings resourceSettings;
    private HousePlotRecord housePlot;
    private GameObject claimedSpaceObject;
    private ResourceObject gatheringResource;
    private Transform rightArm;
    private VillagerHandMotion handMotion;
    private readonly List<VillagerInventoryEntry> inventory = new List<VillagerInventoryEntry>();
    private VillagerWeaponType equippedWeapon;
    private float movementPhaseTimer;
    private float gatheringTimer;
    private float constructionTimer;
    private float roadReactionTimer;
    private float plotReactionTimer;
    private float resourceReactionTimer;
    private int observedRoadRevision = -1;
    private Vector3 roadTargetPosition;
    private bool isInMovementPhase;
    private bool isWaitingToClaimPlot;
    private bool isWaitingForRoadReaction;
    private bool isWaitingToApproachResource;
    private bool hasRoadTarget;
    private bool loggedMissingMapManager;
    private bool loggedMissingPlayer;

    public IReadOnlyList<VillagerInventoryEntry> Inventory
    {
        get { return inventory; }
    }

    public VillagerWeaponType EquippedWeapon
    {
        get { return equippedWeapon; }
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
        resourceSettings = gameData.resourceSettings;
        if (settings == null || houseSettings == null || resourceSettings == null)
        {
            Debug.LogError("Villager, house, and resource settings could not be loaded from GameData.", this);
            enabled = false;
            return;
        }

        HousingState = VillagerHousingState.FindingRoad;
        MapManager mapManager = MapManager.Instance;
        if (mapManager != null)
        {
            observedRoadRevision = mapManager.RoadRevision;
        }

        EnterBehavior(VillagerBehaviorMode.Wander, settings.idleBehaviorDuration);
    }

    public bool AddInventoryItem(VillagerItemType itemType, int amount)
    {
        if (!System.Enum.IsDefined(typeof(VillagerItemType), itemType) || amount <= 0)
        {
            return false;
        }

        foreach (VillagerInventoryEntry entry in inventory)
        {
            if (entry.itemType == itemType)
            {
                entry.amount += amount;
                return true;
            }
        }

        inventory.Add(new VillagerInventoryEntry { itemType = itemType, amount = amount });
        return true;
    }

    public int GetInventoryItemCount(VillagerItemType itemType)
    {
        foreach (VillagerInventoryEntry entry in inventory)
        {
            if (entry.itemType == itemType)
            {
                return entry.amount;
            }
        }

        return 0;
    }

    public bool TryRemoveInventoryItem(VillagerItemType itemType, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        for (int index = 0; index < inventory.Count; index++)
        {
            VillagerInventoryEntry entry = inventory[index];
            if (entry.itemType != itemType || entry.amount < amount)
            {
                continue;
            }

            entry.amount -= amount;
            if (entry.amount == 0)
            {
                inventory.RemoveAt(index);
                if (itemType == VillagerItemType.Axe)
                {
                    equippedWeapon = VillagerWeaponType.None;
                }
            }

            return true;
        }

        return false;
    }

    public bool EquipWeapon(VillagerWeaponType weaponType)
    {
        if (weaponType == VillagerWeaponType.None)
        {
            equippedWeapon = VillagerWeaponType.None;
            return true;
        }

        VillagerItemType requiredItem;
        if (weaponType == VillagerWeaponType.Axe)
        {
            requiredItem = VillagerItemType.Axe;
        }
        else
        {
            return false;
        }

        foreach (VillagerInventoryEntry entry in inventory)
        {
            if (entry.itemType == requiredItem && entry.amount > 0)
            {
                equippedWeapon = weaponType;
                return true;
            }
        }

        return false;
    }

    public void ConfigureGatheringAnimation(Transform villagerRightArm)
    {
        if (villagerRightArm == null)
        {
            Debug.LogError("Cannot configure gathering animation without the villager's right arm.", this);
            return;
        }

        rightArm = villagerRightArm;
        handMotion = GetComponent<VillagerHandMotion>();
        if (handMotion == null)
        {
            handMotion = gameObject.AddComponent<VillagerHandMotion>();
        }

        handMotion.Configure(rightArm);
    }

    protected override void Update()
    {
        if (settings == null || houseSettings == null || resourceSettings == null)
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
        if (observedRoadRevision != mapManager.RoadRevision)
        {
            observedRoadRevision = mapManager.RoadRevision;
            roadReactionTimer = GetReactionTime();
            isWaitingForRoadReaction = true;
        }

        if (isWaitingForRoadReaction)
        {
            roadReactionTimer = Mathf.Max(0f, roadReactionTimer - Time.deltaTime);
            if (roadReactionTimer > 0f)
            {
                return;
            }

            isWaitingForRoadReaction = false;
            hasRoadTarget = false;
        }

        bool foundRoad = mapManager.TryGetClosestRoadPoint(
            transform.position,
            out Vector3 closestRoadPoint,
            out float roadDistance);
        if (foundRoad && roadDistance <= settings.roadDetectionDistance)
        {
            if (!hasRoadTarget)
            {
                roadTargetPosition = closestRoadPoint;
                hasRoadTarget = true;
            }

            if (!isWaitingToClaimPlot)
            {
                isWaitingToClaimPlot = true;
                plotReactionTimer = GetReactionTime();
                IsLookingForStreet = false;
            }

            if (plotReactionTimer > 0f)
            {
                plotReactionTimer = Mathf.Max(0f, plotReactionTimer - Time.deltaTime);
                StopMoving();
                return;
            }

            isWaitingToClaimPlot = false;
            IsLookingForStreet = false;
            StopMoving();
            HousingState = VillagerHousingState.ClaimingHousePlot;
            EnterBehavior(VillagerBehaviorMode.Work, settings.idleBehaviorDuration);
            return;
        }

        isWaitingToClaimPlot = false;
        IsLookingForStreet = true;
        if (foundRoad)
        {
            if (!hasRoadTarget)
            {
                roadTargetPosition = closestRoadPoint;
                hasRoadTarget = true;
            }

            isInMovementPhase = false;
            SetMovementSpeed(Vector2.Distance(transform.position, roadTargetPosition) <= settings.roadSearchDistance
                ? DefaultMovementSpeed
                : settings.runningSpeed);
            MoveTo(roadTargetPosition);
            return;
        }

        hasRoadTarget = false;
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
        Player player = Player.Instance;
        MapManager mapManager = MapManager.Instance;
        if (player == null || mapManager == null)
        {
            StopMoving();
            return;
        }

        if (HasHouseResources(player))
        {
            if (!player.TrySpendResourceCosts(houseSettings.baseHouseCosts))
            {
                StopMoving();
                return;
            }

            ReleaseGatheringResource(mapManager);
            HousingState = VillagerHousingState.BuildingHouse;
            constructionTimer = 0f;
            EnterBehavior(VillagerBehaviorMode.Work, settings.idleBehaviorDuration);
            return;
        }

        ResourceCost missingCost = FindMissingHouseCost(player);
        if (missingCost == null)
        {
            StopMoving();
            return;
        }

        bool hasAxe = equippedWeapon == VillagerWeaponType.Axe;
        if (gatheringResource == null
            || !gatheringResource.CanGather(hasAxe)
            || gatheringResource.Type != missingCost.resourceType)
        {
            ReleaseGatheringResource(mapManager);
            if (!mapManager.TryGetClosestResourceObject(
                    this,
                    missingCost.resourceType,
                    transform.position,
                    hasAxe,
                    out gatheringResource))
            {
                StopMoving();
                return;
            }

            isWaitingToApproachResource = true;
            resourceReactionTimer = GetReactionTime();
        }

        if (isWaitingToApproachResource)
        {
            resourceReactionTimer = Mathf.Max(0f, resourceReactionTimer - Time.deltaTime);
            if (resourceReactionTimer > 0f)
            {
                StopMoving();
                return;
            }

            isWaitingToApproachResource = false;
        }

        bool usesOffsetInteractionCell = gatheringResource.InteractionOffset != Vector2Int.zero;
        Vector3 interactionPosition = usesOffsetInteractionCell
            ? mapManager.GetGridCellBounds(
                gatheringResource.GridCell + gatheringResource.InteractionOffset).center
            : gatheringResource.transform.position;
        float interactionDistance = usesOffsetInteractionCell
            ? 0.1f
            : settings.gatheringDistance;
        float distance = Vector2.Distance(transform.position, interactionPosition);
        if (distance > interactionDistance)
        {
            gatheringTimer = 0f;
            MoveTo(
                interactionPosition,
                usesOffsetInteractionCell ? interactionDistance : settings.gatheringDistance);
            return;
        }

        StopMoving();
        gatheringTimer -= Time.deltaTime;
        if (gatheringTimer > 0f)
        {
            return;
        }

        if (handMotion == null && rightArm != null)
        {
            ConfigureGatheringAnimation(rightArm);
        }

        if (handMotion != null)
        {
            handMotion.PlayHit(settings.handHitPositions, settings.handMotionDurationSeconds);
        }

        gatheringResource.PlayShake(
            resourceSettings.shakeDurationSeconds,
            resourceSettings.shakeMagnitude,
            resourceSettings.shakeFrequency);

        if (mapManager.TryGatherResource(this, gatheringResource, 1, hasAxe, out int gatheredAmount))
        {
            player.TryAddResourceAmount(missingCost.resourceType, gatheredAmount);
        }
        else
        {
            ReleaseGatheringResource(mapManager);
        }

        gatheringTimer = settings.gatheringPauseDurationSeconds;
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

    private float GetReactionTime()
    {
        return Person != null ? Mathf.Max(0f, Person.reactionTime) : 0f;
    }

    private void ReleaseGatheringResource(MapManager mapManager)
    {
        if (mapManager != null)
        {
            mapManager.ReleaseResourceReservation(gatheringResource, this);
        }

        gatheringResource = null;
        isWaitingToApproachResource = false;
        resourceReactionTimer = 0f;
    }

    public void CancelHousePlotForRoad()
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager != null)
        {
            mapManager.ReleaseHousePlot(this);
            ReleaseGatheringResource(mapManager);
        }

        if (claimedSpaceObject != null)
        {
            Destroy(claimedSpaceObject);
            claimedSpaceObject = null;
        }

        housePlot = null;
        HousingState = VillagerHousingState.FindingRoad;
        IsLookingForStreet = false;
        isWaitingToClaimPlot = false;
        plotReactionTimer = 0f;
        EnterBehavior(VillagerBehaviorMode.Wander, settings.idleBehaviorDuration);
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
        if (mapManager != null)
        {
            mapManager.ReleaseHousePlot(this);
            mapManager.ReleaseResourceReservation(gatheringResource, this);
        }

        if (HousingState != VillagerHousingState.Housed && claimedSpaceObject != null)
        {
            Destroy(claimedSpaceObject);
        }
    }
}
