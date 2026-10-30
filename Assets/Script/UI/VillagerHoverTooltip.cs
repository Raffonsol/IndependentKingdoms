using UnityEngine.UI;
using TMPro;
using UnityEngine;

public class VillagerHoverTooltip : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text activityText;

    private RectTransform rectTransform;
    private RectTransform canvasRectTransform;
    private bool loggedPositionClamp;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRectTransform = canvas != null ? canvas.transform as RectTransform : null;

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            graphic.raycastTarget = false;
        }

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

    public bool TrySetScreenPosition(
        Canvas canvas,
        Vector2 screenPosition,
        UnityEngine.Camera eventCamera)
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (canvasRectTransform == null && canvas != null)
        {
            canvasRectTransform = canvas.transform as RectTransform;
        }

        if (rectTransform == null || canvasRectTransform == null)
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRectTransform,
                screenPosition,
                eventCamera,
                out Vector2 localPosition))
        {
            return false;
        }

        Rect canvasBounds = canvasRectTransform.rect;
        Vector2 tooltipSize = rectTransform.rect.size;
        Vector2 requestedLocalPosition = localPosition;
        bool placeBelowAnchor = localPosition.y + tooltipSize.y > canvasBounds.yMax;
        rectTransform.pivot = placeBelowAnchor
            ? new Vector2(0.5f, 1f)
            : new Vector2(0.5f, 0f);
        localPosition.x = Mathf.Clamp(
            localPosition.x,
            canvasBounds.xMin + tooltipSize.x * rectTransform.pivot.x,
            canvasBounds.xMax - tooltipSize.x * (1f - rectTransform.pivot.x));
        if (placeBelowAnchor)
        {
            localPosition.y = Mathf.Min(localPosition.y, canvasBounds.yMax);
        }

        if (!loggedPositionClamp
            && (localPosition - requestedLocalPosition).sqrMagnitude > 1f)
        {
            Debug.LogWarning(
                "The villager tooltip position is being adjusted to keep it inside the Canvas bounds.",
                this);
            loggedPositionClamp = true;
        }

        rectTransform.anchoredPosition = localPosition;
        return true;
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
            default:
                Debug.LogError("Villager behavior has an unsupported mode.", this);
                return "Activity: Unknown";
        }
    }
}
