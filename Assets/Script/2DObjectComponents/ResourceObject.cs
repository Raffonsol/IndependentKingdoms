using System.Collections.Generic;
using UnityEngine;

public class ResourceObject : MonoBehaviour
{
    private sealed class ResourceStock
    {
        public int amount;
        public int bareHandAmount;
        public int bareHandLimit;
        public int regenerationLimit;
        public float regenerationPerSecond;
        public float regenerationRemainder;
        public Vector2Int interactionOffset;
    }

    [SerializeField] private ResourceType resourceType = ResourceType.Wood;

    public Vector2Int GridCell { get; private set; }

    public bool HasGridCell { get; private set; }

    private readonly Dictionary<ResourceType, ResourceStock> resourceStocks =
        new Dictionary<ResourceType, ResourceStock>();
    private readonly List<ResourceType> availableResourceTypes = new List<ResourceType>();
    private Vector3 startingLocalPosition;
    private float shakeTimer;
    private float activeShakeDuration;
    private float activeShakeMagnitude;
    private float activeShakeFrequency;

    public ResourceType Type
    {
        get { return resourceType; }
    }

    public int Amount
    {
        get { return GetAmount(resourceType); }
    }

    public int FoodAmount
    {
        get { return GetAmount(ResourceType.Food); }
    }

    public IReadOnlyList<ResourceType> AvailableResourceTypes
    {
        get { return availableResourceTypes; }
    }

    public bool CanRegenerateAnyResource
    {
        get
        {
            foreach (ResourceStock stock in resourceStocks.Values)
            {
                if (stock.regenerationPerSecond > 0f && stock.regenerationLimit > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public Vector2Int InteractionOffset { get; private set; }

    private void Awake()
    {
        GameData gameData = GameData.Instance;
        if (gameData == null
            || !gameData.TryGetResourceDefinition(resourceType, out ResourceDefinition definition))
        {
            Debug.LogError("Could not load harvest amounts for resource type " + resourceType + ".", this);
            return;
        }

        AddResourceStock(definition);
        InteractionOffset = definition.interactionOffset;
        foreach (ResourceDefinition resourceDefinition in gameData.resourceDefinitions)
        {
            if (resourceDefinition != null
                && resourceDefinition.type != resourceType
                && resourceDefinition.treeRegenerationLimit > 0)
            {
                AddResourceStock(resourceDefinition);
            }
        }

        startingLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        RegenerateResources(Time.deltaTime);

        if (shakeTimer > 0f)
        {
            shakeTimer = Mathf.Max(0f, shakeTimer - Time.deltaTime);
            if (shakeTimer <= 0f)
            {
                transform.localPosition = startingLocalPosition;
                return;
            }

            float elapsed = activeShakeDuration - shakeTimer;
            float phase = elapsed * activeShakeFrequency * Mathf.PI * 2f;
            float envelope = shakeTimer / activeShakeDuration;
            Vector3 offset = new Vector3(
                Mathf.Sin(phase) * activeShakeMagnitude * envelope,
                Mathf.Sin(phase * 0.73f) * activeShakeMagnitude * 0.5f * envelope,
                0f);
            transform.localPosition = startingLocalPosition + offset;
        }
    }

    private void AddResourceStock(ResourceDefinition definition)
    {
        int amount = definition.treeRegenerationLimit > 0
            ? Mathf.Min(definition.fullCutAmount, definition.treeRegenerationLimit)
            : definition.fullCutAmount;
        ResourceStock stock = new ResourceStock
        {
            amount = amount,
            bareHandAmount = Mathf.Min(definition.bareHandAmount, amount),
            bareHandLimit = definition.bareHandAmount,
            regenerationLimit = definition.treeRegenerationLimit,
            regenerationPerSecond = definition.treeRegenerationPerSecond,
            interactionOffset = definition.interactionOffset
        };
        resourceStocks.Add(definition.type, stock);
        availableResourceTypes.Add(definition.type);
    }

    private void RegenerateResources(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        bool restoredGatherableResource = false;
        foreach (ResourceStock stock in resourceStocks.Values)
        {
            if (stock.regenerationPerSecond <= 0f
                || stock.regenerationLimit <= 0
                || stock.amount >= stock.regenerationLimit)
            {
                stock.regenerationRemainder = 0f;
                continue;
            }

            stock.regenerationRemainder += stock.regenerationPerSecond * deltaTime;
            int amountToRegenerate = Mathf.FloorToInt(stock.regenerationRemainder);
            if (amountToRegenerate <= 0)
            {
                continue;
            }

            int previousAmount = stock.amount;
            int previousBareHandAmount = stock.bareHandAmount;
            stock.amount = Mathf.Min(
                stock.regenerationLimit,
                stock.amount + amountToRegenerate);
            int regeneratedAmount = stock.amount - previousAmount;
            stock.bareHandAmount = Mathf.Min(
                stock.bareHandLimit,
                stock.bareHandAmount + regeneratedAmount);
            stock.regenerationRemainder = stock.amount >= stock.regenerationLimit
                ? 0f
                : stock.regenerationRemainder - amountToRegenerate;
            if ((previousAmount <= 0 && stock.amount > 0)
                || (previousBareHandAmount <= 0 && stock.bareHandAmount > 0))
            {
                restoredGatherableResource = true;
            }
        }

        if (restoredGatherableResource)
        {
            MapManager mapManager = MapManager.Instance;
            if (mapManager != null)
            {
                mapManager.NotifyResourceAvailabilityChanged(this);
            }
        }
    }

    public bool CanGather(bool hasAxe)
    {
        return CanGather(resourceType, hasAxe);
    }

    public bool CanGather(ResourceType requestedType, bool hasAxe)
    {
        return resourceStocks.TryGetValue(requestedType, out ResourceStock stock)
            && stock.amount > 0
            && (hasAxe || stock.bareHandAmount > 0);
    }

    public int Take(int requestedAmount, bool hasAxe)
    {
        return Take(resourceType, requestedAmount, hasAxe);
    }

    public int Take(ResourceType requestedType, int requestedAmount, bool hasAxe)
    {
        if (requestedAmount <= 0
            || !resourceStocks.TryGetValue(requestedType, out ResourceStock stock)
            || !CanGather(requestedType, hasAxe))
        {
            return 0;
        }

        int maximumAmount = hasAxe ? stock.amount : stock.bareHandAmount;
        int takenAmount = Mathf.Min(requestedAmount, maximumAmount);
        stock.amount -= takenAmount;
        stock.bareHandAmount = Mathf.Max(0, stock.bareHandAmount - takenAmount);
        return takenAmount;
    }

    public int GetAmount(ResourceType requestedType)
    {
        return resourceStocks.TryGetValue(requestedType, out ResourceStock stock)
            ? stock.amount
            : 0;
    }

    public Vector2Int GetInteractionOffset(ResourceType requestedType)
    {
        return resourceStocks.TryGetValue(requestedType, out ResourceStock stock)
            ? stock.interactionOffset
            : Vector2Int.zero;
    }

    public bool HasAnyResources
    {
        get
        {
            foreach (ResourceStock stock in resourceStocks.Values)
            {
                if (stock.amount > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public void PlayShake(float duration, float magnitude, float frequency)
    {
        if (duration <= 0f || magnitude < 0f || frequency <= 0f)
        {
            return;
        }

        activeShakeDuration = duration;
        activeShakeMagnitude = magnitude;
        activeShakeFrequency = frequency;
        shakeTimer = duration;
    }

    public void SetGridCell(Vector2Int gridCell)
    {
        GridCell = gridCell;
        HasGridCell = true;
    }
}
