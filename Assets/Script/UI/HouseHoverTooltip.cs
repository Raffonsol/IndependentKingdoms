using System;
using System.Text;
using TMPro;
using UnityEngine;

public class HouseHoverTooltip : HoverTooltip
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text detailsText;

    private RectTransform rootRect;
    private RectTransform nameRect;
    private RectTransform detailsRect;
    private Vector2 originalRootSize;
    private Vector2 originalNamePosition;
    private Vector2 originalDetailsPosition;
    private Vector2 originalDetailsSize;

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

        rootRect = transform as RectTransform;
        nameRect = nameText.rectTransform;
        detailsRect = detailsText.rectTransform;
        originalRootSize = rootRect != null ? rootRect.sizeDelta : Vector2.zero;
        originalNamePosition = nameRect.anchoredPosition;
        originalDetailsPosition = detailsRect.anchoredPosition;
        originalDetailsSize = detailsRect.sizeDelta;
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

        bool hasStoredItems = false;
        foreach (VillagerItemType itemType in Enum.GetValues(typeof(VillagerItemType)))
        {
            int amount = house.GetStoredItemAmount(itemType);
            if (amount <= 0)
            {
                continue;
            }

            details.AppendLine();
            details.Append(itemType);
            details.Append(": ");
            details.Append(amount);
            hasStoredItems = true;
        }

        if (!hasStoredItems)
        {
            details.AppendLine();
            details.Append("Empty");
        }

        detailsText.text = details.ToString();
        ResizeForDetailsText();
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

    private void ResizeForDetailsText()
    {
        if (rootRect == null || nameRect == null || detailsRect == null)
        {
            return;
        }

        float detailsHeight = Mathf.Max(
            originalDetailsSize.y,
            detailsText.GetPreferredValues(
                detailsText.text,
                originalDetailsSize.x,
                0f).y);
        float addedHeight = detailsHeight - originalDetailsSize.y;
        rootRect.sizeDelta = new Vector2(
            originalRootSize.x,
            originalRootSize.y + addedHeight);
        nameRect.anchoredPosition = originalNamePosition + Vector2.up * (addedHeight * 0.5f);
        detailsRect.anchoredPosition =
            originalDetailsPosition + Vector2.down * (addedHeight * 0.5f);
        detailsRect.sizeDelta = new Vector2(originalDetailsSize.x, detailsHeight);
    }
}
