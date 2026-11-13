using System.Collections.Generic;
using UnityEngine;

public class VillagerJobController : MonoBehaviour
{
    internal enum WorkplaceStage
    {
        None,
        GatheringMaterials,
        BuildingHouseStageOne,
        HouseConstructionBreak,
        BuildingHouseStageTwo,
        RenovatingAnnex,
        Ready
    }

    private Villager villager;
    private VillagerSettings settings;
    private ResourceSettings resourceSettings;
    private HouseSettings houseSettings;
    private JobType jobAssignment;
    private JobDefinition jobDefinition;
    private ResourceCost[] annexRenovationCosts;
    private HouseDefinition[] houseDefinitions;
    private Sprite[] houseBuildStageOneSprites;
    private Sprite[] houseBuildStageTwoSprites;
    private Color[] houseBuildStageOneColors;
    private Color[] houseBuildStageTwoColors;
    private HousePlotRecord workplacePlot;
    private GameObject workplaceHouse;
    private HouseDefinition workplaceHouseDefinition;
    private ResourceCost[] workplaceProjectCosts;
    private ResourceObject foragingResource;
    private ResourceType foragingResourceType;
    private HouseWorkplace requiredItemWorkplace;
    private HousePlotRecord requiredItemHousePlot;
    private readonly HashSet<ResourceObject> unreachableResources =
        new HashSet<ResourceObject>();
    private readonly Dictionary<ResourceType, int> jobInputStock =
        new Dictionary<ResourceType, int>();
    private bool hasPendingProductionProgress;
    private VillagerItemType pendingProductionItemType;
    private float pendingProductionProgressSeconds;
    private float pendingProductionDurationSeconds;
    private float foragingReactionTimer;
    private float foragingGatheringTimer;
    private float nextResourceSearchTime;
    private float workplaceConstructionTimer;
    private float workplaceHammeringTimer;
    private float workMotionTimer;
    private float nextRequiredItemSearchTime;
    private int pathfindingRevision = -1;
    private int availabilityRevision = -1;
    private bool isWaitingToApproachResource;
    private bool isForagingForJob;
    private bool isApproachingRequiredItem;
    private bool isWaitingForRequiredItem;
    private bool hasRequiredItemAvailable;
    private bool isSeparateWorkplace;
    private bool isBuildingNewWorkplaceHouse;
    private bool isWorkingAtHomeAnnexThisFrame;
    private WorkplaceStage workplaceStage;

    public JobType JobAssignment
    {
        get { return jobAssignment; }
    }

    internal GameObject WorkplaceHouse
    {
        get { return workplaceHouse; }
    }

    internal HousePlotRecord WorkplacePlot
    {
        get { return workplacePlot; }
    }

    internal HouseDefinition WorkplaceHouseDefinition
    {
        get { return workplaceHouseDefinition; }
    }

    internal bool IsSeparateWorkplace
    {
        get { return isSeparateWorkplace; }
    }

    internal bool IsBuildingNewWorkplaceHouse
    {
        get { return isBuildingNewWorkplaceHouse; }
    }

    internal string WorkplaceStageName
    {
        get { return workplaceStage.ToString(); }
    }

    internal float WorkplaceConstructionProgressSeconds
    {
        get { return workplaceConstructionTimer; }
    }

    internal float WorkplaceHammeringTimer
    {
        get { return workplaceHammeringTimer; }
    }

    public bool IsForagingForJob
    {
        get { return isForagingForJob && foragingResource != null; }
    }

    public bool IsWaitingForRequiredItem
    {
        get { return isWaitingForRequiredItem; }
    }

    public bool HasRequiredItemAvailable
    {
        get { return isWaitingForRequiredItem && hasRequiredItemAvailable; }
    }

    public bool IsPreparingWorkplace
    {
        get
        {
            return jobDefinition != null
                && jobDefinition.needsAnnex
                && workplaceStage != WorkplaceStage.Ready;
        }
    }

    public void Initialize(
        Villager owner,
        VillagerSettings villagerSettings,
        ResourceSettings resourcesSettings)
    {
        if (owner == null || villagerSettings == null || resourcesSettings == null)
        {
            Debug.LogError(
                "VillagerJobController requires an owner, villager settings, and resource settings.",
                this);
            return;
        }

        villager = owner;
        settings = villagerSettings;
        resourceSettings = resourcesSettings;
        GameData gameData = GameData.Instance;
        houseSettings = gameData.houseSettings;
        annexRenovationCosts = gameData.annexRenovationCosts;
        houseDefinitions = gameData.houseDefinitions;
        houseBuildStageOneSprites = gameData.houseBuildStageOneSprites;
        houseBuildStageTwoSprites = gameData.houseBuildStageTwoSprites;
        houseBuildStageOneColors = gameData.houseBuildStageOneColors;
        houseBuildStageTwoColors = gameData.houseBuildStageTwoColors;
        jobAssignment = JobType.None;
        jobDefinition = null;
    }

    internal GameSaveAmount[] CaptureJobInputStock()
    {
        List<GameSaveAmount> savedStock = new List<GameSaveAmount>();
        foreach (ResourceType resourceType in System.Enum.GetValues(typeof(ResourceType)))
        {
            int amount = GetJobInputStockAmount(resourceType);
            if (amount > 0)
            {
                savedStock.Add(new GameSaveAmount
                {
                    type = resourceType.ToString(),
                    amount = amount
                });
            }
        }

        return savedStock.ToArray();
    }

    internal bool TryCaptureProductionProgress(
        out VillagerItemType itemType,
        out float progressSeconds,
        out float durationSeconds)
    {
        itemType = default(VillagerItemType);
        progressSeconds = 0f;
        durationSeconds = 0f;
        if (hasPendingProductionProgress)
        {
            itemType = pendingProductionItemType;
            progressSeconds = pendingProductionProgressSeconds;
            durationSeconds = pendingProductionDurationSeconds;
            return true;
        }

        if (workplaceHouse == null)
        {
            return false;
        }

        HouseWorkplace workplace = workplaceHouse.GetComponent<HouseWorkplace>();
        if (workplace == null || !workplace.HasProductionProgress)
        {
            return false;
        }

        itemType = workplace.ProductionItemType;
        progressSeconds = workplace.ProductionProgressSeconds;
        durationSeconds = workplace.ProductionDurationSeconds;
        return true;
    }

    internal bool RestoreSavedJobState(GameSaveVillager savedState)
    {
        if (savedState == null || savedState.jobInputStock == null)
        {
            Debug.LogError("Cannot restore job work from missing save data.", this);
            return false;
        }

        jobInputStock.Clear();
        foreach (GameSaveAmount savedAmount in savedState.jobInputStock)
        {
            if (savedAmount == null
                || savedAmount.amount <= 0
                || !System.Enum.TryParse(savedAmount.type, out ResourceType resourceType)
                || !System.Enum.IsDefined(typeof(ResourceType), resourceType)
                || jobInputStock.ContainsKey(resourceType))
            {
                Debug.LogError("A saved villager contains invalid job input stock.", this);
                return false;
            }

            jobInputStock.Add(resourceType, savedAmount.amount);
        }

        hasPendingProductionProgress = false;
        if (!savedState.hasJobProductionProgress)
        {
            return true;
        }

        if (jobDefinition == null
            || jobDefinition.workType != JobWorkType.Production
            || !System.Enum.TryParse(
                savedState.jobProductionItemType,
                out VillagerItemType savedItemType)
            || !System.Enum.IsDefined(typeof(VillagerItemType), savedItemType)
            || savedItemType != jobDefinition.outputItemType
            || savedState.jobProductionDurationSeconds <= 0f
            || float.IsNaN(savedState.jobProductionDurationSeconds)
            || float.IsInfinity(savedState.jobProductionDurationSeconds)
            || savedState.jobProductionProgressSeconds < 0f
            || savedState.jobProductionProgressSeconds > savedState.jobProductionDurationSeconds
            || float.IsNaN(savedState.jobProductionProgressSeconds)
            || float.IsInfinity(savedState.jobProductionProgressSeconds))
        {
            Debug.LogError("A saved villager contains invalid production progress.", this);
            return false;
        }

        hasPendingProductionProgress = true;
        pendingProductionItemType = savedItemType;
        pendingProductionProgressSeconds = savedState.jobProductionProgressSeconds;
        pendingProductionDurationSeconds = savedState.jobProductionDurationSeconds;
        return true;
    }

    internal bool RestoreSavedWorkplace(
        HousePlotRecord plot,
        GameObject houseObject,
        HouseDefinition definition,
        GameSaveVillager savedState)
    {
        if (plot == null
            || houseObject == null
            || definition == null
            || savedState == null
            || jobDefinition == null
            || !jobDefinition.needsAnnex
            || !System.Enum.TryParse(
                savedState.jobWorkplaceStage,
                out WorkplaceStage restoredStage)
            || !System.Enum.IsDefined(typeof(WorkplaceStage), restoredStage)
            || restoredStage == WorkplaceStage.None
            || !IsFiniteNonnegative(savedState.jobWorkplaceConstructionProgressSeconds)
            || !IsFiniteNonnegative(savedState.jobWorkplaceHammeringTimer))
        {
            Debug.LogError("Cannot restore a job workplace from invalid save data.", this);
            return false;
        }

        HouseVisual houseVisual = houseObject.GetComponent<HouseVisual>();
        if (houseVisual == null)
        {
            Debug.LogError("A saved job workplace requires a HouseVisual component.", houseObject);
            return false;
        }

        workplaceHouse = houseObject;
        workplacePlot = plot;
        workplaceHouseDefinition = definition;
        workplaceStage = restoredStage;
        workplaceConstructionTimer = savedState.jobWorkplaceConstructionProgressSeconds;
        workplaceHammeringTimer = savedState.jobWorkplaceHammeringTimer;
        isSeparateWorkplace = savedState.jobWorkplaceIsSeparate;
        isBuildingNewWorkplaceHouse = savedState.jobWorkplaceBuildingNewHouse;
        workplaceProjectCosts = restoredStage == WorkplaceStage.GatheringMaterials
            ? isBuildingNewWorkplaceHouse
                ? CombineResourceCosts(houseSettings.baseHouseCosts, annexRenovationCosts)
                : annexRenovationCosts
            : null;

        float duration = 0f;
        VillagerBehaviorMode behavior = VillagerBehaviorMode.Idle;
        switch (restoredStage)
        {
            case WorkplaceStage.BuildingHouseStageOne:
                duration = houseSettings.firstConstructionDurationSeconds;
                behavior = VillagerBehaviorMode.Building;
                BeginConstructionWorkStep();
                break;
            case WorkplaceStage.HouseConstructionBreak:
                duration = houseSettings.constructionBreakDurationSeconds;
                break;
            case WorkplaceStage.BuildingHouseStageTwo:
                duration = houseSettings.secondConstructionDurationSeconds;
                behavior = VillagerBehaviorMode.Building;
                BeginConstructionWorkStep();
                break;
            case WorkplaceStage.RenovatingAnnex:
                duration = houseSettings.secondConstructionDurationSeconds;
                behavior = VillagerBehaviorMode.Building;
                BeginConstructionWorkStep();
                break;
            case WorkplaceStage.GatheringMaterials:
                villager.StopForagingMovement();
                break;
        }

        if (duration > 0f)
        {
            villager.SetJobBehavior(
                GetHomeAwareConstructionBehavior(behavior),
                Mathf.Max(0.1f, duration - workplaceConstructionTimer));
        }

        return true;
    }

    public bool AssignJob(JobType job)
    {
        JobDefinition assignedDefinition = null;
        if (job != JobType.None
            && (!GameData.Instance.CanAssignJob(job, out assignedDefinition)
                || (assignedDefinition.workType != JobWorkType.FieldGathering
                    && assignedDefinition.workType != JobWorkType.Production)))
        {
            return false;
        }

        if (jobAssignment == job)
        {
            return true;
        }

        StopForagingForJob();
        if (jobDefinition == null
            || assignedDefinition == null
            || !jobDefinition.needsAnnex
            || !assignedDefinition.needsAnnex)
        {
            ReleaseWorkplace();
        }

        jobAssignment = job;
        jobDefinition = assignedDefinition;
        workMotionTimer = 0f;
        ClearRequiredItemWaiting();
        return true;
    }

    internal void BeginOperationFrame()
    {
        isWorkingAtHomeAnnexThisFrame = false;
    }

    internal void EndOperationFrame()
    {
        bool isWorkingAtHomeAnnex = isWorkingAtHomeAnnexThisFrame;
        isWorkingAtHomeAnnexThisFrame = false;
        if (!isWorkingAtHomeAnnex && villager != null)
        {
            villager.LeaveHomeAnnexWorkPosition();
        }
    }

    public bool TryStartProductiveOperation()
    {
        if (villager == null || settings == null || jobDefinition == null)
        {
            return false;
        }

        if (villager.Fatigue >= settings.productiveFatigueThreshold)
        {
            StopForagingForJob();
            return false;
        }

        HouseWorkplace storage = GetProductiveHouseStorage();
        if (storage != null
            && (storage.IsFull
                || (jobDefinition.workType == JobWorkType.Production
                    && storage.RemainingCapacity < jobDefinition.outputAmount)))
        {
            StopForagingForJob();
            return false;
        }

        if (jobDefinition.needsAnnex)
        {
            UpdateWorkplacePreparation();
            if (workplaceStage != WorkplaceStage.Ready)
            {
                return true;
            }
        }

        if (!TryEnsureRequiredItemEquipped())
        {
            return isApproachingRequiredItem;
        }

        switch (jobDefinition.workType)
        {
            case JobWorkType.FieldGathering:
                if (!isForagingForJob)
                {
                    isForagingForJob = true;
                    ResetForagingSearch();
                }

                return UpdateForaging(true);
            case JobWorkType.Production:
                return UpdateProduction();
            default:
                return false;
        }
    }

    public void UpdateRequiredItemAvailability()
    {
        if (jobDefinition == null || villager == null)
        {
            return;
        }

        if (!jobDefinition.requiresEquippedItem
            || !jobDefinition.requiredEquippedItemType.HasValue)
        {
            ClearRequiredItemWaiting();
            return;
        }

        VillagerItemType requiredItemType = jobDefinition.requiredEquippedItemType.Value;
        if (villager.EquippedItem == requiredItemType
            || villager.GetInventoryItemCount(requiredItemType) > 0)
        {
            ClearRequiredItemWaiting();
            return;
        }

        if (requiredItemWorkplace != null || isApproachingRequiredItem)
        {
            isWaitingForRequiredItem = false;
            hasRequiredItemAvailable = false;
            return;
        }

        if (!isWaitingForRequiredItem)
        {
            isWaitingForRequiredItem = true;
            hasRequiredItemAvailable = false;
            nextRequiredItemSearchTime = Time.time;
        }

        if (Time.time < nextRequiredItemSearchTime)
        {
            return;
        }

        nextRequiredItemSearchTime =
            Time.time + jobDefinition.resourceSearchRetrySeconds;
        MapManager mapManager = MapManager.Instance;
        hasRequiredItemAvailable = mapManager != null
            && mapManager.HasAvailableHouseItem(villager, requiredItemType);
    }

    private bool TryEnsureRequiredItemEquipped()
    {
        if (!jobDefinition.requiresEquippedItem)
        {
            ClearRequiredItemWaiting();
            return true;
        }

        if (!jobDefinition.requiredEquippedItemType.HasValue)
        {
            Debug.LogError(
                "Job '" + jobDefinition.jobType
                + "' requires an item but has no required item type.",
                this);
            return false;
        }

        VillagerItemType requiredItemType = jobDefinition.requiredEquippedItemType.Value;
        if (villager.EquippedItem == requiredItemType)
        {
            ReleaseRequiredItemPickup();
            ClearRequiredItemWaiting();
            return true;
        }

        ReleaseForagingResource(MapManager.Instance);
        if (villager.GetInventoryItemCount(requiredItemType) > 0)
        {
            if (!villager.EquipItem(requiredItemType))
            {
                Debug.LogError(
                    "Could not equip required job item '" + requiredItemType + "'.",
                    villager);
                return false;
            }

            ReleaseRequiredItemPickup();
            ClearRequiredItemWaiting();
            return true;
        }

        MapManager mapManager = MapManager.Instance;
        if (mapManager == null)
        {
            ReleaseRequiredItemPickup();
            WaitForRequiredItem();
            return false;
        }

        if (requiredItemWorkplace == null)
        {
            ReleaseRequiredItemPickup();
            if (!hasRequiredItemAvailable && Time.time < nextRequiredItemSearchTime)
            {
                isWaitingForRequiredItem = true;
                return false;
            }

            if (!mapManager.TryReserveHouseItem(
                    villager,
                    requiredItemType,
                    out requiredItemWorkplace,
                    out requiredItemHousePlot))
            {
                WaitForRequiredItem();
                return false;
            }

            isWaitingForRequiredItem = false;
            hasRequiredItemAvailable = false;
        }

        if (!requiredItemWorkplace.isActiveAndEnabled
            || requiredItemHousePlot == null)
        {
            ReleaseRequiredItemPickup();
            WaitForRequiredItem();
            villager.StopForagingMovement();
            return false;
        }

        Vector3 entrancePosition =
            mapManager.GetHouseEntrancePosition(requiredItemHousePlot);
        if (isApproachingRequiredItem
            && villager.HasFailedPathRequestForAction
            && Vector2.Distance(villager.transform.position, entrancePosition) > 0.1f)
        {
            ReleaseRequiredItemPickup();
            WaitForRequiredItem();
            villager.StopForagingMovement();
            return false;
        }

        if (!villager.MoveToHouseWorkPosition(mapManager, requiredItemHousePlot))
        {
            isApproachingRequiredItem = true;
            return false;
        }

        if (!requiredItemWorkplace.TryWithdrawReservedItem(
                villager,
                out VillagerItemType withdrawnItemType))
        {
            ReleaseRequiredItemPickup();
            WaitForRequiredItem();
            return false;
        }

        HouseWorkplace sourceWorkplace = requiredItemWorkplace;
        ReleaseRequiredItemPickup();
        if (!villager.AddInventoryItem(withdrawnItemType, 1))
        {
            if (!sourceWorkplace.TryStoreItem(withdrawnItemType, 1))
            {
                Debug.LogError(
                    "Could not restore a required item after the villager could not add it to inventory.",
                    sourceWorkplace);
            }

            WaitForRequiredItem();
            Debug.LogError(
                "Could not add required job item '" + withdrawnItemType + "' to villager inventory.",
                villager);
            return false;
        }

        if (!villager.EquipItem(withdrawnItemType))
        {
            Debug.LogError(
                "Could not equip required job item '" + withdrawnItemType + "' after pickup.",
                villager);
            return false;
        }

        ClearRequiredItemWaiting();
        return true;
    }

    private void WaitForRequiredItem()
    {
        isWaitingForRequiredItem = true;
        hasRequiredItemAvailable = false;
        nextRequiredItemSearchTime =
            Time.time + jobDefinition.resourceSearchRetrySeconds;
    }

    private void ClearRequiredItemWaiting()
    {
        isWaitingForRequiredItem = false;
        hasRequiredItemAvailable = false;
        nextRequiredItemSearchTime = 0f;
    }

    private void ReleaseRequiredItemPickup()
    {
        if (requiredItemWorkplace != null)
        {
            requiredItemWorkplace.ReleaseStoredItemReservation(villager);
        }

        requiredItemWorkplace = null;
        requiredItemHousePlot = null;
        isApproachingRequiredItem = false;
    }

    private HouseWorkplace GetProductiveHouseStorage()
    {
        GameObject houseObject = workplaceHouse != null
            ? workplaceHouse
            : villager.HomeHouseObject;
        MapManager mapManager = MapManager.Instance;
        if (houseObject == null || mapManager == null)
        {
            return null;
        }

        return mapManager.GetOrCreateHouseWorkplace(houseObject);
    }

    private bool UpdateProduction()
    {
        if (!jobDefinition.needsAnnex && workplaceHouse == null)
        {
            workplaceHouse = villager.HomeHouseObject;
            workplacePlot = villager.HomeHousePlot;
            if (workplaceHouse == null || workplacePlot == null)
            {
                return false;
            }
        }

        MapManager mapManager = MapManager.Instance;
        if (mapManager == null || workplacePlot == null || workplaceHouse == null)
        {
            return false;
        }

        HouseWorkplace workplace = mapManager.GetOrCreateHouseWorkplace(workplaceHouse);
        if (workplace == null)
        {
            return false;
        }

        foreach (ResourceCost input in jobDefinition.inputCosts)
        {
            int missingAmount =
                input.amount - GetJobInputStockAmount(input.resourceType);
            if (missingAmount <= 0)
            {
                continue;
            }

            if (villager.TryBeginJobInputWithdrawal(input.resourceType, missingAmount))
            {
                return true;
            }

            villager.StopForagingMovement();
            return true;
        }

        if (!workplace.CanStoreItem(jobDefinition.outputItemType, jobDefinition.outputAmount))
        {
            return true;
        }

        HouseVisual houseVisual = workplaceHouse.GetComponent<HouseVisual>();
        if (workplaceHouse == villager.HomeHouseObject
            && workplacePlot == villager.HomeHousePlot)
        {
            if (!villager.MoveToHomeAnnexWorkPosition(mapManager, workplacePlot, houseVisual))
            {
                return true;
            }
        }
        else if (!villager.MoveToHouseWorkPosition(mapManager, workplacePlot))
        {
            return true;
        }

        if (villager.IsAtHomeAnnexWorkPosition)
        {
            isWorkingAtHomeAnnexThisFrame = true;
        }

        if (workMotionTimer > 0f)
        {
            workMotionTimer -= Time.deltaTime;
        }

        if (workMotionTimer <= 0f)
        {
            villager.PlayHomeWorkMotion(
                jobDefinition.handHitPositions,
                jobDefinition.handMotionDurationSeconds);
            workMotionTimer = jobDefinition.workMotionIntervalSeconds;
        }

        if (hasPendingProductionProgress)
        {
            if (pendingProductionItemType != jobDefinition.outputItemType
                || !workplace.RestoreProductionProgress(
                    pendingProductionItemType,
                    pendingProductionDurationSeconds,
                    pendingProductionProgressSeconds))
            {
                Debug.LogError("Could not resume the saved production operation.", villager);
                return true;
            }

            hasPendingProductionProgress = false;
        }

        workplace.AdvanceProduction(
            jobDefinition.outputItemType,
            jobDefinition.productionDurationSeconds,
            Time.deltaTime);
        if (!workplace.IsProductionComplete(jobDefinition.outputItemType))
        {
            return true;
        }

        if (!workplace.CanStoreItem(
                jobDefinition.outputItemType,
                jobDefinition.outputAmount))
        {
            return true;
        }

        if (!villager.TryRemoveResourceCostsForJob(jobDefinition.inputCosts))
        {
            Debug.LogError("Blacksmith could not consume the configured production inputs.", villager);
            return true;
        }

        foreach (ResourceCost input in jobDefinition.inputCosts)
        {
            jobInputStock[input.resourceType] -= input.amount;
        }

        if (!workplace.TryStoreItem(jobDefinition.outputItemType, jobDefinition.outputAmount))
        {
            Debug.LogError("Could not store the produced item at the workplace.", workplaceHouse);
            return true;
        }

        workplace.CompleteProduction(jobDefinition.outputItemType);
        return true;
    }

    internal void OnJobInputsWithdrawn(ResourceType resourceType, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (jobInputStock.TryGetValue(resourceType, out int currentAmount))
        {
            jobInputStock[resourceType] = currentAmount + amount;
        }
        else
        {
            jobInputStock.Add(resourceType, amount);
        }
    }

    private int GetJobInputStockAmount(ResourceType resourceType)
    {
        return jobInputStock.TryGetValue(resourceType, out int amount) ? amount : 0;
    }

    private void UpdateWorkplacePreparation()
    {
        if (workplaceStage == WorkplaceStage.None && !TrySelectWorkplace())
        {
            villager.StopForagingMovement();
            return;
        }

        switch (workplaceStage)
        {
            case WorkplaceStage.GatheringMaterials:
                UpdateWorkplaceMaterialGathering();
                break;
            case WorkplaceStage.BuildingHouseStageOne:
                if (UpdateWorkplaceConstructionStage(houseSettings.firstConstructionDurationSeconds))
                {
                    ShowWorkplaceConstructionStageTwo();
                }
                break;
            case WorkplaceStage.HouseConstructionBreak:
                villager.StopForagingMovement();
                workplaceConstructionTimer += Time.deltaTime;
                if (workplaceConstructionTimer >= houseSettings.constructionBreakDurationSeconds)
                {
                    workplaceStage = WorkplaceStage.BuildingHouseStageTwo;
                    workplaceConstructionTimer = 0f;
                    workplaceHammeringTimer = 0f;
                    BeginConstructionWorkStep();
                    villager.SetJobBehavior(
                        GetHomeAwareConstructionBehavior(VillagerBehaviorMode.Building),
                        houseSettings.secondConstructionDurationSeconds);
                }
                break;
            case WorkplaceStage.BuildingHouseStageTwo:
                if (UpdateWorkplaceConstructionStage(houseSettings.secondConstructionDurationSeconds))
                {
                    FinishWorkplaceHouseConstruction();
                }
                break;
            case WorkplaceStage.RenovatingAnnex:
                if (UpdateWorkplaceConstructionStage(houseSettings.secondConstructionDurationSeconds))
                {
                    FinishWorkplaceAnnexRenovation();
                }
                break;
        }
    }

    private bool TrySelectWorkplace()
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager == null)
        {
            return false;
        }

        GameObject homeHouse = villager.HomeHouseObject;
        if (villager.HousingState == VillagerHousingState.Housed
            && homeHouse != null
            && villager.HomeHousePlot != null
            && mapManager.TryReserveWorkplaceAnnex(homeHouse, villager))
        {
            SetWorkplaceHouse(homeHouse, villager.HomeHousePlot, false);
            if (HasWorkplaceAnnex())
            {
                workplaceStage = WorkplaceStage.Ready;
            }
            else
            {
                BeginAnnexRenovation();
            }

            return true;
        }

        if (mapManager.TryClaimAvailableWorkplaceHouse(
                villager,
                out HousePlotRecord availablePlot,
                out GameObject availableHouse))
        {
            SetWorkplaceHouse(availableHouse, availablePlot, true);
            if (HasWorkplaceAnnex())
            {
                workplaceStage = WorkplaceStage.Ready;
            }
            else
            {
                BeginAnnexRenovation();
            }

            return true;
        }

        isSeparateWorkplace = true;
        if (!mapManager.TryClaimWorkplaceHousePlot(
                villager,
                houseSettings.defaultHouseDimensions,
                out HousePlotRecord newPlot))
        {
            return false;
        }

        Player player = Player.Instance;
        if (player == null)
        {
            Debug.LogError("Cannot build a workplace house without an active Player.", villager);
            mapManager.ReleaseWorkplaceHouse(villager);
            isSeparateWorkplace = false;
            return false;
        }

        GameObject newHouse = player.PlaceClaimedSpace(newPlot);
        if (newHouse == null)
        {
            mapManager.ReleaseWorkplaceHouse(villager);
            isSeparateWorkplace = false;
            return false;
        }

        HouseVisual houseVisual = newHouse.GetComponent<HouseVisual>();
        if (houseVisual == null)
        {
            houseVisual = newHouse.AddComponent<HouseVisual>();
        }

        SetWorkplaceHouse(newHouse, newPlot, true);
        mapManager.SetWorkplaceHouseObject(villager, newHouse);
        if (!mapManager.TryReserveWorkplaceAnnex(newHouse, villager))
        {
            mapManager.ReleaseWorkplaceHouse(villager);
            workplaceHouse = null;
            workplacePlot = null;
            workplaceStage = WorkplaceStage.None;
            return false;
        }

        workplaceHouseDefinition =
            houseDefinitions[UnityEngine.Random.Range(0, houseDefinitions.Length)];
        workplaceProjectCosts = CombineResourceCosts(
            houseSettings.baseHouseCosts,
            annexRenovationCosts);
        isBuildingNewWorkplaceHouse = true;
        workplaceStage = WorkplaceStage.GatheringMaterials;
        return true;
    }

    private void SetWorkplaceHouse(
        GameObject houseObject,
        HousePlotRecord housePlot,
        bool isSeparate)
    {
        workplaceHouse = houseObject;
        workplacePlot = housePlot;
        isSeparateWorkplace = isSeparate;
        workplaceHouseDefinition = houseObject.GetComponent<HouseVisual>() != null
            ? houseObject.GetComponent<HouseVisual>().Definition
            : null;
    }

    private VillagerBehaviorMode GetHomeAwareConstructionBehavior(
        VillagerBehaviorMode fallbackBehavior)
    {
        return workplaceHouse != null && workplaceHouse == villager.HomeHouseObject
            ? VillagerBehaviorMode.WorkingOnHome
            : fallbackBehavior;
    }

    private bool HasWorkplaceAnnex()
    {
        HouseVisual houseVisual = workplaceHouse != null
            ? workplaceHouse.GetComponent<HouseVisual>()
            : null;
        return houseVisual != null && houseVisual.HasAnnex;
    }

    private void BeginAnnexRenovation()
    {
        workplaceProjectCosts = annexRenovationCosts;
        isBuildingNewWorkplaceHouse = false;
        workplaceStage = WorkplaceStage.GatheringMaterials;
    }

    private void UpdateWorkplaceMaterialGathering()
    {
        ResourceCost missingCost = FindMissingCost(workplaceProjectCosts);
        if (missingCost != null)
        {
            int missingAmount = missingCost.amount
                - villager.GetResourceInventoryAmountForJob(missingCost.resourceType);
            if (!villager.TryBeginWorkplaceMaterialsWithdrawal(
                    missingCost.resourceType,
                    missingAmount))
            {
                villager.TryGatherWorkplaceResource(missingCost.resourceType, missingAmount);
            }

            return;
        }

        MapManager mapManager = MapManager.Instance;
        if (!villager.MoveToHouseWorkPosition(mapManager, workplacePlot))
        {
            return;
        }

        if (!villager.TryRemoveResourceCostsForJob(workplaceProjectCosts))
        {
            Debug.LogError("Villager reached a workplace without its required materials.", villager);
            return;
        }

        workplacePlot.storedMaterials = CopyCosts(workplaceProjectCosts);
        if (isBuildingNewWorkplaceHouse)
        {
            StartWorkplaceHouseConstruction();
        }
        else
        {
            StartWorkplaceAnnexRenovation();
        }
    }

    private void StartWorkplaceHouseConstruction()
    {
        MapManager mapManager = MapManager.Instance;
        HouseVisual houseVisual = workplaceHouse.GetComponent<HouseVisual>();
        float visualWidth = villager.GetHouseVisualWidth(
            mapManager,
            workplacePlot,
            workplaceHouseDefinition);
        houseVisual.ShowConstructionStage(
            houseBuildStageOneSprites,
            houseBuildStageOneColors,
            visualWidth,
            workplaceHouseDefinition.visualOffset,
            workplaceHouseDefinition.sortingAnchorOffset,
            false);
        mapManager.NotifyWorkplaceConstructionStarted(villager);
        BeginConstructionWorkStep();
        workplaceStage = WorkplaceStage.BuildingHouseStageOne;
        workplaceConstructionTimer = 0f;
        workplaceHammeringTimer = 0f;
        villager.SetJobBehavior(
            GetHomeAwareConstructionBehavior(VillagerBehaviorMode.Building),
            houseSettings.firstConstructionDurationSeconds);
    }

    private void ShowWorkplaceConstructionStageTwo()
    {
        HouseConstructionSite constructionSite = GetConstructionSite();
        if (constructionSite != null)
        {
            constructionSite.EndWorkStep();
        }

        HouseVisual houseVisual = workplaceHouse.GetComponent<HouseVisual>();
        houseVisual.ShowConstructionStage(
            houseBuildStageTwoSprites,
            houseBuildStageTwoColors,
            villager.GetHouseVisualWidth(
                MapManager.Instance,
                workplacePlot,
                workplaceHouseDefinition),
            workplaceHouseDefinition.visualOffset,
            workplaceHouseDefinition.sortingAnchorOffset,
            true);
        workplaceStage = WorkplaceStage.HouseConstructionBreak;
        workplaceConstructionTimer = 0f;
        workplaceHammeringTimer = 0f;
        villager.SetJobBehavior(
            GetHomeAwareConstructionBehavior(VillagerBehaviorMode.Idle),
            houseSettings.constructionBreakDurationSeconds);
    }

    private void FinishWorkplaceHouseConstruction()
    {
        HouseConstructionSite constructionSite = GetConstructionSite();
        if (constructionSite != null)
        {
            constructionSite.EndWorkStep();
        }

        float visualWidth = villager.GetHouseVisualWidth(
            MapManager.Instance,
            workplacePlot,
            workplaceHouseDefinition);
        HouseVisual houseVisual = workplaceHouse.GetComponent<HouseVisual>();
        houseVisual.ShowFinishedHouse(workplaceHouseDefinition, visualWidth);
        if (!MapManager.Instance.RegisterCompletedWorkplaceHouse(
                villager,
                workplacePlot,
                workplaceHouse))
        {
            return;
        }

        isBuildingNewWorkplaceHouse = false;
        StartWorkplaceAnnexRenovation();
    }

    private void StartWorkplaceAnnexRenovation()
    {
        BeginConstructionWorkStep();
        HouseVisual houseVisual = workplaceHouse.GetComponent<HouseVisual>();
        houseVisual.ShowRenovationProgress(
            houseBuildStageTwoSprites,
            houseBuildStageTwoColors,
            villager.GetHouseVisualWidth(
                MapManager.Instance,
                workplacePlot,
                workplaceHouseDefinition),
            workplaceHouseDefinition.visualOffset,
            workplaceHouseDefinition.sortingAnchorOffset);
        workplaceStage = WorkplaceStage.RenovatingAnnex;
        workplaceConstructionTimer = 0f;
        workplaceHammeringTimer = 0f;
        villager.SetJobBehavior(
            GetHomeAwareConstructionBehavior(VillagerBehaviorMode.Building),
            houseSettings.secondConstructionDurationSeconds);
    }

    private void FinishWorkplaceAnnexRenovation()
    {
        HouseConstructionSite constructionSite = GetConstructionSite();
        if (constructionSite != null)
        {
            constructionSite.Complete();
        }

        HouseVisual houseVisual = workplaceHouse.GetComponent<HouseVisual>();
        houseVisual.CompleteAnnexRenovation();
        workplaceStage = WorkplaceStage.Ready;
        workplaceConstructionTimer = 0f;
        workplaceHammeringTimer = 0f;
        villager.SetJobBehavior(VillagerBehaviorMode.Idle, settings.idleBehaviorDuration);
    }

    private bool UpdateWorkplaceConstructionStage(float duration)
    {
        MapManager mapManager = MapManager.Instance;
        if (!villager.MoveToHouseWorkPosition(mapManager, workplacePlot))
        {
            return false;
        }

        HouseConstructionSite constructionSite = GetConstructionSite();
        workplaceConstructionTimer += Time.deltaTime
            * (constructionSite != null ? constructionSite.WorkerCount : 1);
        workplaceHammeringTimer -= Time.deltaTime;
        if (workplaceHammeringTimer <= 0f)
        {
            villager.PlayHouseHammerMotion();
            workplaceHammeringTimer = houseSettings.hammerPauseDurationSeconds;
        }

        return workplaceConstructionTimer >= duration;
    }

    private ResourceCost FindMissingCost(ResourceCost[] costs)
    {
        if (costs == null)
        {
            return null;
        }

        foreach (ResourceCost cost in costs)
        {
            if (cost != null
                && villager.GetResourceInventoryAmountForJob(cost.resourceType) < cost.amount)
            {
                return cost;
            }
        }

        return null;
    }

    private static ResourceCost[] CopyCosts(ResourceCost[] costs)
    {
        ResourceCost[] copy = new ResourceCost[costs.Length];
        for (int index = 0; index < costs.Length; index++)
        {
            copy[index] = new ResourceCost
            {
                resourceType = costs[index].resourceType,
                amount = costs[index].amount
            };
        }

        return copy;
    }

    private static ResourceCost[] CombineResourceCosts(
        ResourceCost[] firstCosts,
        ResourceCost[] secondCosts)
    {
        List<ResourceCost> combinedCosts = new List<ResourceCost>();
        AddResourceCosts(combinedCosts, firstCosts);
        AddResourceCosts(combinedCosts, secondCosts);
        return combinedCosts.ToArray();
    }

    private static void AddResourceCosts(List<ResourceCost> combinedCosts, ResourceCost[] costs)
    {
        if (costs == null)
        {
            return;
        }

        foreach (ResourceCost cost in costs)
        {
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            ResourceCost combinedCost = combinedCosts.Find(
                existing => existing.resourceType == cost.resourceType);
            if (combinedCost == null)
            {
                combinedCosts.Add(new ResourceCost
                {
                    resourceType = cost.resourceType,
                    amount = cost.amount
                });
            }
            else
            {
                combinedCost.amount += cost.amount;
            }
        }
    }

    private static bool IsFiniteNonnegative(float value)
    {
        return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    internal void ReleaseWorkplace()
    {
        MapManager mapManager = MapManager.Instance;
        HouseConstructionSite constructionSite = GetConstructionSite();
        if (constructionSite != null)
        {
            constructionSite.Complete();
        }

        if (mapManager != null)
        {
            if (workplaceHouse != null)
            {
                mapManager.ReleaseWorkplaceAnnex(workplaceHouse, villager);
            }

            if (isSeparateWorkplace)
            {
                mapManager.ReleaseWorkplaceHouse(villager);
            }
        }

        workplaceHouse = null;
        workplacePlot = null;
        workplaceHouseDefinition = null;
        workplaceProjectCosts = null;
        workplaceStage = WorkplaceStage.None;
        isSeparateWorkplace = false;
        isBuildingNewWorkplaceHouse = false;
    }

    private HouseConstructionSite GetConstructionSite()
    {
        return workplaceHouse != null
            ? workplaceHouse.GetComponent<HouseConstructionSite>()
            : null;
    }

    private void BeginConstructionWorkStep()
    {
        MapManager mapManager = MapManager.Instance;
        HouseConstructionSite constructionSite = mapManager != null && workplaceHouse != null
            ? mapManager.GetOrCreateHouseConstructionSite(workplaceHouse)
            : null;
        if (mapManager == null || constructionSite == null)
        {
            Debug.LogError("Cannot begin workplace construction without an active construction site.", this);
            return;
        }

        if (!constructionSite.BeginWorkStep(
                villager,
                mapManager.GetHouseEntrancePosition(workplacePlot)))
        {
            Debug.LogError("Could not begin the workplace construction work step.", this);
        }
    }

    public void CancelWorkplacePlotForRoad()
    {
        ReleaseWorkplace();
        villager.StopForagingMovement();
    }

    public void PauseJobForEmergencyFood()
    {
        PauseJobWork();
    }

    public void PauseJobWork()
    {
        bool wasPickingUpRequiredItem =
            requiredItemWorkplace != null || isApproachingRequiredItem;
        StopForagingForJob();
        if (wasPickingUpRequiredItem
            && jobDefinition != null
            && jobDefinition.requiresEquippedItem
            && jobDefinition.requiredEquippedItemType.HasValue
            && villager.EquippedItem != jobDefinition.requiredEquippedItemType.Value
            && villager.GetInventoryItemCount(
                jobDefinition.requiredEquippedItemType.Value) == 0)
        {
            WaitForRequiredItem();
        }
    }

    public void BeginEmergencyFoodSearch()
    {
        ResetForagingSearch();
    }

    public void UpdateEmergencyFoodSearch()
    {
        UpdateForaging(false);
    }

    public void ReleaseForagingResource()
    {
        ReleaseForagingResource(MapManager.Instance);
    }

    private bool UpdateForaging(bool isForagerJob)
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager == null)
        {
            villager.StopForagingMovement();
            return false;
        }

        if (pathfindingRevision != mapManager.PathfindingRevision)
        {
            unreachableResources.Clear();
            pathfindingRevision = mapManager.PathfindingRevision;
            nextResourceSearchTime = 0f;
        }

        if (availabilityRevision != mapManager.ResourceAvailabilityRevision)
        {
            unreachableResources.Clear();
            availabilityRevision = mapManager.ResourceAvailabilityRevision;
            nextResourceSearchTime = 0f;
        }

        if (foragingResource != null && villager.HasFailedPathRequestForAction)
        {
            unreachableResources.Add(foragingResource);
            ReleaseForagingResource(mapManager);
        }

        bool requiresEquippedTool = isForagerJob
            && jobDefinition != null
            && jobDefinition.requiresEquippedItem;
        bool canUseEquippedToolForWork = !requiresEquippedTool;
        if (requiresEquippedTool)
        {
            if (jobDefinition.resourceTypes == null
                || jobDefinition.resourceTypes.Length != 1
                || villager.EquippedItem != jobDefinition.requiredEquippedItemType)
            {
                villager.StopForagingMovement();
                return false;
            }

            canUseEquippedToolForWork = villager.CanUseEquippedToolForResource(jobDefinition.resourceTypes[0]);
            if (!canUseEquippedToolForWork)
            {
                villager.StopForagingMovement();
                return false;
            }
        }

        if (foragingResource == null
            || !foragingResource.CanGather(
                foragingResourceType,
                requiresEquippedTool && canUseEquippedToolForWork))
        {
            ReleaseForagingResource(mapManager);
            if (Time.time < nextResourceSearchTime)
            {
                villager.StopForagingMovement();
                return false;
            }

            bool foundResource = isForagerJob
                ? (requiresEquippedTool
                    ? mapManager.TryGetClosestReachableResourceObject(
                        villager,
                        jobDefinition.resourceTypes[0],
                        villager.transform.position,
                        true,
                        unreachableResources,
                        jobDefinition.gatheringDistance,
                        out foragingResource)
                    : mapManager.TryGetClosestReachableBareHandResourceObject(
                        villager,
                        villager.transform.position,
                        unreachableResources,
                        GetAllowedResourceTypes(),
                        isForagerJob ? jobDefinition.gatheringDistance : settings.gatheringDistance,
                        out foragingResource,
                        out foragingResourceType))
                : mapManager.TryGetClosestReachableResourceObject(
                    villager,
                    ResourceType.Food,
                    villager.transform.position,
                    false,
                    unreachableResources,
                    settings.gatheringDistance,
                    out foragingResource);
            if (!foundResource)
            {
                nextResourceSearchTime = Time.time
                    + (isForagerJob
                        ? jobDefinition.resourceSearchRetrySeconds
                        : settings.foodResourceSearchRetrySeconds);
                villager.StopForagingMovement();
                return false;
            }

            if (requiresEquippedTool)
            {
                foragingResourceType = jobDefinition.resourceTypes[0];
            }
            else if (!isForagerJob)
            {
                foragingResourceType = ResourceType.Food;
            }

            nextResourceSearchTime = 0f;
            isWaitingToApproachResource = true;
            foragingReactionTimer = villager.GetReactionTimeForAction();
        }

        if (isWaitingToApproachResource)
        {
            foragingReactionTimer = Mathf.Max(0f, foragingReactionTimer - Time.deltaTime);
            if (foragingReactionTimer > 0f)
            {
                villager.StopForagingMovement();
                return true;
            }

            isWaitingToApproachResource = false;
        }

        Vector2Int interactionOffset =
            foragingResource.GetInteractionOffset(foragingResourceType);
        Vector3 interactionPosition = interactionOffset != Vector2Int.zero
            ? mapManager.GetGridCellBounds(foragingResource.GridCell + interactionOffset).center
            : foragingResource.transform.position;
        float interactionDistance = interactionOffset != Vector2Int.zero
            ? 0.1f
            : isForagerJob ? jobDefinition.gatheringDistance : settings.gatheringDistance;
        if (Vector2.Distance(villager.transform.position, interactionPosition) > interactionDistance)
        {
            foragingGatheringTimer = 0f;
            villager.MoveToForagingPosition(interactionPosition, interactionDistance);
            return true;
        }

        villager.StopForagingMovement();
        foragingGatheringTimer -= Time.deltaTime;
        if (foragingGatheringTimer > 0f)
        {
            return true;
        }

        villager.PlayForagingMotion(
            isForagerJob ? jobDefinition.handHitPositions : settings.handHitPositions,
            isForagerJob
                ? jobDefinition.handMotionDurationSeconds
                : settings.handMotionDurationSeconds);
        foragingResource.PlayShake(
            resourceSettings.shakeDurationSeconds,
            resourceSettings.shakeMagnitude,
            resourceSettings.shakeFrequency);
        bool hasRequiredToolForCurrentGather = requiresEquippedTool
            && villager.EquippedItem == jobDefinition.requiredEquippedItemType
            && villager.CanUseEquippedToolForResource(foragingResourceType);
        if (mapManager.TryGatherResource(
                villager,
                foragingResource,
                foragingResourceType,
                isForagerJob ? jobDefinition.harvestAmount : 1,
                hasRequiredToolForCurrentGather,
                out int gatheredAmount)
            && gatheredAmount > 0)
        {
            if (isForagerJob)
            {
                villager.OnForagedResource(foragingResourceType, gatheredAmount);
            }
            else
            {
                villager.OnForagedFood(gatheredAmount);
            }
        }
        else
        {
            ReleaseForagingResource(mapManager);
        }

        foragingGatheringTimer = isForagerJob
            ? jobDefinition.gatheringPauseDurationSeconds
            : settings.gatheringPauseDurationSeconds;
        return true;
    }

    private ISet<ResourceType> GetAllowedResourceTypes()
    {
        if (jobDefinition == null
            || jobDefinition.gatherAnyAvailableResource
            || jobDefinition.resourceTypes == null)
        {
            return null;
        }

        return new HashSet<ResourceType>(jobDefinition.resourceTypes);
    }

    private void StopForagingForJob()
    {
        ReleaseRequiredItemPickup();
        if (isForagingForJob)
        {
            ReleaseForagingResource(MapManager.Instance);
        }

        isForagingForJob = false;
    }

    private void ResetForagingSearch()
    {
        unreachableResources.Clear();
        nextResourceSearchTime = 0f;
        foragingGatheringTimer = 0f;
        isWaitingToApproachResource = false;
        MapManager mapManager = MapManager.Instance;
        pathfindingRevision = mapManager != null ? mapManager.PathfindingRevision : -1;
        availabilityRevision = mapManager != null
            ? mapManager.ResourceAvailabilityRevision
            : -1;
        ReleaseForagingResource(mapManager);
    }

    private void ReleaseForagingResource(MapManager mapManager)
    {
        if (mapManager != null)
        {
            mapManager.ReleaseResourceReservation(foragingResource, villager);
        }

        foragingResource = null;
        foragingResourceType = default(ResourceType);
        isWaitingToApproachResource = false;
        foragingReactionTimer = 0f;
    }

    private void OnDestroy()
    {
        ReleaseRequiredItemPickup();
        ReleaseForagingResource(MapManager.Instance);
        ReleaseWorkplace();
    }
}
