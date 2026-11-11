using System;
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

    public Vector2Int GridCell { get; private set; }

    public ResourceType PrimaryResourceType { get; private set; }

    public bool HasGridCell { get; private set; }
    public string DefinitionId { get; private set; }
    internal Transform VisualRoot
    {
        get { return visualRoot; }
    }

    private readonly Dictionary<ResourceType, ResourceStock> resourceStocks =
        new Dictionary<ResourceType, ResourceStock>();
    private readonly List<ResourceType> availableResourceTypes = new List<ResourceType>();
    private ResourceType resourceType;
    private bool isConfigured;
    private bool canBeRemovedByConstruction;
    private Transform sortingAnchor;
    private Transform visualRoot;
    private GroundShadow groundShadow;
    private Vector3 sortingAnchorStartingPosition;
    private Vector3 startingLocalPosition;
    private Vector2 appliedSortingAnchorOffset;
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

    public bool CanBeRemovedByConstruction
    {
        get { return canBeRemovedByConstruction; }
    }

    public Vector2Int InteractionOffset { get; private set; }

    private void Awake()
    {
        sortingAnchor = transform.Find("SortingAnchor");
        visualRoot = sortingAnchor != null
            ? sortingAnchor.Find("Visuals")
            : null;
        groundShadow = GetComponentInChildren<GroundShadow>(true);
        if (sortingAnchor == null || visualRoot == null)
        {
            Debug.LogError("Resource object prefab is missing SortingAnchor/Visuals.", this);
            return;
        }

        sortingAnchorStartingPosition = sortingAnchor.localPosition;
        startingLocalPosition = visualRoot.localPosition;
    }

    private void Update()
    {
        if (!isConfigured)
        {
            return;
        }

        RegenerateResources(Time.deltaTime);

        if (shakeTimer > 0f)
        {
            shakeTimer = Mathf.Max(0f, shakeTimer - Time.deltaTime);
            if (shakeTimer <= 0f)
            {
                visualRoot.localPosition = startingLocalPosition;
                return;
            }

            float elapsed = activeShakeDuration - shakeTimer;
            float phase = elapsed * activeShakeFrequency * Mathf.PI * 2f;
            float envelope = shakeTimer / activeShakeDuration;
            Vector3 offset = new Vector3(
                Mathf.Sin(phase) * activeShakeMagnitude * envelope,
                Mathf.Sin(phase * 0.73f) * activeShakeMagnitude * 0.5f * envelope,
                0f);
            visualRoot.localPosition = startingLocalPosition + offset;
        }
    }

    public bool Initialize(ResourceObjectDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.id)
            || definition.outputs == null
            || definition.outputs.Length == 0
            || definition.outputs[0] == null
            || !Enum.IsDefined(typeof(ResourceType), definition.outputs[0].type))
        {
            Debug.LogError("Cannot initialize a resource object without a valid definition.", this);
            return false;
        }

        if (sortingAnchor == null || visualRoot == null)
        {
            sortingAnchor = transform.Find("SortingAnchor");
            visualRoot = sortingAnchor != null
                ? sortingAnchor.Find("Visuals")
                : null;
        }

        if (sortingAnchor == null || visualRoot == null)
        {
            Debug.LogError("Resource object prefab is missing SortingAnchor/Visuals.", this);
            return false;
        }

        Vector2 sortingAnchorOffsetDelta = definition.sortingAnchorOffset - appliedSortingAnchorOffset;
        sortingAnchor.localPosition = sortingAnchorStartingPosition
            + new Vector3(definition.sortingAnchorOffset.x, definition.sortingAnchorOffset.y, 0f);
        visualRoot.localPosition -= new Vector3(
            sortingAnchorOffsetDelta.x,
            sortingAnchorOffsetDelta.y,
            0f);
        appliedSortingAnchorOffset = definition.sortingAnchorOffset;

        if (groundShadow == null)
        {
            groundShadow = GetComponentInChildren<GroundShadow>(true);
        }

        if (groundShadow == null)
        {
            Debug.LogError("Resource object prefab is missing a valid GroundShadow child.", this);
            return false;
        }

        if (!groundShadow.Configure(definition.shadowSize, definition.shadowOffset))
        {
            return false;
        }

        isConfigured = false;
        resourceStocks.Clear();
        availableResourceTypes.Clear();
        DefinitionId = definition.id;
        resourceType = definition.outputs[0].type;
        PrimaryResourceType = definition.primaryResourceTypeWasExplicitlySet
            ? definition.primaryResourceType
            : definition.outputs[0].type;
        InteractionOffset = definition.outputs[0].interactionOffset;
        canBeRemovedByConstruction = true;
        foreach (ResourceOutputDefinition output in definition.outputs)
        {
            if (output == null
                || !Enum.IsDefined(typeof(ResourceType), output.type)
                || resourceStocks.ContainsKey(output.type))
            {
                Debug.LogError(
                    "Resource object definition '" + definition.id + "' has an invalid or duplicate output.",
                    this);
                resourceStocks.Clear();
                availableResourceTypes.Clear();
                return false;
            }

            if (output.bareHandAmount != output.fullCutAmount)
            {
                canBeRemovedByConstruction = false;
            }

            AddResourceStock(output);
        }

        startingLocalPosition = visualRoot.localPosition;
        isConfigured = true;
        return true;
    }

    private void AddResourceStock(ResourceOutputDefinition definition)
    {
        int amount = definition.resourceRegenerationLimit > 0
            ? Mathf.Min(definition.fullCutAmount, definition.resourceRegenerationLimit)
            : definition.fullCutAmount;
        ResourceStock stock = new ResourceStock
        {
            amount = amount,
            bareHandAmount = Mathf.Min(definition.bareHandAmount, amount),
            bareHandLimit = definition.bareHandAmount,
            regenerationLimit = definition.resourceRegenerationLimit,
            regenerationPerSecond = definition.resourceRegenerationPerSecond,
            interactionOffset = definition.interactionOffset
        };
        if (definition.primaryResourceTypeWasExplicitlySet
            && definition.type == definition.primaryResourceType)
        {
            PrimaryResourceType = definition.type;
        }
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

    public bool IsPrimaryResourceDepleted
    {
        get
        {
            return resourceStocks.TryGetValue(PrimaryResourceType, out ResourceStock stock)
                && stock.amount <= 0;
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
