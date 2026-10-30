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
    public ResourceDefinition[] resourceDefinitions;
    public NamePart[] nameParts;

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
            resourceDefinitions = LoadResourceDefinitions(),
            nameParts = LoadNameParts()
        };
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
                || settings.resourceSettings == null)
            {
                Debug.LogError("Settings at Resources/" + GameSettingsResourcePath + ".json must include uiSettings, mapSettings, playerSettings, houseSettings, villagerSettings, and resourceSettings.");
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

            if (settings.playerSettings.initialVillagerCount < 0)
            {
                Debug.LogError(
                    "Settings at Resources/" + GameSettingsResourcePath
                    + ".json cannot have a negative initialVillagerCount. Using the default value of 8.");
                settings.playerSettings.initialVillagerCount = 8;
            }

            if (settings.houseSettings.defaultHouseDimensions.x <= 0
                || settings.houseSettings.defaultHouseDimensions.y <= 0
                || settings.houseSettings.constructionDurationSeconds <= 0f
                || settings.houseSettings.baseHouseCosts == null
                || settings.houseSettings.baseHouseCosts.Length == 0)
            {
                Debug.LogError("House settings must define positive dimensions and construction duration, and at least one resource cost.");
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
                || villagerSettings.gatheringDistance < 0f
                || villagerSettings.gatheringPauseDurationSeconds < 0f
                || villagerSettings.handMotionDurationSeconds <= 0f
                || villagerSettings.handHitPositions == null
                || villagerSettings.handHitPositions.Length == 0)
            {
                Debug.LogError("Villager settings contain invalid movement or gathering values.");
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

            return settings;
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("Could not parse settings at Resources/" + GameSettingsResourcePath + ".json: " + exception.Message);
            return new GameSettingsCollection();
        }
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
