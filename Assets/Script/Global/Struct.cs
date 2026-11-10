using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// UI

public enum UIButtonAction
{
    Build,
    BuildRoad,
    TogglePanel,
    Pause
}

public enum UIValueType
{
    Resource,
    Population,
    AdultPopulation,
    ChildPopulation,
    Happiness,
}

[Serializable]
public class UIValueDefinition
{
    public UIValueType type = UIValueType.Resource;
    public ResourceType resourceType;
}


// Game Data

[Serializable]
public class BuildingPrefab
{
    public string name;
    public int[] prefabIds;
}

// character creation

[Serializable]
public class Body
{
    public string name;
    public bool isMale;
    public Sprite torsoSprite;
    public Sprite leftArmSprite;
    public Sprite rightArmSprite;
    public Sprite legSprite;
    public Sprite[] faceSprite;
    public Sprite[] earSprite;
    public Sprite[] hairSprite;
    public Sprite[] clothingTopSprite;
    public Sprite[] clothingBottomSprite;
    public Sprite[] leggingSprite;
    public Sprite[] bootSprite;
    public Sprite[] leftSleeveSprite;
    public Sprite[] rightSleeveSprite;

    public float rightLegXOffset;
    public float rightLegYOffset;
    public Vector2 sortingAnchorOffset;
    public Vector2 shadowSize = new Vector2(1f, 0.35f);
    public Vector2 shadowOffset;

   public float movementSpeed = 2f;
   public float movementDurationRangeX = 1f;
   public float movementDurationRangeY = 3f;
   public float idleDurationRangeX = 1f;
   public float idleDurationRangeY = 3f;
   
   public float legSwingAngle = 30f;
   public float legSwingSpeed = 6f;
   public float legPivotOffsetX = 0.45f;
   public float legPivotOffsetY = 0.45f;
   public float legSwingRunningAngle = 30f;
   public float legSwingRunningSpeed = 6f;
   public float idleHeaveHeight = 0.05f;
   public float idleHeaveSpeed = 1.5f;
}

[Serializable]
public class LegDefinitions
{
    public GameObject leg;
    public float legSwingAngle = 30f;
    public float legSwingSpeed = 6f;
    public Vector2 legPivotOffset = new Vector2(0.45f, 0.45f);
    public float legSwingRunningAngle = 30f;
    public float legSwingRunningSpeed = 6f;
}

[Serializable]
public class BodyCollection
{
    public BodyRecord[] bodies;
}

[Serializable]
public class BodyRecord
{
    public string name;
    public string torsoSprite;
    public string leftArmSprite;
    public string rightArmSprite;
    public string legSprite;
    public string[] faceSprite;
    public string[] earSprite;
    public string[] hairSprite;
    public string[] clothingTopSprite;
    public string[] clothingBottomSprite;
    public string[] leggingSprite;
    public string[] bootSprite;
    public string[] leftSleeveSprite;
    public string[] rightSleeveSprite;
    public bool isMale;
    public float rightLegXOffset;
    public float rightLegYOffset;
    public Vector2 sortingAnchorOffset;
    public Vector2 shadowSize = new Vector2(1f, 0.35f);
    public Vector2 shadowOffset;
    public float movementSpeed = 2f;
    public float movementDurationRangeX = 1f;
    public float movementDurationRangeY = 3f;
    public float idleDurationRangeX = 1f;
    public float idleDurationRangeY = 3f;
    public float legSwingAngle = 30f;
    public float legSwingSpeed = 6f;
    public float legPivotOffsetX = 0.45f;
    public float legPivotOffsetY = 0.45f;
    public float legSwingRunningAngle = 30f;
    public float legSwingRunningSpeed = 6f;
    public float idleHeaveHeight = 0.05f;
    public float idleHeaveSpeed = 1.5f;
}

[Serializable]
public class MonsterSpawnSettings
{
    public float spawnIntervalSeconds = 30f;
    public int maximumTotalAmount = 18;
    public float minimumVillagerDistance = 30f;
    public int spawnPositionAttemptsPerInterval = 100;
}

[Serializable]
public class MonsterDataCollection
{
    public MonsterSpawnSettings spawnSettings;
    public MonsterDefinition[] monsters;
}

[Serializable]
public class MonsterDataRecordCollection
{
    public MonsterSpawnSettings spawnSettings;
    public MonsterDefinitionRecord[] monsters;
}

[Serializable]
public class MonsterDefinitionRecord
{
    public string id;
    public float spawnWeight;
    public int maximumAmount;
    public float movementSpeed = 2f;
    public Vector2 movementDurationRange = new Vector2(1f, 3f);
    public Vector2 idleDurationRange = new Vector2(1f, 3f);
    public string[] faceSprites;
    public string[] torsoSprites;
    public string[] frontLegSprites;
    public string[] backLegSprites;
    public string[] tailSprites;
    public MonsterLegDefinition[] legDefinitions;
    public Vector2 rightFrontLegOffset;
    public Vector2 rightBackLegOffset;
    public Vector2 shadowSize = new Vector2(1.4f, 0.6f);
    public Vector2 shadowOffset;
    public CombatSettings combatSettings = new CombatSettings();
}

[Serializable]
public class MonsterDefinition
{
    public string id;
    public float spawnWeight;
    public int maximumAmount;
    public float movementSpeed;
    public Vector2 movementDurationRange;
    public Vector2 idleDurationRange;
    public Sprite[] faceSprites;
    public Sprite[] torsoSprites;
    public Sprite[] frontLegSprites;
    public Sprite[] backLegSprites;
    public Sprite[] tailSprites;
    public MonsterLegDefinition[] legDefinitions;
    public Vector2 rightFrontLegOffset;
    public Vector2 rightBackLegOffset;
    public Vector2 shadowSize = new Vector2(1.4f, 0.6f);
    public Vector2 shadowOffset;
    public CombatSettings combatSettings;
}

[Serializable]
public class MonsterLegDefinition
{
    public string partName;
    public float legSwingAngle = 30f;
    public float legSwingSpeed = 6f;
    public Vector2 legPivotOffset = new Vector2(0.45f, 0.45f);
    public float legSwingRunningAngle = 30f;
    public float legSwingRunningSpeed = 6f;
}

[Serializable]
public class CharacterSettings
{
    public string[] eyeColorCodes;
    public string[] hairColorCodes;
    public string[] skinColorCodes;
    public string[] clothingTopColorCodes;
    public string[] clothingBottomColorCodes;
    public string[] leggingColorCodes;
    public string[] bootColorCodes;
}

[Serializable]
public class UISettings
{
    public Vector2 defaultCursorHotspot;
    public Vector2 roadBuildCursorHotspot;
    public bool trackVillagers = true;
    public Vector2 villagerIndicatorSize = new Vector2(24f, 24f);
    public float villagerIndicatorEdgeInset = 48f;
    public Vector2 hoverTooltipWorldOffset = new Vector2(0f, -2f);
}

[Serializable]
public class MapSettings
{
    public float gridCellSize = 2f;
    public float roadMovementSpeedMultiplier = 1.2f;
    public float roadMovementBonusRange = 1f;
    public float keyPointSelectionRadius = 9f;
    public float roadEndpointKeyPointTolerance = 2.5f;
    public float clickDragThresholdPixels = 5f;
    public Color validRoadPreviewColor = Color.green;
    public Color invalidRoadPreviewColor = Color.red;
    public float roadTileSpacing = 1f;
    public float maxRoadKeyPointDistance = 10f;
    public float roadEndpointKeyPointClearance = 9f;
    public int manaCostPerKeyPoint = 1;
}

[Serializable]
public class PlayerSettings
{
    public int initialVillagerCount = 8;
    public float manaGainIntervalSeconds = 5f;
    public int manaGainAmount = 1;
    public ResourceInitialAmount[] initialResourceAmounts;
}

[Serializable]
public class ResourceCost
{
    public ResourceType resourceType;
    public int amount;
}

[Serializable]
public class ArmMotionPoint
{
    public float x;
    public float y;
    public float z; // Local rotation offset in degrees.
}

[Serializable]
public class HouseSettings
{
    public int storageCapacity = 50;
    public Vector2Int defaultHouseDimensions = new Vector2Int(3, 3);
    public ResourceCost[] baseHouseCosts =
    {
        new ResourceCost { resourceType = ResourceType.Wood, amount = 10 }
    };
    public float firstConstructionDurationSeconds = 15f;
    public float constructionBreakDurationSeconds = 4f;
    public float secondConstructionDurationSeconds = 15f;
    public float hammerMotionDurationSeconds = 0.35f;
    public float hammerPauseDurationSeconds = 1.65f;
    public ArmMotionPoint[] hammerHitPositions =
    {
        new ArmMotionPoint { x = 0.15f, y = 0.2f, z = 0f },
        new ArmMotionPoint { x = 0.3f, y = 0.05f, z = 0f }
    };
}

[Serializable]
public class HouseSettingsJson
{
    public int storageCapacity;
    public Vector2Int defaultHouseDimensions;
    public ResourceCostJson[] baseHouseCosts;
    public float firstConstructionDurationSeconds;
    public float constructionBreakDurationSeconds;
    public float secondConstructionDurationSeconds;
    public float hammerMotionDurationSeconds;
    public float hammerPauseDurationSeconds;
    public ArmMotionPoint[] hammerHitPositions;
}

[Serializable]
public class ResourceCostJson
{
    public string resourceType;
    public int amount;
}

[Serializable]
public class GameSettingsJsonCollection
{
    public UISettings uiSettings;
    public MapSettings mapSettings;
    public PlayerSettingsJson playerSettings;
    public HouseSettingsJson houseSettings;
    public CommunityStorageSettingsJson communityStorageSettings;
    public VillagerSettings villagerSettings;
    public ResourceSettings resourceSettings;
    public CombatSettings combatSettings;
}

[Serializable]
public class CommunityStorageSettingsJson
{
    public Vector2Int dimensions = new Vector2Int(7, 7);
    public int maximumAmountPerStorage = 100;
    public int unitsPerAdditionalIcon = 10;
}

[Serializable]
public class CommunityStorageSettings
{
    public Vector2Int dimensions = new Vector2Int(7, 7);
    public int maximumAmountPerStorage = 100;
    public int unitsPerAdditionalIcon = 10;
}

[Serializable]
public class PlayerSettingsJson
{
    public int initialVillagerCount = 8;
    public float manaGainIntervalSeconds = 5f;
    public int manaGainAmount = 1;
    public ResourceInitialAmountRecord[] initialResourceAmounts;
}

[Serializable]
public class ScaleModifierRange
{
    public Vector2 minimum = new Vector2(0.9f, 0.9f);
    public Vector2 maximum = new Vector2(1.1f, 1.1f);
}

[Serializable]
public class VillagerScaleSettings
{
    public ScaleModifierRange male = new ScaleModifierRange();
    public ScaleModifierRange female = new ScaleModifierRange();
}

[Serializable]
public class VillagerSettings
{
    public VillagerScaleSettings villagerScaleSettings = new VillagerScaleSettings();
    public float communityStorageOperationPriority = 20f;
    public float communityStoragePriorityPerItem = 1f;
    public float communityStorageSearchRetrySeconds = 5f;
    public float idleBehaviorDuration = 30f;
    public float roadDetectionDistance = 2f;
    public float roadSearchDistance = 35f;
    public float runningSpeed = 4f;
    public Vector2 reactionTimeRange = new Vector2(0.5f, 1.3f);
    public float hungerIncreasePerSecond = 1f;
    public float fatigueIncreasePerSecond = 1f;
    public float fatigueRecoveryPerSecond = 2f;
    public float homelessIdleFatigueRecoveryPerSecond = 0.25f;
    public float homeRestStartFatigueThreshold = 70f;
    public float homeRestFatigueThreshold = 40f;
    public float lazyHomeRestFatigueThreshold = 10f;
    public float productiveFatigueThreshold = 75f;
    public float idleHomeStayProbability = 0.5f;
    public float lazyIdleHomeStayProbability = 0.8f;
    public float emergencyFoodHungerThreshold = 70f;
    public float resumeTaskHungerThreshold = 50f;
    public float boredFoodSeekingHungerThreshold = 40f;
    public float hungerRestoredPerFoodUnit = 60f;
    public float housingOperationPriority = 100f;
    public float lazyHousingPriorityPenalty = 40f;
    public float foodOperationPriority = 0f;
    public float hungerFoodPriorityMultiplier = 2f;
    public float emergencyFoodPriorityBonus = 1000f;
    public float productiveOperationPriority = 80f;
    public float helpfulConstructionOperationPriority = 150f;
    public float foodResourceSearchRetrySeconds = 1f;
    public int housingSearchAttemptsBeforePause = 3;
    public float housingSearchRetrySeconds = 3f;
    public float gatheringDistance = 1.2f;
    public float gatheringPauseDurationSeconds = 1.65f;
    public float handMotionDurationSeconds = 0.35f;
    public ArmMotionPoint[] handHitPositions =
    {
        new ArmMotionPoint { x = 0.15f, y = 0.2f, z = 0f },
        new ArmMotionPoint { x = 0.3f, y = 0.05f, z = 0f }
    };
}

[Serializable]
public class ResourceSettings
{
    public float shakeDurationSeconds = 0.3f;
    public float shakeMagnitude = 0.08f;
    public float shakeFrequency = 24f;
}

[Serializable]
public class CombatSettings
{
    public float maximumHealth = 100f;
    public float attackDamage = 10f;
    public float attackRange = 1.5f;
    public float attackWindupSeconds = 0.35f;
    public float attackRecoverySeconds = 0.65f;
    public float runningSpeed = 4f;
    public float monsterReactionTimeSeconds = 0.8f;
    public float detectionIntervalReactionTimeMultiplier = 1f;
    public int detectionRangeCells = 6;
    public ArmMotionPoint[] attackHitPositions =
    {
        new ArmMotionPoint { x = 0.25f, y = 0.15f, z = 0f },
        new ArmMotionPoint { x = 0.45f, y = 0f, z = 0f }
    };
}

[Serializable]
public class GameSettingsCollection
{
    public UISettings uiSettings = new UISettings();
    public MapSettings mapSettings = new MapSettings();
    public PlayerSettings playerSettings = new PlayerSettings();
    public HouseSettings houseSettings = new HouseSettings();
    public CommunityStorageSettings communityStorageSettings = new CommunityStorageSettings();
    public VillagerSettings villagerSettings = new VillagerSettings();
    public ResourceSettings resourceSettings = new ResourceSettings();
    public CombatSettings combatSettings = new CombatSettings();
}

public enum VillagerItemType
{
    Axe
}

public enum VillagerWeaponType
{
    None,
    Axe,
    Pickaxe,
    Sword,
    SwordAndShield,
    Spear,
    SpearAndShield,
    Bow,
    Staff,
}

[Serializable]
public class VillagerInventoryEntry
{
    public VillagerItemType itemType;
    public int amount;
}

[Serializable]
public class VillagerResourceEntry
{
    public ResourceType resourceType;
    public int amount;
}

public enum VillagerHousingState
{
    FindingRoad,
    ClaimingHousePlot,
    GatheringHouseResources,
    DeliveringHouseResources,
    BuildingHouseStageOne,
    HouseConstructionBreak,
    BuildingHouseStageTwo,
    Housed
}

[Serializable]
public class HousePlotRecord
{
    public Vector2Int originCell;
    public Vector2Int dimensions;
    public Vector2Int entranceCell;
    public Vector3 center;
    public ResourceCost[] storedMaterials;
}

[Serializable]
public class CommunityStorageRecord
{
    public Vector2Int originCell;
    public Vector2Int dimensions;
    public Vector2Int entranceCell;
}

[Serializable]
public class HouseDefinitionRecord
{
    public string id;
    public float visualScale = 1f;
    public Vector2 visualOffset;
    public Vector2 sortingAnchorOffset;
    public string[] yardSprites;
    public string[] yardColorCodes;
    public string[] baseSprites;
    public string[] lowRoofSprites;
    public string[] highRoofSprites;
    public string[] secondLevelSprites;
    public string[] secondLevelExtensionSprites;
    public string[] extensionRoofSprites;
    public string[] annexSprites;
    public string[] annexRoofSprites;
    public string[] wallColorCodes;
    public string[] roofColorCodes;
}

[Serializable]
public class HouseDefinitionCollection
{
    public string buildStage1SpriteSheet;
    public string[] buildStage1ColorCodes;
    public string buildStage2SpriteSheet;
    public string[] buildStage2ColorCodes;
    public HouseDefinitionRecord[] houseDefinitions;
}

[Serializable]
public class HouseDefinition
{
    public string id;
    public float visualScale;
    public Vector2 visualOffset;
    public Vector2 sortingAnchorOffset;
    public Sprite[] yardSprites;
    public Color[] yardColors;
    public Sprite[] baseSprites;
    public Sprite[] lowRoofSprites;
    public Sprite[] highRoofSprites;
    public Sprite[] secondLevelSprites;
    public Sprite[] secondLevelExtensionSprites;
    public Sprite[] extensionRoofSprites;
    public Sprite[] annexSprites;
    public Sprite[] annexRoofSprites;
    public Color[] wallColors;
    public Color[] roofColors;
}

// AI

public enum NamePartType
{
    FullName,
    FirstPart,
    LastPart,
}
[Serializable]
public class NamePart
{
    public NamePartType type;
    public string value;
    public bool worksForMen;
    public bool worksForWomen;
}

[Serializable]
public class NamePartJson
{
    public string type;
    public string value;
    public bool worksForMen;
    public bool worksForWomen;
}

[Serializable]
public class NamePartJsonCollection
{
    public NamePartJson[] nameParts;
}

public enum JobType
{
    None,
    Forager,
    Farmer,
    Miner,
    Lumberjack,
    Hunter,
    Blacksmith,
    Tailor,
    Alchemist,
    Soldier,
    Mage
}

public enum JobWorkType
{
    FieldGathering,
    Production,
    Combat
}

[Serializable]
public class JobDefinitionJsonCollection
{
    public JobDefinitionJson[] jobs;
}

[Serializable]
public class JobDefinitionJson
{
    public string jobType;
    public string workType;
    public bool needsAnnex;
    public bool gatherAnyAvailableResource;
    public string[] resourceTypes;
    public int harvestAmount;
    public float gatheringDistance;
    public float gatheringPauseDurationSeconds;
    public float resourceSearchRetrySeconds;
    public float handMotionDurationSeconds;
    public ArmMotionPoint[] handHitPositions;
    public ResourceCostJson[] inputCosts;
    public string outputItemType;
    public int outputAmount;
    public float productionDurationSeconds;
}

[Serializable]
public class JobDefinition
{
    public JobType jobType;
    public JobWorkType workType;
    public bool needsAnnex;
    public bool gatherAnyAvailableResource;
    public ResourceType[] resourceTypes;
    public int harvestAmount;
    public float gatheringDistance;
    public float gatheringPauseDurationSeconds;
    public float resourceSearchRetrySeconds;
    public float handMotionDurationSeconds;
    public ArmMotionPoint[] handHitPositions;
    public ResourceCost[] inputCosts;
    public VillagerItemType outputItemType;
    public int outputAmount;
    public float productionDurationSeconds;
}

[Serializable]
public class RenovationDefinitionJsonCollection
{
    public ResourceCostJson[] annexRenovationCosts;
}


public enum VillagerBehaviorMode
{
    Idle,
    Wander,
    Work,
    Eat,
    Sleeps,
    EndingTask,
    Alert,
    Fighting,
    Fleeing,
    Dying,
    Building,
    LookingForFood,
    Foraging,
    LoadingToStorage
}

[Serializable]
public class VillagerBehaviorStage
{
    public float duration;
    public VillagerBehaviorMode mode;
    public bool isPrimary;
}

public enum VillagerPersonalityType
{
    Angry, //Stops whatever they are doing when there is a chance to go for a fight
    Coward, //Runs from fights
    Explorer, //More likely to go hunting/foraging
    Lazy, //Takes longer to start new actions/routines
    Helpful, //helps other villagers build their homes and upgrades
    Impatient, //Gathers resource faster, and moves slightly faster, but has less health in combat
    Perfectionist, //Building and crafting takes longer, but produces more output
    Polyamorous, //Does not need to marry another villager in order to reproduce with them
    Loner, //Less likely to get married
}

[Serializable]
public class VillagerPersonalityValue
{
    public VillagerPersonalityType type;
    public int chance; //percentage
    public VillagerPersonalityType[] incompatibleWith;
    public string description;
}

[Serializable]
public class VillagerPersonalityValueJson
{
    public string type;
    public int chance;
    public string[] incompatibleWith;
    public string description;
}

[Serializable]
public class VillagerPersonalityValueJsonCollection
{
    public VillagerPersonalityValueJson[] villagerPersonalityValues;
}

[Serializable]
public class Person
{
    public string name;
    public string familyName;
    public int age;
    public bool isMale;
    public float reactionTime;
    public VillagerPersonalityType[] personalityTypes;

    public string skinColor;
    public string hairColor;
    public string eyeColor;
    public string clothingTopColor;
    public string clothingBottomColor;
    public string leggingColor;
    public string bootColor;

    public string hairAssetName;
    public string faceAssetName;
    public string earAssetName;
    public string baseClothingTopAssetName;
    public string baseClothingBottomAssetName;
    public string baseLeggingAssetName;
    public string baseBootAssetName;
    public string baseSleeveAssetName;
}

[Serializable]
public class VillagerRecord
{
    public Person person;

    public Villager Villager { get; private set; }

    public VillagerRecord(Person person, Villager villager)
    {
        this.person = person;
        Villager = villager;
    }
}




/////////

public enum ResourceType
{
    Gold,
    Food,
    Mana,
    Wood,
    Stone,
    Iron,
    Leather,
    Cloth,
    Gems,
    Herbs,
    // Add more resources as needed

}

[Serializable]
public class ResourceRecord
{
    public ResourceType type;
    public int amount;
}

[Serializable]
public class ResourceDefinition
{
    public ResourceType type;
    public int iconIndex;
}

[Serializable]
public class ResourceDefinitionJson
{
    public string type;
    public int iconIndex;
}

[Serializable]
public class ResourceDefinitionJsonCollection
{
    public ResourceDefinitionJson[] resourceDefinitions;
}

[Serializable]
public class ResourceObjectDataCollection
{
    public ResourceObjectDefinitionRecord[] resourceObjects;
}

[Serializable]
public class ResourceInitialAmountRecord
{
    public string type;
    public int initialAmount;
}

[Serializable]
public class ResourceObjectData
{
    public ResourceObjectDefinition[] resourceObjects;
}

[Serializable]
public class ResourceInitialAmount
{
    public ResourceType type;
    public int initialAmount;
}

[Serializable]
public class ResourceOutputRecord
{
    public string type;
    public int fullCutAmount;
    public int bareHandAmount;
    public Vector2Int interactionOffset;
    public float resourceRegenerationPerSecond;
    public int resourceRegenerationLimit;
}

[Serializable]
public class ResourceOutputDefinition
{
    public ResourceType type;
    public int fullCutAmount;
    public int bareHandAmount;
    public Vector2Int interactionOffset;
    public float resourceRegenerationPerSecond;
    public int resourceRegenerationLimit;
}

[Serializable]
public class ResourceObjectSpawnSettings
{
    public int count;
    public float edgePadding;
    public float minimumSpacing;
    public int placementAttemptsPerObject;
    public bool randomizeRotation;
    public Vector2 visualOffset;
    public Vector2 scaleRange = Vector2.one;
    public int randomSeed;
}

[Serializable]
public class ResourceObjectDefinitionRecord
{
    public string id;
    public ResourceObjectSpawnSettings spawnSettings;
    public Vector2 sortingAnchorOffset;
    public Vector2 shadowSize = new Vector2(1f, 0.5f);
    public Vector2 shadowOffset;
    public ResourceOutputRecord[] outputs;
}

[Serializable]
public class ResourceObjectDefinition
{
    public string id;
    public ResourceObjectSpawnSettings spawnSettings;
    public Vector2 sortingAnchorOffset;
    public Vector2 shadowSize = new Vector2(1f, 0.5f);
    public Vector2 shadowOffset;
    public ResourceOutputDefinition[] outputs;
}

[Serializable]
public class ResourceObjectPrefabReference
{
    public string id;
    public GameObject prefab;
}
