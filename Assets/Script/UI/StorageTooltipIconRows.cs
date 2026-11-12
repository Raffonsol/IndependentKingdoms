using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StorageTooltipIconRows : MonoBehaviour
{
    public const float RowGap = 2f;

    private sealed class Row
    {
        public RectTransform rectTransform;
        public Image icon;
        public TextMeshProUGUI label;
    }

    private readonly List<Row> rows = new List<Row>();
    private RectTransform container;

    public static float GetRowHeight(TMP_Text textTemplate)
    {
        return Mathf.Max(18f, textTemplate.fontSize * 1.5f);
    }

    public void SetRows(
        TMP_Text textTemplate,
        IList<StorageTooltipIconEntry> entries,
        float topOffset,
        float rowHeight)
    {
        if (textTemplate == null || entries == null)
        {
            Debug.LogError("Storage tooltip icon rows require a text template and entries.", this);
            return;
        }

        EnsureContainer(textTemplate.rectTransform);
        for (int index = 0; index < entries.Count; index++)
        {
            Row row = GetOrCreateRow(index);
            StorageTooltipIconEntry entry = entries[index];
            ConfigureRow(row, textTemplate, entry, topOffset + index * rowHeight, rowHeight);
            row.rectTransform.gameObject.SetActive(true);
        }

        for (int index = entries.Count; index < rows.Count; index++)
        {
            rows[index].rectTransform.gameObject.SetActive(false);
        }
    }

    private void EnsureContainer(RectTransform textRect)
    {
        if (container != null)
        {
            return;
        }

        GameObject containerObject = new GameObject("Storage Icon Rows", typeof(RectTransform));
        container = containerObject.GetComponent<RectTransform>();
        container.SetParent(textRect, false);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
    }

    private Row GetOrCreateRow(int index)
    {
        if (index < rows.Count)
        {
            return rows[index];
        }

        GameObject rowObject = new GameObject("Storage Icon Row", typeof(RectTransform));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.SetParent(container, false);

        GameObject iconObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(rowRect, false);

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(rowRect, false);
        label.raycastTarget = false;

        Row row = new Row
        {
            rectTransform = rowRect,
            icon = iconObject.GetComponent<Image>(),
            label = label
        };
        row.icon.raycastTarget = false;
        rows.Add(row);
        return row;
    }

    private static void ConfigureRow(
        Row row,
        TMP_Text textTemplate,
        StorageTooltipIconEntry entry,
        float topOffset,
        float rowHeight)
    {
        row.rectTransform.anchorMin = new Vector2(0f, 1f);
        row.rectTransform.anchorMax = new Vector2(1f, 1f);
        row.rectTransform.pivot = new Vector2(0.5f, 1f);
        row.rectTransform.sizeDelta = new Vector2(0f, rowHeight);
        row.rectTransform.anchoredPosition = new Vector2(0f, -topOffset);

        float iconSize = Mathf.Min(18f, rowHeight);
        RectTransform iconRect = row.icon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        iconRect.anchoredPosition = new Vector2(5f, 0f);
        row.icon.sprite = entry.icon;
        row.icon.enabled = entry.icon != null;
        row.icon.preserveAspect = true;

        RectTransform labelRect = row.label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(iconSize + 12f, 0f);
        labelRect.offsetMax = new Vector2(-4f, 0f);
        row.label.font = textTemplate.font;
        row.label.fontSharedMaterial = textTemplate.fontSharedMaterial;
        row.label.fontSize = textTemplate.fontSize;
        row.label.fontStyle = textTemplate.fontStyle;
        row.label.color = textTemplate.color;
        row.label.alignment = TextAlignmentOptions.MidlineLeft;
        row.label.textWrappingMode = TextWrappingModes.NoWrap;
        row.label.overflowMode = TextOverflowModes.Ellipsis;
        row.label.text = entry.label;
    }
}

public struct StorageTooltipIconEntry
{
    public StorageTooltipIconEntry(string label, Sprite icon)
    {
        this.label = label;
        this.icon = icon;
    }

    public string label { get; private set; }
    public Sprite icon { get; private set; }
}
