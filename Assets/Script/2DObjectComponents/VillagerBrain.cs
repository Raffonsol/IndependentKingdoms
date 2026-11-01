using UnityEngine;

public class VillagerBrain : MonoBehaviour
{
    private enum Operation
    {
        Housing,
        Food,
        Productive
    }

    private Villager villager;
    private VillagerSettings settings;

    public void Initialize(Villager owner, VillagerSettings villagerSettings)
    {
        if (owner == null || villagerSettings == null)
        {
            Debug.LogError("VillagerBrain requires an owner and villager settings.", this);
            return;
        }

        villager = owner;
        settings = villagerSettings;
    }

    public void DecideNextOperation()
    {
        if (villager == null || settings == null)
        {
            return;
        }

        bool housingAvailable = villager.HousingState != VillagerHousingState.Housed;
        bool emergencyFoodAvailable = villager.Hunger > settings.emergencyFoodHungerThreshold;
        bool foodAvailable = emergencyFoodAvailable
            || (villager.HousingState == VillagerHousingState.Housed
                && villager.Hunger > settings.boredFoodSeekingHungerThreshold);

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
            && villager.Fatigue < settings.productiveFatigueThreshold
            ? settings.productiveOperationPriority
            : float.NegativeInfinity;

        Operation firstOperation = Operation.Housing;
        float firstPriority = housingPriority;
        Operation secondOperation = Operation.Food;
        float secondPriority = foodPriority;
        Operation thirdOperation = Operation.Productive;
        float thirdPriority = productivePriority;
        OrderByPriority(ref firstOperation, ref firstPriority, ref secondOperation, ref secondPriority);
        OrderByPriority(ref secondOperation, ref secondPriority, ref thirdOperation, ref thirdPriority);
        OrderByPriority(ref firstOperation, ref firstPriority, ref secondOperation, ref secondPriority);

        if (TryRunOperation(firstOperation, housingAvailable, foodAvailable)
            || TryRunOperation(secondOperation, housingAvailable, foodAvailable)
            || TryRunOperation(thirdOperation, housingAvailable, foodAvailable)
            || villager.TryGoHomeOperation())
        {
            return;
        }

        villager.IdleWithoutHome();
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
