using UnityEngine;

public class VillagerHandMotion : MonoBehaviour
{
    private Transform arm;
    private Vector3 startingLocalPosition;
    private Vector2[] hitPositions;
    private float hitDuration;
    private float hitTimer;

    public void Configure(Transform villagerArm)
    {
        if (villagerArm == null)
        {
            Debug.LogError("Cannot configure hand motion without an arm transform.", this);
            return;
        }

        arm = villagerArm;
        startingLocalPosition = arm.localPosition;
    }

    public void PlayHit(Vector2[] positions, float duration)
    {
        if (arm == null || positions == null || positions.Length == 0 || duration <= 0f)
        {
            return;
        }

        hitPositions = positions;
        hitDuration = duration;
        hitTimer = duration;
    }

    private void Update()
    {
        if (arm == null || hitTimer <= 0f)
        {
            return;
        }

        hitTimer = Mathf.Max(0f, hitTimer - Time.deltaTime);
        float progress = 1f - hitTimer / hitDuration;
        int segmentCount = hitPositions.Length + 1;
        float segmentProgress = progress * segmentCount;
        int segmentIndex = Mathf.Min(Mathf.FloorToInt(segmentProgress), segmentCount - 1);
        float interpolation = Mathf.Clamp01(segmentProgress - segmentIndex);

        Vector3 segmentStart = segmentIndex == 0
            ? startingLocalPosition
            : startingLocalPosition + (Vector3)hitPositions[segmentIndex - 1];
        Vector3 segmentEnd = segmentIndex < hitPositions.Length
            ? startingLocalPosition + (Vector3)hitPositions[segmentIndex]
            : startingLocalPosition;

        arm.localPosition = Vector3.Lerp(segmentStart, segmentEnd, interpolation);
        if (hitTimer <= 0f)
        {
            arm.localPosition = startingLocalPosition;
        }
    }
}
