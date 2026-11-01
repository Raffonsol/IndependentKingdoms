using TMPro;
using UnityEngine;

public class VillagerHoverTooltip : HoverTooltip
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text activityText;

    protected override void Awake()
    {
        base.Awake();

        if (nameText == null || activityText == null)
        {
            Debug.LogError(
                "Villager tooltip requires both name and activity TextMeshPro references.",
                this);
        }
    }

    public void SetVillager(Villager villager)
    {
        if (villager == null)
        {
            Debug.LogError("Cannot display a tooltip for a null villager.", this);
            return;
        }

        Person person = villager.Person;
        if (nameText != null)
        {
            nameText.text = person != null && !string.IsNullOrWhiteSpace(person.name)
                ? person.name
                : "Unknown";
        }

        if (activityText != null)
        {
            activityText.text = villager.IsLookingForStreet
                ? "Looking for street"
                : GetActivityText(villager.CurrentBehavior);
        }
    }

    private string GetActivityText(VillagerBehaviorStage behavior)
    {
        if (behavior == null)
        {
            return "Unknown";
        }

        switch (behavior.mode)
        {
            case VillagerBehaviorMode.Idle:
                return "Idle";
            case VillagerBehaviorMode.Wander:
                return "Wandering";
            case VillagerBehaviorMode.Work:
                return "Working";
            case VillagerBehaviorMode.Building:
                return "Building";
            case VillagerBehaviorMode.Eat:
                return "Eating";
            case VillagerBehaviorMode.Sleeps:
                return "Sleeping";
            case VillagerBehaviorMode.EndingTask:
                return "Finishing task";
            case VillagerBehaviorMode.Alert:
                return "Alert";
            case VillagerBehaviorMode.Fighting:
                return "Fighting";
            case VillagerBehaviorMode.Fleeing:
                return "Fleeing";
            case VillagerBehaviorMode.Dying:
                return "Dying";
            case VillagerBehaviorMode.LookingForFood:
                return "Looking for food";
            case VillagerBehaviorMode.Foraging:
                return "Foraging";
            default:
                Debug.LogError("Villager behavior has an unsupported mode.", this);
                return "Activity: Unknown";
        }
    }
}
