using UnityEngine;

public class VillagerBrain : MonoBehaviour
{
    private enum Operation
    {
        Housing,
        Food,
        Productive,
        CommunityStorage
    }

    private Villager villager;
    private VillagerSettings settings;
    private bool hasNoWorkDecision;
    private bool noWorkDecisionIsStayHome;
    private VillagerHousingState observedHousingState;
    private JobType observedJobAssignment;

    public void Initialize(Villager owner, VillagerSettings villagerSettings)
    {
        if (owner == null || villagerSettings == null)
        {
            Debug.LogError("VillagerBrain requires an owner and villager settings.", this);
            return;
        }

        villager = owner;
        settings = villagerSettings;
        observedHousingState = owner.HousingState;
        observedJobAssignment = owner.JobAssignment;
    }

    public void DecideNextOperation()
    {
        if (villager == null || settings == null)
        {
            return;
        }

        if (observedHousingState != villager.HousingState
            || observedJobAssignment != villager.JobAssignment)
        {
            ResetNoWorkDecision();
            observedHousingState = villager.HousingState;
            observedJobAssignment = villager.JobAssignment;
        }

        if (villager.JobAssignment != JobType.None
            && villager.Fatigue >= settings.productiveFatigueThreshold)
        {
            villager.BeginWorkFatigueRest();
        }

        bool housingAvailable = villager.HousingState != VillagerHousingState.Housed;
        bool emergencyFoodAvailable = villager.Hunger > settings.emergencyFoodHungerThreshold;
        bool foodAvailable = emergencyFoodAvailable
            || (villager.HousingState == VillagerHousingState.Housed
                && villager.Hunger > settings.boredFoodSeekingHungerThreshold);
        bool communityStorageAvailable = villager.CanChooseCommunityStorageOperation;

        float housingPriority = housingAvailable
            ? settings.housingOperationPriority
                - (villager.HasPersonality(VillagerPersonalityType.Lazy)
                    ? settings.lazyHousingPriorityPenalty
                    : 0f)
            : float.NegativeInfinity;
        float foodPriority = foodAvailable
            ? settings.foodOperationPriority
                + villager.Hunger * settings.hungerFoodPriorityMultiplier
                + (emergencyFoodAvailable ? settings.emergencyFoodPriorityBonus : 0f)
            : float.NegativeInfinity;
        float productivePriority = villager.JobAssignment != JobType.None
            && !villager.IsRestingFromWork
            && villager.Fatigue < settings.productiveFatigueThreshold
            ? settings.productiveOperationPriority
            : float.NegativeInfinity;
        float communityStoragePriority = float.NegativeInfinity;
        if (communityStorageAvailable)
        {
            // Storage priority scales from its configured base; with little surplus,
            // productive work can still win, while larger loads make storage more urgent.
            communityStoragePriority = settings.communityStorageOperationPriority
                + villager.CommunityStorageSurplusAmount
                    * settings.communityStoragePriorityPerItem;
        }

        Operation firstOperation = Operation.Housing;
        float firstPriority = housingPriority;
        Operation secondOperation = Operation.Food;
        float secondPriority = foodPriority;
        Operation thirdOperation = Operation.Productive;
        float thirdPriority = productivePriority;
        Operation fourthOperation = Operation.CommunityStorage;
        float fourthPriority = communityStoragePriority;
        OrderByPriority(ref firstOperation, ref firstPriority, ref secondOperation, ref secondPriority);
        OrderByPriority(ref secondOperation, ref secondPriority, ref thirdOperation, ref thirdPriority);
        OrderByPriority(ref firstOperation, ref firstPriority, ref secondOperation, ref secondPriority);
        OrderByPriority(ref thirdOperation, ref thirdPriority, ref fourthOperation, ref fourthPriority);
        OrderByPriority(ref secondOperation, ref secondPriority, ref thirdOperation, ref thirdPriority);
        OrderByPriority(ref firstOperation, ref firstPriority, ref secondOperation, ref secondPriority);

        if (TryRunOperation(firstOperation, housingAvailable, foodAvailable)
            || TryRunOperation(secondOperation, housingAvailable, foodAvailable)
            || TryRunOperation(thirdOperation, housingAvailable, foodAvailable)
            || TryRunOperation(fourthOperation, housingAvailable, foodAvailable))
        {
            ResetNoWorkDecision();
            return;
        }

        if (villager.HasHome
            && villager.Fatigue >= settings.homeRestStartFatigueThreshold)
        {
            noWorkDecisionIsStayHome = true;
            hasNoWorkDecision = true;
        }
        else if (!hasNoWorkDecision)
        {
            float stayHomeProbability = villager.HasPersonality(VillagerPersonalityType.Lazy)
                ? settings.lazyIdleHomeStayProbability
                : settings.idleHomeStayProbability;
            noWorkDecisionIsStayHome = villager.HasHome
                && Random.value < stayHomeProbability;
            hasNoWorkDecision = true;
        }

        villager.ApplyNoWorkDecision(noWorkDecisionIsStayHome);
    }

    public void ResetNoWorkDecision()
    {
        hasNoWorkDecision = false;
        noWorkDecisionIsStayHome = false;
    }

    private bool TryRunOperation(Operation operation, bool housingAvailable, bool foodAvailable)
    {
        switch (operation)
        {
            case Operation.Housing:
                return housingAvailable && villager.TryRunHousingOperation();
            case Operation.Food:
                return foodAvailable && villager.TryRunFoodOperation(
                    villager.Hunger > settings.emergencyFoodHungerThreshold);
            case Operation.Productive:
                return villager.TryRunProductiveOperation();
            case Operation.CommunityStorage:
                return villager.TryRunCommunityStorageOperation();
            default:
                Debug.LogError("VillagerBrain received an unsupported operation.", this);
                return false;
        }
    }

    private static void OrderByPriority(
        ref Operation firstOperation,
        ref float firstPriority,
        ref Operation secondOperation,
        ref float secondPriority)
    {
        if (secondPriority <= firstPriority)
        {
            return;
        }

        Operation temporaryOperation = firstOperation;
        firstOperation = secondOperation;
        secondOperation = temporaryOperation;

        float temporaryPriority = firstPriority;
        firstPriority = secondPriority;
        secondPriority = temporaryPriority;
    }
}
