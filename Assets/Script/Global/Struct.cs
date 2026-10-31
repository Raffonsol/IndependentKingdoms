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

   public float movementSpeed = 2f;
   public float movementDurationRangeX = 1f;
   public float movementDurationRangeY = 3f;
   public float idleDurationRangeX = 1f;
   public float idleDurationRangeY = 3f;
   
   public float legSwingAngle = 30f;
   public float legSwingSpeed = 6f;
   public float legPivotOffsetX = 0.45f;
   public float legPivotOffsetY = 0.45f;
   public float idleHeaveHeight = 0.05f;
   public float idleHeaveSpeed = 1.5f;
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
    public float movementSpeed = 2f;
    public float movementDurationRangeX = 1f;
    public float movementDurationRangeY = 3f;
    public float idleDurationRangeX = 1f;
    public float idleDurationRangeY = 3f;
    public float legSwingAngle = 30f;
    public float legSwingSpeed = 6f;
    public float legPivotOffsetX = 0.45f;
    public float legPivotOffsetY = 0.45f;
    public float idleHeaveHeight = 0.05f;
    public float idleHeaveSpeed = 1.5f;
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
    public Vector2 villagerHoverTooltipWorldOffset = new Vector2(0f, -2f);
}

[Serializable]
public class MapSettings
{
    public float gridCellSize = 2f;
    public float keyPointSelectionRadius = 9f;
    public float roadEndpointKeyPointTolerance = 2.5f;
    public float clickDragThresholdPixels = 5f;
    public Color validRoadPreviewColor = Color.green;
    public Color invalidRoadPreviewColor = Color.red;
    public float roadTileSpacing = 1f;
    public float maxRoadKeyPointDistance = 10f;
    public float roadEndpointKeyPointClearance = 9f;
}

[Serializable]
public class PlayerSettings
{
    public int initialVillagerCount = 8;
    public float manaGainIntervalSeconds = 5f;
    public int manaGainAmount = 1;
}

[Serializable]
public class ResourceCost
{
    public ResourceType resourceType;
    public int amount;
}

[Serializable]
public class HouseSettings
{
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
    public Vector2[] hammerHitPositions =
    {
        new Vector2(0.15f, 0.2f),
        new Vector2(0.3f, 0.05f)
    };
}

[Serializable]
public class VillagerSettings
{
    public float idleBehaviorDuration = 30f;
    public float roadDetectionDistance = 2f;
    public float roadSearchDistance = 35f;
    public float runningSpeed = 4f;
    public Vector2 reactionTimeRange = new Vector2(0.5f, 1.3f);
    public float gatheringDistance = 1.2f;
    public float gatheringPauseDurationSeconds = 1.65f;
    public float handMotionDurationSeconds = 0.35f;
    public Vector2[] handHitPositions =
    {
        new Vector2(0.15f, 0.2f),
        new Vector2(0.3f, 0.05f)
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
public class GameSettingsCollection
{
    public UISettings uiSettings = new UISettings();
    public MapSettings mapSettings = new MapSettings();
    public PlayerSettings playerSettings = new PlayerSettings();
    public HouseSettings houseSettings = new HouseSettings();
    public VillagerSettings villagerSettings = new VillagerSettings();
    public ResourceSettings resourceSettings = new ResourceSettings();
}

public enum VillagerItemType
{
    Axe
}

public enum VillagerWeaponType
{
    None,
    Axe
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
    public Vector3 center;
    public ResourceCost[] storedMaterials;
}

[Serializable]
public class HouseDefinitionRecord
{
    public string id;
    public float visualScale = 1f;
    public Vector2 visualOffset;
    public string[] baseSprites;
    public string[] baseColorCodes;
    public string[] lowRoofSprites;
    public string[] lowRoofColorCodes;
    public string[] highRoofSprites;
    public string[] highRoofColorCodes;
    public string[] secondLevelSprites;
    public string[] secondLevelColorCodes;
    public string[] secondLevelExtensionSprites;
    public string[] secondLevelExtensionColorCodes;
    public string[] extensionRoofSprites;
    public string[] extensionRoofColorCodes;
    public string[] annexSprites;
    public string[] annexColorCodes;
    public string[] annexRoofSprites;
    public string[] annexRoofColorCodes;
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
    public Sprite[] baseSprites;
    public Color[] baseColors;
    public Sprite[] lowRoofSprites;
    public Color[] lowRoofColors;
    public Sprite[] highRoofSprites;
    public Color[] highRoofColors;
    public Sprite[] secondLevelSprites;
    public Color[] secondLevelColors;
    public Sprite[] secondLevelExtensionSprites;
    public Color[] secondLevelExtensionColors;
    public Sprite[] extensionRoofSprites;
    public Color[] extensionRoofColors;
    public Sprite[] annexSprites;
    public Color[] annexColors;
    public Sprite[] annexRoofSprites;
    public Color[] annexRoofColors;
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
public class NamePartCollection
{
    public NamePart[] nameParts;
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
    Building
}

[Serializable]
public class VillagerBehaviorStage
{
    public float duration;
    public VillagerBehaviorMode mode;
    public bool isPrimary;
}

[Serializable]
public class Person
{
    public string name;
    public string familyName;
    public int age;
    public bool isMale;
    public float reactionTime;

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
    public int initialAmount;
    public int fullCutAmount;
    public int bareHandAmount;
    public Vector2Int interactionOffset;
}

[Serializable]
public class ResourceDefinitionCollection
{
    public ResourceDefinition[] resourceDefinitions;
}
