using UnityEngine;
using UnityEngine.UI;

public class HouseProductionProgressBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private Canvas worldCanvas;
    private HouseWorkplace workplace;

    public bool Initialize(HouseWorkplace target)
    {
        if (target == null)
        {
            Debug.LogError("House production progress bar requires a HouseWorkplace.", this);
            return false;
        }

        worldCanvas = GetComponent<Canvas>();
        if (worldCanvas == null || fillImage == null)
        {
            Debug.LogError(
                "House production progress bar requires a Canvas and a fill Image.",
                this);
            return false;
        }

        workplace = target;
        if (transform.parent != null)
        {
            Vector3 desiredWorldScale = transform.localScale;
            Vector3 parentScale = transform.parent.lossyScale;
            transform.localScale = new Vector3(
                DivideScale(desiredWorldScale.x, parentScale.x),
                DivideScale(desiredWorldScale.y, parentScale.y),
                DivideScale(desiredWorldScale.z, parentScale.z));
        }

        worldCanvas.sortingLayerName = "Everything";
        worldCanvas.sortingOrder = 300;
        fillImage.raycastTarget = false;
        Refresh();
        return true;
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (worldCanvas == null || workplace == null)
        {
            return;
        }

        worldCanvas.enabled = workplace.HasProductionProgress;
        fillImage.fillAmount = workplace.ProductionProgress;
    }

    private static float DivideScale(float scale, float parentScale)
    {
        return Mathf.Abs(parentScale) > Mathf.Epsilon
            ? scale / parentScale
            : scale;
    }
}
