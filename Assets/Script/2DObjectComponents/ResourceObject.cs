using UnityEngine;

public class ResourceObject : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType = ResourceType.Wood;

    public Vector2Int GridCell { get; private set; }

    public bool HasGridCell { get; private set; }

    private int remainingAmount;
    private int remainingBareHandAmount;
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
        get { return remainingAmount; }
    }

    private void Awake()
    {
        GameData gameData = GameData.Instance;
        if (gameData == null
            || !gameData.TryGetResourceDefinition(resourceType, out ResourceDefinition definition))
        {
            Debug.LogError("Could not load harvest amounts for resource type " + resourceType + ".", this);
            return;
        }

        remainingAmount = definition.fullCutAmount;
        remainingBareHandAmount = definition.bareHandAmount;
        startingLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        if (shakeTimer <= 0f)
        {
            return;
        }

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

    public bool CanGather(bool hasAxe)
    {
        return remainingAmount > 0 && (hasAxe || remainingBareHandAmount > 0);
    }

    public int Take(int requestedAmount, bool hasAxe)
    {
        if (requestedAmount <= 0 || !CanGather(hasAxe))
        {
            return 0;
        }

        int maximumAmount = hasAxe
            ? remainingAmount
            : remainingBareHandAmount;
        int takenAmount = Mathf.Min(requestedAmount, maximumAmount);
        remainingAmount -= takenAmount;
        remainingBareHandAmount = Mathf.Max(0, remainingBareHandAmount - takenAmount);
        return takenAmount;
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
