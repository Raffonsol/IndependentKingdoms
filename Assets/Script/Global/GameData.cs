using System;
using UnityEngine;

[Serializable]
public class GameData
{
    private const string BodyDataResourcePath = "Data/Bodies";
    private const string CharacterSettingsDataResourcePath = "Data/CharacterSettings";
    private const string GameSettingsResourcePath = "Data/GameSettings";
    private const string ResourceDefinitionsResourcePath = "Data/ResourceDefinitions";
    private const string NamingPartsResourcePath = "Data/NamingParts";
    private const string HouseDefinitionsResourcePath = "Data/HouseDefinitions";
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
    public VillagerSettings villagerSettings;
    public ResourceSettings resourceSettings;
    public CombatSettings combatSettings;
    public ResourceDefinition[] resourceDefinitions;
    public NamePart[] nameParts;
    public VillagerPersonalityValue[] villagerPersonalityValues;
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

    private static GameData Load()
    {
        GameSettingsCollection gameSettings = LoadGameSettings();
        HouseDefinitionCollection houseDefinitionCollection = LoadHouseDefinitionCollection();
        return new GameData
        {
            bodies = LoadBodies(),
            characterSettings = LoadCharacterSettings(),
            uiSettings = gameSettings.uiSettings,
            mapSettings = gameSettings.mapSettings,
            playerSettings = gameSettings.playerSettings,
            houseSettings = gameSettings.houseSettings,
            villagerSettings = gameSettings.villagerSettings,
            resourceSettings = gameSettings.resourceSettings,
            combatSettings = gameSettings.combatSettings,
            resourceDefinitions = LoadResourceDefinitions(),
            nameParts = LoadNameParts(),
            villagerPersonalityValues = LoadVillagerPersonalityValues(),
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
            VillagerPersonalityValueCollection collection =
                JsonUtility.FromJson<VillagerPersonalityValueCollection>(valuesJson.text);
            Array personalityTypes = Enum.GetValues(typeof(VillagerPersonalityType));
            if (collection == null
                || collection.villagerPersonalityValues == null
                || collection.villagerPersonalityValues.Length != personalityTypes.Length)
            {
                Debug.LogError(
                    "Villager personality values at Resources/" + VillagerPersonalityValuesResourcePath
                    + ".json must contain exactly one entry per VillagerPersonalityType.");
                return new VillagerPersonalityValue[0];
            }

            bool[] foundTypes = new bool[personalityTypes.Length];
            foreach (VillagerPersonalityValue value in collection.villagerPersonalityValues)
            {
                if (value == null || !Enum.IsDefined(typeof(VillagerPersonalityType), value.type))
                {
                    Debug.LogError("Villager personality values contain an invalid or null type.");
                    return new VillagerPersonalityValue[0];
                }

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

            return collection.villagerPersonalityValues;
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
                baseSprites = LoadHouseSprites(record.baseSprites, record.id, "baseSprites", true),
                baseColors = LoadHouseColors(record.baseColorCodes, record.id, "baseColorCodes"),
                lowRoofSprites = LoadHouseSprites(record.lowRoofSprites, record.id, "lowRoofSprites", true),
                lowRoofColors = LoadHouseColors(record.lowRoofColorCodes, record.id, "lowRoofColorCodes"),
                highRoofSprites = LoadHouseSprites(record.highRoofSprites, record.id, "highRoofSprites", false),
                highRoofColors = LoadHouseColors(record.highRoofColorCodes, record.id, "highRoofColorCodes"),
                secondLevelSprites = LoadHouseSprites(record.secondLevelSprites, record.id, "secondLevelSprites", false),
                secondLevelColors = LoadHouseColors(
                    record.secondLevelColorCodes, record.id, "secondLevelColorCodes"),
                secondLevelExtensionSprites = LoadHouseSprites(
                    record.secondLevelExtensionSprites, record.id, "secondLevelExtensionSprites", false),
                secondLevelExtensionColors = LoadHouseColors(
                    record.secondLevelExtensionColorCodes, record.id, "secondLevelExtensionColorCodes"),
                extensionRoofSprites = LoadHouseSprites(
                    record.extensionRoofSprites, record.id, "extensionRoofSprites", false),
                extensionRoofColors = LoadHouseColors(
                    record.extensionRoofColorCodes, record.id, "extensionRoofColorCodes"),
                annexSprites = LoadHouseSprites(record.annexSprites, record.id, "annexSprites", false),
                annexColors = LoadHouseColors(record.annexColorCodes, record.id, "annexColorCodes"),
                annexRoofSprites = LoadHouseSprites(record.annexRoofSprites, record.id, "annexRoofSprites", false),
                annexRoofColors = LoadHouseColors(record.annexRoofColorCodes, record.id, "annexRoofColorCodes")
            };
            if (definition.visualScale <= 0f)
            {
                Debug.LogError(
                    "House '" + record.id + "' must define a visualScale greater than zero.");
                return new HouseDefinition[0];
            }

            if (definition.baseSprites == null
                || definition.baseColors == null
                || definition.lowRoofSprites == null
                || definition.lowRoofColors == null
                || definition.highRoofSprites == null
                || definition.highRoofColors == null
                || definition.secondLevelSprites == null
                || definition.secondLevelColors == null
                || definition.secondLevelExtensionSprites == null
                || definition.secondLevelExtensionColors == null
                || definition.extensionRoofSprites == null
                || definition.extensionRoofColors == null
                || definition.annexSprites == null
                || definition.annexColors == null
                || definition.annexRoofSprites == null
                || definition.annexRoofColors == null)
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
                movementSpeed = record.movementSpeed,
                movementDurationRangeX = record.movementDurationRangeX,
                movementDurationRangeY = record.movementDurationRangeY,
                idleDurationRangeX = record.idleDurationRangeX,
                idleDurationRangeY = record.idleDurationRangeY,
                legSwingAngle = record.legSwingAngle,
                legSwingSpeed = record.legSwingSpeed,
                legPivotOffsetX = record.legPivotOffsetX,
                legPivotOffsetY = record.legPivotOffsetY,
                idleHeaveHeight = record.idleHeaveHeight,
                idleHeaveSpeed = record.idleHeaveSpeed
            };
        }

        return bodies;
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
            NamePartCollection collection = JsonUtility.FromJson<NamePartCollection>(namingPartsJson.text);
            if (collection == null || collection.nameParts == null)
            {
                Debug.LogError("Naming parts at Resources/" + NamingPartsResourcePath + ".json must contain a JSON array.");
                return new NamePart[0];
            }

            return collection.nameParts;
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
            ResourceDefinitionCollection collection =
                JsonUtility.FromJson<ResourceDefinitionCollection>(definitionsJson.text);
            Array resourceTypes = Enum.GetValues(typeof(ResourceType));
            if (collection == null
                || collection.resourceDefinitions == null
                || collection.resourceDefinitions.Length != resourceTypes.Length)
            {
                Debug.LogError(
                    "Resource definitions at Resources/" + ResourceDefinitionsResourcePath
                    + ".json must contain exactly one entry per ResourceType.");
                return new ResourceDefinition[0];
            }

            bool[] foundTypes = new bool[resourceTypes.Length];
            foreach (ResourceDefinition definition in collection.resourceDefinitions)
            {
                if (definition == null || !Enum.IsDefined(typeof(ResourceType), definition.type))
                {
                    Debug.LogError("Resource definitions contain an invalid or null ResourceType entry.");
                    return new ResourceDefinition[0];
                }

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

                if (definition.initialAmount < 0)
                {
                    Debug.LogError("Initial resource amounts cannot be negative.");
                    return new ResourceDefinition[0];
                }

                if (definition.fullCutAmount < 0
                    || definition.bareHandAmount < 0
                    || definition.bareHandAmount > definition.fullCutAmount)
                {
                    Debug.LogError(
                        "Resource harvest amounts must be non-negative, and bareHandAmount cannot exceed fullCutAmount.");
                    return new ResourceDefinition[0];
                }

                if (definition.treeRegenerationPerSecond < 0f
                    || definition.treeRegenerationLimit < 0
                    || (definition.treeRegenerationPerSecond > 0f
                        && definition.treeRegenerationLimit <= 0))
                {
                    Debug.LogError(
                        "Tree regeneration rates and limits must be non-negative, and a positive rate requires a positive limit.");
                    return new ResourceDefinition[0];
                }

                foundTypes[enumIndex] = true;
            }

            return collection.resourceDefinitions;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError(
                "Could not parse resource definitions at Resources/" + ResourceDefinitionsResourcePath
                + ".json: " + exception.Message);
            return new ResourceDefinition[0];
        }
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
            GameSettingsCollection settings = JsonUtility.FromJson<GameSettingsCollection>(settingsJson.text);
            if (settings == null
                || settings.uiSettings == null
                || settings.mapSettings == null
                || settings.playerSettings == null
                || settings.houseSettings == null
                || settings.villagerSettings == null
                || settings.resourceSettings == null
                || settings.combatSettings == null)
            {
                Debug.LogError("Settings at Resources/" + GameSettingsResourcePath + ".json must include uiSettings, mapSettings, playerSettings, houseSettings, villagerSettings, resourceSettings, and combatSettings.");
                return new GameSettingsCollection();
            }

            if (settings.uiSettings.villagerIndicatorSize.x <= 0f
                || settings.uiSettings.villagerIndicatorSize.y <= 0f
                || settings.uiSettings.villagerIndicatorEdgeInset < 0f)
            {
                Debug.LogError(
                    "Settings at Resources/" + GameSettingsResourcePath
                    + ".json must define a positive villagerIndicatorSize and a non-negative villagerIndicatorEdgeInset.");
                return new GameSettingsCollection();
            }

            if (settings.mapSettings.roadMovementSpeedMultiplier < 1f)
            {
                Debug.LogError(
                    "Settings at Resources/" + GameSettingsResourcePath
                    + ".json must define a roadMovementSpeedMultiplier of at least 1.");
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
                || settings.houseSettings.firstConstructionDurationSeconds <= 0f
                || settings.houseSettings.constructionBreakDurationSeconds <= 0f
                || settings.houseSettings.secondConstructionDurationSeconds <= 0f
                || settings.houseSettings.hammerMotionDurationSeconds <= 0f
                || settings.houseSettings.hammerPauseDurationSeconds < 0f
                || !AreValidArmMotionPoints(settings.houseSettings.hammerHitPositions)
                || settings.houseSettings.baseHouseCosts == null
                || settings.houseSettings.baseHouseCosts.Length == 0)
            {
                Debug.LogError(
                    "House settings must define positive dimensions and construction times, non-negative break/hammer pauses, "
                    + "hammer positions, and at least one resource cost.");
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
                || villagerSettings.homeRestFatigueThreshold < 0f
                || villagerSettings.homeRestFatigueThreshold > 100f
                || villagerSettings.lazyHomeRestFatigueThreshold < 0f
                || villagerSettings.lazyHomeRestFatigueThreshold
                    > villagerSettings.homeRestFatigueThreshold
                || villagerSettings.productiveFatigueThreshold < 0f
                || villagerSettings.productiveFatigueThreshold > 100f
                || villagerSettings.emergencyFoodHungerThreshold <= 0f
                || villagerSettings.emergencyFoodHungerThreshold > 100f
                || villagerSettings.resumeTaskHungerThreshold < 0f
                || villagerSettings.resumeTaskHungerThreshold >= villagerSettings.emergencyFoodHungerThreshold
                || villagerSettings.boredFoodSeekingHungerThreshold < 0f
                || villagerSettings.boredFoodSeekingHungerThreshold > 100f
                || villagerSettings.hungerRestoredPerFoodUnit <= 0f
                || villagerSettings.housingOperationPriority < 0f
                || villagerSettings.lazyHousingPriorityPenalty < 0f
                || villagerSettings.foodOperationPriority < 0f
                || villagerSettings.hungerFoodPriorityMultiplier < 0f
                || villagerSettings.emergencyFoodPriorityBonus < 0f
                || villagerSettings.productiveOperationPriority < 0f
                || villagerSettings.foodResourceSearchRetrySeconds <= 0f
                || villagerSettings.housingSearchAttemptsBeforePause <= 0
                || villagerSettings.housingSearchRetrySeconds <= 0f
                || villagerSettings.gatheringDistance < 0f
                || villagerSettings.gatheringPauseDurationSeconds < 0f
                || villagerSettings.handMotionDurationSeconds <= 0f
                || !AreValidArmMotionPoints(villagerSettings.handHitPositions))
            {
                Debug.LogError("Villager settings contain invalid movement, fatigue, housing-search, or gathering values.");
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
                || !AreValidArmMotionPoints(combatSettings.attackHitPositions))
            {
                Debug.LogError("Combat settings must define positive health, damage, range, and windup, non-negative recovery, and attack positions.");
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
