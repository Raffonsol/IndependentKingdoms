using System;
using System.Collections.Generic;
using UnityEngine;

public class CommunityStorage : MonoBehaviour
{
    private int[] resourceAmounts;

    public Vector2Int OriginCell { get; private set; }
    public Vector2Int Dimensions { get; private set; }
    public Vector2Int EntranceCell { get; private set; }
    public int Capacity { get; private set; }
    public bool IsInitialized
    {
        get { return resourceAmounts != null; }
    }

    public int RemainingCapacity
    {
        get { return Mathf.Max(0, Capacity - GetTotalAmount()); }
    }

    public bool Initialize(CommunityStorageRecord record, int capacity)
    {
        if (record == null
            || record.dimensions.x <= 0
            || record.dimensions.y <= 0
            || capacity <= 0)
        {
            Debug.LogError("Community storage requires valid dimensions and a positive capacity.", this);
            return false;
        }

        OriginCell = record.originCell;
        Dimensions = record.dimensions;
        EntranceCell = record.entranceCell;
        Capacity = capacity;
        resourceAmounts = new int[Enum.GetValues(typeof(ResourceType)).Length];
        return true;
    }

    public bool ContainsCell(Vector2Int cell)
    {
        return IsInitialized
            && cell.x >= OriginCell.x
            && cell.x < OriginCell.x + Dimensions.x
            && cell.y >= OriginCell.y
            && cell.y < OriginCell.y + Dimensions.y;
    }

    public int GetAmount(ResourceType resourceType)
    {
        if (!TryGetResourceIndex(resourceType, out int index))
        {
            return 0;
        }

        return resourceAmounts[index];
    }

    public int GetTotalAmount()
    {
        if (!IsInitialized)
        {
            return 0;
        }

        int total = 0;
        for (int index = 0; index < resourceAmounts.Length; index++)
        {
            if (GameData.Instance.IsCommunityStorageResource((ResourceType)index))
            {
                total += resourceAmounts[index];
            }
        }

        return total;
    }

    public int GetDepositAmount(IReadOnlyList<VillagerResourceEntry> inventory)
    {
        if (!IsInitialized || inventory == null)
        {
            return 0;
        }

        int carriedAmount = 0;
        foreach (VillagerResourceEntry entry in inventory)
        {
            if (entry != null
                && GameData.Instance.IsCommunityStorageResource(entry.resourceType))
            {
                carriedAmount += Mathf.Max(0, entry.amount);
            }
        }

        return Mathf.Min(carriedAmount, RemainingCapacity);
    }

    public bool TryDeposit(ResourceType resourceType, int requestedAmount, out int depositedAmount)
    {
        depositedAmount = 0;
        if (!IsInitialized
            || requestedAmount <= 0
            || !TryGetResourceIndex(resourceType, out int resourceIndex))
        {
            return false;
        }

        depositedAmount = Mathf.Min(requestedAmount, RemainingCapacity);
        resourceAmounts[resourceIndex] += depositedAmount;
        return depositedAmount > 0;
    }

    public bool TryWithdraw(ResourceType resourceType, int requestedAmount, out int withdrawnAmount)
    {
        withdrawnAmount = 0;
        if (!IsInitialized
            || requestedAmount <= 0
            || !TryGetResourceIndex(resourceType, out int resourceIndex))
        {
            return false;
        }

        withdrawnAmount = Mathf.Min(requestedAmount, resourceAmounts[resourceIndex]);
        resourceAmounts[resourceIndex] -= withdrawnAmount;
        return withdrawnAmount > 0;
    }

    private bool TryGetResourceIndex(ResourceType resourceType, out int resourceIndex)
    {
        resourceIndex = -1;
        if (!IsInitialized
            || !Enum.IsDefined(typeof(ResourceType), resourceType)
            || !GameData.Instance.IsCommunityStorageResource(resourceType))
        {
            return false;
        }

        resourceIndex = Convert.ToInt32(resourceType);
        return resourceIndex >= 0 && resourceIndex < resourceAmounts.Length;
    }
}
