using UnityEngine;

public class ResourceObject : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType = ResourceType.Wood;
    [SerializeField, Min(0)] private int amount = 10;

    public Vector2Int GridCell { get; private set; }

    public bool HasGridCell { get; private set; }

    public ResourceType Type
    {
        get { return resourceType; }
    }

    public int Amount
    {
        get { return amount; }
    }

    public void SetGridCell(Vector2Int gridCell)
    {
        GridCell = gridCell;
        HasGridCell = true;
    }
}
