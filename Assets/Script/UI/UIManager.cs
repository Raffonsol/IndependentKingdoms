using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    private static UIManager instance;

    public static UIManager Instance
    {
        get { return instance; }
    }

    [SerializeField] private GameObject[] buildingPrefabs;
    [SerializeField] private GameObject[] panels;
    [SerializeField] private GameObject topPanel;
    [SerializeField] private GameObject bottomPanel;
    [SerializeField] private Sprite[] resourceIcons;
    [SerializeField] private Texture2D defaultCursorImage;
    [SerializeField] private Texture2D roadBuildCursorImage;
    [SerializeField] private GameObject roadEndpointPrefab;
    [SerializeField] private LineRenderer roadPreviewPrefab;
    [SerializeField] private Sprite villagerOffscreenIndicatorSprite;
    [SerializeField] private VillagerHoverTooltip villagerHoverTooltipPrefab;
    [SerializeField] private bool showDebugGrid;
    [SerializeField] private Color debugGridColor = new Color(0.1f, 0.85f, 1f, 0.55f);
    [SerializeField] private Color debugOccupiedCellFillColor = new Color(1f, 0.2f, 0.1f, 0.3f);
    [SerializeField] private Color debugRoadCellFillColor = new Color(0.1f, 1f, 0.2f, 0.45f);
    [SerializeField, Min(1f)] private float debugGridLineWidth = 1f;

    private UISettings settings;
    private UnityEngine.Camera villagerTrackingCamera;
    private Canvas villagerIndicatorCanvas;
    private readonly List<Image> villagerOffscreenIndicators = new List<Image>();
    private readonly List<Vector2> visibleIndicatorScreenPositions = new List<Vector2>();
    private const int InitialResourceDisplayCount = 3;
    private bool loggedMissingVillagerIndicator;
    private bool loggedMissingVillagerCamera;
    private bool loggedMissingVillagerPlayer;
    private bool loggedInvalidVillagerIndicatorCanvas;

    public GameObject RoadEndpointPrefab
    {
        get { return roadEndpointPrefab; }
    }

    public LineRenderer RoadPreviewPrefab
    {
        get { return roadPreviewPrefab; }
    }

    public Sprite GetResourceIcon(ResourceType resourceType)
    {
        GameData gameData = GameData.Instance;
        ResourceDefinition[] definitions = gameData.resourceDefinitions;
        if (definitions == null)
        {
            Debug.LogError("Resource definitions could not be loaded from GameData.", this);
            return null;
        }

        foreach (ResourceDefinition definition in definitions)
        {
            if (definition == null || definition.type != resourceType)
            {
                continue;
            }

            if (resourceIcons == null
                || definition.iconIndex < 0
                || definition.iconIndex >= resourceIcons.Length)
            {
                Debug.LogError(
                    "Resource icon index " + definition.iconIndex + " for " + resourceType
                    + " is outside the configured resource icon range.",
                    this);
                return null;
            }

            Sprite icon = resourceIcons[definition.iconIndex];
            if (icon == null)
            {
                Debug.LogError("No resource icon is assigned at index " + definition.iconIndex + ".", this);
            }

            return icon;
        }

        Debug.LogError("No resource definition is configured for " + resourceType + ".", this);
        return null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Only one UIManager can be active at a time.", this);
            enabled = false;
            return;
        }

        instance = this;
        GameData gameData = GameData.Instance;
        settings = gameData.uiSettings;
        if (settings == null)
        {
            Debug.LogError("UI settings could not be loaded from GameData.", this);
            enabled = false;
            return;
        }

        SetRoadBuildingCursor(false);

        if (villagerHoverTooltipPrefab != null)
        {
            Transform canvasTransform = transform.Find("Canvas");
            Canvas villagerHoverTooltipCanvas = canvasTransform != null
                ? canvasTransform.GetComponent<Canvas>()
                : null;
            VillagerHoverTooltipController tooltipController =
                GetComponent<VillagerHoverTooltipController>();
            if (tooltipController == null)
            {
                tooltipController = gameObject.AddComponent<VillagerHoverTooltipController>();
            }

            tooltipController.Initialize(
                villagerHoverTooltipPrefab,
                villagerHoverTooltipCanvas,
                settings.villagerHoverTooltipWorldOffset);
        }
    }

    private void Start()
    {
        CreateInitialResourceDisplays();
        HideUnusedVillagerIndicators(0);
    }

    private void LateUpdate()
    {
        visibleIndicatorScreenPositions.Clear();
        if (!settings.trackVillagers)
        {
            HideUnusedVillagerIndicators(0);
            return;
        }

        if (villagerTrackingCamera == null)
        {
            villagerTrackingCamera = UnityEngine.Camera.main;
        }

        if (villagerTrackingCamera == null)
        {
            if (!loggedMissingVillagerCamera)
            {
                Debug.LogError(
                    "Villager tracking requires a camera tagged MainCamera.",
                    this);
                loggedMissingVillagerCamera = true;
            }

            HideUnusedVillagerIndicators(0);
            return;
        }

        Player player = Player.Instance;
        if (player == null || player.Villagers == null)
        {
            if (!loggedMissingVillagerPlayer)
            {
                Debug.LogError("Villager tracking requires an active Player with a villager registry.", this);
                loggedMissingVillagerPlayer = true;
            }

            HideUnusedVillagerIndicators(0);
            return;
        }

        Rect cameraBounds = villagerTrackingCamera.pixelRect;
        Vector2 screenCenter = cameraBounds.center;
        Vector2 halfBounds = new Vector2(
            Mathf.Max(0f, cameraBounds.width * 0.5f - settings.villagerIndicatorEdgeInset),
            Mathf.Max(0f, cameraBounds.height * 0.5f - settings.villagerIndicatorEdgeInset));
        int visibleIndicatorCount = 0;

        foreach (VillagerRecord record in player.Villagers)
        {
            Villager villager = record != null ? record.Villager : null;
            if (villager == null || !villager.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 screenPosition = villagerTrackingCamera.WorldToScreenPoint(villager.transform.position);
            bool isOffscreen = screenPosition.z <= 0f
                || screenPosition.x < cameraBounds.xMin
                || screenPosition.x > cameraBounds.xMax
                || screenPosition.y < cameraBounds.yMin
                || screenPosition.y > cameraBounds.yMax;
            if (!isOffscreen)
            {
                continue;
            }

            Vector2 direction = new Vector2(screenPosition.x, screenPosition.y) - screenCenter;
            if (screenPosition.z <= 0f)
            {
                direction = -direction;
            }

            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                continue;
            }

            direction.Normalize();
            float distanceToEdge = Mathf.Min(
                Mathf.Abs(direction.x) > Mathf.Epsilon
                    ? halfBounds.x / Mathf.Abs(direction.x)
                    : float.PositiveInfinity,
                Mathf.Abs(direction.y) > Mathf.Epsilon
                    ? halfBounds.y / Mathf.Abs(direction.y)
                    : float.PositiveInfinity);
            Vector2 indicatorScreenPosition = screenCenter + direction * distanceToEdge;
            indicatorScreenPosition = FindAvailableIndicatorPosition(
                indicatorScreenPosition,
                direction,
                cameraBounds);

            if (!TryCreateVillagerIndicator(visibleIndicatorCount))
            {
                HideUnusedVillagerIndicators(visibleIndicatorCount);
                return;
            }

            Image indicator = villagerOffscreenIndicators[visibleIndicatorCount];
            if (TryPositionVillagerIndicator(indicator, indicatorScreenPosition))
            {
                SetVillagerIndicatorVisible(indicator, true);
                visibleIndicatorScreenPositions.Add(indicatorScreenPosition);
                visibleIndicatorCount++;
            }
            else
            {
                SetVillagerIndicatorVisible(indicator, false);
            }
        }

        HideUnusedVillagerIndicators(visibleIndicatorCount);
    }

    private void OnGUI()
    {
        if (!showDebugGrid || Event.current.type != EventType.Repaint)
        {
            return;
        }

        MapManager mapManager = MapManager.Instance;
        UnityEngine.Camera mapCamera = UnityEngine.Camera.main;
        if (mapManager == null || mapCamera == null || mapManager.GridCellSize <= 0f)
        {
            return;
        }

        Vector2 mapSize = mapManager.MapSize;
        if (mapSize.x <= 0f || mapSize.y <= 0f)
        {
            return;
        }

        Vector3 mapCenter = mapManager.MapCenter;
        float minX = mapCenter.x - mapSize.x * 0.5f;
        float maxX = mapCenter.x + mapSize.x * 0.5f;
        float minY = mapCenter.y - mapSize.y * 0.5f;
        float maxY = mapCenter.y + mapSize.y * 0.5f;
        float gridCellSize = mapManager.GridCellSize;
        Color previousColor = GUI.color;
        DrawOccupiedResourceCells(mapManager, mapCamera);
        DrawRoadCells(mapManager, mapCamera);
        GUI.color = debugGridColor;

        DrawDebugGridAxis(
            mapCamera,
            minX,
            maxX,
            minY,
            maxY,
            mapCenter.z,
            gridCellSize,
            true);
        DrawDebugGridAxis(
            mapCamera,
            minY,
            maxY,
            minX,
            maxX,
            mapCenter.z,
            gridCellSize,
            false);

        GUI.color = previousColor;
    }

    private void DrawOccupiedResourceCells(MapManager mapManager, UnityEngine.Camera mapCamera)
    {
        Color previousColor = GUI.color;
        GUI.color = debugOccupiedCellFillColor;

        foreach (Vector2Int cell in mapManager.OccupiedResourceCells)
        {
            if (!mapManager.TryGetResourceObjectAtCell(cell, out ResourceObject resourceObject)
                || resourceObject == null)
            {
                continue;
            }

            DrawGridCellFill(mapManager, mapCamera, cell);
        }

        GUI.color = previousColor;
    }

    private void DrawRoadCells(MapManager mapManager, UnityEngine.Camera mapCamera)
    {
        Color previousColor = GUI.color;
        GUI.color = debugRoadCellFillColor;

        foreach (Vector2Int cell in mapManager.RoadCells)
        {
            DrawGridCellFill(mapManager, mapCamera, cell);
        }

        GUI.color = previousColor;
    }

    private void DrawGridCellFill(
        MapManager mapManager,
        UnityEngine.Camera mapCamera,
        Vector2Int cell)
    {
        Bounds cellBounds = mapManager.GetGridCellBounds(cell);
        Vector3 screenBottomLeft = mapCamera.WorldToScreenPoint(cellBounds.min);
        Vector3 screenTopRight = mapCamera.WorldToScreenPoint(cellBounds.max);
        if (screenBottomLeft.z <= 0f || screenTopRight.z <= 0f)
        {
            return;
        }

        float left = Mathf.Min(screenBottomLeft.x, screenTopRight.x);
        float right = Mathf.Max(screenBottomLeft.x, screenTopRight.x);
        float top = Screen.height - Mathf.Max(screenBottomLeft.y, screenTopRight.y);
        float bottom = Screen.height - Mathf.Min(screenBottomLeft.y, screenTopRight.y);
        if (right <= left || bottom <= top)
        {
            return;
        }

        GUI.DrawTexture(new Rect(left, top, right - left, bottom - top), Texture2D.whiteTexture);
    }

    private void DrawDebugGridAxis(
        UnityEngine.Camera mapCamera,
        float axisMinimum,
        float axisMaximum,
        float crossAxisMinimum,
        float crossAxisMaximum,
        float worldZ,
        float gridCellSize,
        bool isVertical)
    {
        DrawProjectedDebugGridLine(
            mapCamera,
            axisMinimum,
            crossAxisMinimum,
            axisMinimum,
            crossAxisMaximum,
            worldZ,
            isVertical);

        float firstGridLine = Mathf.Ceil(axisMinimum / gridCellSize) * gridCellSize;
        for (float axisPosition = firstGridLine;
             axisPosition < axisMaximum;
             axisPosition += gridCellSize)
        {
            if (axisPosition <= axisMinimum)
            {
                continue;
            }

            DrawProjectedDebugGridLine(
                mapCamera,
                axisPosition,
                crossAxisMinimum,
                axisPosition,
                crossAxisMaximum,
                worldZ,
                isVertical);
        }

        DrawProjectedDebugGridLine(
            mapCamera,
            axisMaximum,
            crossAxisMinimum,
            axisMaximum,
            crossAxisMaximum,
            worldZ,
            isVertical);
    }

    private void DrawProjectedDebugGridLine(
        UnityEngine.Camera mapCamera,
        float axisStart,
        float crossAxisStart,
        float axisEnd,
        float crossAxisEnd,
        float worldZ,
        bool isVertical)
    {
        Vector3 worldStart = isVertical
            ? new Vector3(axisStart, crossAxisStart, worldZ)
            : new Vector3(crossAxisStart, axisStart, worldZ);
        Vector3 worldEnd = isVertical
            ? new Vector3(axisEnd, crossAxisEnd, worldZ)
            : new Vector3(crossAxisEnd, axisEnd, worldZ);

        Vector3 screenStart = mapCamera.WorldToScreenPoint(worldStart);
        Vector3 screenEnd = mapCamera.WorldToScreenPoint(worldEnd);
        if (screenStart.z <= 0f || screenEnd.z <= 0f)
        {
            return;
        }

        Vector2 guiStart = new Vector2(screenStart.x, Screen.height - screenStart.y);
        Vector2 guiEnd = new Vector2(screenEnd.x, Screen.height - screenEnd.y);
        Vector2 difference = guiEnd - guiStart;
        float length = difference.magnitude;
        if (length <= Mathf.Epsilon)
        {
            return;
        }

        Matrix4x4 previousMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg, guiStart);
        GUI.DrawTexture(
            new Rect(guiStart.x, guiStart.y - debugGridLineWidth * 0.5f, length, debugGridLineWidth),
            Texture2D.whiteTexture);
        GUI.matrix = previousMatrix;
    }

    private Vector2 FindAvailableIndicatorPosition(
        Vector2 basePosition,
        Vector2 direction,
        Rect cameraBounds)
    {
        float marginX = Mathf.Min(
            Mathf.Max(settings.villagerIndicatorEdgeInset, settings.villagerIndicatorSize.x * 0.5f),
            cameraBounds.width * 0.5f);
        float marginY = Mathf.Min(
            Mathf.Max(settings.villagerIndicatorEdgeInset, settings.villagerIndicatorSize.y * 0.5f),
            cameraBounds.height * 0.5f);
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        float spacing = Mathf.Max(settings.villagerIndicatorSize.x, settings.villagerIndicatorSize.y) + 4f;
        int candidateCount = visibleIndicatorScreenPositions.Count * 2 + 1;

        for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            int offsetIndex = (candidateIndex + 1) / 2;
            float offset = candidateIndex == 0
                ? 0f
                : ((candidateIndex % 2 == 1) ? offsetIndex : -offsetIndex) * spacing;
            Vector2 candidate = basePosition + tangent * offset;
            candidate.x = Mathf.Clamp(
                candidate.x,
                cameraBounds.xMin + marginX,
                cameraBounds.xMax - marginX);
            candidate.y = Mathf.Clamp(
                candidate.y,
                cameraBounds.yMin + marginY,
                cameraBounds.yMax - marginY);

            bool overlapsExistingIndicator = false;
            foreach (Vector2 existingPosition in visibleIndicatorScreenPositions)
            {
                bool overlapsHorizontally = Mathf.Abs(candidate.x - existingPosition.x)
                    < settings.villagerIndicatorSize.x + 4f;
                bool overlapsVertically = Mathf.Abs(candidate.y - existingPosition.y)
                    < settings.villagerIndicatorSize.y + 4f;
                if (overlapsHorizontally && overlapsVertically)
                {
                    overlapsExistingIndicator = true;
                    break;
                }
            }

            if (!overlapsExistingIndicator)
            {
                return candidate;
            }
        }

        return basePosition;
    }

    private bool TryPositionVillagerIndicator(Image indicator, Vector2 screenPosition)
    {
        Canvas canvas = indicator.canvas;
        RectTransform parentRectTransform = indicator.transform.parent as RectTransform;
        if (canvas == null || parentRectTransform == null)
        {
            if (!loggedInvalidVillagerIndicatorCanvas)
            {
                Debug.LogError(
                    "The villager off-screen indicator must be a UI Image under a Canvas.",
                    this);
                loggedInvalidVillagerIndicatorCanvas = true;
            }

            return false;
        }

        UnityEngine.Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera != null ? canvas.worldCamera : villagerTrackingCamera;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                parentRectTransform,
                screenPosition,
                canvasCamera,
                out Vector3 worldPosition))
        {
            return false;
        }

        RectTransform indicatorRectTransform = indicator.rectTransform;
        indicatorRectTransform.position = worldPosition;
        return true;
    }

    private bool TryCreateVillagerIndicator(int index)
    {
        if (index < villagerOffscreenIndicators.Count)
        {
            return villagerOffscreenIndicators[index] != null;
        }

        if (villagerOffscreenIndicatorSprite == null)
        {
            if (!loggedMissingVillagerIndicator)
            {
                Debug.LogError(
                    "Villager tracking is enabled, but no off-screen indicator Sprite is assigned.",
                    this);
                loggedMissingVillagerIndicator = true;
            }

            return false;
        }

        if (!TryGetVillagerIndicatorCanvas())
        {
            return false;
        }

        GameObject indicatorObject = new GameObject(
            "VillagerOffscreenIndicator_" + index,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        indicatorObject.transform.SetParent(villagerIndicatorCanvas.transform, false);

        RectTransform indicatorRectTransform = indicatorObject.GetComponent<RectTransform>();
        indicatorRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        indicatorRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        indicatorRectTransform.pivot = new Vector2(0.5f, 0.5f);
        indicatorRectTransform.sizeDelta = settings.villagerIndicatorSize;
        indicatorRectTransform.localRotation = Quaternion.identity;

        Image indicator = indicatorObject.GetComponent<Image>();
        indicator.sprite = villagerOffscreenIndicatorSprite;
        indicator.preserveAspect = true;
        indicator.raycastTarget = false;
        indicatorObject.SetActive(false);
        villagerOffscreenIndicators.Add(indicator);
        return true;
    }

    private bool TryGetVillagerIndicatorCanvas()
    {
        if (villagerIndicatorCanvas == null)
        {
            Transform canvasTransform = transform.Find("Canvas");
            if (canvasTransform == null)
            {
                if (!loggedInvalidVillagerIndicatorCanvas)
                {
                    Debug.LogError(
                        "Villager tracking requires an immediate child named Canvas under the UIManager GameObject.",
                        this);
                    loggedInvalidVillagerIndicatorCanvas = true;
                }

                return false;
            }

            villagerIndicatorCanvas = canvasTransform.GetComponent<Canvas>();
        }

        if (villagerIndicatorCanvas == null)
        {
            if (!loggedInvalidVillagerIndicatorCanvas)
            {
                Debug.LogError(
                    "The immediate child named Canvas under the UIManager GameObject must have a Canvas component.",
                    this);
                loggedInvalidVillagerIndicatorCanvas = true;
            }

            return false;
        }

        return true;
    }

    private void HideUnusedVillagerIndicators(int firstHiddenIndex)
    {
        for (int index = firstHiddenIndex; index < villagerOffscreenIndicators.Count; index++)
        {
            SetVillagerIndicatorVisible(villagerOffscreenIndicators[index], false);
        }
    }

    private void SetVillagerIndicatorVisible(Image indicator, bool isVisible)
    {
        if (indicator != null && indicator.gameObject.activeSelf != isVisible)
        {
            indicator.gameObject.SetActive(isVisible);
        }
    }

    private void CreateInitialResourceDisplays()
    {
        if (topPanel == null)
        {
            Debug.LogError("A top panel must be assigned to display player resources.", this);
            return;
        }

        Player player = Player.Instance;
        if (player == null || player.resources == null)
        {
            Debug.LogError("Player resources are not available to create resource displays.", this);
            return;
        }

        int displayCount = Mathf.Min(InitialResourceDisplayCount, player.resources.Length);
        if (displayCount == 0)
        {
            return;
        }

        GameObject containerObject = new GameObject("PlayerResourceDisplays", typeof(RectTransform));
        RectTransform container = containerObject.GetComponent<RectTransform>();
        container.SetParent(topPanel.transform, false);
        container.anchorMin = new Vector2(0f, 1f);
        container.anchorMax = new Vector2(0f, 1f);
        container.pivot = new Vector2(0f, 1f);
        container.anchoredPosition = new Vector2(12f, -12f);
        container.sizeDelta = new Vector2(displayCount * 112f, 40f);

        HorizontalLayoutGroup containerLayout = containerObject.AddComponent<HorizontalLayoutGroup>();
        containerLayout.childAlignment = TextAnchor.MiddleLeft;
        containerLayout.spacing = 8f;
        containerLayout.childControlWidth = false;
        containerLayout.childControlHeight = false;
        containerLayout.childForceExpandWidth = false;
        containerLayout.childForceExpandHeight = false;

        for (int index = 0; index < displayCount; index++)
        {
            ResourceRecord resource = player.resources[index];
            if (resource == null)
            {
                Debug.LogError("Player resource entry " + index + " is null.", this);
                continue;
            }

            Sprite icon = GetResourceIcon(resource.type);
            if (icon == null)
            {
                continue;
            }

            CreateResourceDisplay(container, resource.type, icon);
        }
    }

    private void CreateResourceDisplay(Transform parent, ResourceType resourceType, Sprite icon)
    {
        GameObject rowObject = new GameObject("Resource_" + resourceType, typeof(RectTransform));
        RectTransform rowTransform = rowObject.GetComponent<RectTransform>();
        rowTransform.SetParent(parent, false);
        rowTransform.sizeDelta = new Vector2(104f, 36f);

        HorizontalLayoutGroup rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.spacing = 6f;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        RectTransform iconTransform = iconObject.GetComponent<RectTransform>();
        iconTransform.SetParent(rowTransform, false);
        iconTransform.sizeDelta = new Vector2(28f, 28f);
        Image image = iconObject.GetComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
        iconLayout.preferredWidth = 28f;
        iconLayout.preferredHeight = 28f;

        GameObject valueObject = new GameObject(
            "Value",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(UITextValue),
            typeof(LayoutElement));
        RectTransform valueTransform = valueObject.GetComponent<RectTransform>();
        valueTransform.SetParent(rowTransform, false);
        valueTransform.sizeDelta = new Vector2(64f, 32f);

        TextMeshProUGUI text = valueObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 18f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = Color.white;

        LayoutElement textLayout = valueObject.GetComponent<LayoutElement>();
        textLayout.preferredWidth = 64f;
        textLayout.preferredHeight = 32f;

        UITextValue textValue = valueObject.GetComponent<UITextValue>();
        textValue.SetValueDefinition(new UIValueDefinition
        {
            type = UIValueType.Resource,
            resourceType = resourceType
        });
    }

    public void TogglePanel(int panelIndex)
    {
        if (panels == null || panelIndex < 0 || panelIndex >= panels.Length)
        {
            Debug.LogError("Panel index " + panelIndex + " is outside the configured panel range.", this);
            return;
        }

        GameObject panel = panels[panelIndex];
        if (panel == null)
        {
            Debug.LogError("No panel is configured at index " + panelIndex + ".", this);
            return;
        }

        panel.SetActive(!panel.activeSelf);
    }

    public void ToggleRoadBuilding()
    {
        if (MapManager.Instance == null)
        {
            Debug.LogError("Cannot toggle road building because no MapManager is active.", this);
            return;
        }

        MapManager.Instance.ToggleRoadBuilding();
    }

    public void SetRoadBuildingCursor(bool isRoadBuilding)
    {
        if (settings == null)
        {
            return;
        }

        Texture2D cursorImage = isRoadBuilding ? roadBuildCursorImage : defaultCursorImage;
        Vector2 hotspot = isRoadBuilding
            ? settings.roadBuildCursorHotspot
            : settings.defaultCursorHotspot;
        Cursor.SetCursor(cursorImage, hotspot, CursorMode.Auto);
        Cursor.visible = true;
    }
}
