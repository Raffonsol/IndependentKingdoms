using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class UITextValue : MonoBehaviour
{
    [SerializeField] private UIValueDefinition valueDefinition = new UIValueDefinition();

    private TMP_Text valueText;
    private int lastValue;
    private bool hasLastValue;

    private void Awake()
    {
        valueText = GetComponent<TMP_Text>();
        if (valueText == null)
        {
            Debug.LogError("UITextValue requires a TMP_Text component.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (valueDefinition == null || valueDefinition.type != UIValueType.Resource)
        {
            return;
        }

        Player player = Player.Instance;
        if (player == null || !player.TryGetResourceAmount(valueDefinition.resourceType, out int currentValue))
        {
            return;
        }

        if (hasLastValue && currentValue == lastValue)
        {
            return;
        }

        valueText.text = currentValue.ToString();
        lastValue = currentValue;
        hasLastValue = true;
    }

    public void SetValueDefinition(UIValueDefinition definition)
    {
        valueDefinition = definition;
        hasLastValue = false;
    }
}
