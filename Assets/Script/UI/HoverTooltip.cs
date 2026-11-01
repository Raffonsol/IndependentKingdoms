using UnityEngine;
using UnityEngine.UI;

public abstract class HoverTooltip : MonoBehaviour
{
    private RectTransform rectTransform;
    private RectTransform canvasRectTransform;
    private bool loggedPositionClamp;

    protected virtual void Awake()
    {
        rectTransform = transform as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRectTransform = canvas != null ? canvas.transform as RectTransform : null;

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            graphic.raycastTarget = false;
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
                "The hover tooltip position is being adjusted to keep it inside the Canvas bounds.",
                this);
            loggedPositionClamp = true;
        }

        rectTransform.anchoredPosition = localPosition;
        return true;
    }
}
