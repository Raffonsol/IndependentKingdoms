using System.Text;
using TMPro;
using UnityEngine;

public class ResourceHoverTooltip : HoverTooltip
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;

    private RectTransform rootRect;
    private RectTransform nameRect;
    private RectTransform amountRect;
    private Vector2 originalRootSize;
    private Vector2 originalNamePosition;
    private Vector2 originalAmountPosition;
    private Vector2 originalAmountSize;

    protected override void Awake()
    {
        base.Awake();

        if (nameText == null || amountText == null)
        {
            Debug.LogError(
                "Resource tooltip requires both name and amount TextMeshPro references.",
                this);
            return;
        }

        rootRect = transform as RectTransform;
        nameRect = nameText.rectTransform;
        amountRect = amountText.rectTransform;
        originalRootSize = rootRect != null ? rootRect.sizeDelta : Vector2.zero;
        originalNamePosition = nameRect.anchoredPosition;
        originalAmountPosition = amountRect.anchoredPosition;
        originalAmountSize = amountRect.sizeDelta;
    }

    public void SetResource(ResourceObject resourceObject)
    {
        if (resourceObject == null)
        {
            Debug.LogError("Cannot display a tooltip for a null resource.", this);
            return;
        }

        if (nameText != null)
        {
            nameText.text = resourceObject.Type.ToString();
        }

        if (amountText != null)
        {
            StringBuilder amountLines = new StringBuilder();
            foreach (ResourceType resourceType in resourceObject.AvailableResourceTypes)
            {
                if (amountLines.Length > 0)
                {
                    amountLines.AppendLine();
                }

                amountLines.Append(resourceType);
                amountLines.Append(": ");
                amountLines.Append(resourceObject.GetAmount(resourceType));
                amountLines.Append(" remaining");
            }

            amountText.text = amountLines.ToString();
            ResizeForAmountText();
        }
    }

    private void ResizeForAmountText()
    {
        if (rootRect == null || nameRect == null || amountRect == null)
        {
            return;
        }

        float preferredHeight = amountText.GetPreferredValues(
            amountText.text,
            originalAmountSize.x,
            0f).y;
        float amountHeight = Mathf.Max(originalAmountSize.y, preferredHeight);
        float addedHeight = amountHeight - originalAmountSize.y;
        rootRect.sizeDelta = new Vector2(
            originalRootSize.x,
            originalRootSize.y + addedHeight);
        nameRect.anchoredPosition = originalNamePosition + Vector2.up * (addedHeight * 0.5f);
        amountRect.anchoredPosition = originalAmountPosition + Vector2.down * (addedHeight * 0.5f);
        amountRect.sizeDelta = new Vector2(originalAmountSize.x, amountHeight);
    }
}
