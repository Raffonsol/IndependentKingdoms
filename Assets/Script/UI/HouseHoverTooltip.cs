using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class HouseHoverTooltip : HoverTooltip
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text detailsText;

    private StorageTooltipIconRows storageIconRows;
    private readonly List<StorageTooltipIconEntry> storedItemRows =
        new List<StorageTooltipIconEntry>();
    private readonly Dictionary<VillagerItemType, Sprite> itemIconCache =
        new Dictionary<VillagerItemType, Sprite>();
    private bool loggedMissingIconManager;

    protected override void Awake()
    {
        base.Awake();

        if (nameText == null || detailsText == null)
        {
            Debug.LogError(
                "House tooltip requires both name and details TextMeshPro references.",
                this);
            return;
        }
    }

    public void SetHouse(HouseWorkplace house)
    {
        if (house == null)
        {
            Debug.LogError("Cannot display a tooltip for a null house.", this);
            return;
        }

        if (nameText != null)
        {
            nameText.text = "House";
        }

        if (detailsText == null)
        {
            return;
        }

        Villager resident = house.Resident;
        Villager worker = house.AssignedWorker != null ? house.AssignedWorker : resident;
        StringBuilder details = new StringBuilder();
        details.Append("Residents: ");
        details.Append(GetVillagerName(resident));
        details.AppendLine();
        details.Append("Work: ");
        if (worker == null || worker.JobAssignment == JobType.None)
        {
            details.Append("None");
        }
        else
        {
            details.Append(worker.JobAssignment);
            if (worker != resident)
            {
                details.Append(" (");
                details.Append(GetVillagerName(worker));
                details.Append(')');
            }
        }

        details.AppendLine();
        details.Append("Stored items: ");
        details.Append(house.StoredItemCount);
        details.Append(" / ");
        details.Append(house.Capacity);

        storedItemRows.Clear();
        bool hasStoredItems = false;
        foreach (VillagerItemType itemType in Enum.GetValues(typeof(VillagerItemType)))
        {
            int amount = house.GetStoredItemAmount(itemType);
            if (amount <= 0)
            {
                continue;
            }

            Sprite icon = GetItemIcon(itemType);
            storedItemRows.Add(new StorageTooltipIconEntry(
                itemType + ": " + amount,
                icon));
            hasStoredItems = true;
        }

        if (!hasStoredItems)
        {
            details.AppendLine();
            details.Append("Empty");
        }

        detailsText.text = details.ToString();
        ResizeForDetailsText(storedItemRows);
    }

    private Sprite GetItemIcon(VillagerItemType itemType)
    {
        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            if (!loggedMissingIconManager)
            {
                Debug.LogError(
                    "House storage icons require an active UIManager.",
                    this);
                loggedMissingIconManager = true;
            }

            return null;
        }

        if (!itemIconCache.TryGetValue(itemType, out Sprite icon))
        {
            icon = uiManager.GetItemIcon(itemType);
            itemIconCache.Add(itemType, icon);
        }

        return icon;
    }

    private static string GetVillagerName(Villager villager)
    {
        if (villager == null || villager.Person == null
            || string.IsNullOrWhiteSpace(villager.Person.name))
        {
            return "None";
        }

        return villager.Person.name;
    }

    private void ResizeForDetailsText(IList<StorageTooltipIconEntry> itemRows)
    {
        if (nameText == null || detailsText == null)
        {
            return;
        }

        float contentWidth = detailsText.rectTransform.sizeDelta.x;
        float summaryHeight = detailsText.GetPreferredValues(
            detailsText.text,
            contentWidth,
            0f).y;
        float rowHeight = StorageTooltipIconRows.GetRowHeight(detailsText);
        float rowsTopOffset = summaryHeight
            + (itemRows.Count > 0 ? StorageTooltipIconRows.RowGap : 0f);
        float contentHeight = rowsTopOffset + itemRows.Count * rowHeight;
        ResizeToFit(nameText, detailsText, contentHeight);

        if (storageIconRows == null)
        {
            storageIconRows = detailsText.GetComponent<StorageTooltipIconRows>();
            if (storageIconRows == null)
            {
                storageIconRows = detailsText.gameObject.AddComponent<StorageTooltipIconRows>();
            }
        }

        storageIconRows.SetRows(detailsText, itemRows, rowsTopOffset, rowHeight);
    }
}
