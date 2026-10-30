using UnityEngine;
using UnityEngine.EventSystems;

public class VillagerHoverTooltipController : MonoBehaviour
{
    private VillagerHoverTooltip tooltipPrefab;
    private Canvas tooltipCanvas;
    private Vector2 worldOffset;
    private VillagerHoverTooltip activeTooltip;
    private bool loggedMissingCamera;

    public void Initialize(VillagerHoverTooltip prefab, Canvas canvas, Vector2 worldSpaceOffset)
    {
        tooltipPrefab = prefab;
        tooltipCanvas = canvas;
        worldOffset = worldSpaceOffset;

        if (tooltipPrefab == null)
        {
            Debug.LogError("A villager hover tooltip prefab must be assigned.", this);
        }

        if (tooltipCanvas == null)
        {
            Debug.LogError(
                "Villager hover tooltips require a Canvas on the UIManager's immediate child named Canvas.",
                this);
        }
    }

    private void Update()
    {
        if (tooltipPrefab == null || tooltipCanvas == null)
        {
            HideTooltip();
            return;
        }

        UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
        if (mainCamera == null)
        {
            if (!loggedMissingCamera)
            {
                Debug.LogError("Villager hover tooltips require a camera tagged MainCamera.", this);
                loggedMissingCamera = true;
            }

            HideTooltip();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            HideTooltip();
            return;
        }

        Ray pointerRay = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane villagerPlane = new Plane(Vector3.forward, Vector3.zero);
        if (!villagerPlane.Raycast(pointerRay, out float rayDistance))
        {
            HideTooltip();
            return;
        }

        Vector3 worldPosition = pointerRay.GetPoint(rayDistance);
        Collider2D collider = Physics2D.OverlapPoint(worldPosition);
        Villager villager = collider != null ? collider.GetComponentInParent<Villager>() : null;
        if (villager == null || !villager.isActiveAndEnabled || villager.HoverAnchor == null)
        {
            HideTooltip();
            return;
        }

        if (!EnsureTooltip())
        {
            return;
        }

        activeTooltip.SetVillager(villager);

        Vector3 worldOffsetPosition = mainCamera.transform.right * worldOffset.x
            + mainCamera.transform.up * worldOffset.y;
        Vector3 anchorScreenPosition = mainCamera.WorldToScreenPoint(
            villager.HoverAnchor.position + worldOffsetPosition);
        if (anchorScreenPosition.z <= 0f
            || !activeTooltip.TrySetScreenPosition(
                tooltipCanvas,
                anchorScreenPosition,
                GetCanvasCamera(tooltipCanvas, mainCamera)))
        {
            HideTooltip();
            return;
        }

        if (!activeTooltip.gameObject.activeSelf)
        {
            activeTooltip.gameObject.SetActive(true);
        }
    }

    private bool EnsureTooltip()
    {
        if (activeTooltip != null)
        {
            return true;
        }

        activeTooltip = Instantiate(tooltipPrefab, tooltipCanvas.transform, false);
        if (activeTooltip == null)
        {
            Debug.LogError("Failed to instantiate the villager hover tooltip prefab.", this);
            return false;
        }

        RectTransform tooltipRect = activeTooltip.transform as RectTransform;
        if (tooltipRect == null)
        {
            Debug.LogError("The villager hover tooltip prefab root must have a RectTransform.", activeTooltip);
            Destroy(activeTooltip.gameObject);
            activeTooltip = null;
            return false;
        }

        Vector2 tooltipSize = tooltipRect.rect.size;
        if (tooltipSize.x <= 0f || tooltipSize.y <= 0f)
        {
            Debug.LogError(
                "The villager hover tooltip prefab must have a positive width and height.",
                activeTooltip);
            Destroy(activeTooltip.gameObject);
            activeTooltip = null;
            return false;
        }

        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0.5f, 0f);
        tooltipRect.sizeDelta = tooltipSize;
        tooltipRect.localRotation = Quaternion.identity;
        activeTooltip.gameObject.SetActive(false);
        return true;
    }

    private UnityEngine.Camera GetCanvasCamera(Canvas canvas, UnityEngine.Camera mainCamera)
    {
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera != null ? canvas.worldCamera : mainCamera;
    }

    private void HideTooltip()
    {
        if (activeTooltip != null && activeTooltip.gameObject.activeSelf)
        {
            activeTooltip.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        HideTooltip();
    }
}
