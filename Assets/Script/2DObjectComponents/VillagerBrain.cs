using UnityEngine;

public class VillagerBrain : MonoBehaviour
{
    private enum Operation
    {
        Housing,
        Food,
        Productive,
        CommunityStorage,
        ConstructionAssistance
    }

    private Villager villager;
    private VillagerSettings settings;
    private bool hasNoWorkDecision;
    private bool noWorkDecisionIsStayHome;
    private bool hasActiveOperation;
    private Operation activeOperation;
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
            hasActiveOperation = false;
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
        bool requiredItemAvailable = villager.IsWaitingForRequiredItem
            && villager.HasRequiredItemAvailable;

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
        float productivePriority = villager.HousingState == VillagerHousingState.Housed
            && villager.JobAssignment != JobType.None
            && !villager.IsRestingFromWork
            && villager.Fatigue < settings.productiveFatigueThreshold
            ? (villager.IsPreparingJobWorkplace
                ? settings.housingOperationPriority
                    - (villager.HasPersonality(VillagerPersonalityType.Lazy)
                        ? settings.lazyHousingPriorityPenalty
                        : 0f)
                : villager.IsWaitingForRequiredItem && !villager.HasRequiredItemAvailable
                    ? settings.productiveOperationPriorityWhileMissingRequiredItem
                    : settings.productiveOperationPriority)
            : float.NegativeInfinity;
        VillagerBehaviorStage currentBehavior = villager.CurrentBehavior;
        bool isNoWorkBehavior = !hasActiveOperation
            && (currentBehavior == null
                || currentBehavior.mode == VillagerBehaviorMode.Idle
                || currentBehavior.mode == VillagerBehaviorMode.Wander);
        bool noWorkBehaviorHasEnded = currentBehavior == null
            || villager.BehaviorTimer >= currentBehavior.duration;
        if (requiredItemAvailable
            && isNoWorkBehavior
            && !settings.requiredItemWaitInterruptibility.idleRestWander
            && !noWorkBehaviorHasEnded)
        {
            productivePriority =
                settings.productiveOperationPriorityWhileMissingRequiredItem;
        }
        float communityStoragePriority = float.NegativeInfinity;
        if (communityStorageAvailable)
        {
            // Storage priority scales from its configured base; with little surplus,
            // productive work can still win, while larger loads make storage more urgent.
            communityStoragePriority = settings.communityStorageOperationPriority
                + villager.CommunityStorageSurplusAmount
                    * settings.communityStoragePriorityPerItem;
        }
        float constructionAssistancePriority = float.NegativeInfinity;
        bool continuingConstructionAssistance = villager.IsHelpingConstruction;
        bool anotherOperationIsActive = hasActiveOperation
            && activeOperation != Operation.ConstructionAssistance;
        MapManager mapManager = MapManager.Instance;
        if (continuingConstructionAssistance
            || (!anotherOperationIsActive
                && villager.HasPersonality(VillagerPersonalityType.Helpful)
                && villager.CanStartConstructionAssistance
                && mapManager != null
                && mapManager.HasActiveConstructionSiteFor(villager)))
        {
            constructionAssistancePriority = settings.helpfulConstructionOperationPriority;
        }

        if (communityStorageAvailable
            && constructionAssistancePriority > communityStoragePriority)
        {
            constructionAssistancePriority = communityStoragePriority;
        }

        if (requiredItemAvailable
            && continuingConstructionAssistance
            && settings.requiredItemWaitInterruptibility.helpfulConstruction
            && productivePriority > float.NegativeInfinity)
        {
            constructionAssistancePriority = Mathf.Min(
                constructionAssistancePriority,
                productivePriority - 1f);
        }

        Operation firstOperation = Operation.Housing;
        float firstPriority = housingPriority;
        Operation secondOperation = Operation.Food;
        float secondPriority = foodPriority;
        Operation thirdOperation = Operation.Productive;
        float thirdPriority = productivePriority;
        Operation fourthOperation = Operation.CommunityStorage;
        float fourthPriority = communityStoragePriority;
        Operation fifthOperation = Operation.ConstructionAssistance;
        float fifthPriority = constructionAssistancePriority;
        OrderByPriority(ref firstOperation, ref firstPriority, ref secondOperation, ref secondPriority);
        OrderByPriority(ref secondOperation, ref secondPriority, ref thirdOperation, ref thirdPriority);
        OrderByPriority(ref thirdOperation, ref thirdPriority, ref fourthOperation, ref fourthPriority);
        OrderByPriority(ref fourthOperation, ref fourthPriority, ref fifthOperation, ref fifthPriority);
        OrderByPriority(ref firstOperation, ref firstPriority, ref secondOperation, ref secondPriority);
        OrderByPriority(ref secondOperation, ref secondPriority, ref thirdOperation, ref thirdPriority);
        OrderByPriority(ref thirdOperation, ref thirdPriority, ref fourthOperation, ref fourthPriority);
        OrderByPriority(ref firstOperation, ref firstPriority, ref secondOperation, ref secondPriority);
        OrderByPriority(ref secondOperation, ref secondPriority, ref thirdOperation, ref thirdPriority);
        OrderByPriority(ref firstOperation, ref firstPriority, ref secondOperation, ref secondPriority);

        Operation selectedOperation = firstOperation;
        if (TryRunPrioritizedOperation(
                firstOperation,
                firstPriority,
                housingAvailable,
                foodAvailable,
                out selectedOperation)
            || TryRunPrioritizedOperation(
                secondOperation,
                secondPriority,
                housingAvailable,
                foodAvailable,
                out selectedOperation)
            || TryRunPrioritizedOperation(
                thirdOperation,
                thirdPriority,
                housingAvailable,
                foodAvailable,
                out selectedOperation)
            || TryRunPrioritizedOperation(
                fourthOperation,
                fourthPriority,
                housingAvailable,
                foodAvailable,
                out selectedOperation)
            || TryRunPrioritizedOperation(
                fifthOperation,
                fifthPriority,
                housingAvailable,
                foodAvailable,
                out selectedOperation))
        {
            hasActiveOperation = true;
            activeOperation = selectedOperation;
            ResetNoWorkDecision();
            return;
        }

        hasActiveOperation = false;
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
                && UnityEngine.Random.value < stayHomeProbability;
            hasNoWorkDecision = true;
        }

        villager.ApplyNoWorkDecision(noWorkDecisionIsStayHome);
    }

    public void ResetNoWorkDecision()
    {
        hasNoWorkDecision = false;
        noWorkDecisionIsStayHome = false;
    }

    private bool TryRunPrioritizedOperation(
        Operation operation,
        float priority,
        bool housingAvailable,
        bool foodAvailable,
        out Operation selectedOperation)
    {
        selectedOperation = operation;
        if (priority <= float.NegativeInfinity)
        {
            return false;
        }

        return TryRunOperation(operation, housingAvailable, foodAvailable);
    }

    private bool TryRunOperation(Operation operation, bool housingAvailable, bool foodAvailable)
    {
        if (operation != Operation.ConstructionAssistance)
        {
            villager.StopConstructionAssistance();
        }

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
            case Operation.ConstructionAssistance:
                return villager.TryRunConstructionAssistance();
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
