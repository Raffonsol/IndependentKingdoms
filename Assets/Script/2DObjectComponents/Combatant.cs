using System.Collections.Generic;
using UnityEngine;

public abstract class Combatant : Mover
{
    private static readonly List<Combatant> activeCombatants = new List<Combatant>();

    private int factionId;

    private CombatSettings combatSettings;
    private VillagerHandMotion armMotion;
    private Combatant currentTarget;
    private float health;
    private float attackWindupTimer;
    private float attackRecoveryTimer;
    private float detectionTimer;
    private bool attackHasImpacted;

    public int FactionId
    {
        get { return factionId; }
    }

    public float Health
    {
        get { return health; }
    }

    public float MaximumHealth
    {
        get { return combatSettings != null ? combatSettings.maximumHealth : 0f; }
    }

    public float AttackDamage
    {
        get { return combatSettings != null ? combatSettings.attackDamage : 0f; }
    }

    public float AttackRange
    {
        get { return combatSettings != null ? combatSettings.attackRange : 0f; }
    }

    public float AttackWindupSeconds
    {
        get { return combatSettings != null ? combatSettings.attackWindupSeconds : 0f; }
    }

    public float AttackRecoverySeconds
    {
        get { return combatSettings != null ? combatSettings.attackRecoverySeconds : 0f; }
    }

    public bool IsAlive { get; private set; }
    public bool IsCombatEngaged { get; private set; }

    protected virtual int DefaultFactionId
    {
        get { return 1; }
    }

    protected virtual float DetectionReactionTime
    {
        get { return combatSettings != null ? combatSettings.monsterReactionTimeSeconds : 0f; }
    }

    protected override void Awake()
    {
        base.Awake();
        factionId = DefaultFactionId;
        combatSettings = GameData.Instance.combatSettings;
        if (combatSettings == null)
        {
            Debug.LogError("Combatant settings could not be loaded from GameData.", this);
            enabled = false;
            return;
        }

        health = combatSettings.maximumHealth;
        IsAlive = true;
        Transform rightArm = FindRightArm();
        if (rightArm != null)
        {
            ConfigureCombatAnimation(rightArm);
        }
    }

    protected virtual void OnEnable()
    {
        if (!activeCombatants.Contains(this))
        {
            activeCombatants.Add(this);
        }
    }

    protected virtual void OnDisable()
    {
        activeCombatants.Remove(this);
    }

    protected virtual void Update()
    {
        if (!IsAlive || combatSettings == null)
        {
            return;
        }

        if (currentTarget != null && (!currentTarget.IsAlive || !currentTarget.isActiveAndEnabled))
        {
            currentTarget = null;
            StopMoving();
            SetMovementSpeed(DefaultMovementSpeed);
            IsCombatEngaged = false;
            detectionTimer = 0f;
            ResetAttack();
        }

        if (currentTarget == null)
        {
            detectionTimer -= Time.deltaTime;
            if (detectionTimer <= 0f)
            {
                currentTarget = FindEnemyInDetectionCone();
                detectionTimer = Mathf.Max(
                    Time.deltaTime,
                    DetectionReactionTime * combatSettings.detectionIntervalReactionTimeMultiplier);
            }
        }

        if (currentTarget == null)
        {
            if (IsCombatEngaged)
            {
                StopMoving();
                SetMovementSpeed(DefaultMovementSpeed);
                ResetAttack();
            }

            IsCombatEngaged = false;
            base.Update();
            return;
        }

        if (!IsCombatEngaged)
        {
            SetMovementSpeed(RunningSpeed);
        }

        IsCombatEngaged = true;
        float distance = Vector2.Distance(transform.position, currentTarget.transform.position);
        UpdateFacingFromDirection(currentTarget.transform.position - transform.position);
        if (distance > combatSettings.attackRange)
        {
            ResetAttack();
            MoveTo(currentTarget.transform.position, combatSettings.attackRange);
        }
        else
        {
            StopMoving();
            UpdateAttack(currentTarget);
        }

        base.Update();
    }

    public bool SetFaction(int newFactionId)
    {
        if (newFactionId < 0)
        {
            Debug.LogError("Combatant faction IDs cannot be negative.", this);
            return false;
        }

        factionId = newFactionId;
        currentTarget = null;
        StopMoving();
        SetMovementSpeed(DefaultMovementSpeed);
        detectionTimer = 0f;
        IsCombatEngaged = false;
        ResetAttack();
        return true;
    }

    internal bool ConfigureCombatSettings(CombatSettings settings)
    {
        if (settings == null)
        {
            Debug.LogError("Cannot configure a combatant without combat settings.", this);
            return false;
        }

        combatSettings = settings;
        health = settings.maximumHealth;
        IsAlive = true;
        currentTarget = null;
        detectionTimer = 0f;
        IsCombatEngaged = false;
        ResetAttack();
        return true;
    }

    public void ConfigureCombatAnimation(Transform combatantArm)
    {
        if (combatantArm == null)
        {
            Debug.LogError("Cannot configure combat animation without an arm transform.", this);
            return;
        }

        armMotion = GetComponent<VillagerHandMotion>();
        if (armMotion == null)
        {
            armMotion = gameObject.AddComponent<VillagerHandMotion>();
        }

        armMotion.Configure(combatantArm);
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
        {
            return;
        }

        health = Mathf.Max(0f, health - amount);
        if (health <= 0f)
        {
            Die();
        }
    }

    protected void PlayArmMotion(ArmMotionPoint[] positions, float duration)
    {
        if (armMotion != null)
        {
            armMotion.PlayHit(positions, duration);
        }
    }

    protected virtual void OnDeath()
    {
    }

    private Combatant FindEnemyInDetectionCone()
    {
        float gridCellSize = GameData.Instance.mapSettings.gridCellSize;
        if (gridCellSize <= 0f)
        {
            return null;
        }

        Vector2Int ownCell = WorldPositionToGridCell(transform.position, gridCellSize);
        Vector2Int forward = FacingDirection;
        Combatant detected = null;
        int nearestDistanceSquared = int.MaxValue;
        for (int index = activeCombatants.Count - 1; index >= 0; index--)
        {
            Combatant candidate = activeCombatants[index];
            if (candidate == null || !candidate.IsAlive || !candidate.isActiveAndEnabled)
            {
                activeCombatants.RemoveAt(index);
                continue;
            }

            if (candidate == this || candidate.factionId == factionId)
            {
                continue;
            }

            Vector2Int candidateCell = WorldPositionToGridCell(
                candidate.transform.position,
                gridCellSize);
            Vector2Int offset = candidateCell - ownCell;
            int forwardDistance = offset.x * forward.x + offset.y * forward.y;
            int sideDistance = Mathf.Abs(offset.x * forward.y - offset.y * forward.x);
            if (forwardDistance < 1
                || forwardDistance > combatSettings.detectionRangeCells
                || sideDistance > forwardDistance)
            {
                continue;
            }

            int distanceSquared = offset.sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                detected = candidate;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return detected;
    }

    private static Vector2Int WorldPositionToGridCell(Vector3 position, float gridCellSize)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / gridCellSize),
            Mathf.FloorToInt(position.y / gridCellSize));
    }

    private void UpdateAttack(Combatant target)
    {
        if (attackRecoveryTimer > 0f)
        {
            attackRecoveryTimer = Mathf.Max(0f, attackRecoveryTimer - Time.deltaTime);
            return;
        }

        if (attackWindupTimer <= 0f)
        {
            attackWindupTimer = combatSettings.attackWindupSeconds;
            attackHasImpacted = false;
            PlayArmMotion(
                combatSettings.attackHitPositions,
                combatSettings.attackWindupSeconds + combatSettings.attackRecoverySeconds);
        }

        attackWindupTimer = Mathf.Max(0f, attackWindupTimer - Time.deltaTime);
        if (!attackHasImpacted && attackWindupTimer <= 0f)
        {
            attackHasImpacted = true;
            if (target != null
                && target.IsAlive
                && target.factionId != factionId
                && Vector2.Distance(transform.position, target.transform.position) <= combatSettings.attackRange)
            {
                target.TakeDamage(combatSettings.attackDamage);
            }

            attackRecoveryTimer = combatSettings.attackRecoverySeconds;
        }
    }

    private void ResetAttack()
    {
        attackWindupTimer = 0f;
        attackRecoveryTimer = 0f;
        attackHasImpacted = false;
    }

    private Transform FindRightArm()
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in childTransforms)
        {
            if (child != transform && child.name == "rightArm")
            {
                return child;
            }
        }

        return null;
    }

    protected void Die()
    {
        if (!IsAlive)
        {
            return;
        }

        IsAlive = false;
        IsCombatEngaged = false;
        activeCombatants.Remove(this);
        StopMoving();
        OnDeath();
        Destroy(gameObject);
    }
}
