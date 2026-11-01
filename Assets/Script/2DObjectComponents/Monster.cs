using UnityEngine;

public class Monster : Combatant
{
    private float wanderTimer;
    private bool isWandering;

    protected override int DefaultFactionId
    {
        get { return 0; }
    }

    protected override bool PrioritizeRoadPaths
    {
        get { return false; }
    }

    protected override void Update()
    {
        base.Update();
        if (!IsAlive || IsCombatEngaged)
        {
            wanderTimer = 0f;
            isWandering = false;
            return;
        }

        wanderTimer -= Time.deltaTime;
        if (wanderTimer > 0f)
        {
            return;
        }

        isWandering = !isWandering;
        if (isWandering)
        {
            Move(Random.Range(0f, 360f));
            wanderTimer = Random.Range(MovementDurationRange.x, MovementDurationRange.y);
        }
        else
        {
            StopMoving();
            wanderTimer = Random.Range(IdleDurationRange.x, IdleDurationRange.y);
        }
    }
}
