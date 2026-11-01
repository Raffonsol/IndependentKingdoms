using UnityEngine;
using UnityEngine.EventSystems;

public class HoverTooltipController : MonoBehaviour
{
    private VillagerHoverTooltip villagerTooltipPrefab;
    private ResourceHoverTooltip resourceTooltipPrefab;
    private Canvas tooltipCanvas;
    private Vector2 worldOffset;
    private VillagerHoverTooltip activeVillagerTooltip;
    private ResourceHoverTooltip activeResourceTooltip;
    private bool loggedMissingCamera;

    public void Initialize(
        VillagerHoverTooltip villagerPrefab,
        ResourceHoverTooltip resourcePrefab,
        Canvas canvas,
        Vector2 worldSpaceOffset)
    {
        villagerTooltipPrefab = villagerPrefab;
        resourceTooltipPrefab = resourcePrefab;
        tooltipCanvas = canvas;
        worldOffset = worldSpaceOffset;

        if (villagerTooltipPrefab == null && resourceTooltipPrefab == null)
        {
            Debug.LogError("At least one hover tooltip prefab must be assigned.", this);
        }

        if (tooltipCanvas == null)
        {
            Debug.LogError(
                "Hover tooltips require a Canvas on the UIManager's immediate child named Canvas.",
                this);
        }
    }

    private void Update()
    {
        if (tooltipCanvas == null)
        {
            HideTooltips();
            return;
        }

        UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
        if (mainCamera == null)
        {
            if (!loggedMissingCamera)
            {
                Debug.LogError("Hover tooltips require a camera tagged MainCamera.", this);
                loggedMissingCamera = true;
            }

            HideTooltips();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            HideTooltips();
            return;
        }

        Ray pointerRay = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane objectPlane = new Plane(Vector3.forward, Vector3.zero);
        if (!objectPlane.Raycast(pointerRay, out float rayDistance))
        {
            HideTooltips();
            return;
        }

        Vector3 worldPosition = pointerRay.GetPoint(rayDistance);
        Collider2D collider = Physics2D.OverlapPoint(worldPosition);
        Villager villager = collider != null ? collider.GetComponentInParent<Villager>() : null;
        if (villager != null && villager.isActiveAndEnabled && villager.HoverAnchor != null)
        {
            ShowVillagerTooltip(villager, mainCamera);
            return;
        }

        ResourceObject resourceObject = collider != null
            ? collider.GetComponentInParent<ResourceObject>()
            : null;
        if (resourceObject == null && MapManager.Instance != null)
        {
            MapManager.Instance.TryGetResourceObjectAtWorldPosition(
                worldPosition,
                out resourceObject);
        }

        if (resourceObject != null && resourceObject.isActiveAndEnabled)
        {
            ShowResourceTooltip(resourceObject, mainCamera);
            return;
        }

        HideTooltips();
    }

    private void ShowVillagerTooltip(Villager villager, UnityEngine.Camera mainCamera)
    {
        HideResourceTooltip();
        if (villagerTooltipPrefab == null || !EnsureTooltip(ref activeVillagerTooltip, villagerTooltipPrefab))
        {
            HideVillagerTooltip();
            return;
        }

        activeVillagerTooltip.SetVillager(villager);
        Vector3 anchorPosition = GetAnchorScreenPosition(mainCamera, villager.HoverAnchor.position);
        if (!TryPositionTooltip(activeVillagerTooltip, anchorPosition, mainCamera))
        {
            HideVillagerTooltip();
            return;
        }

        activeVillagerTooltip.gameObject.SetActive(true);
    }

    private void ShowResourceTooltip(ResourceObject resourceObject, UnityEngine.Camera mainCamera)
    {
        HideVillagerTooltip();
        if (resourceTooltipPrefab == null || !EnsureTooltip(ref activeResourceTooltip, resourceTooltipPrefab))
        {
            HideResourceTooltip();
            return;
        }

        activeResourceTooltip.SetResource(resourceObject);
        Vector3 anchorPosition = GetAnchorScreenPosition(mainCamera, resourceObject.transform.position);
        if (!TryPositionTooltip(activeResourceTooltip, anchorPosition, mainCamera))
        {
            HideResourceTooltip();
            return;
        }

        activeResourceTooltip.gameObject.SetActive(true);
    }

    private bool EnsureTooltip<T>(ref T activeTooltip, T tooltipPrefab)
        where T : HoverTooltip
    {
        if (activeTooltip != null)
        {
            return true;
        }

        activeTooltip = Instantiate(tooltipPrefab, tooltipCanvas.transform, false);
        if (activeTooltip == null)
        {
            Debug.LogError("Failed to instantiate a hover tooltip prefab.", this);
            return false;
        }

        RectTransform tooltipRect = activeTooltip.transform as RectTransform;
        if (tooltipRect == null)
        {
            Debug.LogError("A hover tooltip prefab root must have a RectTransform.", activeTooltip);
            Destroy(activeTooltip.gameObject);
            activeTooltip = null;
            return false;
        }

        Vector2 tooltipSize = tooltipRect.rect.size;
        if (tooltipSize.x <= 0f || tooltipSize.y <= 0f)
        {
            Debug.LogError(
                "A hover tooltip prefab must have a positive width and height.",
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

    private Vector3 GetAnchorScreenPosition(
        UnityEngine.Camera mainCamera,
        Vector3 anchorPosition)
    {
        Vector3 worldOffsetPosition = mainCamera.transform.right * worldOffset.x
            + mainCamera.transform.up * worldOffset.y;
        return mainCamera.WorldToScreenPoint(anchorPosition + worldOffsetPosition);
    }

    private bool TryPositionTooltip(
        HoverTooltip tooltip,
        Vector3 anchorScreenPosition,
        UnityEngine.Camera mainCamera)
    {
        return anchorScreenPosition.z > 0f
            && tooltip.TrySetScreenPosition(
                tooltipCanvas,
                anchorScreenPosition,
                GetCanvasCamera(tooltipCanvas, mainCamera));
    }

    private UnityEngine.Camera GetCanvasCamera(Canvas canvas, UnityEngine.Camera mainCamera)
    {
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera != null ? canvas.worldCamera : mainCamera;
    }

    private void HideTooltips()
    {
        HideVillagerTooltip();
        HideResourceTooltip();
    }

    private void HideVillagerTooltip()
    {
        if (activeVillagerTooltip != null && activeVillagerTooltip.gameObject.activeSelf)
        {
            activeVillagerTooltip.gameObject.SetActive(false);
        }
    }

    private void HideResourceTooltip()
    {
        if (activeResourceTooltip != null && activeResourceTooltip.gameObject.activeSelf)
        {
            activeResourceTooltip.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        HideTooltips();
    }
}
