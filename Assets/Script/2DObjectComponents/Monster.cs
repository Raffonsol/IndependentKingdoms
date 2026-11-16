using System;
using System.Collections.Generic;
using UnityEngine;

public class Monster : Combatant
{
    private float wanderTimer;
    private bool isWandering;
    private MonsterAttackAnimator attackAnimator;
    private MonsterLootDropDefinition[] deathDrops;

    [SerializeField] private Transform monsterVisualsRoot;

    public string DefinitionId { get; private set; }

    protected override int DefaultFactionId
    {
        get { return 0; }
    }

    protected override bool PrioritizeRoadPaths
    {
        get { return false; }
    }

    public bool Configure(MonsterDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogError("Cannot configure a monster without a definition.", this);
            return false;
        }

        if (!ConfigureCombatSettings(definition.combatSettings))
        {
            return false;
        }

        DefinitionId = definition.id;
        if (!ConfigureShadow(definition.shadowSize, definition.shadowOffset))
        {
            return false;
        }

        ConfigureMovement(
            definition.movementSpeed,
            definition.combatSettings.runningSpeed,
            definition.movementDurationRange,
            definition.idleDurationRange);
        if (!ConfigureLegs(definition.legDefinitions))
        {
            return false;
        }

        bool bodyPartsConfigured = SetPartSprite("face", definition.faceSprites)
            && SetPartSprite("torso", definition.torsoSprites)
            && SetPartSprite("frontLeg", definition.frontLegSprites)
            && SetPartSprite("backLeg", definition.backLegSprites)
            && SetPartSprite("frontLegRight", definition.frontLegSprites, definition.rightFrontLegOffset)
            && SetPartSprite("backLegRight", definition.backLegSprites, definition.rightBackLegOffset)
            && SetPartSprite("tail", definition.tailSprites);
        if (!bodyPartsConfigured)
        {
            return false;
        }

        attackAnimator = new MonsterAttackAnimator();
        if (!attackAnimator.Configure(
            transform,
            monsterVisualsRoot,
            definition.attackAnimationFrames,
            this))
        {
            return false;
        }

        deathDrops = definition.deathDrops ?? new MonsterLootDropDefinition[0];
        return true;
    }

    private bool ConfigureLegs(MonsterLegDefinition[] definitions)
    {
        if (definitions == null || definitions.Length == 0)
        {
            Debug.LogError("Monster definitions must configure at least one leg.", this);
            return false;
        }

        LegDefinitions[] configuredLegs = new LegDefinitions[definitions.Length];
        for (int index = 0; index < definitions.Length; index++)
        {
            MonsterLegDefinition definition = definitions[index];
            Transform leg = FindPart(definition.partName);
            if (leg == null)
            {
                Debug.LogError(
                    "Monster prefab is missing the '" + definition.partName + "' leg.",
                    this);
                return false;
            }

            configuredLegs[index] = new LegDefinitions
            {
                leg = leg.gameObject,
                legSwingAngle = definition.legSwingAngle,
                legSwingSpeed = definition.legSwingSpeed,
                legPivotOffset = definition.legPivotOffset,
                legSwingRunningAngle = definition.legSwingRunningAngle,
                legSwingRunningSpeed = definition.legSwingRunningSpeed
            };
        }

        ConfigureLegDefinitions(configuredLegs);
        return true;
    }

    protected override void Update()
    {
        base.Update();
        if (attackAnimator != null)
        {
            attackAnimator.Update(IsKnockedBack ? 0f : Time.deltaTime);
        }

        if (IsAttackPositionLocked || (attackAnimator != null && attackAnimator.IsPlaying))
        {
            return;
        }

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
            Move(UnityEngine.Random.Range(0f, 360f));
            wanderTimer = UnityEngine.Random.Range(MovementDurationRange.x, MovementDurationRange.y);
        }
        else
        {
            StopMoving();
            wanderTimer = UnityEngine.Random.Range(IdleDurationRange.x, IdleDurationRange.y);
        }
    }

    protected override void PlayCombatAttackAnimation(float duration)
    {
        if (attackAnimator != null)
        {
            StopMoving();
            attackAnimator.Play(duration);
            return;
        }

        base.PlayCombatAttackAnimation(duration);
    }

    protected override void CancelCombatAttackAnimation()
    {
        if (attackAnimator != null)
        {
            attackAnimator.Cancel();
        }
    }

    protected override void OnDeath(Combatant killer)
    {
        base.OnDeath(killer);
        if (deathDrops == null || deathDrops.Length == 0)
        {
            return;
        }

        MapManager mapManager = MapManager.Instance;
        if (mapManager == null)
        {
            Debug.LogError("Dropping monster loot requires an active MapManager.", this);
            return;
        }

        Villager preferredCollector = killer as Villager;
        foreach (MonsterLootDropDefinition drop in deathDrops)
        {
            if (drop == null)
            {
                Debug.LogError("Monster has an invalid configured death drop.", this);
                continue;
            }

            int amount = UnityEngine.Random.Range(
                drop.minimumAmount,
                drop.maximumAmount + 1);
            for (int index = 0; index < amount; index++)
            {
                bool spawned = drop.isResource
                    ? mapManager.SpawnDroppedResource(
                        transform.position,
                        drop.resourceType,
                        preferredCollector)
                    : mapManager.SpawnDroppedItem(
                        transform.position,
                        drop.itemType,
                        preferredCollector);
                if (!spawned)
                {
                    Debug.LogError(
                        "Could not spawn a configured loot drop from monster '" + DefinitionId + "'.",
                        this);
                    return;
                }
            }
        }
    }

    private bool SetPartSprite(string partName, Sprite[] sprites)
    {
        return SetPartSprite(partName, sprites, null);
    }

    private bool SetPartSprite(string partName, Sprite[] sprites, Vector2? localPosition)
    {
        Transform part = FindPart(partName);
        if (part == null)
        {
            Debug.LogError("Monster prefab is missing the '" + partName + "' part.", this);
            return false;
        }

        SpriteRenderer spriteRenderer = part.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || sprites == null || sprites.Length == 0)
        {
            Debug.LogError("Monster part '" + partName + "' has no renderer or configured sprites.", this);
            return false;
        }

        spriteRenderer.sprite = sprites[UnityEngine.Random.Range(0, sprites.Length)];
        if (localPosition.HasValue)
        {
            part.localPosition = new Vector3(
                localPosition.Value.x,
                localPosition.Value.y,
                part.localPosition.z);
        }

        return true;
    }

    private Transform FindPart(string partName)
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        string suffix = "_" + partName;
        foreach (Transform child in childTransforms)
        {
            if (child != transform
                && (string.Equals(child.name, partName, StringComparison.OrdinalIgnoreCase)
                    || child.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                return child;
            }
        }

        return null;
    }

    private sealed class MonsterAttackAnimator
    {
        private sealed class BodyPartState
        {
            public Transform transform;
            public SpriteRenderer spriteRenderer;
            public Vector3 originalLocalPosition;
            public Quaternion originalLocalRotation;
            public bool originalFlipX;
            public bool originalFlipY;
        }

        private readonly List<BodyPartState> bodyParts = new List<BodyPartState>();
        private readonly Dictionary<string, int> bodyPartIndices =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private Transform visualRoot;
        private Vector3 originalVisualLocalScale;
        private MonsterAttackFrame[] frames;
        private Vector3[][] framePositions;
        private bool[][] frameFlipX;
        private bool[][] frameFlipY;
        private bool[] frameVisualFlipX;
        private bool[] frameVisualFlipY;
        private float duration;
        private float elapsed;
        private bool isPlaying;

        public bool IsPlaying
        {
            get { return isPlaying; }
        }

        public bool Configure(
            Transform monsterRoot,
            Transform configuredVisualRoot,
            MonsterAttackFrame[] configuredFrames,
            Component context)
        {
            if (monsterRoot == null
                || configuredVisualRoot == null
                || configuredFrames == null
                || configuredFrames.Length == 0)
            {
                Debug.LogError(
                    "Monster attack animation requires a root, visual parent, and at least one frame.",
                    context);
                return false;
            }

            if (configuredVisualRoot == monsterRoot || !configuredVisualRoot.IsChildOf(monsterRoot))
            {
                Debug.LogError(
                    "Monster visual parent must be a child of the Monster root.",
                    context);
                return false;
            }

            visualRoot = configuredVisualRoot;
            originalVisualLocalScale = visualRoot.localScale;
            bodyParts.Clear();
            bodyPartIndices.Clear();
            SpriteRenderer[] renderers = monsterRoot.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer.transform == monsterRoot
                    || renderer.GetComponentInParent<GroundShadow>() != null)
                {
                    continue;
                }

                if (bodyPartIndices.ContainsKey(renderer.gameObject.name))
                {
                    Debug.LogError(
                        "Monster body-part object names must be unique for attack animation.",
                        context);
                    return false;
                }

                bodyPartIndices.Add(renderer.gameObject.name, bodyParts.Count);
                bodyParts.Add(new BodyPartState
                {
                    transform = renderer.transform,
                    spriteRenderer = renderer,
                    originalLocalPosition = renderer.transform.localPosition,
                    originalLocalRotation = renderer.transform.localRotation,
                    originalFlipX = renderer.flipX,
                    originalFlipY = renderer.flipY
                });
            }

            if (bodyParts.Count == 0)
            {
                Debug.LogError("Monster prefab has no body-part SpriteRenderers to animate.", context);
                return false;
            }

            frames = configuredFrames;
            framePositions = new Vector3[frames.Length][];
            frameFlipX = new bool[frames.Length][];
            frameFlipY = new bool[frames.Length][];
            frameVisualFlipX = new bool[frames.Length];
            frameVisualFlipY = new bool[frames.Length];
            Vector3[] currentPositions = new Vector3[bodyParts.Count];
            bool[] currentFlipX = new bool[bodyParts.Count];
            bool[] currentFlipY = new bool[bodyParts.Count];
            bool currentVisualFlipX = false;
            bool currentVisualFlipY = false;

            for (int partIndex = 0; partIndex < bodyParts.Count; partIndex++)
            {
                currentFlipX[partIndex] = bodyParts[partIndex].originalFlipX;
                currentFlipY[partIndex] = bodyParts[partIndex].originalFlipY;
            }

            for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
            {
                MonsterAttackFrame frame = frames[frameIndex];
                if (frame == null || frame.bodyParts == null)
                {
                    Debug.LogError("Monster attack animation contains an invalid frame.", context);
                    return false;
                }

                foreach (MonsterAttackPart part in frame.bodyParts)
                {
                    if (part == null || !IsValidPartName(part.name))
                    {
                        Debug.LogError(
                            "Monster attack animation references missing body part '"
                            + (part != null ? part.name : "<null>") + "'.",
                            context);
                        return false;
                    }

                    if (part.name == "all")
                    {
                        ApplyPart(
                            part,
                            currentPositions,
                            currentFlipX,
                            currentFlipY,
                            ref currentVisualFlipX,
                            ref currentVisualFlipY);
                    }
                }

                foreach (MonsterAttackPart part in frame.bodyParts)
                {
                    if (part.name != "all")
                    {
                        ApplyPart(
                            part,
                            currentPositions,
                            currentFlipX,
                            currentFlipY,
                            ref currentVisualFlipX,
                            ref currentVisualFlipY);
                    }
                }

                framePositions[frameIndex] = (Vector3[])currentPositions.Clone();
                frameFlipX[frameIndex] = (bool[])currentFlipX.Clone();
                frameFlipY[frameIndex] = (bool[])currentFlipY.Clone();
                frameVisualFlipX[frameIndex] = currentVisualFlipX;
                frameVisualFlipY[frameIndex] = currentVisualFlipY;
            }

            return true;
        }

        public void Play(float animationDuration)
        {
            if (animationDuration <= 0f || frames == null || frames.Length == 0)
            {
                return;
            }

            RestorePose();
            duration = animationDuration;
            elapsed = 0f;
            isPlaying = true;
        }

        public void Cancel()
        {
            RestorePose();
            isPlaying = false;
        }

        public void Update(float deltaTime)
        {
            if (!isPlaying)
            {
                return;
            }

            elapsed = Mathf.Min(duration, elapsed + deltaTime);
            float progress = elapsed / duration;
            if (progress >= 1f)
            {
                RestorePose();
                isPlaying = false;
                return;
            }

            float segmentCount = frames.Length + 1f;
            float segmentProgress = progress * segmentCount;
            int segmentIndex = Mathf.FloorToInt(segmentProgress);
            float interpolation = segmentProgress - segmentIndex;
            for (int index = 0; index < bodyParts.Count; index++)
            {
                BodyPartState part = bodyParts[index];
                Vector3 startPosition = segmentIndex == 0
                    ? Vector3.zero
                    : framePositions[segmentIndex - 1][index];
                Vector3 endPosition = segmentIndex < frames.Length
                    ? framePositions[segmentIndex][index]
                    : Vector3.zero;
                Vector2 positionOffset = Vector2.Lerp(
                    new Vector2(startPosition.x, startPosition.y),
                    new Vector2(endPosition.x, endPosition.y),
                    interpolation);
                part.transform.localPosition = part.originalLocalPosition
                    + new Vector3(positionOffset.x, positionOffset.y, 0f);
                float rotationOffset = Mathf.LerpAngle(startPosition.z, endPosition.z, interpolation);
                part.transform.localRotation = part.originalLocalRotation
                    * Quaternion.Euler(0f, 0f, rotationOffset);
            }

            int activeFlipFrame = Mathf.Min(segmentIndex, frames.Length - 1);
            for (int index = 0; index < bodyParts.Count; index++)
            {
                BodyPartState part = bodyParts[index];
                part.spriteRenderer.flipX = frameFlipX[activeFlipFrame][index];
                part.spriteRenderer.flipY = frameFlipY[activeFlipFrame][index];
            }

            visualRoot.localScale = new Vector3(
                originalVisualLocalScale.x * (frameVisualFlipX[activeFlipFrame] ? -1f : 1f),
                originalVisualLocalScale.y * (frameVisualFlipY[activeFlipFrame] ? -1f : 1f),
                originalVisualLocalScale.z);
        }

        private bool IsValidPartName(string partName)
        {
            return partName == "all"
                || (!string.IsNullOrWhiteSpace(partName) && bodyPartIndices.ContainsKey(partName));
        }

        private void ApplyPart(
            MonsterAttackPart part,
            Vector3[] positions,
            bool[] flipX,
            bool[] flipY,
            ref bool visualFlipX,
            ref bool visualFlipY)
        {
            if (part.name == "all")
            {
                for (int index = 0; index < bodyParts.Count; index++)
                {
                    if (part.hasPosition)
                    {
                        positions[index] = part.position;
                    }
                }

                SetVisualFlipValue(part.flipOnAxis, ref visualFlipX, ref visualFlipY);
                return;
            }

            int partIndex = bodyPartIndices[part.name];
            if (part.hasPosition)
            {
                positions[partIndex] = part.position;
            }

            SetFlipValue(part.flipOnAxis, partIndex, flipX, flipY);
        }

        private static void SetVisualFlipValue(
            MonsterAttackFlipAxis flipOnAxis,
            ref bool flipX,
            ref bool flipY)
        {
            if (flipOnAxis == MonsterAttackFlipAxis.X)
            {
                flipX = !flipX;
            }
            else if (flipOnAxis == MonsterAttackFlipAxis.Y)
            {
                flipY = !flipY;
            }
        }

        private static void SetFlipValue(
            MonsterAttackFlipAxis flipOnAxis,
            int index,
            bool[] flipX,
            bool[] flipY)
        {
            // Repeating the same axis on a later frame flips the sprite back.
            if (flipOnAxis == MonsterAttackFlipAxis.X)
            {
                flipX[index] = !flipX[index];
            }
            else if (flipOnAxis == MonsterAttackFlipAxis.Y)
            {
                flipY[index] = !flipY[index];
            }
        }

        private void RestorePose()
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = originalVisualLocalScale;
            }

            foreach (BodyPartState part in bodyParts)
            {
                part.transform.localPosition = part.originalLocalPosition;
                part.transform.localRotation = part.originalLocalRotation;
                part.spriteRenderer.flipX = part.originalFlipX;
                part.spriteRenderer.flipY = part.originalFlipY;
            }
        }
    }
}
