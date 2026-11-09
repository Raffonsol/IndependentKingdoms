using System.Collections.Generic;
using UnityEngine;

public class VillagerStorageSelector : MonoBehaviour
{
    private Villager villager;

    public void Initialize(Villager owner)
    {
        if (owner == null)
        {
            Debug.LogError("VillagerStorageSelector requires an owning villager.", this);
            return;
        }

        villager = owner;
    }

    public bool TrySelectStorageForDeposit(
        IReadOnlyList<VillagerResourceEntry> inventory,
        out CommunityStorage storage)
    {
        storage = null;
        if (villager == null || inventory == null || MapManager.Instance == null)
        {
            return false;
        }

        MapManager mapManager = MapManager.Instance;
        float nearestDistanceSquared = float.PositiveInfinity;
        foreach (CommunityStorage candidate in mapManager.CommunityStorages)
        {
            if (candidate == null
                || !candidate.isActiveAndEnabled
                || candidate.GetDepositAmount(inventory) <= 0
                || !mapManager.TryGetCommunityStorageEntrancePosition(
                    candidate,
                    out Vector3 entrancePosition))
            {
                continue;
            }

            float distanceSquared =
                ((Vector2)villager.transform.position - (Vector2)entrancePosition).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                storage = candidate;
            }
        }

        return storage != null;
    }

    public bool TrySelectStorageForWithdrawal(
        ResourceType resourceType,
        out CommunityStorage storage)
    {
        storage = null;
        if (villager == null || MapManager.Instance == null)
        {
            return false;
        }

        MapManager mapManager = MapManager.Instance;
        float nearestDistanceSquared = float.PositiveInfinity;
        foreach (CommunityStorage candidate in mapManager.CommunityStorages)
        {
            if (candidate == null
                || !candidate.isActiveAndEnabled
                || candidate.GetAmount(resourceType) <= 0
                || !mapManager.TryGetCommunityStorageEntrancePosition(
                    candidate,
                    out Vector3 entrancePosition))
            {
                continue;
            }

            float distanceSquared =
                ((Vector2)villager.transform.position - (Vector2)entrancePosition).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                storage = candidate;
            }
        }

        return storage != null;
    }
}
