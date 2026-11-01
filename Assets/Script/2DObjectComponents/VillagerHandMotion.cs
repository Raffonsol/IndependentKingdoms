using UnityEngine;

public class VillagerHandMotion : MonoBehaviour
{
    private Transform arm;
    private Vector3 startingLocalPosition;
    private Quaternion startingLocalRotation;
    private ArmMotionPoint[] hitPositions;
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
        startingLocalRotation = arm.localRotation;
    }

    public void PlayHit(ArmMotionPoint[] positions, float duration)
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

        ArmMotionPoint previousPoint = segmentIndex == 0 ? null : hitPositions[segmentIndex - 1];
        ArmMotionPoint nextPoint = segmentIndex < hitPositions.Length
            ? hitPositions[segmentIndex]
            : null;
        float startX = previousPoint != null ? previousPoint.x : 0f;
        float startY = previousPoint != null ? previousPoint.y : 0f;
        float startRotation = previousPoint != null ? previousPoint.z : 0f;
        float endX = nextPoint != null ? nextPoint.x : 0f;
        float endY = nextPoint != null ? nextPoint.y : 0f;
        float endRotation = nextPoint != null ? nextPoint.z : 0f;

        arm.localPosition = startingLocalPosition + new Vector3(
            Mathf.Lerp(startX, endX, interpolation),
            Mathf.Lerp(startY, endY, interpolation),
            0f);
        float rotationOffset = Mathf.LerpAngle(
            startRotation,
            endRotation,
            interpolation);
        arm.localRotation = startingLocalRotation * Quaternion.Euler(0f, 0f, rotationOffset);
        if (hitTimer <= 0f)
        {
            arm.localPosition = startingLocalPosition;
            arm.localRotation = startingLocalRotation;
        }
    }
}
