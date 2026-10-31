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
    private HouseDefinition[] houseDefinitions;
    private Sprite[] houseBuildStageOneSprites;
    private Sprite[] houseBuildStageTwoSprites;
    private Color[] houseBuildStageOneColors;
    private Color[] houseBuildStageTwoColors;
    private HousePlotRecord housePlot;
    private GameObject claimedSpaceObject;
    private HouseVisual houseVisual;
    private HouseDefinition selectedHouseDefinition;
    private ResourceObject gatheringResource;
    private Transform rightArm;
    private VillagerHandMotion handMotion;
    private readonly List<VillagerInventoryEntry> inventory = new List<VillagerInventoryEntry>();
    private readonly List<VillagerResourceEntry> resourceInventory = new List<VillagerResourceEntry>();
    private VillagerWeaponType equippedWeapon;
    private float movementPhaseTimer;
    private float gatheringTimer;
    private float constructionTimer;
    private float hammeringTimer;
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

    public IReadOnlyList<VillagerResourceEntry> ResourceInventory
    {
        get { return resourceInventory; }
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
        houseDefinitions = gameData.houseDefinitions;
        houseBuildStageOneSprites = gameData.houseBuildStageOneSprites;
        houseBuildStageTwoSprites = gameData.houseBuildStageTwoSprites;
        houseBuildStageOneColors = gameData.houseBuildStageOneColors;
        houseBuildStageTwoColors = gameData.houseBuildStageTwoColors;
        if (settings == null
            || houseSettings == null
            || resourceSettings == null
            || houseDefinitions == null
            || houseDefinitions.Length == 0
            || houseBuildStageOneSprites == null
            || houseBuildStageOneSprites.Length == 0
            || houseBuildStageOneColors == null
            || houseBuildStageOneColors.Length == 0
            || houseBuildStageTwoSprites == null
            || houseBuildStageTwoSprites.Length == 0
            || houseBuildStageTwoColors == null
            || houseBuildStageTwoColors.Length == 0)
        {
            Debug.LogError(
                "Villager settings, house settings, and house definitions could not be loaded from GameData.",
                this);
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
            case VillagerHousingState.DeliveringHouseResources:
                UpdateDeliveringHouseResources();
                break;
            case VillagerHousingState.BuildingHouseStageOne:
                UpdateBuildingHouseStageOne();
                break;
            case VillagerHousingState.HouseConstructionBreak:
                UpdateHouseConstructionBreak();
                break;
            case VillagerHousingState.BuildingHouseStageTwo:
                UpdateBuildingHouseStageTwo();
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

        houseVisual = claimedSpaceObject.GetComponent<HouseVisual>();
        if (houseVisual == null)
        {
            houseVisual = claimedSpaceObject.AddComponent<HouseVisual>();
        }

        HousingState = VillagerHousingState.GatheringHouseResources;
        EnterBehavior(VillagerBehaviorMode.Work, settings.idleBehaviorDuration);
    }

    private void UpdateGatheringHouseResources()
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager == null)
        {
            StopMoving();
            return;
        }

        if (HasHouseResources())
        {
            ReleaseGatheringResource(mapManager);
            HousingState = VillagerHousingState.DeliveringHouseResources;
            EnterBehavior(VillagerBehaviorMode.Work, settings.idleBehaviorDuration);
            return;
        }

        ResourceCost missingCost = FindMissingHouseCost();
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
            if (!AddResourceInventory(missingCost.resourceType, gatheredAmount))
            {
                Debug.LogError(
                    "Could not add gathered " + missingCost.resourceType + " to the villager's inventory.",
                    this);
            }
        }
        else
        {
            ReleaseGatheringResource(mapManager);
        }

        gatheringTimer = settings.gatheringPauseDurationSeconds;
    }

    private void UpdateDeliveringHouseResources()
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager == null || housePlot == null)
        {
            StopMoving();
            return;
        }

        Vector3 workPosition = GetHouseWorkPosition(mapManager);
        if (Vector2.Distance(transform.position, workPosition) > 0.1f)
        {
            MoveTo(workPosition, 0.1f);
            return;
        }

        StopMoving();
        if (!TryRemoveHouseCosts())
        {
            Debug.LogError("Villager reached the house plot without carrying its required materials.", this);
            HousingState = VillagerHousingState.GatheringHouseResources;
            return;
        }

        housePlot.storedMaterials = CopyHouseCosts();
        selectedHouseDefinition = houseDefinitions[Random.Range(0, houseDefinitions.Length)];
        float visualWidth = GetHouseVisualWidth(mapManager);
        houseVisual.ShowConstructionStage(
            houseBuildStageOneSprites,
            houseBuildStageOneColors,
            visualWidth,
            selectedHouseDefinition.visualOffset);
        HousingState = VillagerHousingState.BuildingHouseStageOne;
        constructionTimer = 0f;
        hammeringTimer = 0f;
        mapManager.NotifyHouseConstructionStarted(this);
        EnterBehavior(VillagerBehaviorMode.Building, houseSettings.firstConstructionDurationSeconds);
    }

    private void UpdateBuildingHouseStageOne()
    {
        if (!UpdateHouseConstruction(houseSettings.firstConstructionDurationSeconds))
        {
            return;
        }

        MapManager mapManager = MapManager.Instance;
        if (mapManager == null || housePlot == null)
        {
            return;
        }

        houseVisual.ShowConstructionStage(
            houseBuildStageTwoSprites,
            houseBuildStageTwoColors,
            GetHouseVisualWidth(mapManager),
            selectedHouseDefinition.visualOffset);
        HousingState = VillagerHousingState.HouseConstructionBreak;
        constructionTimer = 0f;
        hammeringTimer = 0f;
        EnterBehavior(VillagerBehaviorMode.Idle, houseSettings.constructionBreakDurationSeconds);
    }

    private void UpdateHouseConstructionBreak()
    {
        StopMoving();
        constructionTimer += Time.deltaTime;
        if (constructionTimer < houseSettings.constructionBreakDurationSeconds)
        {
            return;
        }

        HousingState = VillagerHousingState.BuildingHouseStageTwo;
        constructionTimer = 0f;
        hammeringTimer = 0f;
        EnterBehavior(VillagerBehaviorMode.Building, houseSettings.secondConstructionDurationSeconds);
    }

    private void UpdateBuildingHouseStageTwo()
    {
        if (!UpdateHouseConstruction(houseSettings.secondConstructionDurationSeconds))
        {
            return;
        }

        if (houseVisual == null || selectedHouseDefinition == null || housePlot == null)
        {
            Debug.LogError("Cannot finish house construction because its visual or definition is missing.", this);
            return;
        }

        MapManager mapManager = MapManager.Instance;
        float visualWidth = mapManager != null
            ? GetHouseVisualWidth(mapManager)
            : housePlot.dimensions.x * selectedHouseDefinition.visualScale;
        houseVisual.ShowFinishedHouse(selectedHouseDefinition, visualWidth);
        HousingState = VillagerHousingState.Housed;
        EnterBehavior(VillagerBehaviorMode.Idle, settings.idleBehaviorDuration);
        StopMoving();
    }

    private bool UpdateHouseConstruction(float duration)
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager == null || housePlot == null)
        {
            StopMoving();
            return false;
        }

        Vector3 workPosition = GetHouseWorkPosition(mapManager);
        if (Vector2.Distance(transform.position, workPosition) > 0.1f)
        {
            MoveTo(workPosition, 0.1f);
            return false;
        }

        StopMoving();
        constructionTimer += Time.deltaTime;
        hammeringTimer -= Time.deltaTime;
        if (hammeringTimer <= 0f)
        {
            if (handMotion == null && rightArm != null)
            {
                ConfigureGatheringAnimation(rightArm);
            }

            if (handMotion != null)
            {
                handMotion.PlayHit(
                    houseSettings.hammerHitPositions,
                    houseSettings.hammerMotionDurationSeconds);
            }

            hammeringTimer = houseSettings.hammerPauseDurationSeconds;
        }

        return constructionTimer >= duration;
    }

    private bool HasHouseResources()
    {
        return FindMissingHouseCost() == null;
    }

    private ResourceCost FindMissingHouseCost()
    {
        foreach (ResourceCost cost in houseSettings.baseHouseCosts)
        {
            if (cost != null
                && GetResourceInventoryAmount(cost.resourceType) < cost.amount)
            {
                return cost;
            }
        }

        return null;
    }

    private bool AddResourceInventory(ResourceType resourceType, int amount)
    {
        if (!System.Enum.IsDefined(typeof(ResourceType), resourceType) || amount <= 0)
        {
            return false;
        }

        foreach (VillagerResourceEntry entry in resourceInventory)
        {
            if (entry.resourceType == resourceType)
            {
                entry.amount += amount;
                return true;
            }
        }

        resourceInventory.Add(new VillagerResourceEntry
        {
            resourceType = resourceType,
            amount = amount
        });
        return true;
    }

    private int GetResourceInventoryAmount(ResourceType resourceType)
    {
        foreach (VillagerResourceEntry entry in resourceInventory)
        {
            if (entry.resourceType == resourceType)
            {
                return entry.amount;
            }
        }

        return 0;
    }

    private bool TryRemoveHouseCosts()
    {
        if (!HasHouseResources())
        {
            return false;
        }

        foreach (ResourceCost cost in houseSettings.baseHouseCosts)
        {
            for (int index = 0; index < resourceInventory.Count; index++)
            {
                VillagerResourceEntry entry = resourceInventory[index];
                if (entry.resourceType != cost.resourceType)
                {
                    continue;
                }

                entry.amount -= cost.amount;
                if (entry.amount == 0)
                {
                    resourceInventory.RemoveAt(index);
                }

                break;
            }
        }

        return true;
    }

    private ResourceCost[] CopyHouseCosts()
    {
        ResourceCost[] storedMaterials = new ResourceCost[houseSettings.baseHouseCosts.Length];
        for (int index = 0; index < houseSettings.baseHouseCosts.Length; index++)
        {
            ResourceCost cost = houseSettings.baseHouseCosts[index];
            storedMaterials[index] = new ResourceCost
            {
                resourceType = cost.resourceType,
                amount = cost.amount
            };
        }

        return storedMaterials;
    }

    private Vector3 GetHouseWorkPosition(MapManager mapManager)
    {
        float gridCellSize = mapManager.GridCellSize;
        Vector3 position = housePlot.center;
        position.y -= housePlot.dimensions.y * gridCellSize * 0.5f + gridCellSize * 0.5f;
        return position;
    }

    private float GetHouseVisualWidth(MapManager mapManager)
    {
        return housePlot.dimensions.x
            * mapManager.GridCellSize
            * selectedHouseDefinition.visualScale;
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
