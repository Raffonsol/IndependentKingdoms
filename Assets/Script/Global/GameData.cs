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
    private const string ResourceObjectDataResourcePath = "Data/ResourceObjects";
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
    public MonsterSpawnSettings monsterSpawnSettings;
    public MonsterDefinition[] monsterDefinitions;
    public ResourceDefinition[] resourceDefinitions;
    public ResourceObjectDefinition[] resourceObjectDefinitions;
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
            villagerSettings = gameSettings.villagerSettings,
            resourceSettings = gameSettings.resourceSettings,
            combatSettings = gameSettings.combatSettings,
            monsterSpawnSettings = monsterData.spawnSettings,
            monsterDefinitions = monsterData.monsters,
            resourceDefinitions = LoadResourceDefinitions(),
            resourceObjectDefinitions = resourceObjectData.resourceObjects,
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
            if (definition.visualScale <= 0f
                || !IsFinite(definition.sortingAnchorOffset.x)
                || !IsFinite(definition.sortingAnchorOffset.y))
            {
                Debug.LogError(
                    "House '" + record.id
                    + "' must define a positive visualScale and finite sortingAnchorOffset.");
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

            if (!IsFinite(record.sortingAnchorOffset.x)
                || !IsFinite(record.sortingAnchorOffset.y))
            {
                Debug.LogError(
                    "Body '" + record.name + "' must define a finite sortingAnchorOffset.");
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
                    iconIndex = jsonDefinition.iconIndex
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

                    foundOutputTypes[typeIndex] = true;
                    outputs[outputIndex] = new ResourceOutputDefinition
                    {
                        type = type,
                        fullCutAmount = outputRecord.fullCutAmount,
                        bareHandAmount = outputRecord.bareHandAmount,
                        interactionOffset = outputRecord.interactionOffset,
                        resourceRegenerationPerSecond = outputRecord.resourceRegenerationPerSecond,
                        resourceRegenerationLimit = outputRecord.resourceRegenerationLimit
                    };
                }

                definitions[index] = new ResourceObjectDefinition
                {
                    id = record.id,
                    spawnSettings = record.spawnSettings,
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
                || jsonSettings.villagerSettings == null
                || jsonSettings.resourceSettings == null
                || jsonSettings.combatSettings == null)
            {
                Debug.LogError("Settings at Resources/" + GameSettingsResourcePath + ".json must include uiSettings, mapSettings, playerSettings, houseSettings, villagerSettings, resourceSettings, and combatSettings.");
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
                    defaultHouseDimensions = jsonHouseSettings.defaultHouseDimensions,
                    baseHouseCosts = houseCosts,
                    firstConstructionDurationSeconds = jsonHouseSettings.firstConstructionDurationSeconds,
                    constructionBreakDurationSeconds = jsonHouseSettings.constructionBreakDurationSeconds,
                    secondConstructionDurationSeconds = jsonHouseSettings.secondConstructionDurationSeconds,
                    hammerMotionDurationSeconds = jsonHouseSettings.hammerMotionDurationSeconds,
                    hammerPauseDurationSeconds = jsonHouseSettings.hammerPauseDurationSeconds,
                    hammerHitPositions = jsonHouseSettings.hammerHitPositions
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
