using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class HouseVisual : MonoBehaviour
{
    private const int BasePartSortingOrder = 55;
    private const int RoofPartSortingOrder = 105;
    private const string HouseSortingLayer = "Everything";
    private const float SpriteDepthOffset = -0.01f;
    private readonly List<GameObject> visualParts = new List<GameObject>();

    public void ShowConstructionStage(
        Sprite[] stageSprites,
        Color[] stageColors,
        float footprintWidth,
        Vector2 visualOffset)
    {
        ConfigureSortingGroup();
        ClearVisualParts();
        LineRenderer plotOutline = GetComponent<LineRenderer>();
        if (plotOutline != null)
        {
            plotOutline.enabled = false;
        }

        AddSpriteGroup(
            stageSprites,
            SelectColor(stageColors),
            footprintWidth,
            BasePartSortingOrder,
            visualOffset);
    }

    public void ShowFinishedHouse(HouseDefinition definition, float footprintWidth)
    {
        if (definition == null)
        {
            Debug.LogError("Cannot display a finished house without a house definition.", this);
            return;
        }

        ConfigureSortingGroup();
        ClearVisualParts();
        AddSprite(
            SelectSprite(definition.baseSprites),
            SelectColor(definition.baseColors),
            footprintWidth,
            BasePartSortingOrder,
            definition.visualOffset);
        AddSprite(
            SelectSprite(definition.lowRoofSprites),
            SelectColor(definition.lowRoofColors),
            footprintWidth,
            RoofPartSortingOrder,
            definition.visualOffset);

        Vector3 visualScale = transform.localScale;
        visualScale.x = Mathf.Abs(visualScale.x) * (Random.value < 0.5f ? -1f : 1f);
        transform.localScale = visualScale;
    }

    private void ConfigureSortingGroup()
    {
        SortingGroup sortingGroup = GetComponent<SortingGroup>();
        if (sortingGroup == null)
        {
            sortingGroup = gameObject.AddComponent<SortingGroup>();
        }

        sortingGroup.enabled = true;
        sortingGroup.sortingLayerName = HouseSortingLayer;
        sortingGroup.sortingOrder = 0;
        sortingGroup.sortAtRoot = true;
    }

    private void AddSpriteGroup(
        Sprite[] sprites,
        Color color,
        float footprintWidth,
        int sortingOrder,
        Vector2 visualOffset)
    {
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError("Cannot display house parts because the sprite group is empty.", this);
            return;
        }

        foreach (Sprite sprite in sprites)
        {
            AddSprite(sprite, color, footprintWidth, sortingOrder, visualOffset);
        }
    }

    private void AddSprite(
        Sprite sprite,
        Color color,
        float footprintWidth,
        int sortingOrder,
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

        GameObject part = new GameObject(sprite.name);
        part.transform.SetParent(transform, false);
        part.transform.localPosition = new Vector3(
            pixelPosition.x * pixelsToWorld + visualOffset.x,
            pixelPosition.y * pixelsToWorld + visualOffset.y,
            SpriteDepthOffset);
        part.transform.localScale = Vector3.one * pixelsToWorld * sprite.pixelsPerUnit;

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = HouseSortingLayer;
        renderer.sortingOrder = sortingOrder;
        visualParts.Add(part);
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

    private static Sprite SelectSprite(Sprite[] variants)
    {
        if (variants == null || variants.Length == 0)
        {
            return null;
        }

        return variants[Random.Range(0, variants.Length)];
    }

    private static Color SelectColor(Color[] colors)
    {
        if (colors == null || colors.Length == 0)
        {
            return Color.white;
        }

        return colors[Random.Range(0, colors.Length)];
    }
}
