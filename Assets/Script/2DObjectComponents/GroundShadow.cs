using UnityEngine;

public class GroundShadow : MonoBehaviour
{
    private const int CircleTextureSize = 64;

    private static Sprite circleSprite;
    private SpriteRenderer shadowRenderer;

    private void Awake()
    {
        InitializeRenderer();
    }

    public bool Configure(Vector2 worldSize, Vector2 worldOffset)
    {
        if (!IsFinitePositive(worldSize.x) || !IsFinitePositive(worldSize.y))
        {
            Debug.LogError("Ground shadow dimensions must be finite and positive.", this);
            return false;
        }

        if (!IsFinite(worldOffset.x) || !IsFinite(worldOffset.y))
        {
            Debug.LogError("Ground shadow offset must be finite.", this);
            return false;
        }

        if (!InitializeRenderer())
        {
            return false;
        }

        Transform parentTransform = transform.parent;
        Vector3 parentScale = parentTransform != null
            ? parentTransform.lossyScale
            : Vector3.one;
        transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(Mathf.Abs(parentScale.x), Mathf.Epsilon),
            worldSize.y / Mathf.Max(Mathf.Abs(parentScale.y), Mathf.Epsilon),
            1f);
        Vector3 worldOffsetVector = new Vector3(worldOffset.x, worldOffset.y, 0f);
        transform.localPosition = parentTransform != null
            ? parentTransform.InverseTransformVector(worldOffsetVector)
            : worldOffsetVector;
        return true;
    }

    private bool InitializeRenderer()
    {
        if (shadowRenderer == null)
        {
            shadowRenderer = GetComponent<SpriteRenderer>();
            if (shadowRenderer == null)
            {
                shadowRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        if (circleSprite == null)
        {
            circleSprite = CreateCircleSprite();
        }

        if (circleSprite == null)
        {
            Debug.LogError("Could not create the ground shadow sprite.", this);
            return false;
        }

        shadowRenderer.sprite = circleSprite;
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.28f);
        shadowRenderer.sortingLayerName = "Ground";
        shadowRenderer.sortingOrder = 0;
        return true;
    }

    private static Sprite CreateCircleSprite()
    {
        Texture2D texture = new Texture2D(
            CircleTextureSize,
            CircleTextureSize,
            TextureFormat.RGBA32,
            false);
        texture.name = "GroundShadowCircle";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[CircleTextureSize * CircleTextureSize];
        float center = CircleTextureSize * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < CircleTextureSize; y++)
        {
            for (int x = 0; x < CircleTextureSize; x++)
            {
                float distance = Vector2.Distance(
                    new Vector2(x + 0.5f, y + 0.5f),
                    new Vector2(center, center));
                byte alpha = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(radius + 0.5f - distance) * 255f);
                pixels[y * CircleTextureSize + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, CircleTextureSize, CircleTextureSize),
            new Vector2(0.5f, 0.5f),
            CircleTextureSize,
            0,
            SpriteMeshType.FullRect);
        sprite.name = "GroundShadowCircle";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static bool IsFinitePositive(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
