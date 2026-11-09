using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class HouseVisual : MonoBehaviour
{
    private const int BasePartSortingOrder = 55;
    private const int RoofPartSortingOrder = 105;
    private const int RenovationProgressSortingOrder = 200;
    private const int FirstConstructionStageSortingOrder = 30;
    private const string HouseSortingLayer = "Everything";
    private const string FirstConstructionStageSortingLayer = "Ground";
    private const string YardSortingLayer = "Ground";
    private const float SpriteDepthOffset = -0.01f;
    private readonly List<GameObject> visualParts = new List<GameObject>();
    private readonly List<GameObject> progressParts = new List<GameObject>();
    private SortingAnchor sortingAnchor;
    private GameObject yardVisual;
    private HouseDefinition houseDefinition;
    private float houseFootprintWidth;
    private Color wallColor;
    private Color roofColor;
    private bool hasHouseColors;

    public bool HasAnnex { get; private set; }
    public HouseDefinition Definition
    {
        get { return houseDefinition; }
    }

    public void ShowConstructionStage(
        Sprite[] stageSprites,
        Color[] stageColors,
        float footprintWidth,
        Vector2 visualOffset,
        Vector2 sortingAnchorOffset,
        bool useSortingGroup)
    {
        ConfigureSortingGroup(sortingAnchorOffset, useSortingGroup);
        ClearVisualParts();
        ClearProgressParts();
        ClearYardVisual();
        LineRenderer plotOutline = GetComponent<LineRenderer>();
        if (plotOutline != null)
        {
            plotOutline.enabled = false;
        }

        AddSpriteGroup(
            stageSprites,
            SelectColor(stageColors),
            footprintWidth,
            useSortingGroup ? BasePartSortingOrder : FirstConstructionStageSortingOrder,
            useSortingGroup ? HouseSortingLayer : FirstConstructionStageSortingLayer,
            visualParts,
            visualOffset);
    }

    public void ShowFinishedHouse(HouseDefinition definition, float footprintWidth)
    {
        if (definition == null)
        {
            Debug.LogError("Cannot display a finished house without a house definition.", this);
            return;
        }

        ConfigureSortingGroup(definition.sortingAnchorOffset, true);
        ClearVisualParts();
        ClearProgressParts();
        ClearYardVisual();
        if (houseDefinition != definition || !hasHouseColors)
        {
            houseDefinition = definition;
            houseFootprintWidth = footprintWidth;
            wallColor = SelectColor(definition.wallColors);
            roofColor = SelectColor(definition.roofColors);
            hasHouseColors = true;
        }

        CreateYardVisual(
            SelectSprite(definition.yardSprites),
            SelectColor(definition.yardColors),
            footprintWidth,
            definition.visualOffset);
        AddSprite(
            SelectSprite(definition.baseSprites),
            wallColor,
            footprintWidth,
            BasePartSortingOrder,
            HouseSortingLayer,
            visualParts,
            definition.visualOffset);
        AddSprite(
            SelectSprite(definition.lowRoofSprites),
            roofColor,
            footprintWidth,
            RoofPartSortingOrder,
            HouseSortingLayer,
            visualParts,
            definition.visualOffset);
        HasAnnex = false;

        Vector3 visualScale = transform.localScale;
        visualScale.x = Mathf.Abs(visualScale.x)
            * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        transform.localScale = visualScale;
    }

    public void ShowRenovationProgress(
        Sprite[] stageSprites,
        Color[] stageColors,
        float footprintWidth,
        Vector2 visualOffset,
        Vector2 sortingAnchorOffset)
    {
        ConfigureSortingGroup(sortingAnchorOffset, true);
        ClearProgressParts();
        AddSpriteGroup(
            stageSprites,
            SelectColor(stageColors),
            footprintWidth,
            RenovationProgressSortingOrder,
            HouseSortingLayer,
            progressParts,
            visualOffset);
    }

    public void CompleteAnnexRenovation()
    {
        ClearProgressParts();
        if (HasAnnex)
        {
            return;
        }

        if (houseDefinition == null || !hasHouseColors)
        {
            Debug.LogError("Cannot complete an annex renovation without a finished house.", this);
            return;
        }

        AddSprite(
            SelectSprite(houseDefinition.annexSprites),
            wallColor,
            houseFootprintWidth,
            BasePartSortingOrder,
            HouseSortingLayer,
            visualParts,
            houseDefinition.visualOffset);
        AddSprite(
            SelectSprite(houseDefinition.annexRoofSprites),
            roofColor,
            houseFootprintWidth,
            RoofPartSortingOrder,
            HouseSortingLayer,
            visualParts,
            houseDefinition.visualOffset);
        HasAnnex = true;
    }

    private void CreateYardVisual(
        Sprite sprite,
        Color color,
        float footprintWidth,
        Vector2 visualOffset)
    {
        if (sprite == null || sprite.texture == null || sprite.texture.width <= 0)
        {
            Debug.LogError("Cannot display a missing or invalid house yard sprite.", this);
            return;
        }

        float pixelsToWorld = footprintWidth / sprite.texture.width;
        Vector2 pixelPosition = sprite.rect.position + sprite.pivot;
        pixelPosition -= new Vector2(sprite.texture.width, sprite.texture.height) * 0.5f;

        yardVisual = new GameObject(sprite.name);
        yardVisual.transform.SetParent(transform, false);
        yardVisual.transform.localPosition = new Vector3(
            pixelPosition.x * pixelsToWorld + visualOffset.x,
            pixelPosition.y * pixelsToWorld + visualOffset.y,
            SpriteDepthOffset);
        yardVisual.transform.localScale = Vector3.one * pixelsToWorld * sprite.pixelsPerUnit;

        SpriteRenderer renderer = yardVisual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = YardSortingLayer;
    }

    private void ClearYardVisual()
    {
        if (yardVisual != null)
        {
            Destroy(yardVisual);
            yardVisual = null;
        }
    }

    private void ConfigureSortingGroup(Vector2 sortingAnchorOffset, bool useSortingGroup)
    {
        sortingAnchor = GetComponent<SortingAnchor>();
        if (sortingAnchor == null)
        {
            sortingAnchor = gameObject.AddComponent<SortingAnchor>();
        }

        SortingGroup sortingGroup = sortingAnchor.Group;
        if (sortingGroup == null)
        {
            Debug.LogError("House sorting anchor has no SortingGroup.", this);
            return;
        }

        sortingGroup.enabled = useSortingGroup;
        sortingGroup.sortingLayerName = HouseSortingLayer;
        sortingGroup.sortingOrder = 0;
        sortingGroup.sortAtRoot = true;
        sortingAnchor.Configure(sortingAnchorOffset);
    }

    private void AddSpriteGroup(
        Sprite[] sprites,
        Color color,
        float footprintWidth,
        int sortingOrder,
        string sortingLayerName,
        List<GameObject> destination,
        Vector2 visualOffset)
    {
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError("Cannot display house parts because the sprite group is empty.", this);
            return;
        }

        foreach (Sprite sprite in sprites)
        {
            AddSprite(
                sprite,
                color,
                footprintWidth,
                sortingOrder,
                sortingLayerName,
                destination,
                visualOffset);
        }
    }

    private void AddSprite(
        Sprite sprite,
        Color color,
        float footprintWidth,
        int sortingOrder,
        string sortingLayerName,
        List<GameObject> destination,
        Vector2 visualOffset)
    {
        if (sprite == null || sprite.texture == null || sprite.texture.width <= 0)
        {
            Debug.LogError("Cannot display a missing or invalid house sprite.", this);
            return;
        }

        float pixelsToWorld = footprintWidth / sprite.texture.width;
        Vector2 pixelPosition = sprite.rect.position + sprite.pivot;
        pixelPosition -= new Vector2(sprite.texture.width, sprite.texture.height) * 0.5f;

        if (sortingAnchor == null || sortingAnchor.VisualRoot == null)
        {
            Debug.LogError("Cannot create house visuals without a configured sorting anchor.", this);
            return;
        }

        GameObject part = new GameObject(sprite.name);
        part.transform.SetParent(sortingAnchor.VisualRoot, false);
        part.transform.localPosition = new Vector3(
            pixelPosition.x * pixelsToWorld + visualOffset.x,
            pixelPosition.y * pixelsToWorld + visualOffset.y,
            SpriteDepthOffset);
        part.transform.localScale = Vector3.one * pixelsToWorld * sprite.pixelsPerUnit;

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        destination.Add(part);
    }

    private void ClearVisualParts()
    {
        foreach (GameObject part in visualParts)
        {
            if (part != null)
            {
                Destroy(part);
            }
        }

        visualParts.Clear();
    }

    private void ClearProgressParts()
    {
        foreach (GameObject part in progressParts)
        {
            if (part != null)
            {
                Destroy(part);
            }
        }

        progressParts.Clear();
    }

    private static Sprite SelectSprite(Sprite[] variants)
    {
        if (variants == null || variants.Length == 0)
        {
            return null;
        }

        return variants[UnityEngine.Random.Range(0, variants.Length)];
    }

    private static Color SelectColor(Color[] colors)
    {
        if (colors == null || colors.Length == 0)
        {
            return Color.white;
        }

        return colors[UnityEngine.Random.Range(0, colors.Length)];
    }
}
