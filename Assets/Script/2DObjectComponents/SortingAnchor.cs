using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SortingAnchor : MonoBehaviour
{
    private Transform anchorTransform;
    private Transform visualRoot;
    private SortingGroup sortingGroup;

    public Transform VisualRoot
    {
        get
        {
            Initialize();
            return visualRoot;
        }
    }

    public SortingGroup Group
    {
        get
        {
            Initialize();
            return sortingGroup;
        }
    }

    private void Awake()
    {
        Initialize();
    }

    public void Configure(Vector2 localOffset)
    {
        Initialize();
        anchorTransform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
        // Offset the artwork back so only the SortingGroup's sort position changes.
        visualRoot.localPosition = new Vector3(-localOffset.x, -localOffset.y, 0f);
    }

    private void Initialize()
    {
        if (anchorTransform != null)
        {
            return;
        }

        SortingGroup rootSortingGroup = GetComponent<SortingGroup>();
        anchorTransform = new GameObject("SortingAnchor").transform;
        anchorTransform.SetParent(transform, false);

        sortingGroup = anchorTransform.gameObject.AddComponent<SortingGroup>();
        if (rootSortingGroup != null)
        {
            sortingGroup.sortingLayerName = rootSortingGroup.sortingLayerName;
            sortingGroup.sortingOrder = rootSortingGroup.sortingOrder;
            sortingGroup.sortAtRoot = rootSortingGroup.sortAtRoot;
            rootSortingGroup.enabled = false;
        }

        visualRoot = new GameObject("Visuals").transform;
        visualRoot.SetParent(anchorTransform, false);

        List<Transform> existingChildren = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child != anchorTransform && child.GetComponent<GroundShadow>() == null)
            {
                existingChildren.Add(child);
            }
        }

        foreach (Transform child in existingChildren)
        {
            child.SetParent(visualRoot, true);
        }
    }
}
