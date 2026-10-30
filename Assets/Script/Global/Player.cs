
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    private static Player instance;

    public static Player Instance
    {
        get { return instance; }
    }

    public bool HasClaimedSpacePrefab
    {
        get { return claimedSpacePrefab != null; }
    }

    [NonSerialized] public ResourceRecord[] resources;
    public List<VillagerRecord> Villagers { get; private set; } = new List<VillagerRecord>();

    [SerializeField] private GameObject baseBodyPrefab;
    [SerializeField] private GameObject claimedSpacePrefab;

    private int manaResourceIndex = -1;
    private Body[] bodies;
    private CharacterSettings characterSettings;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Only one Player can be active at a time.", this);
            enabled = false;
            return;
        }

        instance = this;
        GameData gameData = GameData.Instance;
        bodies = gameData.bodies;
        characterSettings = gameData.characterSettings;
        if (!InitializeResources(gameData.resourceDefinitions))
        {
            return;
        }

        PlayerSettings settings = gameData.playerSettings;
        if (settings == null)
        {
            Debug.LogError("Player settings could not be loaded from GameData.", this);
            return;
        }

        if (settings.manaGainIntervalSeconds <= 0f || settings.manaGainAmount <= 0)
        {
            Debug.LogError("Mana gain interval and amount must both be greater than zero.", this);
            return;
        }

        if (manaResourceIndex < 0)
        {
            Debug.LogError("The ResourceType enum must include Mana for mana regeneration.", this);
            return;
        }

        StartCoroutine(RegenerateMana(settings));
    }

    private void Start()
    {
        InstantiateInitialVillagers();
    }

    public void RegisterVillager(Person person, Villager villager)
    {
        if (person == null || villager == null)
        {
            Debug.LogError("Cannot register a villager without both person data and a Villager component.", this);
            return;
        }

        Villagers.Add(new VillagerRecord(person, villager));
    }

    private bool InitializeResources(ResourceDefinition[] resourceDefinitions)
    {
        Array resourceTypes = Enum.GetValues(typeof(ResourceType));
        if (resourceDefinitions == null || resourceDefinitions.Length != resourceTypes.Length)
        {
            Debug.LogError("Resource definitions could not be loaded from GameData.", this);
            return false;
        }

        resources = new ResourceRecord[resourceTypes.Length];
        foreach (ResourceDefinition definition in resourceDefinitions)
        {
            if (definition == null || !Enum.IsDefined(typeof(ResourceType), definition.type))
            {
                Debug.LogError("GameData contains an invalid resource definition.", this);
                resources = null;
                return false;
            }

            int resourceIndex = Convert.ToInt32(definition.type);
            if (resourceIndex < 0
                || resourceIndex >= resources.Length
                || resources[resourceIndex] != null
                || definition.initialAmount < 0)
            {
                Debug.LogError("GameData contains an invalid or duplicate resource definition.", this);
                resources = null;
                return false;
            }

            resources[resourceIndex] = new ResourceRecord
            {
                type = definition.type,
                amount = definition.initialAmount
            };

            if (definition.type == ResourceType.Mana)
            {
                manaResourceIndex = resourceIndex;
            }
        }

        return true;
    }

    public bool TryGetResourceAmount(ResourceType resourceType, out int amount)
    {
        if (resources != null)
        {
            foreach (ResourceRecord resource in resources)
            {
                if (resource != null && resource.type == resourceType)
                {
                    amount = resource.amount;
                    return true;
                }
            }
        }

        amount = 0;
        return false;
    }

    public bool TryAddResourceAmount(ResourceType resourceType, int amount)
    {
        if (amount <= 0 || resources == null)
        {
            return false;
        }

        foreach (ResourceRecord resource in resources)
        {
            if (resource != null && resource.type == resourceType)
            {
                resource.amount += amount;
                return true;
            }
        }

        Debug.LogError("Cannot add resources because " + resourceType + " is not initialized.", this);
        return false;
    }

    public bool TrySpendResourceCosts(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
        {
            Debug.LogError("Cannot spend an empty or missing resource cost list.", this);
            return false;
        }

        foreach (ResourceCost cost in costs)
        {
            if (cost == null || !TryGetResourceAmount(cost.resourceType, out int amount)
                || amount < cost.amount)
            {
                return false;
            }
        }

        foreach (ResourceCost cost in costs)
        {
            foreach (ResourceRecord resource in resources)
            {
                if (resource != null && resource.type == cost.resourceType)
                {
                    resource.amount -= cost.amount;
                    break;
                }
            }
        }

        return true;
    }

    public GameObject PlaceClaimedSpace(HousePlotRecord housePlot)
    {
        if (claimedSpacePrefab == null)
        {
            Debug.LogError("A claimedSpacePrefab must be assigned on Player before villagers can claim house plots.", this);
            return null;
        }

        if (housePlot == null)
        {
            Debug.LogError("Cannot place a claimed-space prefab without a house plot.", this);
            return null;
        }

        GameObject claimedSpace = Instantiate(claimedSpacePrefab, housePlot.center, Quaternion.identity);
        LineRenderer outline = claimedSpace.GetComponent<LineRenderer>();
        if (outline != null)
        {
            float cellSize = GameData.Instance.mapSettings.gridCellSize;
            float halfWidth = housePlot.dimensions.x * cellSize * 0.5f;
            float halfHeight = housePlot.dimensions.y * cellSize * 0.5f;
            outline.useWorldSpace = false;
            outline.loop = false;
            outline.positionCount = 5;
            outline.SetPosition(0, new Vector3(-halfWidth, -halfHeight, 0f));
            outline.SetPosition(1, new Vector3(-halfWidth, halfHeight, 0f));
            outline.SetPosition(2, new Vector3(halfWidth, halfHeight, 0f));
            outline.SetPosition(3, new Vector3(halfWidth, -halfHeight, 0f));
            outline.SetPosition(4, new Vector3(-halfWidth, -halfHeight, 0f));
            outline.startColor = Color.green;
            outline.endColor = Color.green;
        }

        return claimedSpace;
    }

    private void InstantiateInitialVillagers()
    {
        PlayerSettings playerSettings = GameData.Instance.playerSettings;
        if (bodies == null || bodies.Length == 0)
        {
            Debug.LogError("GameData has no bodies available for initial villagers.", this);
            return;
        }

        if (characterSettings == null)
        {
            Debug.LogError("Character settings could not be loaded from GameData.", this);
            return;
        }

        if (baseBodyPrefab == null || baseBodyPrefab.GetComponent<Villager>() == null)
        {
            Debug.LogError("Player requires a base body prefab with a Villager component.", this);
            return;
        }

        if (playerSettings == null)
        {
            Debug.LogError("Player settings could not be loaded from GameData.", this);
            return;
        }

        for (int index = 0; index < playerSettings.initialVillagerCount; index++)
        {
            Body body = SelectBodyForInitialVillager();
            if (body == null)
            {
                Debug.LogError("No body is available for creating an initial villager.", this);
                return;
            }

            VillagerRecord villagerRecord = CreateVillager(body);
            if (villagerRecord != null)
            {
                RegisterVillager(villagerRecord.person, villagerRecord.Villager);
            }
        }
    }

    private Body SelectBodyForInitialVillager()
    {
        int maleCount = 0;
        int femaleCount = 0;
        foreach (VillagerRecord villager in Villagers)
        {
            if (villager == null || villager.person == null)
            {
                continue;
            }

            if (villager.person.isMale)
            {
                maleCount++;
            }
            else
            {
                femaleCount++;
            }
        }

        bool? preferredGender = maleCount > femaleCount
            ? (bool?)false
            : femaleCount > maleCount
                ? (bool?)true
                : null;
        List<Body> eligibleBodies = new List<Body>();
        foreach (Body body in bodies)
        {
            if (body != null && (!preferredGender.HasValue || body.isMale == preferredGender.Value))
            {
                eligibleBodies.Add(body);
            }
        }

        if (eligibleBodies.Count == 0 && preferredGender.HasValue)
        {
            Debug.LogWarning(
                "No body is available for the less numerous gender; selecting from all available bodies.",
                this);
            foreach (Body body in bodies)
            {
                if (body != null)
                {
                    eligibleBodies.Add(body);
                }
            }
        }

        return eligibleBodies.Count == 0
            ? null
            : eligibleBodies[UnityEngine.Random.Range(0, eligibleBodies.Count)];
    }

    private VillagerRecord CreateVillager(Body body)
    {
        GameObject bodyObject = Instantiate(baseBodyPrefab, transform, false);
        Transform bodyTransform = bodyObject.transform;
        string skinColorCode = SelectRandomColorCode(characterSettings.skinColorCodes);
        string hairColorCode = SelectRandomColorCode(characterSettings.hairColorCodes);
        string eyeColorCode = SelectRandomColorCode(characterSettings.eyeColorCodes);
        string clothingTopColorCode = SelectRandomColorCode(characterSettings.clothingTopColorCodes);
        string clothingBottomColorCode = SelectRandomColorCode(characterSettings.clothingBottomColorCodes);
        string leggingColorCode = SelectRandomColorCode(characterSettings.leggingColorCodes);
        string bootColorCode = SelectRandomColorCode(characterSettings.bootColorCodes);
        Sprite faceSprite = SelectRandomSprite(body.faceSprite);
        Sprite earSprite = SelectRandomSprite(body.earSprite);
        Sprite hairSprite = SelectRandomSprite(body.hairSprite);
        Sprite clothingTopSprite = SelectRandomSprite(body.clothingTopSprite);
        Sprite clothingBottomSprite = SelectRandomSprite(body.clothingBottomSprite);
        Sprite leggingSprite = SelectRandomSprite(body.leggingSprite);
        Sprite bootSprite = SelectRandomSprite(body.bootSprite);
        Sprite leftSleeveSprite;
        Sprite rightSleeveSprite;
        if (clothingTopSprite == null || clothingTopSprite.name.StartsWith("none", StringComparison.Ordinal))
        {
            leftSleeveSprite = FindNoneSleeveSprite(body.leftSleeveSprite, body.name, "leftSleeveSprite");
            rightSleeveSprite = FindNoneSleeveSprite(body.rightSleeveSprite, body.name, "rightSleeveSprite");
        }
        else
        {
            leftSleeveSprite = SelectRandomSprite(body.leftSleeveSprite);
            rightSleeveSprite = SelectMatchingRightSleeveSprite(
                leftSleeveSprite,
                body.rightSleeveSprite,
                body.name);
        }

        GameObject torso = SetPartSprite(bodyTransform, "torso", body.torsoSprite, skinColorCode);
        SetPartSprite(bodyTransform, "leftArm", body.leftArmSprite, skinColorCode);
        SetPartSprite(bodyTransform, "rightArm", body.rightArmSprite, skinColorCode);
        GameObject leftLeg = SetPartSprite(bodyTransform, "leftLeg", body.legSprite, skinColorCode);
        GameObject rightLeg = SetPartSprite(bodyTransform, "rightLeg", body.legSprite, skinColorCode);
        SetPartSprite(bodyTransform, "face", faceSprite, skinColorCode);
        SetPartSprite(bodyTransform, "ear", earSprite, skinColorCode);
        SetPartSprite(bodyTransform, "hair", hairSprite, hairColorCode);
        SetPartSprite(bodyTransform, "clothingTop", clothingTopSprite, clothingTopColorCode);
        SetPartSprite(bodyTransform, "clothingBottom", clothingBottomSprite, clothingBottomColorCode);
        SetPartSprite(bodyTransform, "leftLegging", leggingSprite, leggingColorCode);
        SetPartSprite(bodyTransform, "rightLegging", leggingSprite, leggingColorCode);
        SetPartSprite(bodyTransform, "leftBoot", bootSprite, bootColorCode);
        SetPartSprite(bodyTransform, "rightBoot", bootSprite, bootColorCode);
        SetPartSprite(bodyTransform, "leftSleeve", leftSleeveSprite, clothingTopColorCode);
        SetPartSprite(bodyTransform, "rightSleeve", rightSleeveSprite, clothingTopColorCode);

        GameObject npcContainer = GameObject.Find("NPC");
        bodyTransform.SetParent(npcContainer != null ? npcContainer.transform : transform, true);
        if (rightLeg != null)
        {
            rightLeg.transform.localPosition = new Vector3(body.rightLegXOffset, body.rightLegYOffset, 0f);
        }

        Villager villager = bodyObject.GetComponent<Villager>();
        villager.Configure(body, torso, new[] { leftLeg, rightLeg });
        Person person = new Person
        {
            isMale = body.isMale,
            skinColor = skinColorCode,
            hairColor = hairColorCode,
            eyeColor = eyeColorCode,
            clothingTopColor = clothingTopColorCode,
            clothingBottomColor = clothingBottomColorCode,
            leggingColor = leggingColorCode,
            bootColor = bootColorCode,
            hairAssetName = hairSprite != null ? hairSprite.name : null,
            faceAssetName = faceSprite != null ? faceSprite.name : null,
            earAssetName = earSprite != null ? earSprite.name : null,
            baseClothingTopAssetName = clothingTopSprite != null ? clothingTopSprite.name : null,
            baseClothingBottomAssetName = clothingBottomSprite != null ? clothingBottomSprite.name : null,
            baseLeggingAssetName = leggingSprite != null ? leggingSprite.name : null,
            baseBootAssetName = bootSprite != null ? bootSprite.name : null,
            baseSleeveAssetName = leftSleeveSprite != null ? leftSleeveSprite.name : null
        };

        Transform hoverAnchor = ConfigureVillagerHoverTarget(bodyObject.transform);
        villager.ConfigureIdentity(person, hoverAnchor);
        VillagerNameGenerator nameGenerator = bodyObject.AddComponent<VillagerNameGenerator>();
        if (!nameGenerator.TryGenerateName(person))
        {
            Destroy(bodyObject);
            return null;
        }

        return new VillagerRecord(person, villager);
    }

    private Transform ConfigureVillagerHoverTarget(Transform villagerTransform)
    {
        SpriteRenderer[] spriteRenderers = villagerTransform.GetComponentsInChildren<SpriteRenderer>();
        Bounds spriteBounds = default;
        bool hasSpriteBounds = false;
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null || !spriteRenderer.enabled)
            {
                continue;
            }

            if (!hasSpriteBounds)
            {
                spriteBounds = spriteRenderer.bounds;
                hasSpriteBounds = true;
            }
            else
            {
                spriteBounds.Encapsulate(spriteRenderer.bounds);
            }
        }

        if (!hasSpriteBounds)
        {
            Debug.LogError("Cannot create villager hover bounds because the villager has no visible sprites.", villagerTransform);
            return null;
        }

        BoxCollider2D hoverCollider = villagerTransform.GetComponent<BoxCollider2D>();
        if (hoverCollider == null)
        {
            hoverCollider = villagerTransform.gameObject.AddComponent<BoxCollider2D>();
        }

        Vector3 localMinimum = villagerTransform.InverseTransformPoint(spriteBounds.min);
        Vector3 localMaximum = villagerTransform.InverseTransformPoint(spriteBounds.max);
        hoverCollider.offset = new Vector2(
            (localMinimum.x + localMaximum.x) * 0.5f,
            (localMinimum.y + localMaximum.y) * 0.5f);
        hoverCollider.size = new Vector2(
            Mathf.Abs(localMaximum.x - localMinimum.x),
            Mathf.Abs(localMaximum.y - localMinimum.y));

        Transform hoverAnchor = villagerTransform.Find("HeadAnchor");
        if (hoverAnchor == null)
        {
            GameObject anchorObject = new GameObject("HeadAnchor");
            hoverAnchor = anchorObject.transform;
            hoverAnchor.SetParent(villagerTransform, false);
        }

        Vector3 worldHeadPosition = new Vector3(
            spriteBounds.center.x,
            spriteBounds.max.y + 0.15f,
            spriteBounds.center.z);
        hoverAnchor.localPosition = villagerTransform.InverseTransformPoint(worldHeadPosition);
        return hoverAnchor;
    }

    private GameObject SetPartSprite(Transform bodyTransform, string partName, Sprite sprite, string colorCode)
    {
        Transform partTransform = FindChild(bodyTransform, partName);
        if (partTransform == null)
        {
            Debug.LogError("The base body prefab is missing the '" + partName + "' object.", bodyTransform);
            return null;
        }

        SpriteRenderer spriteRenderer = partTransform.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("The '" + partName + "' object on the base body prefab must have a SpriteRenderer.", partTransform);
            return partTransform.gameObject;
        }

        spriteRenderer.sprite = sprite;
        if (!string.IsNullOrEmpty(colorCode))
        {
            if (ColorUtility.TryParseHtmlString(colorCode, out Color color))
            {
                spriteRenderer.color = color;
            }
            else
            {
                Debug.LogError("The color code '" + colorCode + "' for '" + partName + "' is invalid.", spriteRenderer);
            }
        }

        return partTransform.gameObject;
    }

    private string SelectRandomColorCode(string[] colorCodes)
    {
        return colorCodes == null || colorCodes.Length == 0
            ? null
            : colorCodes[UnityEngine.Random.Range(0, colorCodes.Length)];
    }

    private Sprite SelectRandomSprite(Sprite[] sprites)
    {
        return sprites == null || sprites.Length == 0
            ? null
            : sprites[UnityEngine.Random.Range(0, sprites.Length)];
    }

    private Sprite FindNoneSleeveSprite(Sprite[] sprites, string bodyName, string fieldName)
    {
        if (sprites != null)
        {
            foreach (Sprite sprite in sprites)
            {
                if (sprite != null && sprite.name.StartsWith("none", StringComparison.Ordinal))
                {
                    return sprite;
                }
            }
        }

        Debug.LogError("No 'none' sleeve sprite is configured for body '" + bodyName + "' field '" + fieldName + "'.");
        return null;
    }

    private Sprite SelectMatchingRightSleeveSprite(
        Sprite leftSleeveSprite,
        Sprite[] rightSleeveSprites,
        string bodyName)
    {
        if (leftSleeveSprite == null)
        {
            return null;
        }

        string expectedName = leftSleeveSprite.name.StartsWith("none", StringComparison.Ordinal)
            ? "none"
            : leftSleeveSprite.name.Replace("leftSleeve", "rightSleeve");
        if (rightSleeveSprites != null)
        {
            foreach (Sprite sprite in rightSleeveSprites)
            {
                if (sprite != null && sprite.name == expectedName)
                {
                    return sprite;
                }
            }
        }

        Debug.LogError("No matching right sleeve is configured for body '" + bodyName + "'.");
        return null;
    }

    private Transform FindChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform result = FindChild(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private IEnumerator RegenerateMana(PlayerSettings settings)
    {
        WaitForSeconds interval = new WaitForSeconds(settings.manaGainIntervalSeconds);
        while (true)
        {
            yield return interval;
            resources[manaResourceIndex].amount += settings.manaGainAmount;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
