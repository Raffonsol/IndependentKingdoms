using UnityEngine;
using UnityEngine.UI;
using TMPro;

public abstract class HoverTooltip : MonoBehaviour
{
    private RectTransform rectTransform;
    private RectTransform canvasRectTransform;
    private RectTransform measuredTitleRect;
    private RectTransform measuredContentRect;
    private Vector2 originalRootSize;
    private Vector2 originalRootPivot;
    private Vector2 originalTitlePosition;
    private Vector2 originalTitleSize;
    private float titleTopInset;
    private float titleContentGap;
    private float contentBottomInset;
    private Vector2 originalContentSize;
    private bool hasResizeBaseline;
    private bool loggedPositionClamp;
    private bool loggedInvalidResizeRequest;

    protected virtual void Awake()
    {
        rectTransform = transform as RectTransform;
        originalRootSize = rectTransform != null ? rectTransform.sizeDelta : Vector2.zero;
        originalRootPivot = rectTransform != null ? rectTransform.pivot : Vector2.zero;
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRectTransform = canvas != null ? canvas.transform as RectTransform : null;

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            graphic.raycastTarget = false;
        }
    }

    protected void ResizeToFit(
        TMP_Text titleText,
        TMP_Text contentText,
        float minimumContentHeight = 0f)
    {
        if (rectTransform == null
            || titleText == null
            || contentText == null
            || titleText.rectTransform == contentText.rectTransform)
        {
            if (!loggedInvalidResizeRequest)
            {
                Debug.LogError(
                    "Hover tooltips require distinct title and content TextMeshPro elements.",
                    this);
                loggedInvalidResizeRequest = true;
            }

            return;
        }

        RectTransform titleRect = titleText.rectTransform;
        RectTransform contentRect = contentText.rectTransform;
        if (!hasResizeBaseline)
        {
            measuredTitleRect = titleRect;
            measuredContentRect = contentRect;
            originalTitlePosition = titleRect.anchoredPosition;
            originalTitleSize = titleRect.sizeDelta;
            originalContentSize = contentRect.sizeDelta;
            float rootTop = originalRootSize.y * (1f - originalRootPivot.y);
            float rootBottom = -originalRootSize.y * originalRootPivot.y;
            float titlePivotY = GetOriginalChildPivotY(titleRect);
            float contentPivotY = GetOriginalChildPivotY(contentRect);
            float titleTop = titlePivotY + (1f - titleRect.pivot.y) * titleRect.rect.height;
            float titleBottom = titlePivotY - titleRect.pivot.y * titleRect.rect.height;
            float contentTop = contentPivotY + (1f - contentRect.pivot.y) * contentRect.rect.height;
            float contentBottom = contentPivotY - contentRect.pivot.y * contentRect.rect.height;
            titleTopInset = Mathf.Max(0f, rootTop - titleTop);
            titleContentGap = Mathf.Max(0f, titleBottom - contentTop);
            contentBottomInset = Mathf.Max(0f, contentBottom - rootBottom);
            hasResizeBaseline = true;
        }
        else if (measuredTitleRect != titleRect || measuredContentRect != contentRect)
        {
            if (!loggedInvalidResizeRequest)
            {
                Debug.LogError(
                    "A hover tooltip cannot resize against changing title or content elements.",
                    this);
                loggedInvalidResizeRequest = true;
            }

            return;
        }

        float titleHeight = Mathf.Max(
            originalTitleSize.y,
            titleText.GetPreferredValues(titleText.text, originalTitleSize.x, 0f).y);
        float contentHeight = Mathf.Max(
            originalContentSize.y,
            minimumContentHeight,
            contentText.GetPreferredValues(contentText.text, originalContentSize.x, 0f).y);
        float rootHeight = Mathf.Max(
            originalRootSize.y,
            titleTopInset
                + titleHeight
                + titleContentGap
                + contentHeight
                + contentBottomInset);

        rectTransform.sizeDelta = new Vector2(
            originalRootSize.x,
            rootHeight);
        titleRect.anchorMin = new Vector2(titleRect.anchorMin.x, 1f);
        titleRect.anchorMax = new Vector2(titleRect.anchorMax.x, 1f);
        titleRect.pivot = new Vector2(titleRect.pivot.x, 1f);
        titleRect.sizeDelta = new Vector2(originalTitleSize.x, titleHeight);
        titleRect.anchoredPosition = new Vector2(originalTitlePosition.x, -titleTopInset);

        contentRect.anchorMin = new Vector2(contentRect.anchorMin.x, 1f);
        contentRect.anchorMax = new Vector2(contentRect.anchorMax.x, 1f);
        contentRect.pivot = new Vector2(contentRect.pivot.x, 1f);
        contentRect.sizeDelta = new Vector2(originalContentSize.x, contentHeight);
        contentRect.anchoredPosition = new Vector2(
            measuredContentRect.anchoredPosition.x,
            -(titleTopInset + titleHeight + titleContentGap));
    }

    private float GetOriginalChildPivotY(RectTransform childRect)
    {
        float parentBottom = -originalRootSize.y * originalRootPivot.y;
        float anchorRatio = Mathf.Lerp(
            childRect.anchorMin.y,
            childRect.anchorMax.y,
            childRect.pivot.y);
        return parentBottom
            + originalRootSize.y * anchorRatio
            + childRect.anchoredPosition.y;
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
