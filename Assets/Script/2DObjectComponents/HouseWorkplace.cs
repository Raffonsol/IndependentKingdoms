using System.Collections.Generic;
using UnityEngine;

public class HouseWorkplace : MonoBehaviour
{
    private readonly List<VillagerInventoryEntry> storedItems =
        new List<VillagerInventoryEntry>();
    private readonly Dictionary<Villager, VillagerItemType> reservedStoredItems =
        new Dictionary<Villager, VillagerItemType>();
    private Villager assignedWorker;
    private HouseProductionProgressBar productionProgressBar;
    private bool hasProductionProgress;
    private VillagerItemType productionItemType;
    private float productionProgressSeconds;
    private float productionDurationSeconds;
    private bool productionProgressBarSetupAttempted;
    private bool loggedMissingProductionProgressBar;

    public int Capacity { get; private set; }
    public bool IsInitialized
    {
        get { return Capacity > 0; }
    }

    public Villager Resident { get; private set; }
    public Villager AssignedWorker
    {
        get { return assignedWorker; }
    }

    public int StoredItemCount
    {
        get
        {
            int total = 0;
            foreach (VillagerInventoryEntry entry in storedItems)
            {
                total += entry != null ? Mathf.Max(0, entry.amount) : 0;
            }

            return total;
        }
    }

    public int RemainingCapacity
    {
        get { return Mathf.Max(0, Capacity - StoredItemCount); }
    }

    public bool IsFull
    {
        get { return !IsInitialized || RemainingCapacity == 0; }
    }

    public bool HasAnnex
    {
        get
        {
            HouseVisual houseVisual = GetComponent<HouseVisual>();
            return houseVisual != null && houseVisual.HasAnnex;
        }
    }

    public bool HasProductionProgress
    {
        get { return hasProductionProgress; }
    }

    public VillagerItemType ProductionItemType
    {
        get { return productionItemType; }
    }

    public float ProductionProgress
    {
        get
        {
            return hasProductionProgress && productionDurationSeconds > 0f
                ? Mathf.Clamp01(productionProgressSeconds / productionDurationSeconds)
                : 0f;
        }
    }

    public bool TryReserve(Villager worker)
    {
        if (worker == null || (assignedWorker != null && assignedWorker != worker))
        {
            return false;
        }

        assignedWorker = worker;
        return true;
    }

    public bool Initialize(int capacity)
    {
        if (capacity <= 0)
        {
            Debug.LogError("House storage capacity must be positive.", this);
            return false;
        }

        if (IsInitialized)
        {
            if (Capacity != capacity)
            {
                Debug.LogError("House storage cannot be reinitialized with a different capacity.", this);
                return false;
            }

            return true;
        }

        Capacity = capacity;
        return true;
    }

    public bool ConfigureProductionProgressBar(GameObject prefab)
    {
        if (productionProgressBar != null)
        {
            return true;
        }

        if (prefab == null || productionProgressBarSetupAttempted)
        {
            return false;
        }

        productionProgressBarSetupAttempted = true;
        Bounds visualBounds = new Bounds(transform.position, Vector3.zero);
        bool foundRenderer = false;
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!foundRenderer)
            {
                visualBounds = renderer.bounds;
                foundRenderer = true;
            }
            else
            {
                visualBounds.Encapsulate(renderer.bounds);
            }
        }

        GameObject barObject = Instantiate(
            prefab,
            transform.position,
            Quaternion.identity,
            transform);
        productionProgressBar = barObject.GetComponent<HouseProductionProgressBar>();
        if (productionProgressBar == null)
        {
            Debug.LogError(
                "House production progress prefab is missing HouseProductionProgressBar.",
                barObject);
            Destroy(barObject);
            return false;
        }

        Vector3 barPosition = transform.position;
        barPosition.y = foundRenderer ? visualBounds.max.y : transform.position.y;
        Vector3 offset = GameData.Instance.houseSettings.productionProgressBarOffsetFromTop;
        barObject.transform.position = barPosition + offset;

        if (!productionProgressBar.Initialize(this))
        {
            productionProgressBar = null;
            Destroy(barObject);
            return false;
        }

        return true;
    }

    public void AdvanceProduction(
        VillagerItemType itemType,
        float durationSeconds,
        float deltaTime)
    {
        if (!System.Enum.IsDefined(typeof(VillagerItemType), itemType)
            || durationSeconds <= 0f
            || deltaTime < 0f)
        {
            Debug.LogError("Cannot advance house production with invalid progress settings.", this);
            return;
        }

        if (!hasProductionProgress
            || productionItemType != itemType
            || !Mathf.Approximately(productionDurationSeconds, durationSeconds))
        {
            hasProductionProgress = true;
            productionItemType = itemType;
            productionProgressSeconds = 0f;
            productionDurationSeconds = durationSeconds;
        }

        if (productionProgressBar == null && !loggedMissingProductionProgressBar)
        {
            Debug.LogError(
                "House production requires a configured progress bar prefab.",
                this);
            loggedMissingProductionProgressBar = true;
        }

        productionProgressSeconds = Mathf.Min(
            productionDurationSeconds,
            productionProgressSeconds + deltaTime);
    }

    public bool IsProductionComplete(VillagerItemType itemType)
    {
        return hasProductionProgress
            && productionItemType == itemType
            && productionProgressSeconds >= productionDurationSeconds;
    }

    public void CompleteProduction(VillagerItemType itemType)
    {
        if (!hasProductionProgress || productionItemType != itemType)
        {
            return;
        }

        hasProductionProgress = false;
        productionProgressSeconds = 0f;
        productionDurationSeconds = 0f;
    }

    public bool TrySetResident(Villager resident)
    {
        if (resident == null || (Resident != null && Resident != resident))
        {
            return false;
        }

        Resident = resident;
        return true;
    }

    public void ReleaseResident(Villager resident)
    {
        if (Resident == resident)
        {
            Resident = null;
        }
    }

    public void Release(Villager worker)
    {
        if (assignedWorker == worker)
        {
            assignedWorker = null;
        }
    }

    public bool TryStoreItem(VillagerItemType itemType, int amount)
    {
        if (!CanStoreItem(itemType, amount))
        {
            return false;
        }

        foreach (VillagerInventoryEntry entry in storedItems)
        {
            if (entry.itemType == itemType)
            {
                entry.amount += amount;
                return true;
            }
        }

        storedItems.Add(new VillagerInventoryEntry
        {
            itemType = itemType,
            amount = amount
        });
        return true;
    }

    public bool TryReserveStoredItem(Villager villager, VillagerItemType itemType)
    {
        if (villager == null
            || !System.Enum.IsDefined(typeof(VillagerItemType), itemType))
        {
            return false;
        }

        if (reservedStoredItems.TryGetValue(villager, out VillagerItemType reservedItem))
        {
            return reservedItem == itemType;
        }

        if (GetStoredItemAmount(itemType) - GetReservedStoredItemAmount(itemType) <= 0)
        {
            return false;
        }

        reservedStoredItems.Add(villager, itemType);
        return true;
    }

    public bool TryWithdrawReservedItem(
        Villager villager,
        out VillagerItemType itemType)
    {
        itemType = default(VillagerItemType);
        if (villager == null
            || !reservedStoredItems.TryGetValue(villager, out itemType))
        {
            return false;
        }

        reservedStoredItems.Remove(villager);
        return RemoveStoredItem(itemType, 1);
    }

    public void ReleaseStoredItemReservation(Villager villager)
    {
        if (villager != null)
        {
            reservedStoredItems.Remove(villager);
        }
    }

    public bool CanStoreItem(VillagerItemType itemType, int amount)
    {
        return IsInitialized
            && System.Enum.IsDefined(typeof(VillagerItemType), itemType)
            && amount > 0
            && amount <= RemainingCapacity;
    }

    public bool TryWithdrawStoredItem(
        VillagerItemType itemType,
        int requestedAmount,
        out int withdrawnAmount)
    {
        withdrawnAmount = 0;
        if (!System.Enum.IsDefined(typeof(VillagerItemType), itemType)
            || requestedAmount <= 0)
        {
            return false;
        }

        for (int index = 0; index < storedItems.Count; index++)
        {
            VillagerInventoryEntry entry = storedItems[index];
            if (entry == null || entry.itemType != itemType)
            {
                continue;
            }

            int availableAmount = Mathf.Max(
                0,
                entry.amount - GetReservedStoredItemAmount(itemType));
            withdrawnAmount = Mathf.Min(requestedAmount, availableAmount);
            if (withdrawnAmount <= 0)
            {
                return false;
            }

            entry.amount -= withdrawnAmount;
            if (entry.amount <= 0)
            {
                storedItems.RemoveAt(index);
            }

            return withdrawnAmount > 0;
        }

        return false;
    }

    private bool RemoveStoredItem(VillagerItemType itemType, int amount)
    {
        for (int index = 0; index < storedItems.Count; index++)
        {
            VillagerInventoryEntry entry = storedItems[index];
            if (entry == null || entry.itemType != itemType || entry.amount < amount)
            {
                continue;
            }

            entry.amount -= amount;
            if (entry.amount <= 0)
            {
                storedItems.RemoveAt(index);
            }

            return true;
        }

        return false;
    }

    private int GetReservedStoredItemAmount(VillagerItemType itemType)
    {
        int reservedAmount = 0;
        foreach (VillagerItemType reservedItem in reservedStoredItems.Values)
        {
            if (reservedItem == itemType)
            {
                reservedAmount++;
            }
        }

        return reservedAmount;
    }

    public int GetStoredItemAmount(VillagerItemType itemType)
    {
        foreach (VillagerInventoryEntry entry in storedItems)
        {
            if (entry.itemType == itemType)
            {
                return entry.amount;
            }
        }

        return 0;
    }

    public int GetAvailableStoredItemAmount(VillagerItemType itemType)
    {
        return Mathf.Max(
            0,
            GetStoredItemAmount(itemType) - GetReservedStoredItemAmount(itemType));
    }

    private void OnDestroy()
    {
        reservedStoredItems.Clear();
    }
}
