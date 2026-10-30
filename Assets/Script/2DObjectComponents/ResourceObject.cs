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

    public int Take(int requestedAmount)
    {
        if (requestedAmount <= 0 || amount <= 0)
        {
            return 0;
        }

        int takenAmount = Mathf.Min(requestedAmount, amount);
        amount -= takenAmount;
        return takenAmount;
    }

    public void SetGridCell(Vector2Int gridCell)
    {
        GridCell = gridCell;
        HasGridCell = true;
    }
}
