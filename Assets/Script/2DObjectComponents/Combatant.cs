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

        Combatant target = FindNearestEnemy();
        if (target == null)
        {
            if (IsCombatEngaged)
            {
                StopMoving();
                ResetAttack();
            }

            currentTarget = null;
            IsCombatEngaged = false;
            base.Update();
            return;
        }

        if (currentTarget != target)
        {
            ResetAttack();
            currentTarget = target;
        }

        IsCombatEngaged = true;
        float distance = Vector2.Distance(transform.position, target.transform.position);
        UpdateFacingFromDirection(target.transform.position - transform.position);
        if (distance > combatSettings.attackRange)
        {
            ResetAttack();
            MoveTo(target.transform.position, combatSettings.attackRange);
        }
        else
        {
            StopMoving();
            UpdateAttack(target);
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

    private Combatant FindNearestEnemy()
    {
        Combatant nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;
        for (int index = activeCombatants.Count - 1; index >= 0; index--)
        {
            Combatant candidate = activeCombatants[index];
            if (candidate == null || !candidate.IsAlive)
            {
                activeCombatants.RemoveAt(index);
                continue;
            }

            if (candidate == this || candidate.factionId == factionId)
            {
                continue;
            }

            float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearest = candidate;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest;
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
