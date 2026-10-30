using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButton : MonoBehaviour
{
    [SerializeField] private string tooltipText;
    [SerializeField] private UIButtonAction buttonAction;
    [SerializeField] private int id; // To identify which building to build, or which action, etc.

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (UIManager.Instance == null)
        {
            Debug.LogError("Cannot process the button action because no UIManager is active.", this);
            return;
        }

        if (buttonAction == UIButtonAction.TogglePanel)
        {
            UIManager.Instance.TogglePanel(id);
        }
        else if (buttonAction == UIButtonAction.BuildRoad)
        {
            UIManager.Instance.ToggleRoadBuilding();
        }
    }
}
