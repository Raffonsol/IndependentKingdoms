using System;
using UnityEngine;

public class Monster : Combatant
{
    private float wanderTimer;
    private bool isWandering;

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

        return SetPartSprite("face", definition.faceSprites)
            && SetPartSprite("torso", definition.torsoSprites)
            && SetPartSprite("frontLeg", definition.frontLegSprites)
            && SetPartSprite("backLeg", definition.backLegSprites)
            && SetPartSprite("frontLegRight", definition.frontLegSprites, definition.rightFrontLegOffset)
            && SetPartSprite("backLegRight", definition.backLegSprites, definition.rightBackLegOffset)
            && SetPartSprite("tail", definition.tailSprites);
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
                && child.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }
}
