using System.Collections.Generic;
using UnityEngine;

public abstract class Combatant : Mover
{
    private static readonly List<Combatant> activeCombatants = new List<Combatant>();
    private static int nextCombatantOrder;

    private int combatantOrder;
    private int factionId;

    private CombatSettings combatSettings;
    private VillagerHandMotion armMotion;
    private Combatant currentTarget;
    private float health;
    private float combatSideDirection;
    private float attackWindupTimer;
    private float attackAnimationEndTime;
    private float attackPositionLockedUntil;
    private float nextAttackAllowedTime;
    private float detectionTimer;
    private bool attackInProgress;
    private bool attackHasImpacted;
    private Vector3 knockbackStartPosition;
    private Vector3 knockbackEndPosition;
    private float knockbackDuration;
    private float knockbackElapsed;
    private CombatEncounter combatEncounter;
    private float combatSlotRetryTimer;

    private sealed class CombatEncounter
    {
        private readonly List<Combatant> participants = new List<Combatant>();
        private readonly Dictionary<Combatant, Vector2Int> slots =
            new Dictionary<Combatant, Vector2Int>();
        private readonly Dictionary<Combatant, float> nextSlotRetryTimes =
            new Dictionary<Combatant, float>();
        private readonly HashSet<Vector2Int> reservedCells = new HashSet<Vector2Int>();
        private Vector2 anchorPosition;
        private bool anchorNeedsRefresh;

        public CombatEncounter(Combatant first, Combatant second)
        {
            anchorPosition = ((Vector2)first.transform.position + (Vector2)second.transform.position) * 0.5f;
            AddParticipant(first);
            AddParticipant(second);
        }

        public void AddParticipant(Combatant participant)
        {
            if (participant == null || participants.Contains(participant))
            {
                return;
            }

            participants.Add(participant);
            participant.combatEncounter = this;
        }

        public void Merge(CombatEncounter other)
        {
            if (other == null || other == this)
            {
                return;
            }

            foreach (Combatant participant in other.participants)
            {
                AddParticipant(participant);
            }

            anchorPosition = (anchorPosition + other.anchorPosition) * 0.5f;
            ClearSlots();
            anchorNeedsRefresh = true;
            other.participants.Clear();
            other.ClearSlots();
        }

        public void RemoveParticipant(Combatant participant)
        {
            participants.Remove(participant);
            slots.Remove(participant);
            nextSlotRetryTimes.Remove(participant);
            RebuildReservedCells();
            if (participant != null && participant.combatEncounter == this)
            {
                participant.combatEncounter = null;
            }

            if (participants.Count < 2)
            {
                foreach (Combatant remaining in participants)
                {
                    if (remaining != null && remaining.combatEncounter == this)
                    {
                        remaining.combatEncounter = null;
                    }
                }

                participants.Clear();
                ClearSlots();
            }
            else
            {
                ClearSlots();
                anchorNeedsRefresh = true;
            }
        }

        public void ReleaseSlotsForKnockback()
        {
            ClearSlots();
            anchorNeedsRefresh = true;
        }

        public bool TryGetDestination(Combatant participant, out Vector3 destination)
        {
            destination = Vector3.zero;
            if (participant == null || !participants.Contains(participant))
            {
                return false;
            }

            foreach (Combatant member in participants)
            {
                if (member == null || member.IsInKnockback)
                {
                    return false;
                }
            }

            if (anchorNeedsRefresh)
            {
                RefreshAnchor();
            }

            MapManager mapManager = MapManager.Instance;
            float gridCellSize = mapManager != null ? mapManager.GridCellSize : 0f;
            if (mapManager == null || gridCellSize <= 0f)
            {
                return false;
            }

            if (slots.Count < 2 && !TryAssignPairSlots(mapManager, gridCellSize))
            {
                return false;
            }

            for (int index = 2; index < participants.Count; index++)
            {
                Combatant member = participants[index];
                if (member != null
                    && !slots.ContainsKey(member)
                    && member.currentTarget != null)
                {
                    if (!nextSlotRetryTimes.TryGetValue(member, out float retryTime)
                        || Time.time >= retryTime)
                    {
                        if (TryAssignAdditionalSlot(member, mapManager, gridCellSize))
                        {
                            nextSlotRetryTimes.Remove(member);
                        }
                        else
                        {
                            nextSlotRetryTimes[member] = Time.time
                                + GameData.Instance.combatSettings.combatSlotRetrySeconds;
                        }
                    }
                }
            }

            if (!slots.TryGetValue(participant, out Vector2Int cell))
            {
                return false;
            }

            destination = mapManager.GetGridCellBounds(cell).center;
            destination.z = participant.transform.position.z;
            return true;
        }

        private bool TryAssignPairSlots(MapManager mapManager, float gridCellSize)
        {
            if (participants.Count < 2)
            {
                return false;
            }

            Combatant first = participants[0];
            Combatant second = participants[1];
            if (first == null || second == null)
            {
                return false;
            }

            Combatant leftCombatant = first.transform.position.x < second.transform.position.x
                || (Mathf.Approximately(first.transform.position.x, second.transform.position.x)
                    && first.combatantOrder < second.combatantOrder)
                ? first
                : second;
            Combatant rightCombatant = leftCombatant == first ? second : first;
            float minimumRange = Mathf.Min(
                leftCombatant.GetEffectiveAttackRange(gridCellSize),
                rightCombatant.GetEffectiveAttackRange(gridCellSize));
            int preferredSeparationCells = Mathf.Max(
                1,
                Mathf.FloorToInt(minimumRange / gridCellSize));
            Vector2Int centerCell = WorldPositionToGridCell(
                new Vector3(anchorPosition.x, anchorPosition.y, 0f),
                gridCellSize);
            int searchRadius = Mathf.Min(
                10,
                Mathf.Max(3, preferredSeparationCells + 2));

            for (int separationCells = preferredSeparationCells;
                separationCells >= 1;
                separationCells--)
            {
                bool foundPair = false;
                float bestScore = float.PositiveInfinity;
                Vector2Int bestLeftCell = Vector2Int.zero;
                Vector2Int bestRightCell = Vector2Int.zero;

                for (int yOffset = -searchRadius; yOffset <= searchRadius; yOffset++)
                {
                    for (int xOffset = -searchRadius; xOffset <= searchRadius; xOffset++)
                    {
                        Vector2Int leftCell = new Vector2Int(
                            centerCell.x + xOffset,
                            centerCell.y + yOffset);
                        Vector2Int rightCell = new Vector2Int(
                            leftCell.x + separationCells,
                            leftCell.y);
                        if (!CanReservePairCell(leftCell, leftCombatant, mapManager)
                            || !CanReservePairCell(rightCell, rightCombatant, mapManager))
                        {
                            continue;
                        }

                        Vector2 pairCenter =
                            ((Vector2)mapManager.GetGridCellBounds(leftCell).center
                                + (Vector2)mapManager.GetGridCellBounds(rightCell).center) * 0.5f;
                        float score = (pairCenter - anchorPosition).sqrMagnitude;
                        if (score < bestScore)
                        {
                            foundPair = true;
                            bestScore = score;
                            bestLeftCell = leftCell;
                            bestRightCell = rightCell;
                        }
                    }
                }

                if (foundPair)
                {
                    slots[leftCombatant] = bestLeftCell;
                    slots[rightCombatant] = bestRightCell;
                    reservedCells.Add(bestLeftCell);
                    reservedCells.Add(bestRightCell);
                    return true;
                }
            }

            return false;
        }

        private bool CanReservePairCell(
            Vector2Int cell,
            Combatant owner,
            MapManager mapManager)
        {
            return !reservedCells.Contains(cell)
                && mapManager.IsCellAvailableForMovement(cell)
                && !IsCurrentCellOccupiedByOtherParticipant(
                    cell,
                    owner,
                    null,
                    mapManager.GridCellSize);
        }

        private bool TryAssignAdditionalSlot(
            Combatant participant,
            MapManager mapManager,
            float gridCellSize)
        {
            Combatant target = participant.currentTarget;
            if (target == null)
            {
                return false;
            }

            float reach = participant.GetEffectiveAttackRange(gridCellSize);
            Vector2Int targetCell = WorldPositionToGridCell(target.transform.position, gridCellSize);
            int cellRadius = Mathf.CeilToInt(reach / gridCellSize);
            bool foundCell = false;
            float bestScore = float.PositiveInfinity;
            Vector2Int bestCell = Vector2Int.zero;

            for (int xOffset = -cellRadius; xOffset <= cellRadius; xOffset++)
            {
                for (int yOffset = -cellRadius; yOffset <= cellRadius; yOffset++)
                {
                    Vector2Int candidate = new Vector2Int(
                        targetCell.x + xOffset,
                        targetCell.y + yOffset);
                    if (reservedCells.Contains(candidate)
                        || !mapManager.IsCellAvailableForMovement(candidate)
                        || IsCurrentCellOccupiedByOtherParticipant(
                            candidate,
                            participant,
                            null,
                            gridCellSize))
                    {
                        continue;
                    }

                    Vector2 candidatePosition = mapManager.GetGridCellBounds(candidate).center;
                    float distance = Vector2.Distance(candidatePosition, target.transform.position);
                    if (distance > reach)
                    {
                        continue;
                    }

                    float score = distance;
                    if (candidate.y != targetCell.y)
                    {
                        score += gridCellSize * 0.25f;
                    }

                    if (participant.combatSideDirection != 0f
                        && (candidate.x - targetCell.x) * participant.combatSideDirection < 0f)
                    {
                        score += gridCellSize * 2f;
                    }

                    if (score < bestScore)
                    {
                        foundCell = true;
                        bestScore = score;
                        bestCell = candidate;
                    }
                }
            }

            if (foundCell)
            {
                slots[participant] = bestCell;
                reservedCells.Add(bestCell);
                return true;
            }

            return false;
        }

        private bool IsCurrentCellOccupiedByOtherParticipant(
            Vector2Int cell,
            Combatant firstAllowed,
            Combatant secondAllowed,
            float gridCellSize)
        {
            foreach (Combatant member in participants)
            {
                if (member == null || member == firstAllowed || member == secondAllowed)
                {
                    continue;
                }

                if (WorldPositionToGridCell(member.transform.position, gridCellSize) == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshAnchor()
        {
            Vector2 center = Vector2.zero;
            int count = 0;
            foreach (Combatant member in participants)
            {
                if (member == null)
                {
                    continue;
                }

                center += (Vector2)member.transform.position;
                count++;
            }

            if (count > 0)
            {
                anchorPosition = center / count;
            }

            anchorNeedsRefresh = false;
        }

        private void RebuildReservedCells()
        {
            reservedCells.Clear();
            foreach (Vector2Int cell in slots.Values)
            {
                reservedCells.Add(cell);
            }
        }

        private void ClearSlots()
        {
            slots.Clear();
            reservedCells.Clear();
            nextSlotRetryTimes.Clear();
        }
    }

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

    public float AttackCooldownSeconds
    {
        get { return combatSettings != null ? combatSettings.attackCooldownSeconds : 0f; }
    }

    public bool IsAlive { get; private set; }
    public bool IsCombatEngaged { get; private set; }
    protected bool IsKnockedBack { get; private set; }
    protected bool IsAttackPositionLocked
    {
        get { return Time.time < attackPositionLockedUntil; }
    }

    private bool IsInKnockback
    {
        get { return IsKnockedBack; }
    }

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
        combatantOrder = ++nextCombatantOrder;
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
        ReleaseCombatEncounter();
    }

    protected override void Update()
    {
        if (!IsAlive || combatSettings == null)
        {
            return;
        }

        if (IsKnockedBack)
        {
            if (attackInProgress && nextAttackAllowedTime > Time.time)
            {
                nextAttackAllowedTime += Time.deltaTime;
            }

            base.Update();
            UpdateKnockback(Time.deltaTime);
            return;
        }

        if (currentTarget != null && (!currentTarget.IsAlive || !currentTarget.isActiveAndEnabled))
        {
            ReleaseCombatEncounter();
            currentTarget = null;
            StopMoving();
            SetMovementSpeed(DefaultMovementSpeed);
            IsCombatEngaged = false;
            detectionTimer = 0f;
            combatSideDirection = 0f;
            ResetAttack(false);
        }

        bool attackPositionLocked = IsAttackPositionLocked;
        if (attackPositionLocked)
        {
            StopMoving();
        }

        if (currentTarget == null)
        {
            detectionTimer -= Time.deltaTime;
            if (detectionTimer <= 0f)
            {
                EngageTarget(FindEnemyInDetectionCone(), false);
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
                ResetAttack(false);
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
        EnsureCombatEncounter(currentTarget);

        if (attackInProgress)
        {
            StopMoving();
            if (!attackHasImpacted && !IsAttackPositionReady(currentTarget))
            {
                ResetAttack(false);
            }
            else
            {
                UpdateAttack(currentTarget);
                base.Update();
                return;
            }
        }

        bool hasCombatPosition = attackPositionLocked || MoveInCombat(currentTarget);
        if (hasCombatPosition && IsAttackPositionReady(currentTarget))
        {
            UpdateAttack(currentTarget);
        }
        else
        {
            ResetAttack(false);
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

        ReleaseCombatEncounter();
        factionId = newFactionId;
        currentTarget = null;
        StopMoving();
        SetMovementSpeed(DefaultMovementSpeed);
        detectionTimer = 0f;
        IsCombatEngaged = false;
        combatSideDirection = 0f;
        ResetAttack(true);
        return true;
    }

    internal bool ConfigureCombatSettings(CombatSettings settings)
    {
        if (settings == null)
        {
            Debug.LogError("Cannot configure a combatant without combat settings.", this);
            return false;
        }

        ReleaseCombatEncounter();
        combatSettings = settings;
        health = settings.maximumHealth;
        IsAlive = true;
        currentTarget = null;
        combatSideDirection = 0f;
        detectionTimer = 0f;
        IsCombatEngaged = false;
        ResetAttack(true);
        return true;
    }

    internal bool RestoreHealth(float restoredHealth)
    {
        if (float.IsNaN(restoredHealth)
            || float.IsInfinity(restoredHealth)
            || restoredHealth <= 0f
            || restoredHealth > MaximumHealth)
        {
            Debug.LogError("Cannot restore combatant health outside its valid range.", this);
            return false;
        }

        health = restoredHealth;
        IsAlive = true;
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
        TakeDamage(amount, null);
    }

    private void TakeDamage(float amount, Combatant attacker)
    {
        if (!IsAlive || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
        {
            return;
        }

        float damageTaken = Mathf.Min(health, amount);
        health = Mathf.Max(0f, health - damageTaken);
        UIManager uiManager = UIManager.Instance;
        if (uiManager != null)
        {
            Vector3 popupPosition = transform.position;
            Villager villager = this as Villager;
            if (villager != null && villager.HoverAnchor != null)
            {
                popupPosition = villager.HoverAnchor.position;
            }

            uiManager.ShowDamagePopup(popupPosition, damageTaken);
        }
        else
        {
            Debug.LogError("Displaying combat damage requires an active UIManager.", this);
        }

        if (health > 0f && IsValidEnemy(attacker))
        {
            EngageTarget(attacker, true);
            if (attacker.combatSettings != null && attacker.combatSettings.knockbackCells > 0)
            {
                BeginKnockback(attacker, attacker.combatSettings.knockbackCells);
            }
        }

        if (health <= 0f)
        {
            Die(attacker);
        }
    }

    protected void PlayArmMotion(ArmMotionPoint[] positions, float duration)
    {
        if (armMotion != null)
        {
            armMotion.PlayHit(positions, duration);
        }
    }

    protected virtual void PlayCombatAttackAnimation(float duration)
    {
        PlayArmMotion(combatSettings.attackHitPositions, duration);
    }

    protected virtual void CancelCombatAttackAnimation()
    {
    }

    protected virtual void OnDeath(Combatant killer)
    {
    }

    private Combatant FindEnemyInDetectionCone()
    {
        if (IsCowardVillager)
        {
            return null;
        }

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

    private void EngageTarget(Combatant target, bool prioritize)
    {
        if (!IsValidEnemy(target)
            || (currentTarget != null && currentTarget != target && !prioritize))
        {
            return;
        }

        if (currentTarget == target)
        {
            return;
        }

        bool enteringCombat = !IsCombatEngaged;
        if (currentTarget != null && currentTarget != target)
        {
            ReleaseCombatEncounter();
        }

        currentTarget = target;
        if (transform.position.x < target.transform.position.x)
        {
            combatSideDirection = -1f;
        }
        else if (transform.position.x > target.transform.position.x)
        {
            combatSideDirection = 1f;
        }
        else
        {
            combatSideDirection = combatantOrder < target.combatantOrder ? -1f : 1f;
        }

        IsCombatEngaged = true;
        SetMovementSpeed(RunningSpeed);
        EnsureCombatEncounter(target);
        if (enteringCombat && this is Villager)
        {
            CallForHelp(target);
        }
    }

    private bool IsValidEnemy(Combatant target)
    {
        return target != null
            && target != this
            && target.IsAlive
            && target.isActiveAndEnabled
            && target.factionId != factionId;
    }

    private void CallForHelp(Combatant enemy)
    {
        VillagerSettings settings = GameData.Instance.villagerSettings;
        MapSettings mapSettings = GameData.Instance.mapSettings;
        if (settings == null || mapSettings == null || mapSettings.gridCellSize <= 0f)
        {
            return;
        }

        float helpRange = settings.combatHelpCallRangeCells * mapSettings.gridCellSize;
        for (int index = activeCombatants.Count - 1; index >= 0; index--)
        {
            Combatant nearby = activeCombatants[index];
            if (nearby == null || !nearby.IsAlive || !nearby.isActiveAndEnabled)
            {
                activeCombatants.RemoveAt(index);
                continue;
            }

            if (nearby == this
                || nearby.factionId != factionId
                || !(nearby is Villager)
                || Vector2.Distance(transform.position, nearby.transform.position) > helpRange)
            {
                continue;
            }

            Villager nearbyVillager = (Villager)nearby;
            if (!nearbyVillager.HasPersonality(VillagerPersonalityType.Coward))
            {
                nearby.EngageTarget(enemy, false);
            }
        }
    }

    private bool IsCowardVillager
    {
        get
        {
            Villager villager = this as Villager;
            return villager != null
                && villager.HasPersonality(VillagerPersonalityType.Coward);
        }
    }

    private bool MoveInCombat(Combatant target)
    {
        if (combatEncounter != null)
        {
            if (combatSlotRetryTimer > 0f)
            {
                combatSlotRetryTimer = Mathf.Max(0f, combatSlotRetryTimer - Time.deltaTime);
                if (combatSlotRetryTimer > 0f)
                {
                    StopMoving();
                    return false;
                }
            }

            if (!combatEncounter.TryGetDestination(this, out Vector3 combatDestination))
            {
                StopMoving();
                combatSlotRetryTimer = GameData.Instance.combatSettings.combatSlotRetrySeconds;
                return false;
            }

            combatSlotRetryTimer = 0f;
            UpdateFacingFromDirection(target.transform.position - transform.position);
            MoveTo(combatDestination, 0.05f);
            return true;
        }

        return MoveTowardCombatTarget(target);
    }

    private bool MoveTowardCombatTarget(Combatant target)
    {
        MapSettings mapSettings = GameData.Instance.mapSettings;
        float gridCellSize = mapSettings != null ? mapSettings.gridCellSize : 0f;
        if (gridCellSize <= 0f)
        {
            UpdateFacingFromDirection(target.transform.position - transform.position);
            MoveTo(target.transform.position, GetEffectiveAttackRange(gridCellSize));
            return true;
        }

        float rowCenter = Mathf.Floor(target.transform.position.y / gridCellSize) * gridCellSize
            + gridCellSize * 0.5f;
        float rowAlignmentTolerance = gridCellSize * 0.05f;
        if (Mathf.Abs(transform.position.y - rowCenter) > rowAlignmentTolerance)
        {
            MoveTo(new Vector3(transform.position.x, rowCenter, transform.position.z));
            UpdateFacingFromDirection(new Vector2(
                target.transform.position.x - transform.position.x,
                0f));
            return true;
        }

        float preferredDistance = Mathf.Max(
            gridCellSize,
            Mathf.Min(
                GetEffectiveAttackRange(gridCellSize),
                target.GetEffectiveAttackRange(gridCellSize)) * 0.4f);
        float destinationX = target.transform.position.x
            + combatSideDirection * preferredDistance;
        MapManager mapManager = MapManager.Instance;
        if (mapManager != null)
        {
            Vector2 mapMinimum = (Vector2)mapManager.MapCenter - mapManager.MapSize * 0.5f;
            Vector2 mapMaximum = (Vector2)mapManager.MapCenter + mapManager.MapSize * 0.5f;
            float mapInset = gridCellSize * 0.5f;
            destinationX = Mathf.Clamp(destinationX, mapMinimum.x + mapInset, mapMaximum.x - mapInset);
        }

        UpdateFacingFromDirection(new Vector2(
            target.transform.position.x - transform.position.x,
            0f));
        MoveTo(new Vector3(destinationX, rowCenter, transform.position.z), 0.05f);
        return true;
    }

    private bool IsWithinAttackRange(Combatant target)
    {
        MapManager mapManager = MapManager.Instance;
        float gridCellSize = mapManager != null
            ? mapManager.GridCellSize
            : GameData.Instance.mapSettings.gridCellSize;
        return Vector2.Distance(transform.position, target.transform.position)
            <= GetEffectiveAttackRange(gridCellSize);
    }

    private bool IsAttackPositionReady(Combatant target)
    {
        if (!IsValidEnemy(target)
            || IsKnockedBack
            || target.IsKnockedBack
            || !HasSettledMovement
            || !IsWithinAttackRange(target))
        {
            return false;
        }

        Vector2 directionToTarget = target.transform.position - transform.position;
        Vector2 facingDirection = new Vector2(FacingDirection.x, FacingDirection.y);
        if (directionToTarget.sqrMagnitude <= 0.000001f
            || Vector2.Dot(directionToTarget, facingDirection) <= 0f)
        {
            return false;
        }

        if (combatEncounter == null)
        {
            return true;
        }

        MapManager mapManager = MapManager.Instance;
        if (target.combatEncounter != combatEncounter
            || mapManager == null
            || mapManager.GridCellSize <= 0f
            || !combatEncounter.TryGetDestination(this, out Vector3 ownDestination)
            || !combatEncounter.TryGetDestination(target, out Vector3 targetDestination))
        {
            return false;
        }

        float positionTolerance = Mathf.Max(0.05f, mapManager.GridCellSize * 0.05f);
        return Vector2.Distance(transform.position, ownDestination) <= positionTolerance
            && Vector2.Distance(target.transform.position, targetDestination) <= positionTolerance;
    }

    private void HoldPositionForAttack(float duration)
    {
        attackPositionLockedUntil = Mathf.Max(
            attackPositionLockedUntil,
            Time.time + duration);
        StopMoving();
    }

    private float GetEffectiveAttackRange(float gridCellSize)
    {
        return combatSettings.attackRange + Mathf.Max(0f, gridCellSize);
    }

    private void EnsureCombatEncounter(Combatant target)
    {
        if (!IsValidEnemy(target)
            || (target.currentTarget != this
                && target.combatEncounter == null
                && combatEncounter == null))
        {
            return;
        }

        CombatEncounter encounter = combatEncounter != null
            ? combatEncounter
            : target.combatEncounter;
        if (encounter == null)
        {
            encounter = new CombatEncounter(this, target);
        }
        else
        {
            if (target.combatEncounter != null
                && target.combatEncounter != encounter)
            {
                encounter.Merge(target.combatEncounter);
            }

            encounter.AddParticipant(this);
            encounter.AddParticipant(target);
        }
    }

    private void ReleaseCombatEncounter()
    {
        combatSlotRetryTimer = 0f;
        if (combatEncounter != null)
        {
            combatEncounter.RemoveParticipant(this);
            combatEncounter = null;
        }
    }

    private static Vector2Int WorldPositionToGridCell(Vector3 position, float gridCellSize)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / gridCellSize),
            Mathf.FloorToInt(position.y / gridCellSize));
    }

    private void UpdateAttack(Combatant target)
    {
        if (attackInProgress && attackHasImpacted)
        {
            if (Time.time < attackAnimationEndTime)
            {
                return;
            }

            ResetAttack(false);
            return;
        }

        if (!attackInProgress)
        {
            if (Time.time < nextAttackAllowedTime
                || !IsAttackPositionReady(target))
            {
                return;
            }

            attackInProgress = true;
            attackWindupTimer = combatSettings.attackWindupSeconds;
            attackHasImpacted = false;
            float animationDuration = combatSettings.attackWindupSeconds
                + combatSettings.attackRecoverySeconds;
            attackAnimationEndTime = Time.time + animationDuration;
            nextAttackAllowedTime = attackAnimationEndTime + combatSettings.attackCooldownSeconds;
            SetMovementAnimationSuppressed(true);
            StopMoving();
            target.HoldPositionForAttack(animationDuration);
            PlayCombatAttackAnimation(animationDuration);
        }

        attackWindupTimer = Mathf.Max(0f, attackWindupTimer - Time.deltaTime);
        if (!attackHasImpacted && attackWindupTimer <= 0f)
        {
            if (!IsAttackPositionReady(target))
            {
                ResetAttack(false);
                return;
            }

            attackHasImpacted = true;
            target.TakeDamage(combatSettings.attackDamage, this);
        }
    }

    private void ResetAttack(bool resetCooldown)
    {
        bool wasInProgress = attackInProgress;
        attackWindupTimer = 0f;
        attackInProgress = false;
        attackHasImpacted = false;
        attackAnimationEndTime = 0f;
        if (wasInProgress)
        {
            SetMovementAnimationSuppressed(false);
            CancelCombatAttackAnimation();
        }

        if (resetCooldown)
        {
            nextAttackAllowedTime = Time.time;
        }
    }

    private void BeginKnockback(Combatant attacker, int distanceInCells)
    {
        MapManager mapManager = MapManager.Instance;
        GameData gameData = GameData.Instance;
        float gridCellSize = mapManager != null ? mapManager.GridCellSize : 0f;
        if (mapManager == null || gridCellSize <= 0f)
        {
            Debug.LogError("Knockback requires an active MapManager with a valid grid.", this);
            return;
        }

        Vector2 direction = (Vector2)transform.position - (Vector2)attacker.transform.position;
        if (direction.sqrMagnitude <= 0.000001f)
        {
            Vector2Int fallbackDirection = -attacker.FacingDirection;
            direction = new Vector2(fallbackDirection.x, fallbackDirection.y);
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = Vector2.right;
            }
        }

        direction.Normalize();
        float requestedDistance = distanceInCells * gridCellSize;
        float availableDistance = GetAvailableKnockbackDistance(
            mapManager,
            transform.position,
            direction,
            requestedDistance,
            gridCellSize);
        if (availableDistance <= 0.001f)
        {
            return;
        }

        CombatSettings sharedCombatSettings = gameData.combatSettings;
        if (sharedCombatSettings == null
            || sharedCombatSettings.knockbackDurationPerCellSeconds <= 0f)
        {
            Debug.LogError("Knockback duration settings could not be loaded from GameData.", this);
            return;
        }

        knockbackStartPosition = transform.position;
        ResetAttack(false);
        knockbackEndPosition = knockbackStartPosition + (Vector3)(direction * availableDistance);
        knockbackEndPosition.z = knockbackStartPosition.z;
        knockbackDuration = availableDistance / gridCellSize
            * sharedCombatSettings.knockbackDurationPerCellSeconds;
        knockbackElapsed = 0f;
        IsKnockedBack = true;
        if (combatEncounter != null)
        {
            combatEncounter.ReleaseSlotsForKnockback();
        }
        StopMoving();
        SetMovementAnimationSuppressed(true);
        if (armMotion != null)
        {
            armMotion.SetPaused(true);
        }
    }

    private static float GetAvailableKnockbackDistance(
        MapManager mapManager,
        Vector3 startPosition,
        Vector2 direction,
        float requestedDistance,
        float gridCellSize)
    {
        int sampleCount = Mathf.CeilToInt(requestedDistance / (gridCellSize * 0.05f));
        Vector2Int lastCell = WorldPositionToGridCell(startPosition, gridCellSize);
        float lastAvailableDistance = 0f;
        for (int sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
        {
            float distance = requestedDistance * sampleIndex / sampleCount;
            Vector2 samplePosition = (Vector2)startPosition + direction * distance;
            Vector2Int sampleCell = WorldPositionToGridCell(samplePosition, gridCellSize);
            if (sampleCell == lastCell)
            {
                lastAvailableDistance = distance;
                continue;
            }

            if (!mapManager.IsCellAvailableForMovement(sampleCell))
            {
                break;
            }

            lastCell = sampleCell;
            lastAvailableDistance = distance;
        }

        return lastAvailableDistance;
    }

    private void UpdateKnockback(float deltaTime)
    {
        knockbackElapsed = Mathf.Min(knockbackDuration, knockbackElapsed + deltaTime);
        float progress = knockbackElapsed / knockbackDuration;
        transform.position = Vector3.Lerp(knockbackStartPosition, knockbackEndPosition, progress);
        if (knockbackElapsed < knockbackDuration)
        {
            return;
        }

        IsKnockedBack = false;
        SetMovementAnimationSuppressed(false);
        if (armMotion != null)
        {
            armMotion.SetPaused(false);
        }
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

    protected void Die(Combatant killer = null)
    {
        if (!IsAlive)
        {
            return;
        }

        IsAlive = false;
        IsCombatEngaged = false;
        ReleaseCombatEncounter();
        activeCombatants.Remove(this);
        StopMoving();
        OnDeath(killer);
        Destroy(gameObject);
    }
}
