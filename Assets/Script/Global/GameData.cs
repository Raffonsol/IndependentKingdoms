using System;
using UnityEngine;

[Serializable]
public class GameData
{
    private const string BodyDataResourcePath = "Data/Bodies";
    private const string CharacterSettingsDataResourcePath = "Data/CharacterSettings";
    private const string GameSettingsResourcePath = "Data/GameSettings";
    private const string MonsterDataResourcePath = "Data/Monsters";
    private const string ResourceDefinitionsResourcePath = "Data/ResourceDefinitions";
    private const string ItemDefinitionsResourcePath = "Data/ItemDefinitions";
    private const string ResourceObjectDataResourcePath = "Data/ResourceObjects";
    private const string NamingPartsResourcePath = "Data/NamingParts";
    private const string HouseDefinitionsResourcePath = "Data/HouseDefinitions";
    private const string JobDefinitionsResourcePath = "Data/JobDefinitions";
    private const string RenovationDefinitionsResourcePath = "Data/RenovationDefinitions";
    private const string VillagerPersonalityValuesResourcePath = "Data/VillagerPersonalityValues";

    private static readonly GameData instance = Load();

    public static GameData Instance
    {
        get { return instance; }
    }

    public Body[] bodies;
    public CharacterSettings characterSettings;
    public UISettings uiSettings;
    public MapSettings mapSettings;
    public PlayerSettings playerSettings;
    public HouseSettings houseSettings;
    public CommunityStorageSettings communityStorageSettings;
    public VillagerSettings villagerSettings;
    public ResourceSettings resourceSettings;
    public CombatSettings combatSettings;
    public MonsterSpawnSettings monsterSpawnSettings;
    public MonsterDefinition[] monsterDefinitions;
    public ResourceDefinition[] resourceDefinitions;
    public ItemDefinition[] itemDefinitions;
    public ResourceObjectDefinition[] resourceObjectDefinitions;
    public NamePart[] nameParts;
    public VillagerPersonalityValue[] villagerPersonalityValues;
    public JobDefinition[] jobDefinitions;
    public ResourceCost[] annexRenovationCosts;
    public HouseDefinition[] houseDefinitions;
    public Sprite[] houseBuildStageOneSprites;
    public Sprite[] houseBuildStageTwoSprites;
    public Color[] houseBuildStageOneColors;
    public Color[] houseBuildStageTwoColors;

    public bool TryGetResourceDefinition(ResourceType resourceType, out ResourceDefinition definition)
    {
        if (resourceDefinitions != null)
        {
            foreach (ResourceDefinition candidate in resourceDefinitions)
            {
                if (candidate != null && candidate.type == resourceType)
                {
                    definition = candidate;
                    return true;
                }
            }
        }

        definition = null;
        return false;
    }

    public bool IsCommunityStorageResource(ResourceType resourceType)
    {
        return TryGetResourceDefinition(resourceType, out ResourceDefinition definition)
            && definition.isCommunityStorageResource;
    }

    public bool TryGetItemDefinition(VillagerItemType itemType, out ItemDefinition definition)
    {
        if (itemDefinitions != null)
        {
            foreach (ItemDefinition candidate in itemDefinitions)
            {
                if (candidate != null && candidate.type == itemType)
                {
                    definition = candidate;
                    return true;
                }
            }
        }

        definition = null;
        return false;
    }

    public bool TryGetItemHarvestResourceType(VillagerItemType itemType, out ResourceType resourceType)
    {
        resourceType = default(ResourceType);
        if (!TryGetItemDefinition(itemType, out ItemDefinition definition))
        {
            return false;
        }

        if (!definition.hasHarvestResourceType)
        {
            return false;
        }

        resourceType = definition.harvestResourceType;
        return true;
    }

    public bool TryGetHeldItemSprite(VillagerItemType itemType, bool isMale, out Sprite sprite)
    {
        sprite = null;
        if (!TryGetItemDefinition(itemType, out ItemDefinition definition)
            || string.IsNullOrWhiteSpace(definition.heldSpriteKey))
        {
            return false;
        }

        string resourcePath = "BodyParts/handHelds/"
            + (isMale ? "m_" : "f_")
            + definition.heldSpriteKey.Trim();
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites == null || sprites.Length != 1 || sprites[0] == null)
        {
            Debug.LogError(
                "Expected exactly one held-item sprite for " + itemType + " at Resources/"
                + resourcePath + ".png, but found "
                + (sprites != null ? sprites.Length : 0) + ".");
            return false;
        }

        sprite = sprites[0];
        return true;
    }

    public bool TryGetJobDefinition(JobType jobType, out JobDefinition definition)
    {
        if (jobDefinitions != null)
        {
            foreach (JobDefinition candidate in jobDefinitions)
            {
                if (candidate != null && candidate.jobType == jobType)
                {
                    definition = candidate;
                    return true;
                }
            }
        }

        definition = null;
        return false;
    }

    public bool CanAssignJob(JobType jobType, out JobDefinition definition)
    {
        if (!TryGetJobDefinition(jobType, out definition))
        {
            return false;
        }

        return !definition.needsAnnex
            || (annexRenovationCosts != null
                && annexRenovationCosts.Length > 0
                && houseDefinitions != null
                && houseDefinitions.Length > 0);
    }

    private static GameData Load()
    {
        GameSettingsCollection gameSettings = LoadGameSettings();
        MonsterDataCollection monsterData = LoadMonsterData();
        ResourceObjectData resourceObjectData = LoadResourceObjectData();
        HouseDefinitionCollection houseDefinitionCollection = LoadHouseDefinitionCollection();
        return new GameData
        {
            bodies = LoadBodies(),
            characterSettings = LoadCharacterSettings(),
            uiSettings = gameSettings.uiSettings,
            mapSettings = gameSettings.mapSettings,
            playerSettings = gameSettings.playerSettings,
            houseSettings = gameSettings.houseSettings,
            communityStorageSettings = gameSettings.communityStorageSettings,
            villagerSettings = gameSettings.villagerSettings,
            resourceSettings = gameSettings.resourceSettings,
            combatSettings = gameSettings.combatSettings,
            monsterSpawnSettings = monsterData.spawnSettings,
            monsterDefinitions = monsterData.monsters,
            resourceDefinitions = LoadResourceDefinitions(),
            itemDefinitions = LoadItemDefinitions(),
            resourceObjectDefinitions = resourceObjectData.resourceObjects,
            nameParts = LoadNameParts(),
            villagerPersonalityValues = LoadVillagerPersonalityValues(),
            jobDefinitions = LoadJobDefinitions(),
            annexRenovationCosts = LoadAnnexRenovationCosts(),
            houseDefinitions = LoadHouseDefinitions(houseDefinitionCollection),
            houseBuildStageOneSprites = LoadHouseBuildStageSprites(
                houseDefinitionCollection.buildStage1SpriteSheet),
            houseBuildStageTwoSprites = LoadHouseBuildStageSprites(
                houseDefinitionCollection.buildStage2SpriteSheet),
            houseBuildStageOneColors = LoadHouseColors(
                houseDefinitionCollection.buildStage1ColorCodes,
                "construction",
                "buildStage1ColorCodes"),
            houseBuildStageTwoColors = LoadHouseColors(
                houseDefinitionCollection.buildStage2ColorCodes,
                "construction",
                "buildStage2ColorCodes")
        };
    }

    private static JobDefinition[] LoadJobDefinitions()
    {
        TextAsset definitionsJson = Resources.Load<TextAsset>(JobDefinitionsResourcePath);
        if (definitionsJson == null)
        {
            Debug.LogError(
                "Could not load job definitions at Resources/" + JobDefinitionsResourcePath + ".json.");
            return new JobDefinition[0];
        }

        try
        {
            JobDefinitionJsonCollection collection =
                JsonUtility.FromJson<JobDefinitionJsonCollection>(definitionsJson.text);
            if (collection == null || collection.jobs == null || collection.jobs.Length == 0)
            {
                Debug.LogError(
                    "Job definitions at Resources/" + JobDefinitionsResourcePath
                    + ".json must contain at least one job.");
                return new JobDefinition[0];
            }

            JobDefinition[] definitions = new JobDefinition[collection.jobs.Length];
            bool[] foundJobTypes = new bool[Enum.GetValues(typeof(JobType)).Length];
            for (int index = 0; index < collection.jobs.Length; index++)
            {
                JobDefinitionJson record = collection.jobs[index];
                if (record == null
                    || !TryParseEnumName(record.jobType, out JobType jobType)
                    || jobType == JobType.None
                    || !TryParseEnumName(record.workType, out JobWorkType workType))
                {
                    Debug.LogError("Job definitions contain an invalid job or work type at index " + index + ".");
                    return new JobDefinition[0];
                }

                int jobIndex = Convert.ToInt32(jobType);
                if (jobIndex < 0 || jobIndex >= foundJobTypes.Length || foundJobTypes[jobIndex])
                {
                    Debug.LogError("Job definitions must define each JobType at most once.");
                    return new JobDefinition[0];
                }

                foundJobTypes[jobIndex] = true;
                JobDefinition definition = new JobDefinition
                {
                    jobType = jobType,
                    workType = workType,
                    needsAnnex = record.needsAnnex
                };
                if (workType == JobWorkType.FieldGathering)
                {
                    if (record.harvestAmount <= 0
                        || !IsFinitePositive(record.gatheringDistance)
                        || !IsFiniteNonNegative(record.gatheringPauseDurationSeconds)
                        || !IsFinitePositive(record.resourceSearchRetrySeconds)
                        || !IsFinitePositive(record.handMotionDurationSeconds)
                        || !AreValidArmMotionPoints(record.handHitPositions)
                        || record.resourceTypes == null)
                    {
                        Debug.LogError("Field-gathering job '" + jobType + "' has invalid settings.");
                        return new JobDefinition[0];
                    }

                    bool hasRequiredEquippedItem = !string.IsNullOrWhiteSpace(record.requiredEquippedItemType);
                    VillagerItemType? requiredEquippedItemType = null;
                    if (hasRequiredEquippedItem)
                    {
                        if (!TryParseEnumName(
                                record.requiredEquippedItemType,
                                out VillagerItemType parsedRequiredEquippedItemType))
                        {
                            Debug.LogError(
                                "Field-gathering job '" + jobType
                                + "' contains an invalid requiredEquippedItemType.");
                            return new JobDefinition[0];
                        }

                        requiredEquippedItemType = parsedRequiredEquippedItemType;
                    }

                    if (hasRequiredEquippedItem && record.resourceTypes.Length != 1)
                    {
                        Debug.LogError(
                            "Field-gathering job '" + jobType
                            + "' currently requires exactly one target resource when a requiredEquippedItemType is defined.");
                        return new JobDefinition[0];
                    }

                    ResourceType[] resourceTypes = new ResourceType[record.resourceTypes.Length];
                    bool[] foundResourceTypes = new bool[Enum.GetValues(typeof(ResourceType)).Length];
                    for (int resourceIndex = 0; resourceIndex < record.resourceTypes.Length; resourceIndex++)
                    {
                        if (!TryParseEnumName(record.resourceTypes[resourceIndex], out ResourceType resourceType))
                        {
                            Debug.LogError(
                                "Field-gathering job '" + jobType
                                + "' contains an invalid ResourceType at index " + resourceIndex + ".");
                            return new JobDefinition[0];
                        }

                        int resourceTypeIndex = Convert.ToInt32(resourceType);
                        if (resourceTypeIndex < 0
                            || resourceTypeIndex >= foundResourceTypes.Length
                            || foundResourceTypes[resourceTypeIndex])
                        {
                            Debug.LogError(
                                "Field-gathering job '" + jobType + "' contains duplicate resource types.");
                            return new JobDefinition[0];
                        }

                        foundResourceTypes[resourceTypeIndex] = true;
                        resourceTypes[resourceIndex] = resourceType;
                    }

                    if (!record.gatherAnyAvailableResource && resourceTypes.Length == 0)
                    {
                        Debug.LogError(
                            "Field-gathering job '" + jobType
                            + "' must define target resource types or enable gatherAnyAvailableResource.");
                        return new JobDefinition[0];
                    }

                    definition.gatherAnyAvailableResource = record.gatherAnyAvailableResource;
                    definition.resourceTypes = resourceTypes;
                    definition.requiresEquippedItem = hasRequiredEquippedItem;
                    definition.requiredEquippedItemType = requiredEquippedItemType;
                    definition.harvestAmount = record.harvestAmount;
                    definition.gatheringDistance = record.gatheringDistance;
                    definition.gatheringPauseDurationSeconds = record.gatheringPauseDurationSeconds;
                    definition.resourceSearchRetrySeconds = record.resourceSearchRetrySeconds;
                    definition.handMotionDurationSeconds = record.handMotionDurationSeconds;
                    definition.handHitPositions = record.handHitPositions;
                }
                else if (workType == JobWorkType.Production)
                {
                    if (record.inputCosts == null
                        || record.inputCosts.Length == 0
                        || !TryParseEnumName(record.outputItemType, out VillagerItemType outputItemType)
                        || record.outputAmount <= 0
                        || !IsFinitePositive(record.productionDurationSeconds)
                        || !IsFinitePositive(record.handMotionDurationSeconds)
                        || !IsFinitePositive(record.workMotionIntervalSeconds)
                        || record.workMotionIntervalSeconds < record.handMotionDurationSeconds
                        || !AreValidArmMotionPoints(record.handHitPositions))
                    {
                        Debug.LogError(
                            "Production job '" + jobType
                            + "' has invalid input/output or hand-motion settings.");
                        return new JobDefinition[0];
                    }

                    ResourceCost[] inputCosts = new ResourceCost[record.inputCosts.Length];
                    bool[] foundResourceTypes = new bool[Enum.GetValues(typeof(ResourceType)).Length];
                    for (int costIndex = 0; costIndex < record.inputCosts.Length; costIndex++)
                    {
                        ResourceCostJson cost = record.inputCosts[costIndex];
                        if (cost == null
                            || !TryParseEnumName(cost.resourceType, out ResourceType resourceType)
                            || cost.amount <= 0)
                        {
                            Debug.LogError(
                                "Production job '" + jobType + "' contains an invalid input cost.");
                            return new JobDefinition[0];
                        }

                        int resourceTypeIndex = Convert.ToInt32(resourceType);
                        if (foundResourceTypes[resourceTypeIndex])
                        {
                            Debug.LogError(
                                "Production job '" + jobType + "' contains duplicate input resource types.");
                            return new JobDefinition[0];
                        }

                        foundResourceTypes[resourceTypeIndex] = true;
                        inputCosts[costIndex] = new ResourceCost
                        {
                            resourceType = resourceType,
                            amount = cost.amount
                        };
                    }

                    definition.inputCosts = inputCosts;
                    definition.outputItemType = outputItemType;
                    definition.outputAmount = record.outputAmount;
                    definition.productionDurationSeconds = record.productionDurationSeconds;
                    definition.handMotionDurationSeconds = record.handMotionDurationSeconds;
                    definition.handHitPositions = record.handHitPositions;
                    definition.workMotionIntervalSeconds = record.workMotionIntervalSeconds;
                }
                else if (workType == JobWorkType.Combat)
                {
                    definition.inputCosts = new ResourceCost[0];
                }

                definitions[index] = definition;
            }

            return definitions;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(
                "Could not parse job definitions at Resources/" + JobDefinitionsResourcePath
                + ".json: " + exception.Message);
            return new JobDefinition[0];
        }
    }

    private static ResourceCost[] LoadAnnexRenovationCosts()
    {
        TextAsset definitionsJson = Resources.Load<TextAsset>(RenovationDefinitionsResourcePath);
        if (definitionsJson == null)
        {
            Debug.LogError(
                "Could not load renovation definitions at Resources/"
                + RenovationDefinitionsResourcePath + ".json.");
            return new ResourceCost[0];
        }

        try
        {
            RenovationDefinitionJsonCollection collection =
                JsonUtility.FromJson<RenovationDefinitionJsonCollection>(definitionsJson.text);
            if (collection == null
                || collection.annexRenovationCosts == null
                || collection.annexRenovationCosts.Length == 0)
            {
                Debug.LogError(
                    "Renovation definitions must define at least one annexRenovationCost.");
                return new ResourceCost[0];
            }

            ResourceCost[] costs = new ResourceCost[collection.annexRenovationCosts.Length];
            bool[] foundResourceTypes = new bool[Enum.GetValues(typeof(ResourceType)).Length];
            for (int index = 0; index < collection.annexRenovationCosts.Length; index++)
            {
                ResourceCostJson record = collection.annexRenovationCosts[index];
                if (record == null
                    || !TryParseEnumName(record.resourceType, out ResourceType resourceType)
                    || record.amount <= 0)
                {
                    Debug.LogError("Annex renovation costs contain an invalid resource entry.");
                    return new ResourceCost[0];
                }

                int resourceIndex = Convert.ToInt32(resourceType);
                if (foundResourceTypes[resourceIndex])
                {
                    Debug.LogError("Annex renovation costs cannot repeat a resource type.");
                    return new ResourceCost[0];
                }

                foundResourceTypes[resourceIndex] = true;
                costs[index] = new ResourceCost
                {
                    resourceType = resourceType,
                    amount = record.amount
                };
            }

            return costs;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(
                "Could not parse renovation definitions at Resources/"
                + RenovationDefinitionsResourcePath + ".json: " + exception.Message);
            return new ResourceCost[0];
        }
    }

    private static VillagerPersonalityValue[] LoadVillagerPersonalityValues()
    {
        TextAsset valuesJson = Resources.Load<TextAsset>(VillagerPersonalityValuesResourcePath);
        if (valuesJson == null)
        {
            Debug.LogError(
                "Could not load villager personality values at Resources/"
                + VillagerPersonalityValuesResourcePath + ".json.");
            return new VillagerPersonalityValue[0];
        }

        try
        {
            VillagerPersonalityValueJsonCollection jsonCollection =
                JsonUtility.FromJson<VillagerPersonalityValueJsonCollection>(valuesJson.text);
            Array personalityTypes = Enum.GetValues(typeof(VillagerPersonalityType));
            if (jsonCollection == null
                || jsonCollection.villagerPersonalityValues == null
                || jsonCollection.villagerPersonalityValues.Length != personalityTypes.Length)
            {
                Debug.LogError(
                    "Villager personality values at Resources/" + VillagerPersonalityValuesResourcePath
                    + ".json must contain exactly one entry per VillagerPersonalityType.");
                return new VillagerPersonalityValue[0];
            }

            bool[] foundTypes = new bool[personalityTypes.Length];
            VillagerPersonalityValue[] values =
                new VillagerPersonalityValue[jsonCollection.villagerPersonalityValues.Length];
            for (int index = 0; index < jsonCollection.villagerPersonalityValues.Length; index++)
            {
                VillagerPersonalityValueJson jsonValue =
                    jsonCollection.villagerPersonalityValues[index];
                if (jsonValue == null
                    || !TryParseEnumName(jsonValue.type, out VillagerPersonalityType type)
                    || jsonValue.incompatibleWith == null)
                {
                    Debug.LogError("Villager personality values contain an invalid or null type.");
                    return new VillagerPersonalityValue[0];
                }

                VillagerPersonalityType[] incompatibleTypes =
                    new VillagerPersonalityType[jsonValue.incompatibleWith.Length];
                for (int incompatibleIndex = 0;
                    incompatibleIndex < jsonValue.incompatibleWith.Length;
                    incompatibleIndex++)
                {
                    if (!TryParseEnumName(
                            jsonValue.incompatibleWith[incompatibleIndex],
                            out incompatibleTypes[incompatibleIndex]))
                    {
                        Debug.LogError(
                            "Villager personality values contain an invalid incompatible personality name.");
                        return new VillagerPersonalityValue[0];
                    }
                }

                VillagerPersonalityValue value = new VillagerPersonalityValue
                {
                    type = type,
                    chance = jsonValue.chance,
                    incompatibleWith = incompatibleTypes,
                    description = jsonValue.description
                };
                values[index] = value;
                int typeIndex = Convert.ToInt32(value.type);
                if (typeIndex < 0 || typeIndex >= foundTypes.Length || foundTypes[typeIndex])
                {
                    Debug.LogError(
                        "Villager personality values must contain exactly one entry per personality type.");
                    return new VillagerPersonalityValue[0];
                }

                if (value.chance < 0 || value.chance > 100
                    || string.IsNullOrEmpty(value.description)
                    || value.incompatibleWith == null)
                {
                    Debug.LogError(
                        "Each villager personality value must define a description, a chance from 0 to 100, "
                        + "and an incompatibility list.");
                    return new VillagerPersonalityValue[0];
                }

                bool[] foundIncompatibleTypes = new bool[personalityTypes.Length];
                foreach (VillagerPersonalityType incompatibleType in value.incompatibleWith)
                {
                    if (!Enum.IsDefined(typeof(VillagerPersonalityType), incompatibleType))
                    {
                        Debug.LogError(
                            "Villager personality values contain an invalid incompatible personality type.");
                        return new VillagerPersonalityValue[0];
                    }

                    int incompatibleIndex = Convert.ToInt32(incompatibleType);
                    if (incompatibleIndex == typeIndex || foundIncompatibleTypes[incompatibleIndex])
                    {
                        Debug.LogError(
                            "Villager personality incompatibilities cannot contain self or duplicate types.");
                        return new VillagerPersonalityValue[0];
                    }

                    foundIncompatibleTypes[incompatibleIndex] = true;
                }

                foundTypes[typeIndex] = true;
            }

            return values;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(
                "Could not parse villager personality values at Resources/"
                + VillagerPersonalityValuesResourcePath + ".json: " + exception.Message);
            return new VillagerPersonalityValue[0];
        }
    }

    private static HouseDefinitionCollection LoadHouseDefinitionCollection()
    {
        TextAsset definitionsJson = Resources.Load<TextAsset>(HouseDefinitionsResourcePath);
        if (definitionsJson == null)
        {
            Debug.LogError(
                "Could not load house definitions at Resources/" + HouseDefinitionsResourcePath + ".json.");
            return new HouseDefinitionCollection();
        }

        try
        {
            HouseDefinitionCollection collection =
                JsonUtility.FromJson<HouseDefinitionCollection>(definitionsJson.text);
            if (collection == null
                || collection.houseDefinitions == null
                || collection.houseDefinitions.Length == 0
                || string.IsNullOrEmpty(collection.buildStage1SpriteSheet)
                || string.IsNullOrEmpty(collection.buildStage2SpriteSheet)
                || collection.buildStage1ColorCodes == null
                || collection.buildStage1ColorCodes.Length == 0
                || collection.buildStage2ColorCodes == null
                || collection.buildStage2ColorCodes.Length == 0)
            {
                Debug.LogError(
                    "House definitions at Resources/" + HouseDefinitionsResourcePath
                    + ".json must define construction sprite sheets and at least one house.");
                return new HouseDefinitionCollection();
            }

            return collection;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(
                "Could not parse house definitions at Resources/" + HouseDefinitionsResourcePath
                + ".json: " + exception.Message);
            return new HouseDefinitionCollection();
        }
    }

    private static HouseDefinition[] LoadHouseDefinitions(HouseDefinitionCollection collection)
    {
        if (collection == null || collection.houseDefinitions == null)
        {
            return new HouseDefinition[0];
        }

        HouseDefinition[] definitions = new HouseDefinition[collection.houseDefinitions.Length];
        for (int index = 0; index < collection.houseDefinitions.Length; index++)
        {
            HouseDefinitionRecord record = collection.houseDefinitions[index];
            if (record == null || string.IsNullOrEmpty(record.id))
            {
                Debug.LogError("House definitions must have a non-empty id at index " + index + ".");
                return new HouseDefinition[0];
            }

            for (int earlierIndex = 0; earlierIndex < index; earlierIndex++)
            {
                if (collection.houseDefinitions[earlierIndex] != null
                    && collection.houseDefinitions[earlierIndex].id == record.id)
                {
                    Debug.LogError("House definitions contain a duplicate id '" + record.id + "'.");
                    return new HouseDefinition[0];
                }
            }

            HouseDefinition definition = new HouseDefinition
            {
                id = record.id,
                visualScale = record.visualScale,
                visualOffset = record.visualOffset,
                sortingAnchorOffset = record.sortingAnchorOffset,
                annexWorkPositionOffset = record.annexWorkPositionOffset,
                yardSprites = LoadHouseSprites(record.yardSprites, record.id, "yardSprites", true),
                yardColors = LoadHouseColors(record.yardColorCodes, record.id, "yardColorCodes"),
                baseSprites = LoadHouseSprites(record.baseSprites, record.id, "baseSprites", true),
                lowRoofSprites = LoadHouseSprites(record.lowRoofSprites, record.id, "lowRoofSprites", true),
                highRoofSprites = LoadHouseSprites(record.highRoofSprites, record.id, "highRoofSprites", false),
                secondLevelSprites = LoadHouseSprites(record.secondLevelSprites, record.id, "secondLevelSprites", false),
                secondLevelExtensionSprites = LoadHouseSprites(
                    record.secondLevelExtensionSprites, record.id, "secondLevelExtensionSprites", false),
                extensionRoofSprites = LoadHouseSprites(
                    record.extensionRoofSprites, record.id, "extensionRoofSprites", false),
                annexSprites = LoadHouseSprites(record.annexSprites, record.id, "annexSprites", true),
                annexRoofSprites = LoadHouseSprites(record.annexRoofSprites, record.id, "annexRoofSprites", true),
                wallColors = LoadHouseColors(record.wallColorCodes, record.id, "wallColorCodes"),
                roofColors = LoadHouseColors(record.roofColorCodes, record.id, "roofColorCodes")
            };
            if (definition.visualScale <= 0f
                || !IsFinite(definition.sortingAnchorOffset.x)
                || !IsFinite(definition.sortingAnchorOffset.y)
                || !IsFinite(definition.annexWorkPositionOffset.x)
                || !IsFinite(definition.annexWorkPositionOffset.y))
            {
                Debug.LogError(
                    "House '" + record.id
                    + "' must define a positive visualScale and finite sortingAnchorOffset "
                    + "and annexWorkPositionOffset.");
                return new HouseDefinition[0];
            }

            if (definition.baseSprites == null
                || definition.yardSprites == null
                || definition.yardColors == null
                || definition.lowRoofSprites == null
                || definition.highRoofSprites == null
                || definition.secondLevelSprites == null
                || definition.secondLevelExtensionSprites == null
                || definition.extensionRoofSprites == null
                || definition.annexSprites == null
                || definition.annexRoofSprites == null
                || definition.wallColors == null
                || definition.roofColors == null)
            {
                return new HouseDefinition[0];
            }

            definitions[index] = definition;
        }

        return definitions;
    }

    private static Sprite[] LoadHouseSprites(
        string[] spriteNames,
        string houseId,
        string fieldName,
        bool required)
    {
        if (spriteNames == null || spriteNames.Length == 0)
        {
            if (required)
            {
                Debug.LogError(
                    "House '" + houseId + "' must define at least one sprite in '" + fieldName + "'.");
                return null;
            }

            return new Sprite[0];
        }

        Sprite[] sprites = new Sprite[spriteNames.Length];
        for (int index = 0; index < spriteNames.Length; index++)
        {
            if (string.IsNullOrEmpty(spriteNames[index]))
            {
                Debug.LogError(
                    "House '" + houseId + "' contains an empty sprite name in '" + fieldName + "'.");
                return null;
            }

            sprites[index] = LoadSprite(
                "BuildingParts/" + spriteNames[index],
                houseId,
                fieldName);
            if (sprites[index] == null)
            {
                return null;
            }
        }

        return sprites;
    }

    private static Color[] LoadHouseColors(string[] colorCodes, string houseId, string fieldName)
    {
        if (colorCodes == null || colorCodes.Length == 0)
        {
            Debug.LogError(
                "House configuration '" + houseId + "' must define at least one color in '" + fieldName + "'.");
            return null;
        }

        Color[] colors = new Color[colorCodes.Length];
        for (int index = 0; index < colorCodes.Length; index++)
        {
            if (string.IsNullOrEmpty(colorCodes[index])
                || !ColorUtility.TryParseHtmlString(colorCodes[index], out colors[index]))
            {
                Debug.LogError(
                    "House configuration '" + houseId + "' has an invalid color code in '" + fieldName
                    + "' at index " + index + ".");
                return null;
            }
        }

        return colors;
    }

    private static Sprite[] LoadHouseBuildStageSprites(string spriteSheetName)
    {
        if (string.IsNullOrEmpty(spriteSheetName))
        {
            Debug.LogError("House construction sprite-sheet names must not be empty.");
            return new Sprite[0];
        }

        string resourcePath = "BuildingParts/" + spriteSheetName;
        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogError(
                "Could not load any house construction sprites at Resources/" + resourcePath + ".png.");
            return new Sprite[0];
        }

        return sprites;
    }

    private static Body[] LoadBodies()
    {
        TextAsset bodyJson = Resources.Load<TextAsset>(BodyDataResourcePath);
        if (bodyJson == null)
        {
            Debug.LogError("Could not load body data at Resources/" + BodyDataResourcePath + ".json.");
            return new Body[0];
        }

        BodyCollection bodyCollection;
        try
        {
            bodyCollection = JsonUtility.FromJson<BodyCollection>(
                "{\"bodies\":" + bodyJson.text + "}");
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("Could not parse body data at Resources/" + BodyDataResourcePath + ".json: " + exception.Message);
            return new Body[0];
        }

        if (bodyCollection == null || bodyCollection.bodies == null)
        {
            Debug.LogError("Body data at Resources/" + BodyDataResourcePath + ".json must contain a JSON array.");
            return new Body[0];
        }

        Body[] bodies = new Body[bodyCollection.bodies.Length];
        for (int index = 0; index < bodyCollection.bodies.Length; index++)
        {
            BodyRecord record = bodyCollection.bodies[index];
            if (record == null)
            {
                Debug.LogError("Body data contains a null entry at index " + index + ".");
                continue;
            }

            if (!IsFinite(record.sortingAnchorOffset.x)
                || !IsFinite(record.sortingAnchorOffset.y)
                || !IsFinitePositive(record.shadowSize.x)
                || !IsFinitePositive(record.shadowSize.y)
                || !IsFinite(record.shadowOffset.x)
                || !IsFinite(record.shadowOffset.y))
            {
                Debug.LogError(
                    "Body '" + record.name
                    + "' must define finite offsets and a positive shadowSize.");
                return new Body[0];
            }

            bodies[index] = new Body
            {
                name = record.name,
                isMale = record.isMale,
                torsoSprite = LoadSprite("BodyParts/" + record.torsoSprite, record.name, "torsoSprite"),
                leftArmSprite = LoadSprite("BodyParts/" + record.leftArmSprite, record.name, "leftArmSprite"),
                rightArmSprite = LoadSprite("BodyParts/" + record.rightArmSprite, record.name, "rightArmSprite"),
                legSprite = LoadSprite("BodyParts/" + record.legSprite, record.name, "legSprite"),
                faceSprite = LoadSprites(record.faceSprite, record.name, "faceSprite"),
                earSprite = LoadSprites(record.earSprite, record.name, "earSprite"),
                hairSprite = LoadSprites(record.hairSprite, record.name, "hairSprite"),
                clothingTopSprite = LoadSprites(record.clothingTopSprite, record.name, "clothingTopSprite"),
                clothingBottomSprite = LoadSprites(record.clothingBottomSprite, record.name, "clothingBottomSprite"),
                leggingSprite = LoadSprites(record.leggingSprite, record.name, "leggingSprite"),
                bootSprite = LoadSprites(record.bootSprite, record.name, "bootSprite"),
                leftSleeveSprite = LoadSprites(record.leftSleeveSprite, record.name, "leftSleeveSprite"),
                rightSleeveSprite = LoadSprites(record.rightSleeveSprite, record.name, "rightSleeveSprite"),
                rightLegXOffset = record.rightLegXOffset,
                rightLegYOffset = record.rightLegYOffset,
                sortingAnchorOffset = record.sortingAnchorOffset,
                shadowSize = record.shadowSize,
                shadowOffset = record.shadowOffset,
                movementSpeed = record.movementSpeed,
                movementDurationRangeX = record.movementDurationRangeX,
                movementDurationRangeY = record.movementDurationRangeY,
                idleDurationRangeX = record.idleDurationRangeX,
                idleDurationRangeY = record.idleDurationRangeY,
                legSwingAngle = record.legSwingAngle,
                legSwingSpeed = record.legSwingSpeed,
                legPivotOffsetX = record.legPivotOffsetX,
                legPivotOffsetY = record.legPivotOffsetY,
                legSwingRunningAngle = record.legSwingRunningAngle,
                legSwingRunningSpeed = record.legSwingRunningSpeed,
                idleHeaveHeight = record.idleHeaveHeight,
                idleHeaveSpeed = record.idleHeaveSpeed
            };
        }

        return bodies;
    }

    private static MonsterDataCollection LoadMonsterData()
    {
        TextAsset monsterDataJson = Resources.Load<TextAsset>(MonsterDataResourcePath);
        if (monsterDataJson == null)
        {
            Debug.LogError(
                "Could not load monster data at Resources/" + MonsterDataResourcePath + ".json.");
            return new MonsterDataCollection();
        }

        try
        {
            MonsterDataRecordCollection collection =
                JsonUtility.FromJson<MonsterDataRecordCollection>(monsterDataJson.text);
            if (collection == null
                || collection.spawnSettings == null
                || collection.monsters == null
                || collection.monsters.Length == 0)
            {
                Debug.LogError(
                    "Monster data at Resources/" + MonsterDataResourcePath
                    + ".json must define spawnSettings and at least one monster.");
                return new MonsterDataCollection();
            }

            MonsterSpawnSettings spawnSettings = collection.spawnSettings;
            if (!IsFinitePositive(spawnSettings.spawnIntervalSeconds)
                || spawnSettings.maximumTotalAmount < 0
                || !IsFiniteNonNegative(spawnSettings.minimumVillagerDistance)
                || spawnSettings.spawnPositionAttemptsPerInterval <= 0)
            {
                Debug.LogError(
                    "Monster spawn settings must define a positive interval and attempt count, "
                    + "a non-negative population cap, and a non-negative villager distance.");
                return new MonsterDataCollection();
            }

            MonsterDefinition[] definitions = new MonsterDefinition[collection.monsters.Length];
            for (int index = 0; index < collection.monsters.Length; index++)
            {
                MonsterDefinitionRecord record = collection.monsters[index];
                if (record == null
                    || string.IsNullOrWhiteSpace(record.id)
                    || record.id.IndexOfAny(new[] { '/', '\\' }) >= 0
                    || record.spawnWeight < 0f
                    || !IsFiniteNonNegative(record.spawnWeight)
                    || record.maximumAmount < 0
                    || !IsFinitePositive(record.movementSpeed)
                    || !IsFinitePositive(record.shadowSize.x)
                    || !IsFinitePositive(record.shadowSize.y)
                    || !IsFinite(record.shadowOffset.x)
                    || !IsFinite(record.shadowOffset.y)
                    || !IsValidDurationRange(record.movementDurationRange)
                    || !IsValidDurationRange(record.idleDurationRange)
                    || !IsValidMonsterLegDefinitions(record.legDefinitions)
                    || !IsValidMonsterCombatSettings(record.combatSettings)
                    || !IsFinite(record.rightFrontLegOffset.x)
                    || !IsFinite(record.rightFrontLegOffset.y)
                    || !IsFinite(record.rightBackLegOffset.x)
                    || !IsFinite(record.rightBackLegOffset.y))
                {
                    Debug.LogError("Monster data contains an invalid definition at index " + index + ".");
                    return new MonsterDataCollection();
                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    if (string.Equals(
                            collection.monsters[previousIndex].id,
                            record.id,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogError("Monster definition IDs must be unique.");
                        return new MonsterDataCollection();
                    }
                }

                MonsterDefinition definition = new MonsterDefinition
                {
                    id = record.id,
                    spawnWeight = record.spawnWeight,
                    maximumAmount = record.maximumAmount,
                    movementSpeed = record.movementSpeed,
                    movementDurationRange = record.movementDurationRange,
                    idleDurationRange = record.idleDurationRange,
                    faceSprites = LoadMonsterSprites(record.faceSprites, record.id, "faceSprites"),
                    torsoSprites = LoadMonsterSprites(record.torsoSprites, record.id, "torsoSprites"),
                    frontLegSprites = LoadMonsterSprites(
                        record.frontLegSprites,
                        record.id,
                        "frontLegSprites"),
                    backLegSprites = LoadMonsterSprites(
                        record.backLegSprites,
                        record.id,
                        "backLegSprites"),
                    tailSprites = LoadMonsterSprites(record.tailSprites, record.id, "tailSprites"),
                    legDefinitions = record.legDefinitions,
                    rightFrontLegOffset = record.rightFrontLegOffset,
                    rightBackLegOffset = record.rightBackLegOffset,
                    shadowSize = record.shadowSize,
                    shadowOffset = record.shadowOffset,
                    combatSettings = record.combatSettings
                };
                if (!AreLoadedSpritesValid(definition.faceSprites)
                    || !AreLoadedSpritesValid(definition.torsoSprites)
                    || !AreLoadedSpritesValid(definition.frontLegSprites)
                    || !AreLoadedSpritesValid(definition.backLegSprites)
                    || !AreLoadedSpritesValid(definition.tailSprites))
                {
                    Debug.LogError("Monster definition '" + record.id + "' has missing part sprites.");
                    return new MonsterDataCollection();
                }

                definitions[index] = definition;
            }

            bool hasSpawnableDefinition = false;
            foreach (MonsterDefinition definition in definitions)
            {
                if (definition.spawnWeight > 0f && definition.maximumAmount > 0)
                {
                    hasSpawnableDefinition = true;
                    break;
                }
            }

            if (!hasSpawnableDefinition)
            {
                Debug.LogError("Monster data must include at least one definition with a positive weight and cap.");
                return new MonsterDataCollection();
            }

            float totalSpawnWeight = 0f;
            foreach (MonsterDefinition definition in definitions)
            {
                totalSpawnWeight += definition.spawnWeight;
            }

            if (!IsFinitePositive(totalSpawnWeight))
            {
                Debug.LogError("Monster definition spawn weights must have a finite, positive total.");
                return new MonsterDataCollection();
            }

            return new MonsterDataCollection
            {
                spawnSettings = spawnSettings,
                monsters = definitions
            };
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(
                "Could not parse monster data at Resources/" + MonsterDataResourcePath
                + ".json: " + exception.Message);
            return new MonsterDataCollection();
        }
    }

    private static Sprite[] LoadMonsterSprites(string[] spriteNames, string monsterId, string fieldName)
    {
        if (spriteNames == null || spriteNames.Length == 0)
        {
            Debug.LogError(
                "Monster '" + monsterId + "' must define at least one sprite in '" + fieldName + "'.");
            return new Sprite[0];
        }

        Sprite[] sprites = new Sprite[spriteNames.Length];
        for (int index = 0; index < spriteNames.Length; index++)
        {
            string spriteName = spriteNames[index];
            sprites[index] = string.IsNullOrWhiteSpace(spriteName)
                ? null
                : LoadSprite(
                    "Monsters/" + monsterId + "/" + spriteName,
                    monsterId,
                    fieldName);
        }

        return sprites;
    }

    private static bool AreLoadedSpritesValid(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return false;
        }

        foreach (Sprite sprite in sprites)
        {
            if (sprite == null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidDurationRange(Vector2 range)
    {
        return IsFiniteNonNegative(range.x)
            && IsFiniteNonNegative(range.y)
            && range.y >= range.x;
    }

    private static bool IsValidMonsterLegDefinitions(MonsterLegDefinition[] definitions)
    {
        if (definitions == null || definitions.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < definitions.Length; index++)
        {
            MonsterLegDefinition definition = definitions[index];
            if (definition == null
                || string.IsNullOrWhiteSpace(definition.partName)
                || !IsFinite(definition.legSwingAngle)
                || !IsFiniteNonNegative(definition.legSwingSpeed)
                || !IsFinite(definition.legPivotOffset.x)
                || !IsFinite(definition.legPivotOffset.y)
                || !IsFinite(definition.legSwingRunningAngle)
                || !IsFiniteNonNegative(definition.legSwingRunningSpeed))
            {
                return false;
            }

            for (int previousIndex = 0; previousIndex < index; previousIndex++)
            {
                if (string.Equals(
                        definitions[previousIndex].partName,
                        definition.partName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsValidMonsterCombatSettings(CombatSettings settings)
    {
        return settings != null
            && IsFinitePositive(settings.maximumHealth)
            && IsFinitePositive(settings.attackDamage)
            && IsFinitePositive(settings.attackRange)
            && IsFinitePositive(settings.attackWindupSeconds)
            && IsFiniteNonNegative(settings.attackRecoverySeconds)
            && IsFinitePositive(settings.runningSpeed)
            && IsFiniteNonNegative(settings.monsterReactionTimeSeconds)
            && IsFinitePositive(settings.detectionIntervalReactionTimeMultiplier)
            && settings.detectionRangeCells > 0
            && AreValidArmMotionPoints(settings.attackHitPositions);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return value >= 0f && IsFinite(value);
    }

    private static CharacterSettings LoadCharacterSettings()
    {
        TextAsset characterSettingsJson = Resources.Load<TextAsset>(CharacterSettingsDataResourcePath);
        if (characterSettingsJson == null)
        {
            Debug.LogError("Could not load character settings data at Resources/" + CharacterSettingsDataResourcePath + ".json.");
            return null;
        }

        try
        {
            CharacterSettings characterSettings =
                JsonUtility.FromJson<CharacterSettings>(characterSettingsJson.text);
            if (characterSettings == null)
            {
                Debug.LogError("Character settings data at Resources/" + CharacterSettingsDataResourcePath + ".json is invalid.");
            }

            return characterSettings;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("Could not parse character settings data at Resources/" + CharacterSettingsDataResourcePath + ".json: " + exception.Message);
            return null;
        }
    }

    private static NamePart[] LoadNameParts()
    {
        TextAsset namingPartsJson = Resources.Load<TextAsset>(NamingPartsResourcePath);
        if (namingPartsJson == null)
        {
            Debug.LogError("Could not load naming parts at Resources/" + NamingPartsResourcePath + ".json.");
            return new NamePart[0];
        }

        try
        {
            NamePartJsonCollection jsonCollection =
                JsonUtility.FromJson<NamePartJsonCollection>(namingPartsJson.text);
            if (jsonCollection == null || jsonCollection.nameParts == null)
            {
                Debug.LogError("Naming parts at Resources/" + NamingPartsResourcePath + ".json must contain a JSON array.");
                return new NamePart[0];
            }

            NamePart[] nameParts = new NamePart[jsonCollection.nameParts.Length];
            for (int index = 0; index < jsonCollection.nameParts.Length; index++)
            {
                NamePartJson jsonNamePart = jsonCollection.nameParts[index];
                if (jsonNamePart == null
                    || !TryParseEnumName(jsonNamePart.type, out NamePartType type))
                {
                    Debug.LogError("Naming parts contain an invalid or null NamePartType name.");
                    return new NamePart[0];
                }

                nameParts[index] = new NamePart
                {
                    type = type,
                    value = jsonNamePart.value,
                    worksForMen = jsonNamePart.worksForMen,
                    worksForWomen = jsonNamePart.worksForWomen
                };
            }

            return nameParts;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(
                "Could not parse naming parts at Resources/" + NamingPartsResourcePath
                + ".json: " + exception.Message);
            return new NamePart[0];
        }
    }

    private static ResourceDefinition[] LoadResourceDefinitions()
    {
        TextAsset definitionsJson = Resources.Load<TextAsset>(ResourceDefinitionsResourcePath);
        if (definitionsJson == null)
        {
            Debug.LogError("Could not load resource definitions at Resources/" + ResourceDefinitionsResourcePath + ".json.");
            return new ResourceDefinition[0];
        }

        try
        {
            ResourceDefinitionJsonCollection jsonCollection =
                JsonUtility.FromJson<ResourceDefinitionJsonCollection>(definitionsJson.text);
            Array resourceTypes = Enum.GetValues(typeof(ResourceType));
            if (jsonCollection == null
                || jsonCollection.resourceDefinitions == null
                || jsonCollection.resourceDefinitions.Length != resourceTypes.Length)
            {
                Debug.LogError(
                    "Resource definitions at Resources/" + ResourceDefinitionsResourcePath
                    + ".json must contain exactly one entry per ResourceType.");
                return new ResourceDefinition[0];
            }

            bool[] foundTypes = new bool[resourceTypes.Length];
            ResourceDefinition[] definitions =
                new ResourceDefinition[jsonCollection.resourceDefinitions.Length];
            for (int index = 0; index < jsonCollection.resourceDefinitions.Length; index++)
            {
                ResourceDefinitionJson jsonDefinition = jsonCollection.resourceDefinitions[index];
                if (jsonDefinition == null
                    || !TryParseEnumName(jsonDefinition.type, out ResourceType type))
                {
                    Debug.LogError("Resource definitions contain an invalid or null ResourceType entry.");
                    return new ResourceDefinition[0];
                }

                ResourceDefinition definition = new ResourceDefinition
                {
                    type = type,
                    iconIndex = jsonDefinition.iconIndex,
                    isCommunityStorageResource = jsonDefinition.isCommunityStorageResource
                };
                definitions[index] = definition;
                int enumIndex = Convert.ToInt32(definition.type);
                if (enumIndex < 0 || enumIndex >= foundTypes.Length || foundTypes[enumIndex])
                {
                    Debug.LogError("Resource definitions must contain exactly one entry for each ResourceType.");
                    return new ResourceDefinition[0];
                }

                if (definition.iconIndex < 0)
                {
                    Debug.LogError("Resource icon indices cannot be negative.");
                    return new ResourceDefinition[0];
                }

                foundTypes[enumIndex] = true;
            }

            return definitions;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(
                "Could not parse resource definitions at Resources/" + ResourceDefinitionsResourcePath
                + ".json: " + exception.Message);
            return new ResourceDefinition[0];
        }
    }

    private static ItemDefinition[] LoadItemDefinitions()
    {
        TextAsset definitionsJson = Resources.Load<TextAsset>(ItemDefinitionsResourcePath);
        if (definitionsJson == null)
        {
            Debug.LogError(
                "Could not load item definitions at Resources/"
                + ItemDefinitionsResourcePath + ".json.");
            return new ItemDefinition[0];
        }

        try
        {
            ItemDefinitionJsonCollection jsonCollection =
                JsonUtility.FromJson<ItemDefinitionJsonCollection>(definitionsJson.text);
            Array itemTypes = Enum.GetValues(typeof(VillagerItemType));
            if (jsonCollection == null
                || jsonCollection.itemDefinitions == null
                || jsonCollection.itemDefinitions.Length != itemTypes.Length)
            {
                Debug.LogError(
                    "Item definitions at Resources/" + ItemDefinitionsResourcePath
                    + ".json must contain exactly one entry per VillagerItemType.");
                return new ItemDefinition[0];
            }

            bool[] foundTypes = new bool[itemTypes.Length];
            ItemDefinition[] definitions =
                new ItemDefinition[jsonCollection.itemDefinitions.Length];
            for (int index = 0; index < jsonCollection.itemDefinitions.Length; index++)
            {
                ItemDefinitionJson jsonDefinition = jsonCollection.itemDefinitions[index];
                if (jsonDefinition == null
                    || !TryParseEnumName(jsonDefinition.type, out VillagerItemType itemType))
                {
                    Debug.LogError("Item definitions contain an invalid or null VillagerItemType entry.");
                    return new ItemDefinition[0];
                }

                int enumIndex = Convert.ToInt32(itemType);
                if (enumIndex < 0 || enumIndex >= foundTypes.Length || foundTypes[enumIndex])
                {
                    Debug.LogError("Item definitions must contain exactly one entry for each VillagerItemType.");
                    return new ItemDefinition[0];
                }

                if (jsonDefinition.iconIndex < 0)
                {
                    Debug.LogError("Item icon indices cannot be negative.");
                    return new ItemDefinition[0];
                }

                string heldSpriteKey = jsonDefinition.heldSpriteKey == null
                    ? null
                    : jsonDefinition.heldSpriteKey.Trim();
                if (string.IsNullOrWhiteSpace(heldSpriteKey))
                {
                    Debug.LogError(
                        "Item definition for " + itemType
                        + " must include a non-empty heldSpriteKey for the gendered hand-held sprite.");
                    return new ItemDefinition[0];
                }

                ResourceType harvestResourceType = default(ResourceType);
                bool hasHarvestResourceType = !string.IsNullOrWhiteSpace(jsonDefinition.harvestResourceType);
                if (hasHarvestResourceType
                    && !TryParseEnumName(jsonDefinition.harvestResourceType, out harvestResourceType))
                {
                    Debug.LogError(
                        "Item definition for " + itemType
                        + " contains an invalid harvestResourceType value.");
                    return new ItemDefinition[0];
                }

                foundTypes[enumIndex] = true;
                definitions[index] = new ItemDefinition
                {
                    type = itemType,
                    iconIndex = jsonDefinition.iconIndex,
                    heldSpriteKey = heldSpriteKey,
                    hasHarvestResourceType = hasHarvestResourceType,
                    harvestResourceType = harvestResourceType
                };
            }

            return definitions;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(
                "Could not parse item definitions at Resources/" + ItemDefinitionsResourcePath
                + ".json: " + exception.Message);
            return new ItemDefinition[0];
        }
    }

    private static ResourceObjectData LoadResourceObjectData()
    {
        TextAsset dataJson = Resources.Load<TextAsset>(ResourceObjectDataResourcePath);
        if (dataJson == null)
        {
            Debug.LogError(
                "Could not load resource object data at Resources/"
                + ResourceObjectDataResourcePath + ".json.");
            return new ResourceObjectData();
        }

        try
        {
            ResourceObjectDataCollection collection =
                JsonUtility.FromJson<ResourceObjectDataCollection>(dataJson.text);
            Array resourceTypes = Enum.GetValues(typeof(ResourceType));
            if (collection == null
                || collection.resourceObjects == null
                || collection.resourceObjects.Length == 0)
            {
                Debug.LogError(
                    "Resource object data must define at least one resource object.");
                return new ResourceObjectData();
            }

            ResourceObjectDefinition[] definitions =
                new ResourceObjectDefinition[collection.resourceObjects.Length];
            for (int index = 0; index < collection.resourceObjects.Length; index++)
            {
                ResourceObjectDefinitionRecord record = collection.resourceObjects[index];
                if (record == null
                    || string.IsNullOrWhiteSpace(record.id)
                    || record.spawnSettings == null
                    || record.outputs == null
                    || record.outputs.Length == 0
                    || !IsFinite(record.sortingAnchorOffset.x)
                    || !IsFinite(record.sortingAnchorOffset.y)
                    || !IsFinitePositive(record.shadowSize.x)
                    || !IsFinitePositive(record.shadowSize.y)
                    || !IsFinite(record.shadowOffset.x)
                    || !IsFinite(record.shadowOffset.y)
                    || !IsValidResourceObjectSpawnSettings(record.spawnSettings))
                {
                    Debug.LogError("Resource object data contains an invalid definition at index " + index + ".");
                    return new ResourceObjectData();
                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    if (string.Equals(
                            collection.resourceObjects[previousIndex].id,
                            record.id,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogError("Resource object IDs must be unique.");
                        return new ResourceObjectData();
                    }
                }

                ResourceOutputDefinition[] outputs =
                    new ResourceOutputDefinition[record.outputs.Length];
                bool[] foundOutputTypes = new bool[resourceTypes.Length];
                bool hasPrimaryResourceType = false;
                ResourceType primaryResourceType = default(ResourceType);
                if (!string.IsNullOrWhiteSpace(record.primaryResourceType)
                    && !TryParseEnumName(record.primaryResourceType, out primaryResourceType))
                {
                    Debug.LogError(
                        "Resource object '" + record.id
                        + "' contains an invalid primaryResourceType.");
                    return new ResourceObjectData();
                }
                else if (!string.IsNullOrWhiteSpace(record.primaryResourceType))
                {
                    hasPrimaryResourceType = true;
                }

                for (int outputIndex = 0; outputIndex < record.outputs.Length; outputIndex++)
                {
                    ResourceOutputRecord outputRecord = record.outputs[outputIndex];
                    if (outputRecord == null
                        || !TryParseEnumName(outputRecord.type, out ResourceType type)
                        || outputRecord.fullCutAmount < 0
                        || outputRecord.bareHandAmount < 0
                        || outputRecord.bareHandAmount > outputRecord.fullCutAmount
                        || outputRecord.resourceRegenerationPerSecond < 0f
                        || !IsFinite(outputRecord.resourceRegenerationPerSecond)
                        || outputRecord.resourceRegenerationLimit < 0
                        || (outputRecord.resourceRegenerationPerSecond > 0f
                            && outputRecord.resourceRegenerationLimit <= 0))
                    {
                        Debug.LogError(
                            "Resource object '" + record.id + "' contains an invalid resource output.");
                        return new ResourceObjectData();
                    }

                    int typeIndex = Convert.ToInt32(type);
                    if (typeIndex < 0 || typeIndex >= foundOutputTypes.Length || foundOutputTypes[typeIndex])
                    {
                        Debug.LogError(
                            "Resource object '" + record.id
                            + "' cannot define the same ResourceType more than once.");
                        return new ResourceObjectData();
                    }

                    bool hasOutputPrimaryResourceType = false;
                    ResourceType outputPrimaryResourceType = default(ResourceType);
                    if (!string.IsNullOrWhiteSpace(outputRecord.primaryResourceType)
                        && !TryParseEnumName(outputRecord.primaryResourceType, out outputPrimaryResourceType))
                    {
                        Debug.LogError(
                            "Resource object '" + record.id
                            + "' contains an invalid output.primaryResourceType.");
                        return new ResourceObjectData();
                    }
                    else if (!string.IsNullOrWhiteSpace(outputRecord.primaryResourceType))
                    {
                        hasOutputPrimaryResourceType = true;
                    }

                    if (!hasPrimaryResourceType && hasOutputPrimaryResourceType)
                    {
                        primaryResourceType = outputPrimaryResourceType;
                        hasPrimaryResourceType = true;
                    }

                    foundOutputTypes[typeIndex] = true;
                    outputs[outputIndex] = new ResourceOutputDefinition
                    {
                        type = type,
                        primaryResourceType = outputPrimaryResourceType,
                        primaryResourceTypeWasExplicitlySet = hasOutputPrimaryResourceType,
                        fullCutAmount = outputRecord.fullCutAmount,
                        bareHandAmount = outputRecord.bareHandAmount,
                        interactionOffset = outputRecord.interactionOffset,
                        resourceRegenerationPerSecond = outputRecord.resourceRegenerationPerSecond,
                        resourceRegenerationLimit = outputRecord.resourceRegenerationLimit
                    };
                }

                if (!hasPrimaryResourceType && outputs != null && outputs.Length > 0 && outputs[0] != null)
                {
                    primaryResourceType = outputs[0].type;
                    hasPrimaryResourceType = true;
                }

                definitions[index] = new ResourceObjectDefinition
                {
                    id = record.id,
                    primaryResourceType = primaryResourceType,
                    primaryResourceTypeWasExplicitlySet = hasPrimaryResourceType,
                    spawnSettings = record.spawnSettings,
                    sortingAnchorOffset = record.sortingAnchorOffset,
                    shadowSize = record.shadowSize,
                    shadowOffset = record.shadowOffset,
                    outputs = outputs
                };
            }

            return new ResourceObjectData
            {
                resourceObjects = definitions
            };
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(
                "Could not parse resource object data at Resources/"
                + ResourceObjectDataResourcePath + ".json: " + exception.Message);
            return new ResourceObjectData();
        }
    }

    private static bool IsValidResourceObjectSpawnSettings(ResourceObjectSpawnSettings settings)
    {
        return settings.count >= 0
            && IsFiniteNonNegative(settings.edgePadding)
            && IsFiniteNonNegative(settings.minimumSpacing)
            && settings.placementAttemptsPerObject > 0
            && IsFinite(settings.visualOffset.x)
            && IsFinite(settings.visualOffset.y)
            && IsFinitePositive(settings.scaleRange.x)
            && IsFinitePositive(settings.scaleRange.y)
            && settings.scaleRange.y >= settings.scaleRange.x;
    }

    private static GameSettingsCollection LoadGameSettings()
    {
        TextAsset settingsJson = Resources.Load<TextAsset>(GameSettingsResourcePath);
        if (settingsJson == null)
        {
            Debug.LogError("Could not load settings at Resources/" + GameSettingsResourcePath + ".json.");
            return new GameSettingsCollection();
        }

        try
        {
            GameSettingsJsonCollection jsonSettings =
                JsonUtility.FromJson<GameSettingsJsonCollection>(settingsJson.text);
            if (jsonSettings == null
                || jsonSettings.uiSettings == null
                || jsonSettings.mapSettings == null
                || jsonSettings.playerSettings == null
                || jsonSettings.houseSettings == null
                || jsonSettings.communityStorageSettings == null
                || jsonSettings.villagerSettings == null
                || jsonSettings.resourceSettings == null
                || jsonSettings.combatSettings == null)
            {
                Debug.LogError("Settings at Resources/" + GameSettingsResourcePath + ".json must include uiSettings, mapSettings, playerSettings, houseSettings, communityStorageSettings, villagerSettings, resourceSettings, and combatSettings.");
                return new GameSettingsCollection();
            }

            if (jsonSettings.houseSettings.baseHouseCosts == null)
            {
                Debug.LogError("House settings must define baseHouseCosts with named resource types.");
                return new GameSettingsCollection();
            }

            Array resourceTypes = Enum.GetValues(typeof(ResourceType));
            if (jsonSettings.playerSettings.initialResourceAmounts == null
                || jsonSettings.playerSettings.initialResourceAmounts.Length != resourceTypes.Length)
            {
                Debug.LogError(
                    "Player settings must define one initial resource amount per ResourceType.");
                return new GameSettingsCollection();
            }

            ResourceInitialAmount[] initialResourceAmounts =
                new ResourceInitialAmount[jsonSettings.playerSettings.initialResourceAmounts.Length];
            bool[] foundInitialAmounts = new bool[resourceTypes.Length];
            for (int index = 0; index < jsonSettings.playerSettings.initialResourceAmounts.Length; index++)
            {
                ResourceInitialAmountRecord record =
                    jsonSettings.playerSettings.initialResourceAmounts[index];
                if (record == null
                    || !TryParseEnumName(record.type, out ResourceType resourceType)
                    || record.initialAmount < 0)
                {
                    Debug.LogError("Player resource starting amounts contain an invalid entry.");
                    return new GameSettingsCollection();
                }

                int resourceIndex = Convert.ToInt32(resourceType);
                if (resourceIndex < 0
                    || resourceIndex >= foundInitialAmounts.Length
                    || foundInitialAmounts[resourceIndex])
                {
                    Debug.LogError(
                        "Player resource starting amounts must define each ResourceType exactly once.");
                    return new GameSettingsCollection();
                }

                foundInitialAmounts[resourceIndex] = true;
                initialResourceAmounts[index] = new ResourceInitialAmount
                {
                    type = resourceType,
                    initialAmount = record.initialAmount
                };
            }

            ResourceCost[] houseCosts = new ResourceCost[jsonSettings.houseSettings.baseHouseCosts.Length];
            for (int index = 0; index < jsonSettings.houseSettings.baseHouseCosts.Length; index++)
            {
                ResourceCostJson jsonCost = jsonSettings.houseSettings.baseHouseCosts[index];
                if (jsonCost == null
                    || !TryParseEnumName(jsonCost.resourceType, out ResourceType resourceType))
                {
                    Debug.LogError("House resource costs must use valid named ResourceType values.");
                    return new GameSettingsCollection();
                }

                houseCosts[index] = new ResourceCost
                {
                    resourceType = resourceType,
                    amount = jsonCost.amount
                };
            }

            HouseSettingsJson jsonHouseSettings = jsonSettings.houseSettings;
            CommunityStorageSettingsJson jsonCommunityStorageSettings =
                jsonSettings.communityStorageSettings;
            if (jsonCommunityStorageSettings.dimensions.x <= 0
                || jsonCommunityStorageSettings.dimensions.y <= 0
                || jsonCommunityStorageSettings.maximumAmountPerStorage <= 0
                || jsonCommunityStorageSettings.unitsPerAdditionalIcon <= 0)
            {
                Debug.LogError(
                    "Community storage settings must define positive dimensions, a positive per-storage capacity, "
                    + "and a positive unitsPerAdditionalIcon.");
                return new GameSettingsCollection();
            }

            GameSettingsCollection settings = new GameSettingsCollection
            {
                uiSettings = jsonSettings.uiSettings,
                mapSettings = jsonSettings.mapSettings,
                playerSettings = new PlayerSettings
                {
                    initialVillagerCount = jsonSettings.playerSettings.initialVillagerCount,
                    manaGainIntervalSeconds = jsonSettings.playerSettings.manaGainIntervalSeconds,
                    manaGainAmount = jsonSettings.playerSettings.manaGainAmount,
                    initialResourceAmounts = initialResourceAmounts
                },
                houseSettings = new HouseSettings
                {
                    storageCapacity = jsonHouseSettings.storageCapacity,
                    defaultHouseDimensions = jsonHouseSettings.defaultHouseDimensions,
                    baseHouseCosts = houseCosts,
                    firstConstructionDurationSeconds = jsonHouseSettings.firstConstructionDurationSeconds,
                    constructionBreakDurationSeconds = jsonHouseSettings.constructionBreakDurationSeconds,
                    secondConstructionDurationSeconds = jsonHouseSettings.secondConstructionDurationSeconds,
                    hammerMotionDurationSeconds = jsonHouseSettings.hammerMotionDurationSeconds,
                    hammerPauseDurationSeconds = jsonHouseSettings.hammerPauseDurationSeconds,
                    productionProgressBarOffsetFromTop =
                        jsonHouseSettings.productionProgressBarOffsetFromTop,
                    hammerHitPositions = jsonHouseSettings.hammerHitPositions
                },
                communityStorageSettings = new CommunityStorageSettings
                {
                    dimensions = jsonCommunityStorageSettings.dimensions,
                    maximumAmountPerStorage = jsonCommunityStorageSettings.maximumAmountPerStorage,
                    unitsPerAdditionalIcon = jsonCommunityStorageSettings.unitsPerAdditionalIcon
                },
                villagerSettings = jsonSettings.villagerSettings,
                resourceSettings = jsonSettings.resourceSettings,
                combatSettings = jsonSettings.combatSettings
            };

            if (settings.villagerSettings.villagerScaleSettings == null
                || !IsValidScaleModifierRange(settings.villagerSettings.villagerScaleSettings.male)
                || !IsValidScaleModifierRange(settings.villagerSettings.villagerScaleSettings.female))
            {
                Debug.LogError(
                    "Villager scale settings must define positive minimum and maximum x/y modifiers "
                    + "for both genders, with each minimum no greater than its maximum.");
                return new GameSettingsCollection();
            }

            if (settings.uiSettings.villagerIndicatorSize.x <= 0f
                || settings.uiSettings.villagerIndicatorSize.y <= 0f
                || settings.uiSettings.villagerIndicatorEdgeInset < 0f
                || !IsFinitePositive(settings.uiSettings.uiIconScaleOnWorld))
            {
                Debug.LogError(
                    "Settings at Resources/" + GameSettingsResourcePath
                    + ".json must define a positive villagerIndicatorSize and uiIconScaleOnWorld, "
                    + "and a non-negative villagerIndicatorEdgeInset.");
                return new GameSettingsCollection();
            }

            if (settings.mapSettings.roadMovementSpeedMultiplier < 1f
                || settings.mapSettings.roadTileSpacing <= 0f
                || !IsFiniteNonNegative(settings.mapSettings.roadMovementBonusRange)
                || settings.mapSettings.manaCostPerKeyPoint < 0
                || !IsFinitePositive(settings.mapSettings.stuckMovementTimeoutSeconds))
            {
                Debug.LogError(
                    "Settings at Resources/" + GameSettingsResourcePath
                    + ".json must define a roadMovementSpeedMultiplier of at least 1, a positive roadTileSpacing, "
                    + "a finite non-negative roadMovementBonusRange, a non-negative manaCostPerKeyPoint, "
                    + "and a positive stuckMovementTimeoutSeconds.");
                return new GameSettingsCollection();
            }

            if (settings.playerSettings.initialVillagerCount < 0)
            {
                Debug.LogError(
                    "Settings at Resources/" + GameSettingsResourcePath
                    + ".json cannot have a negative initialVillagerCount. Using the default value of 8.");
                settings.playerSettings.initialVillagerCount = 8;
            }

            if (settings.houseSettings.defaultHouseDimensions.x <= 0
                || settings.houseSettings.defaultHouseDimensions.y <= 0
                || settings.houseSettings.storageCapacity <= 0
                || settings.houseSettings.firstConstructionDurationSeconds <= 0f
                || settings.houseSettings.constructionBreakDurationSeconds <= 0f
                || settings.houseSettings.secondConstructionDurationSeconds <= 0f
                || settings.houseSettings.hammerMotionDurationSeconds <= 0f
                || settings.houseSettings.hammerPauseDurationSeconds < 0f
                || !IsFinite(settings.houseSettings.productionProgressBarOffsetFromTop.x)
                || !IsFinite(settings.houseSettings.productionProgressBarOffsetFromTop.y)
                || !IsFinite(settings.houseSettings.productionProgressBarOffsetFromTop.z)
                || !AreValidArmMotionPoints(settings.houseSettings.hammerHitPositions)
                || settings.houseSettings.baseHouseCosts == null
                || settings.houseSettings.baseHouseCosts.Length == 0)
            {
                Debug.LogError(
                    "House settings must define positive storage capacity, dimensions and construction times, "
                    + "non-negative break/hammer pauses, "
                    + "a finite productionProgressBarOffsetFromTop, hammer positions, "
                    + "and at least one resource cost.");
                return new GameSettingsCollection();
            }

            bool[] configuredHouseCosts = new bool[Enum.GetValues(typeof(ResourceType)).Length];
            foreach (ResourceCost cost in settings.houseSettings.baseHouseCosts)
            {
                if (cost == null
                    || !Enum.IsDefined(typeof(ResourceType), cost.resourceType)
                    || cost.amount <= 0)
                {
                    Debug.LogError("House resource costs must use valid resource types and positive amounts.");
                    return new GameSettingsCollection();
                }

                int resourceIndex = Convert.ToInt32(cost.resourceType);
                if (resourceIndex < 0
                    || resourceIndex >= configuredHouseCosts.Length
                    || configuredHouseCosts[resourceIndex])
                {
                    Debug.LogError("House resource costs must contain at most one entry per ResourceType.");
                    return new GameSettingsCollection();
                }

                configuredHouseCosts[resourceIndex] = true;
            }

            VillagerSettings villagerSettings = settings.villagerSettings;
            if (villagerSettings.idleBehaviorDuration <= 0f
                || villagerSettings.roadDetectionDistance < 0f
                || villagerSettings.roadSearchDistance < villagerSettings.roadDetectionDistance
                || villagerSettings.runningSpeed <= 0f
                || villagerSettings.reactionTimeRange.x < 0f
                || villagerSettings.reactionTimeRange.y < villagerSettings.reactionTimeRange.x
                || villagerSettings.hungerIncreasePerSecond <= 0f
                || villagerSettings.fatigueIncreasePerSecond <= 0f
                || villagerSettings.fatigueRecoveryPerSecond <= 0f
                || villagerSettings.homelessIdleFatigueRecoveryPerSecond < 0f
                || villagerSettings.homeRestStartFatigueThreshold
                    <= villagerSettings.homeRestFatigueThreshold
                || villagerSettings.homeRestStartFatigueThreshold > 100f
                || villagerSettings.homeRestFatigueThreshold < 0f
                || villagerSettings.homeRestFatigueThreshold > 100f
                || villagerSettings.lazyHomeRestFatigueThreshold < 0f
                || villagerSettings.lazyHomeRestFatigueThreshold
                    > villagerSettings.homeRestFatigueThreshold
                || villagerSettings.productiveFatigueThreshold < 0f
                || villagerSettings.productiveFatigueThreshold > 100f
                // Work must restart below its fatigue trigger to prevent rest/work oscillation.
                || villagerSettings.productiveFatigueThreshold
                    <= villagerSettings.homeRestFatigueThreshold
                || villagerSettings.productiveFatigueThreshold
                    <= villagerSettings.lazyHomeRestFatigueThreshold
                || villagerSettings.idleHomeStayProbability < 0f
                || villagerSettings.idleHomeStayProbability > 1f
                || villagerSettings.lazyIdleHomeStayProbability
                    <= villagerSettings.idleHomeStayProbability
                || villagerSettings.lazyIdleHomeStayProbability > 1f
                || villagerSettings.emergencyFoodHungerThreshold <= 0f
                || villagerSettings.emergencyFoodHungerThreshold > 100f
                || villagerSettings.resumeTaskHungerThreshold < 0f
                || villagerSettings.resumeTaskHungerThreshold >= villagerSettings.emergencyFoodHungerThreshold
                || villagerSettings.boredFoodSeekingHungerThreshold < 0f
                || villagerSettings.boredFoodSeekingHungerThreshold > 100f
                || villagerSettings.hungerRestoredPerFoodUnit <= 0f
                || villagerSettings.communityStorageOperationPriority < 0f
                || villagerSettings.communityStoragePriorityPerItem <= 0f
                || villagerSettings.communityStorageSearchRetrySeconds <= 0f
                || villagerSettings.housingOperationPriority < 0f
                || villagerSettings.lazyHousingPriorityPenalty < 0f
                || villagerSettings.foodOperationPriority < 0f
                || villagerSettings.hungerFoodPriorityMultiplier < 0f
                || villagerSettings.emergencyFoodPriorityBonus < 0f
                || villagerSettings.productiveOperationPriority < 0f
                || villagerSettings.helpfulConstructionOperationPriority <= 0f
                || villagerSettings.foodResourceSearchRetrySeconds <= 0f
                || villagerSettings.housingSearchAttemptsBeforePause <= 0
                || villagerSettings.housingSearchRetrySeconds <= 0f
                || !IsFiniteNonNegative(
                    villagerSettings.productiveOperationPriorityWhileMissingRequiredItem)
                || villagerSettings.requiredItemWaitInterruptibility == null
                || villagerSettings.gatheringDistance < 0f
                || villagerSettings.gatheringPauseDurationSeconds < 0f
                || villagerSettings.handMotionDurationSeconds <= 0f
                || villagerSettings.maleResourceHoldingArmPosition == null
                || !IsFinite(villagerSettings.maleResourceHoldingArmPosition.x)
                || !IsFinite(villagerSettings.maleResourceHoldingArmPosition.y)
                || !IsFinite(villagerSettings.maleResourceHoldingArmPosition.z)
                || villagerSettings.femaleResourceHoldingArmPosition == null
                || !IsFinite(villagerSettings.femaleResourceHoldingArmPosition.x)
                || !IsFinite(villagerSettings.femaleResourceHoldingArmPosition.y)
                || !IsFinite(villagerSettings.femaleResourceHoldingArmPosition.z)
                || !IsFinite(villagerSettings.resourceHoldingIconLocalPosition.x)
                || !IsFinite(villagerSettings.resourceHoldingIconLocalPosition.y)
                || !IsFinite(villagerSettings.resourceHoldingIconLocalPosition.z)
                || !IsValidSortingLayerName(villagerSettings.resourceHoldingIconSortingLayer)
                || !AreValidArmMotionPoints(villagerSettings.handHitPositions))
            {
                Debug.LogError(
                    "Villager settings contain invalid movement, fatigue, housing-search, "
                    + "required-item wait priority/interruptibility, gathering, or resource-holding values.");
                return new GameSettingsCollection();
            }

            ResourceSettings resourceSettings = settings.resourceSettings;
            if (resourceSettings.shakeDurationSeconds <= 0f
                || resourceSettings.shakeMagnitude < 0f
                || resourceSettings.shakeFrequency <= 0f)
            {
                Debug.LogError("Resource shake settings must define positive duration and frequency, and a non-negative magnitude.");
                return new GameSettingsCollection();
            }

            CombatSettings combatSettings = settings.combatSettings;
            if (combatSettings.maximumHealth <= 0f
                || combatSettings.attackDamage <= 0f
                || combatSettings.attackRange <= 0f
                || combatSettings.attackWindupSeconds <= 0f
                || combatSettings.attackRecoverySeconds < 0f
                || combatSettings.runningSpeed <= 0f
                || combatSettings.monsterReactionTimeSeconds < 0f
                || combatSettings.detectionIntervalReactionTimeMultiplier <= 0f
                || combatSettings.detectionRangeCells <= 0
                || !AreValidArmMotionPoints(combatSettings.attackHitPositions))
            {
                Debug.LogError("Combat settings must define positive health, damage, range, windup, running speed, detection interval multiplier, and detection range; reaction time and recovery must be non-negative, and attack positions must be valid.");
                return new GameSettingsCollection();
            }

            return settings;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("Could not parse settings at Resources/" + GameSettingsResourcePath + ".json: " + exception.Message);
            return new GameSettingsCollection();
        }
    }

    private static bool IsValidScaleModifierRange(ScaleModifierRange range)
    {
        return range != null
            && IsFinitePositive(range.minimum.x)
            && IsFinitePositive(range.minimum.y)
            && IsFinitePositive(range.maximum.x)
            && IsFinitePositive(range.maximum.y)
            && range.minimum.x <= range.maximum.x
            && range.minimum.y <= range.maximum.y;
    }

    private static bool IsFinitePositive(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsValidSortingLayerName(string sortingLayerName)
    {
        if (string.IsNullOrWhiteSpace(sortingLayerName))
        {
            return false;
        }

        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.name == sortingLayerName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreValidArmMotionPoints(ArmMotionPoint[] points)
    {
        if (points == null || points.Length == 0)
        {
            return false;
        }

        foreach (ArmMotionPoint point in points)
        {
            if (point == null
                || float.IsNaN(point.x) || float.IsInfinity(point.x)
                || float.IsNaN(point.y) || float.IsInfinity(point.y)
                || float.IsNaN(point.z) || float.IsInfinity(point.z))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseEnumName<T>(string value, out T parsedValue)
        where T : struct
    {
        parsedValue = default(T);
        if (string.IsNullOrEmpty(value)
            || !Enum.TryParse(value, false, out parsedValue)
            || !Enum.IsDefined(typeof(T), parsedValue))
        {
            return false;
        }

        return string.Equals(
            Enum.GetName(typeof(T), parsedValue),
            value,
            StringComparison.Ordinal);
    }

    private static Sprite LoadSprite(string resourcePath, string bodyName, string fieldName)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Debug.LogError(
                "Could not load sprite '" + resourcePath + "' for body '" + bodyName +
                "' field '" + fieldName + "'. Sprite paths must be relative to a Resources folder.");
        }

        return sprite;
    }

    private static Sprite[] LoadSprites(string[] spriteNames, string bodyName, string fieldName)
    {
        if (spriteNames == null || spriteNames.Length == 0)
        {
            return new Sprite[0];
        }

        Sprite[] sprites = new Sprite[spriteNames.Length];
        for (int index = 0; index < spriteNames.Length; index++)
        {
            string spriteName = spriteNames[index];
            sprites[index] = string.IsNullOrEmpty(spriteName)
                ? null
                : LoadSprite("BodyParts/" + spriteName, bodyName, fieldName);
        }

        //Debug.Log("Loaded " + sprites.Length + " sprites for body '" + bodyName + "' field '" + fieldName + "'.");

        return sprites;
    }
}
