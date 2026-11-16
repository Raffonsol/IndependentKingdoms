using UnityEngine;

public class DroppedLoot : MonoBehaviour
{
    private MapManager mapManager;
    private float expiresAt;
    private bool isCollected;

    public DroppedLootType Type { get; private set; }
    public ResourceType ResourceType { get; private set; }
    public VillagerItemType ItemType { get; private set; }
    public Villager PreferredCollector { get; private set; }

    public bool IsAvailable
    {
        get { return !isCollected && Time.time < expiresAt; }
    }

    internal bool Initialize(
        MapManager owner,
        DroppedLootType type,
        ResourceType resourceType,
        VillagerItemType itemType,
        Villager preferredCollector,
        float lifetimeSeconds)
    {
        if (owner == null
            || !System.Enum.IsDefined(typeof(DroppedLootType), type)
            || !IsFinitePositive(lifetimeSeconds)
            || (type == DroppedLootType.Resource
                && !System.Enum.IsDefined(typeof(ResourceType), resourceType))
            || (type == DroppedLootType.Item
                && !System.Enum.IsDefined(typeof(VillagerItemType), itemType)))
        {
            Debug.LogError("Dropped loot was initialized with invalid data.", this);
            return false;
        }

        mapManager = owner;
        Type = type;
        ResourceType = resourceType;
        ItemType = itemType;
        PreferredCollector = preferredCollector;
        expiresAt = Time.time + lifetimeSeconds;
        return true;
    }

    public bool TryCollect(Villager collector)
    {
        if (!IsAvailable || collector == null)
        {
            return false;
        }

        bool addedToInventory = Type == DroppedLootType.Resource
            ? collector.TryAddDroppedResource(ResourceType)
            : collector.AddInventoryItem(ItemType, 1);
        if (!addedToInventory)
        {
            Debug.LogError(
                "Could not add dropped " + Type + " to villager inventory.",
                collector);
            return false;
        }

        isCollected = true;
        if (mapManager != null)
        {
            mapManager.UnregisterDroppedLoot(this);
        }

        Destroy(gameObject);
        return true;
    }

    private void Update()
    {
        if (!isCollected && Time.time >= expiresAt)
        {
            if (mapManager != null)
            {
                mapManager.UnregisterDroppedLoot(this);
            }

            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (mapManager != null)
        {
            mapManager.UnregisterDroppedLoot(this);
        }
    }

    private static bool IsFinitePositive(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
