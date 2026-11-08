using System.Collections.Generic;
using UnityEngine;

public class VillagerJobController : MonoBehaviour
{
    private Villager villager;
    private VillagerSettings settings;
    private ResourceSettings resourceSettings;
    private JobType jobAssignment;
    private ResourceObject foragingResource;
    private ResourceType foragingResourceType;
    private readonly HashSet<ResourceObject> unreachableResources =
        new HashSet<ResourceObject>();
    private float foragingReactionTimer;
    private float foragingGatheringTimer;
    private float nextResourceSearchTime;
    private int pathfindingRevision = -1;
    private int availabilityRevision = -1;
    private bool isWaitingToApproachResource;
    private bool isForagingForJob;

    public JobType JobAssignment
    {
        get { return jobAssignment; }
    }

    public bool IsForagingForJob
    {
        get { return isForagingForJob && foragingResource != null; }
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
                    ResetForagingSearch();
                }

                return UpdateForaging(true);
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

        if (foragingResource == null
            || !foragingResource.CanGather(foragingResourceType, false))
        {
            ReleaseForagingResource(mapManager);
            if (Time.time < nextResourceSearchTime)
            {
                villager.StopForagingMovement();
                return false;
            }

            bool foundResource = isForagerJob
                ? mapManager.TryGetClosestReachableBareHandResourceObject(
                    villager,
                    villager.transform.position,
                    unreachableResources,
                    settings.gatheringDistance,
                    out foragingResource,
                    out foragingResourceType)
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
                nextResourceSearchTime = Time.time + settings.foodResourceSearchRetrySeconds;
                villager.StopForagingMovement();
                return false;
            }

            if (!isForagerJob)
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
            : settings.gatheringDistance;
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

        villager.PlayForagingMotion(settings.handHitPositions, settings.handMotionDurationSeconds);
        foragingResource.PlayShake(
            resourceSettings.shakeDurationSeconds,
            resourceSettings.shakeMagnitude,
            resourceSettings.shakeFrequency);
        if (mapManager.TryGatherResource(
                villager,
                foragingResource,
                foragingResourceType,
                1,
                false,
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

        foragingGatheringTimer = settings.gatheringPauseDurationSeconds;
        return true;
    }

    private void StopForagingForJob()
    {
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
        ReleaseForagingResource(MapManager.Instance);
    }
}
