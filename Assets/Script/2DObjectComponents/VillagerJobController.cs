using System.Collections.Generic;
using UnityEngine;

public class VillagerJobController : MonoBehaviour
{
    private Villager villager;
    private VillagerSettings settings;
    private ResourceSettings resourceSettings;
    private JobType jobAssignment;
    private ResourceObject foodResource;
    private readonly HashSet<ResourceObject> unreachableFoodResources =
        new HashSet<ResourceObject>();
    private float foodReactionTimer;
    private float foodGatheringTimer;
    private float nextFoodResourceSearchTime;
    private int foodPathfindingRevision = -1;
    private int foodAvailabilityRevision = -1;
    private bool isWaitingToApproachFood;
    private bool isForagingForJob;

    public JobType JobAssignment
    {
        get { return jobAssignment; }
    }

    public bool IsForagingForJob
    {
        get { return isForagingForJob; }
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
        jobAssignment = JobType.None;
    }

    public bool AssignJob(JobType job)
    {
        if (!System.Enum.IsDefined(typeof(JobType), job)
            || (job != JobType.None && job != JobType.Forager))
        {
            return false;
        }

        if (jobAssignment == job)
        {
            return true;
        }

        StopForagingForJob();
        jobAssignment = job;
        return true;
    }

    public bool TryStartProductiveOperation()
    {
        if (villager == null || settings == null)
        {
            return false;
        }

        switch (jobAssignment)
        {
            case JobType.None:
                return false;
            case JobType.Forager:
                if (villager.Fatigue >= settings.productiveFatigueThreshold)
                {
                    StopForagingForJob();
                    return false;
                }

                if (!isForagingForJob)
                {
                    isForagingForJob = true;
                    ResetFoodSearch();
                }

                UpdateForaging(true);
                return true;
            default:
                return false;
        }
    }

    public void PauseJobForEmergencyFood()
    {
        StopForagingForJob();
    }

    public void BeginEmergencyFoodSearch()
    {
        ResetFoodSearch();
    }

    public void UpdateEmergencyFoodSearch()
    {
        UpdateForaging(false);
    }

    public void ReleaseFoodResource()
    {
        ReleaseFoodResource(MapManager.Instance);
    }

    private void UpdateForaging(bool storeFood)
    {
        MapManager mapManager = MapManager.Instance;
        if (mapManager == null)
        {
            villager.StopForagingMovement();
            return;
        }

        if (foodPathfindingRevision != mapManager.PathfindingRevision)
        {
            unreachableFoodResources.Clear();
            foodPathfindingRevision = mapManager.PathfindingRevision;
            nextFoodResourceSearchTime = 0f;
        }

        if (foodAvailabilityRevision != mapManager.ResourceAvailabilityRevision)
        {
            unreachableFoodResources.Clear();
            foodAvailabilityRevision = mapManager.ResourceAvailabilityRevision;
            nextFoodResourceSearchTime = 0f;
        }

        if (foodResource != null && villager.HasFailedPathRequestForAction)
        {
            unreachableFoodResources.Add(foodResource);
            ReleaseFoodResource(mapManager);
        }

        if (foodResource == null || !foodResource.CanGather(ResourceType.Food, false))
        {
            ReleaseFoodResource(mapManager);
            if (Time.time < nextFoodResourceSearchTime)
            {
                villager.StopForagingMovement();
                return;
            }

            if (!mapManager.TryGetClosestReachableResourceObject(
                    villager,
                    ResourceType.Food,
                    villager.transform.position,
                    false,
                    unreachableFoodResources,
                    settings.gatheringDistance,
                    out foodResource))
            {
                nextFoodResourceSearchTime = Time.time + settings.foodResourceSearchRetrySeconds;
                villager.StopForagingMovement();
                return;
            }

            nextFoodResourceSearchTime = 0f;
            isWaitingToApproachFood = true;
            foodReactionTimer = villager.GetReactionTimeForAction();
        }

        if (isWaitingToApproachFood)
        {
            foodReactionTimer = Mathf.Max(0f, foodReactionTimer - Time.deltaTime);
            if (foodReactionTimer > 0f)
            {
                villager.StopForagingMovement();
                return;
            }

            isWaitingToApproachFood = false;
        }

        Vector2Int interactionOffset = foodResource.GetInteractionOffset(ResourceType.Food);
        Vector3 interactionPosition = interactionOffset != Vector2Int.zero
            ? mapManager.GetGridCellBounds(foodResource.GridCell + interactionOffset).center
            : foodResource.transform.position;
        float interactionDistance = interactionOffset != Vector2Int.zero
            ? 0.1f
            : settings.gatheringDistance;
        if (Vector2.Distance(villager.transform.position, interactionPosition) > interactionDistance)
        {
            foodGatheringTimer = 0f;
            villager.MoveToForagingPosition(interactionPosition, interactionDistance);
            return;
        }

        villager.StopForagingMovement();
        foodGatheringTimer -= Time.deltaTime;
        if (foodGatheringTimer > 0f)
        {
            return;
        }

        villager.PlayForagingMotion(settings.handHitPositions, settings.handMotionDurationSeconds);
        foodResource.PlayShake(
            resourceSettings.shakeDurationSeconds,
            resourceSettings.shakeMagnitude,
            resourceSettings.shakeFrequency);
        if (mapManager.TryGatherResource(
                villager,
                foodResource,
                ResourceType.Food,
                1,
                false,
                out int gatheredAmount)
            && gatheredAmount > 0)
        {
            villager.OnForagedFood(gatheredAmount, storeFood);
        }
        else
        {
            ReleaseFoodResource(mapManager);
        }

        foodGatheringTimer = settings.gatheringPauseDurationSeconds;
    }

    private void StopForagingForJob()
    {
        if (isForagingForJob)
        {
            ReleaseFoodResource(MapManager.Instance);
        }

        isForagingForJob = false;
    }

    private void ResetFoodSearch()
    {
        unreachableFoodResources.Clear();
        nextFoodResourceSearchTime = 0f;
        foodGatheringTimer = 0f;
        isWaitingToApproachFood = false;
        MapManager mapManager = MapManager.Instance;
        foodPathfindingRevision = mapManager != null ? mapManager.PathfindingRevision : -1;
        foodAvailabilityRevision = mapManager != null
            ? mapManager.ResourceAvailabilityRevision
            : -1;
        ReleaseFoodResource(mapManager);
    }

    private void ReleaseFoodResource(MapManager mapManager)
    {
        if (mapManager != null)
        {
            mapManager.ReleaseResourceReservation(foodResource, villager);
        }

        foodResource = null;
        isWaitingToApproachFood = false;
        foodReactionTimer = 0f;
    }

    private void OnDestroy()
    {
        ReleaseFoodResource(MapManager.Instance);
    }
}
