using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class ResourceHoverTooltip : HoverTooltip
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text amountText;

    private StorageTooltipIconRows storageIconRows;
    private readonly List<StorageTooltipIconEntry> communityResourceRows =
        new List<StorageTooltipIconEntry>();
    private readonly Dictionary<ResourceType, Sprite> resourceIconCache =
        new Dictionary<ResourceType, Sprite>();
    private bool loggedMissingIconManager;

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
            communityResourceRows.Clear();
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
            ResizeForAmountText(communityResourceRows);
        }
    }

    public void SetCommunityStorage(CommunityStorage storage)
    {
        if (storage == null)
        {
            Debug.LogError("Cannot display a tooltip for a null community storage.", this);
            return;
        }

        if (nameText != null)
        {
            nameText.text = "Community Storage";
        }

        if (amountText != null)
        {
            communityResourceRows.Clear();
            StringBuilder amountLines = new StringBuilder();
            foreach (ResourceType resourceType in Enum.GetValues(typeof(ResourceType)))
            {
                int amount = storage.GetAmount(resourceType);
                if (amount <= 0)
                {
                    continue;
                }

                communityResourceRows.Add(new StorageTooltipIconEntry(
                    resourceType + ": " + amount,
                    GetResourceIcon(resourceType)));
            }

            if (communityResourceRows.Count == 0)
            {
                amountLines.Append("Empty");
                amountLines.AppendLine();
            }

            amountLines.Append("Capacity: ");
            amountLines.Append(storage.GetTotalAmount());
            amountLines.Append(" / ");
            amountLines.Append(storage.Capacity);
            amountText.text = amountLines.ToString();
            ResizeForAmountText(communityResourceRows);
        }
    }

    private Sprite GetResourceIcon(ResourceType resourceType)
    {
        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            if (!loggedMissingIconManager)
            {
                Debug.LogError(
                    "Community storage icons require an active UIManager.",
                    this);
                loggedMissingIconManager = true;
            }

            return null;
        }

        if (!resourceIconCache.TryGetValue(resourceType, out Sprite icon))
        {
            icon = uiManager.GetResourceIcon(resourceType);
            resourceIconCache.Add(resourceType, icon);
        }

        return icon;
    }

    private void ResizeForAmountText(IList<StorageTooltipIconEntry> itemRows)
    {
        if (nameText == null || amountText == null)
        {
            return;
        }

        float contentWidth = amountText.rectTransform.sizeDelta.x;
        float summaryHeight = amountText.GetPreferredValues(
            amountText.text,
            contentWidth,
            0f).y;
        float rowHeight = StorageTooltipIconRows.GetRowHeight(amountText);
        float rowsTopOffset = summaryHeight
            + (itemRows.Count > 0 ? StorageTooltipIconRows.RowGap : 0f);
        float contentHeight = rowsTopOffset + itemRows.Count * rowHeight;
        ResizeToFit(nameText, amountText, contentHeight);

        if (storageIconRows == null)
        {
            storageIconRows = amountText.GetComponent<StorageTooltipIconRows>();
            if (storageIconRows == null)
            {
                storageIconRows = amountText.gameObject.AddComponent<StorageTooltipIconRows>();
            }
        }

        storageIconRows.SetRows(amountText, itemRows, rowsTopOffset, rowHeight);
    }
}
