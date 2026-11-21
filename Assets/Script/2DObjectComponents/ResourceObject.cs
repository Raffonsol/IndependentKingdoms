using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceObject : MonoBehaviour
{
    private sealed class ResourceStock
    {
        public int amount;
        public int maximumAmount;
        public int bareHandAmount;
        public int bareHandLimit;
        public int regenerationLimit;
        public float regenerationPerSecond;
        public float regenerationRemainder;
        public float bareHandShakeMagnitudeMultiplier;
        public float toolShakeMagnitudeMultiplier;
        public Vector2Int interactionOffset;
        public SpriteRenderer visualRenderer;
    }

    public Vector2Int GridCell { get; private set; }
    public IReadOnlyList<Vector2Int> OccupiedGridCells
    {
        get { return occupiedGridCells; }
    }

    public ResourceType PrimaryResourceType { get; private set; }

    public bool HasGridCell { get; private set; }
    public string DefinitionId { get; private set; }
    public int CurrentStage
    {
        get { return currentStage; }
    }

    public float GrowthElapsedSeconds
    {
        get { return growthElapsedSeconds; }
    }

    internal Transform VisualRoot
    {
        get { return visualRoot; }
    }

    private readonly Dictionary<ResourceType, ResourceStock> resourceStocks =
        new Dictionary<ResourceType, ResourceStock>();
    private readonly List<SpriteRenderer> stageVisualRenderers = new List<SpriteRenderer>();
    private readonly List<ResourceType> availableResourceTypes = new List<ResourceType>();
    private readonly List<Vector2Int> occupiedGridCells = new List<Vector2Int>();
    private ResourceType resourceType;
    private ResourceObjectDefinition definition;
    private Sprite visualSpriteOverride;
    private OutcropInstance outcropOwner;
    private string outcropChildPath;
    private int currentStage;
    private int nextVisualIndex;
    private float growthElapsedSeconds;
    private bool isConfigured;
    private bool canBeRemovedByConstruction;
    private Transform sortingAnchor;
    private Transform visualRoot;
    private UnityEngine.Rendering.SortingGroup sortingGroup;
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
    public OutcropInstance OutcropOwner
    {
        get { return outcropOwner; }
    }

    private void Awake()
    {
        sortingAnchor = transform.Find("SortingAnchor");
        if (sortingAnchor == null)
        {
            sortingAnchor = transform;
        }

        visualRoot = sortingAnchor.Find("Visuals");
        sortingGroup = sortingAnchor != null
            ? sortingAnchor.GetComponent<UnityEngine.Rendering.SortingGroup>()
            : null;
        if (visualRoot == null)
        {
            GameObject visualsObject = new GameObject("Visuals");
            visualRoot = visualsObject.transform;
            visualRoot.SetParent(sortingAnchor, false);
        }

        groundShadow = GetComponentInChildren<GroundShadow>(true);
        if (sortingGroup == null)
        {
            Debug.LogError(
                "Resource object prefab is missing a SortingGroup on its root or SortingAnchor.",
                this);
            return;
        }

        stageVisualRenderers.AddRange(visualRoot.GetComponentsInChildren<SpriteRenderer>(true));
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
        UpdateGrowth(Time.deltaTime);

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

    public bool Initialize(
        ResourceObjectDefinition definition,
        int stage = 0,
        float elapsedGrowthSeconds = 0f,
        Sprite visualSpriteOverride = null)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.id)
            || definition.stages == null
            || definition.initialStage < 1
            || definition.finalStage < definition.initialStage
            || definition.finalStage > definition.stages.Length)
        {
            Debug.LogError("Cannot initialize a resource object without a valid definition.", this);
            return false;
        }

        if (stage == 0)
        {
            stage = definition.finalStage;
        }

        if (stage < definition.initialStage
            || stage > definition.finalStage
            || definition.stages[stage - 1] == null
            || !IsValidGrowthElapsedTime(definition, stage, elapsedGrowthSeconds))
        {
            Debug.LogError(
                "Cannot initialize resource object '" + definition.id + "' at an invalid stage.",
                this);
            return false;
        }

        if (sortingAnchor == null || visualRoot == null)
        {
            sortingAnchor = transform.Find("SortingAnchor");
            if (sortingAnchor == null)
            {
                sortingAnchor = transform;
            }

            visualRoot = sortingAnchor.Find("Visuals");
            sortingGroup = sortingAnchor != null
                ? sortingAnchor.GetComponent<UnityEngine.Rendering.SortingGroup>()
                : null;
            if (visualRoot == null)
            {
                GameObject visualsObject = new GameObject("Visuals");
                visualRoot = visualsObject.transform;
                visualRoot.SetParent(sortingAnchor, false);
            }
        }

        if (sortingAnchor == null || visualRoot == null || sortingGroup == null)
        {
            Debug.LogError(
                "Resource object prefab is missing SortingAnchor/Visuals/SortingGroup.",
                this);
            return false;
        }

        ResourceObjectStageDefinition stageDefinition = definition.stages[stage - 1];
        if (stageDefinition.visuals == null
            || stageDefinition.outputs == null
            || stageDefinition.shadowSize.x <= 0f
            || stageDefinition.shadowSize.y <= 0f)
        {
            Debug.LogError(
                "Resource object '" + definition.id + "' has an invalid stage " + stage + ".",
                this);
            return false;
        }

        Vector2 sortingAnchorOffsetDelta = definition.sortingAnchorOffset - appliedSortingAnchorOffset;
        sortingAnchor.localPosition = sortingAnchorStartingPosition
            + new Vector3(definition.sortingAnchorOffset.x, definition.sortingAnchorOffset.y, 0f);
        sortingGroup.sortingLayerName = definition.sortingLayerName;
        visualRoot.localPosition -= new Vector3(
            sortingAnchorOffsetDelta.x,
            sortingAnchorOffsetDelta.y,
            0f);
        appliedSortingAnchorOffset = definition.sortingAnchorOffset;

        if (groundShadow == null)
        {
            groundShadow = GetComponentInChildren<GroundShadow>(true);
        }

        this.definition = definition;
        this.visualSpriteOverride = visualSpriteOverride;
        DefinitionId = definition.id;
        PrimaryResourceType = definition.primaryResourceTypeWasExplicitlySet
            ? definition.primaryResourceType
            : stageDefinition.outputs.Length > 0
                ? stageDefinition.outputs[0].type
                : default(ResourceType);
        if (!ConfigureStage(stage, elapsedGrowthSeconds))
        {
            Debug.LogError(
                "Resource object '" + definition.id
                + "' could not configure stage " + stage + " or its GroundShadow.",
                this);
            return false;
        }

        return isConfigured;
    }

    private void AddResourceStock(ResourceOutputDefinition definition)
    {
        int amount = definition.resourceRegenerationLimit > 0
            ? Mathf.Min(definition.fullCutAmount, definition.resourceRegenerationLimit)
            : definition.fullCutAmount;
        ResourceStock stock = new ResourceStock
        {
            amount = amount,
            maximumAmount = definition.resourceRegenerationLimit > 0
                ? definition.resourceRegenerationLimit
                : definition.fullCutAmount,
            bareHandAmount = Mathf.Min(definition.bareHandAmount, amount),
            bareHandLimit = definition.bareHandAmount,
            regenerationLimit = definition.resourceRegenerationLimit,
            regenerationPerSecond = definition.resourceRegenerationPerSecond,
            bareHandShakeMagnitudeMultiplier = Mathf.Max(0f, definition.bareHandShakeMagnitudeMultiplier),
            toolShakeMagnitudeMultiplier = Mathf.Max(0f, definition.toolShakeMagnitudeMultiplier),
            interactionOffset = definition.interactionOffset
        };
        if (definition.primaryResourceTypeWasExplicitlySet
            && definition.type == definition.primaryResourceType)
        {
            PrimaryResourceType = definition.type;
        }

        if (definition.visualSprite != null)
        {
            stock.visualRenderer = CreateVisualRenderer(
                definition.visualSprite,
                definition.visualColor,
                definition.visualSortingOrder,
                "OutputVisual_" + definition.type);
        }

        resourceStocks.Add(definition.type, stock);
        availableResourceTypes.Add(definition.type);
    }

    private void UpdateConfiguredResourceVisualState()
    {
        foreach (KeyValuePair<ResourceType, ResourceStock> entry in resourceStocks)
        {
            if (entry.Value.visualRenderer == null)
            {
                continue;
            }

            entry.Value.visualRenderer.gameObject.SetActive(entry.Value.amount > 0);
        }
    }

    private void UpdateResourceVisualState(ResourceType requestedType)
    {
        if (!resourceStocks.TryGetValue(requestedType, out ResourceStock stock)
            || stock.visualRenderer == null)
        {
            return;
        }

        stock.visualRenderer.gameObject.SetActive(stock.amount > 0);
    }

    private SpriteRenderer CreateVisualRenderer(
        Sprite sprite,
        Color color,
        int sortingOrder,
        string visualName)
    {
        SpriteRenderer renderer;
        if (nextVisualIndex >= stageVisualRenderers.Count)
        {
            renderer = nextVisualIndex == 0
                ? GetComponent<SpriteRenderer>()
                : null;
            if (renderer == null)
            {
                GameObject visualObject = new GameObject(visualName);
                visualObject.transform.SetParent(visualRoot, false);
                renderer = visualObject.AddComponent<SpriteRenderer>();
            }

            stageVisualRenderers.Add(renderer);
        }
        else
        {
            renderer = stageVisualRenderers[nextVisualIndex];
        }

        nextVisualIndex++;
        if (renderer.transform != transform)
        {
            renderer.gameObject.name = visualName;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
        }

        renderer.gameObject.SetActive(true);
        renderer.enabled = true;
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void ClearStageVisuals()
    {
        for (int visualIndex = 0; visualIndex < stageVisualRenderers.Count; visualIndex++)
        {
            SpriteRenderer renderer = stageVisualRenderers[visualIndex];
            if (renderer != null)
            {
                renderer.sprite = null;
                renderer.enabled = false;
            }
        }

        nextVisualIndex = 0;
    }

    private void UpdateGrowth(float deltaTime)
    {
        if (deltaTime <= 0f || currentStage >= definition.finalStage)
        {
            return;
        }

        growthElapsedSeconds += deltaTime;
        bool advanced = false;
        while (currentStage < definition.finalStage)
        {
            ResourceObjectStageDefinition stageDefinition = definition.stages[currentStage - 1];
            if (stageDefinition == null
                || growthElapsedSeconds < stageDefinition.secondsToNextStage)
            {
                break;
            }

            growthElapsedSeconds -= stageDefinition.secondsToNextStage;
            if (!ConfigureStage(currentStage + 1, growthElapsedSeconds))
            {
                Debug.LogError(
                    "Resource object '" + DefinitionId + "' could not advance to stage "
                    + (currentStage + 1) + ".",
                    this);
                return;
            }

            advanced = true;
        }

        if (currentStage >= definition.finalStage)
        {
            growthElapsedSeconds = 0f;
        }

        if (advanced)
        {
            MapManager mapManager = MapManager.Instance;
            if (mapManager != null)
            {
                mapManager.NotifyResourceAvailabilityChanged(this);
            }
        }
    }

    private bool ConfigureStage(int stage, float elapsedGrowthSeconds)
    {
        ResourceObjectStageDefinition stageDefinition = definition.stages[stage - 1];
        if (stageDefinition == null
            || stageDefinition.visuals == null
            || stageDefinition.outputs == null
            || stageDefinition.shadowSize.x <= 0f
            || stageDefinition.shadowSize.y <= 0f
            || (stageDefinition.hasShadow && groundShadow == null))
        {
            return false;
        }

        if (groundShadow != null)
        {
            groundShadow.gameObject.SetActive(stageDefinition.hasShadow);
        }

        if (stageDefinition.hasShadow
            && !groundShadow.Configure(stageDefinition.shadowSize, definition.shadowOffset))
        {
            return false;
        }

        isConfigured = false;
        currentStage = stage;
        growthElapsedSeconds = elapsedGrowthSeconds;
        resourceStocks.Clear();
        availableResourceTypes.Clear();
        ClearStageVisuals();
        for (int visualIndex = 0; visualIndex < stageDefinition.visuals.Length; visualIndex++)
        {
            ResourceObjectVisualDefinition visual = stageDefinition.visuals[visualIndex];
            if (visual == null
                || (visualSpriteOverride == null && visual.sprite == null))
            {
                return false;
            }

            CreateVisualRenderer(
                visualIndex == 0 && visualSpriteOverride != null
                    ? visualSpriteOverride
                    : visual.sprite,
                visual.color,
                visual.sortingOrder,
                "ResourceVisual_" + stage + "_" + visualIndex);
        }

        resourceType = stageDefinition.outputs.Length > 0
            ? stageDefinition.outputs[0].type
            : PrimaryResourceType;
        InteractionOffset = Vector2Int.zero;
        canBeRemovedByConstruction = true;
        foreach (ResourceOutputDefinition output in stageDefinition.outputs)
        {
            if (output == null
                || !Enum.IsDefined(typeof(ResourceType), output.type)
                || resourceStocks.ContainsKey(output.type))
            {
                return false;
            }

            if (output.bareHandAmount != output.fullCutAmount)
            {
                canBeRemovedByConstruction = false;
            }

            AddResourceStock(output);
            if (output.type == PrimaryResourceType || resourceStocks.Count == 1)
            {
                InteractionOffset = output.interactionOffset;
            }
        }

        startingLocalPosition = visualRoot.localPosition;
        UpdateConfiguredResourceVisualState();
        isConfigured = true;
        return true;
    }

    private static bool IsValidGrowthElapsedTime(
        ResourceObjectDefinition definition,
        int stage,
        float elapsedGrowthSeconds)
    {
        if (float.IsNaN(elapsedGrowthSeconds)
            || float.IsInfinity(elapsedGrowthSeconds)
            || elapsedGrowthSeconds < 0f)
        {
            return false;
        }

        if (stage == definition.finalStage)
        {
            return elapsedGrowthSeconds == 0f;
        }

        ResourceObjectStageDefinition stageDefinition = definition.stages[stage - 1];
        return stageDefinition != null
            && stageDefinition.secondsToNextStage > 0f
            && elapsedGrowthSeconds < stageDefinition.secondsToNextStage;
    }

    private void RegenerateResources(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        bool restoredGatherableResource = false;
        foreach (KeyValuePair<ResourceType, ResourceStock> stockEntry in resourceStocks)
        {
            ResourceStock stock = stockEntry.Value;
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

            UpdateResourceVisualState(stockEntry.Key);
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
        UpdateResourceVisualState(requestedType);
        return takenAmount;
    }

    public int GetAmount(ResourceType requestedType)
    {
        return resourceStocks.TryGetValue(requestedType, out ResourceStock stock)
            ? stock.amount
            : 0;
    }

    public int GetBareHandAmount(ResourceType requestedType)
    {
        return resourceStocks.TryGetValue(requestedType, out ResourceStock stock)
            ? stock.bareHandAmount
            : 0;
    }

    public bool CanBeFullyGatheredByBareHand(ResourceType requestedType)
    {
        return resourceStocks.TryGetValue(requestedType, out ResourceStock stock)
            && stock.bareHandLimit >= stock.maximumAmount;
    }

    public bool TryRestoreAmounts(ResourceType requestedType, int amount, int bareHandAmount)
    {
        if (amount < 0
            || bareHandAmount < 0
            || bareHandAmount > amount
            || !resourceStocks.TryGetValue(requestedType, out ResourceStock stock)
            || amount > stock.maximumAmount
            || bareHandAmount > stock.bareHandLimit)
        {
            return false;
        }

        stock.amount = amount;
        stock.bareHandAmount = bareHandAmount;
        stock.regenerationRemainder = 0f;
        UpdateResourceVisualState(requestedType);
        return true;
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

    public void PlayShake(ResourceType requestedType, bool hasTool, float duration, float magnitude, float frequency)
    {
        if (!resourceStocks.TryGetValue(requestedType, out ResourceStock stock))
        {
            PlayShake(duration, magnitude, frequency);
            return;
        }

        float magnitudeMultiplier = hasTool
            ? stock.toolShakeMagnitudeMultiplier
            : stock.bareHandShakeMagnitudeMultiplier;
        if (magnitudeMultiplier <= 0f)
        {
            magnitudeMultiplier = 1f;
        }

        PlayShake(duration, magnitude * magnitudeMultiplier, frequency);
    }

    public void SetGridCell(Vector2Int gridCell)
    {
        GridCell = gridCell;
        HasGridCell = true;
        occupiedGridCells.Clear();
        occupiedGridCells.Add(gridCell);
    }

    public bool TryGetOccupiedGridCells(
        float gridCellSize,
        out Vector2Int[] gridCells,
        Vector2Int? singleCellAnchor = null)
    {
        gridCells = null;
        if (gridCellSize <= 0f
            || float.IsNaN(gridCellSize)
            || float.IsInfinity(gridCellSize))
        {
            return false;
        }

        if (definition != null
            && definition.occupancyMode == ResourceObjectOccupancyMode.SingleCell)
        {
            Vector2Int cell = singleCellAnchor.HasValue
                ? singleCellAnchor.Value
                : HasGridCell
                    ? GridCell
                    : new Vector2Int(
                        Mathf.FloorToInt(transform.position.x / gridCellSize),
                        Mathf.FloorToInt(transform.position.y / gridCellSize));
            gridCells = new[] { cell };
            return true;
        }

        bool hasBounds = false;
        Bounds occupiedBounds = new Bounds();
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null
                || !renderer.enabled
                || !renderer.gameObject.activeInHierarchy
                || renderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                occupiedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                occupiedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            gridCells = new[]
            {
                new Vector2Int(
                    Mathf.FloorToInt(transform.position.x / gridCellSize),
                    Mathf.FloorToInt(transform.position.y / gridCellSize))
            };
            return true;
        }

        int minimumX = Mathf.FloorToInt(occupiedBounds.min.x / gridCellSize);
        int minimumY = Mathf.FloorToInt(occupiedBounds.min.y / gridCellSize);
        int maximumX = Mathf.CeilToInt(occupiedBounds.max.x / gridCellSize) - 1;
        int maximumY = Mathf.CeilToInt(occupiedBounds.max.y / gridCellSize) - 1;
        if (maximumX < minimumX || maximumY < minimumY)
        {
            return false;
        }

        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = minimumX; x <= maximumX; x++)
        {
            for (int y = minimumY; y <= maximumY; y++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }

        gridCells = cells.ToArray();
        return gridCells.Length > 0;
    }

    public void SetGridCells(Vector2Int anchorCell, IList<Vector2Int> gridCells)
    {
        GridCell = anchorCell;
        HasGridCell = true;
        occupiedGridCells.Clear();
        if (gridCells != null)
        {
            for (int index = 0; index < gridCells.Count; index++)
            {
                if (!occupiedGridCells.Contains(gridCells[index]))
                {
                    occupiedGridCells.Add(gridCells[index]);
                }
            }
        }

        if (occupiedGridCells.Count == 0)
        {
            occupiedGridCells.Add(anchorCell);
        }
    }

    internal void SetOutcropOwner(OutcropInstance owner, string childPath)
    {
        outcropOwner = owner;
        outcropChildPath = childPath;
    }

    internal string GetOutcropChildPath()
    {
        return outcropChildPath;
    }
}
