
using System;
using System.Collections.Generic;
using UnityEngine;

public class GameOverlord : MonoBehaviour
{
    private static GameOverlord instance;

    public static GameOverlord Instance
    {
        get { return instance; }
    }

    [NonSerialized] public Body[] bodies;
    [NonSerialized] public CharacterSettings characterSettings;

    [SerializeField] private GameObject baseBodyPrefab;
    [SerializeField] private GameObject roadTilePrefab;

    public GameObject RoadTilePrefab
    {
        get { return roadTilePrefab; }
    }

    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Only one GameOverlord can be active at a time. There can only be one!", this);
            enabled = false;
            return;
        }

        instance = this;
        LoadGameData();
    }

    private void Start()
    {
        InstantiateInitialVillagers();
    }

    private void LoadGameData()
    {
        GameData gameData = GameData.Instance;
        bodies = gameData.bodies;

        if (bodies == null)
        {
            Debug.LogError("Game data has no bodies to load. What is happening??", this);
            return;
        }

        if (baseBodyPrefab == null)
        {
            Debug.LogError("A base body prefab must be assigned to load game data. Like obviously.", this);
            return;
        }

        characterSettings = gameData.characterSettings;

        if (characterSettings == null)
        {
            Debug.LogError("Game data has no character settings to load. So how is it supposed to load characters?", this);
            return;
        }

        if (baseBodyPrefab.GetComponent<Villager>() == null)
        {
            Debug.LogError("The base body prefab must have a Villager component.", this);
            return;
        }
    }

    private void InstantiateInitialVillagers()
    {
        if (bodies == null
            || characterSettings == null
            || baseBodyPrefab == null
            || baseBodyPrefab.GetComponent<Villager>() == null)
        {
            return;
        }

        PlayerSettings playerSettings = GameData.Instance.playerSettings;
        if (playerSettings == null)
        {
            Debug.LogError("Player settings could not be loaded from GameData.", this);
            return;
        }

        int villagerCount = playerSettings.initialVillagerCount;
        if (villagerCount == 0)
        {
            return;
        }

        Player player = Player.Instance;
        if (player == null)
        {
            Debug.LogError("A Player must be active before initial villagers can be instantiated.", this);
            return;
        }

        for (int index = 0; index < villagerCount; index++)
        {
            Body body = SelectBodyForInitialVillager(player);
            if (body == null)
            {
                Debug.LogError("No body is available for creating an initial villager.", this);
                return;
            }

            VillagerRecord villagerRecord = InstantiateVillager(body);
            if (villagerRecord != null)
            {
                player.RegisterVillager(villagerRecord.person, villagerRecord.Villager);
            }
        }
    }

    private Body SelectBodyForInitialVillager(Player player)
    {
        int maleCount = 0;
        int femaleCount = 0;
        foreach (VillagerRecord villager in player.Villagers)
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

        // When one gender is already more numerous, restrict the next selection to the less numerous
        // gender so each new villager balances the existing roster instead of widening the gap.
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

    private VillagerRecord InstantiateVillager(Body body)
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
        Sprite leftSleeveSprite = SelectRandomSprite(body.leftSleeveSprite);
        Sprite rightSleeveSprite;
        if (clothingTopSprite == null || clothingTopSprite.name.StartsWith("none", StringComparison.Ordinal))
        {
            leftSleeveSprite = FindNoneSleeveSprite(body.leftSleeveSprite, body.name, "leftSleeveSprite");
            rightSleeveSprite = FindNoneSleeveSprite(body.rightSleeveSprite, body.name, "rightSleeveSprite");
        }
        else
        {
            rightSleeveSprite = SelectMatchingRightSleeveSprite(leftSleeveSprite, body.rightSleeveSprite, body.name);
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

        bodyTransform.SetParent(GameObject.Find("NPC")?.transform, true);

        if (rightLeg != null)
        {
            rightLeg.transform.localPosition = new Vector3(
                body.rightLegXOffset,
                body.rightLegYOffset,
                0f);
        }

        Villager villager = bodyObject.GetComponent<Villager>();
        if (villager == null)
        {
            Debug.LogError("The instantiated body prefab is missing its Villager component.", bodyObject);
            Destroy(bodyObject);
            return null;
        }

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

    private GameObject SetPartSprite(Transform bodyTransform, string partName, Sprite sprite, string colorCode = null)
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
        ApplySpriteColor(spriteRenderer, partName, colorCode);

        return partTransform.gameObject;
    }

    private string SelectRandomColorCode(string[] colorCodes)
    {
        if (colorCodes == null || colorCodes.Length == 0)
        {
            return null;
        }

        return colorCodes[UnityEngine.Random.Range(0, colorCodes.Length)];
    }

    private void ApplySpriteColor(SpriteRenderer spriteRenderer, string partName, string colorCode)
    {
        if (string.IsNullOrEmpty(colorCode))
        {
            return;
        }

        if (!ColorUtility.TryParseHtmlString(colorCode, out Color color))
        {
            Debug.LogError("The color code '" + colorCode + "' for '" + partName + "' is not a valid HTML color, now is it?", spriteRenderer);
            return;
        }

        spriteRenderer.color = color;
    }

    private Sprite SelectRandomSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return null;
        }

        return sprites[UnityEngine.Random.Range(0, sprites.Length)];
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

        Debug.LogError("No 'none' sleeve sprite is configured for body '" + bodyName + "' field '" + fieldName + "'. What are you even doing?");
        return null;
    }

    private Sprite SelectMatchingRightSleeveSprite(Sprite leftSleeveSprite, Sprite[] rightSleeveSprites, string bodyName)
    {
        if (leftSleeveSprite == null)
        {
            return null;
        }

        if (leftSleeveSprite.name.StartsWith("none", StringComparison.Ordinal))
        {
            return FindNoneSleeveSprite(rightSleeveSprites, bodyName, "rightSleeveSprite");
        }

        string rightSleeveName = leftSleeveSprite.name.Replace("leftSleeve", "rightSleeve");
        if (rightSleeveSprites != null)
        {
            foreach (Sprite sprite in rightSleeveSprites)
            {
                if (sprite != null && sprite.name == rightSleeveName)
                {
                    return sprite;
                }
            }
        }

        Debug.LogError(
            "No right sleeve matching '" + leftSleeveSprite.name + "' is configured for body '" + bodyName + "'. If this problem happens the other way around, you will never know...");
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
}
