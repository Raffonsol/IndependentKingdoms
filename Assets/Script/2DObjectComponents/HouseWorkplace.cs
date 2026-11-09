using System.Collections.Generic;
using UnityEngine;

public class HouseWorkplace : MonoBehaviour
{
    private readonly List<VillagerInventoryEntry> storedItems =
        new List<VillagerInventoryEntry>();
    private Villager assignedWorker;

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

            withdrawnAmount = Mathf.Min(requestedAmount, Mathf.Max(0, entry.amount));
            entry.amount -= withdrawnAmount;
            if (entry.amount <= 0)
            {
                storedItems.RemoveAt(index);
            }

            return withdrawnAmount > 0;
        }

        return false;
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
}
