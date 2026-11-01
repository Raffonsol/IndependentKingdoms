using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VillagerDetailsPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private Button assignJobButton;

    private Villager villager;

    private void Awake()
    {
        if (detailsText == null)
        {
            Debug.LogError("Villager details panel requires a TextMeshPro text reference.", this);
        }

        ConfigureAssignJobButton();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            gameObject.SetActive(false);
            return;
        }

        if (villager == null || !villager.isActiveAndEnabled)
        {
            villager = null;
            gameObject.SetActive(false);
            return;
        }

        if (villager != null)
        {
            RefreshDetails();
        }
    }

    public void SetVillager(Villager selectedVillager)
    {
        if (selectedVillager == null)
        {
            Debug.LogError("Cannot display details for a null villager.", this);
            return;
        }

        villager = selectedVillager;
        RefreshDetails();
    }

    private void ConfigureAssignJobButton()
    {
        if (assignJobButton == null)
        {
            GameObject buttonObject = new GameObject(
                "AssignJobButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            RectTransform buttonTransform = buttonObject.GetComponent<RectTransform>();
            buttonTransform.SetParent(transform, false);
            buttonTransform.anchorMin = new Vector2(0.5f, 0f);
            buttonTransform.anchorMax = new Vector2(0.5f, 0f);
            buttonTransform.anchoredPosition = new Vector2(0f, 24f);
            buttonTransform.sizeDelta = new Vector2(160f, 42f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.22f, 0.39f, 0.58f, 1f);
            assignJobButton = buttonObject.GetComponent<Button>();
            assignJobButton.targetGraphic = buttonImage;

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            RectTransform labelTransform = labelObject.GetComponent<RectTransform>();
            labelTransform.SetParent(buttonTransform, false);
            labelTransform.anchorMin = Vector2.zero;
            labelTransform.anchorMax = Vector2.one;
            labelTransform.sizeDelta = Vector2.zero;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.text = "Assign Job";

            if (detailsText != null)
            {
                RectTransform detailsTransform = detailsText.rectTransform;
                detailsTransform.sizeDelta = new Vector2(
                    detailsTransform.sizeDelta.x,
                    detailsTransform.sizeDelta.y - 68f);
                detailsTransform.anchoredPosition += new Vector2(0f, 34f);
            }
        }

        assignJobButton.onClick.AddListener(OpenJobAssignment);
    }

    private void OpenJobAssignment()
    {
        if (villager == null || !villager.isActiveAndEnabled)
        {
            return;
        }

        if (UIManager.Instance == null)
        {
            Debug.LogError("Cannot assign a job because no UIManager is active.", this);
            return;
        }

        UIManager.Instance.ShowVillagerJobAssignment(villager);
    }

    private void RefreshDetails()
    {
        if (detailsText == null)
        {
            return;
        }

        Person person = villager.Person;
        string fullName = person != null
            ? (person.name ?? string.Empty) + " " + (person.familyName ?? string.Empty)
            : string.Empty;
        StringBuilder text = new StringBuilder();
        text.AppendLine(string.IsNullOrWhiteSpace(fullName) ? "Unknown" : fullName.Trim());
        text.AppendLine();
        text.Append("Health: ").Append(villager.Health.ToString("0.#"))
            .Append(" / ").AppendLine(villager.MaximumHealth.ToString("0.#"));
        text.Append("Hunger: ").Append(villager.Hunger.ToString("0.#"))
            .Append(" / ").AppendLine(Villager.MaximumHunger.ToString("0"));
        text.Append("Fatigue: ").Append(villager.Fatigue.ToString("0.#"))
            .Append(" / ").AppendLine(Villager.MaximumFatigue.ToString("0"));
        text.Append("Job: ").AppendLine(villager.JobAssignment.ToString());
        text.AppendLine("Combat");
        text.Append("Damage: ").AppendLine(villager.AttackDamage.ToString("0.#"));
        text.Append("Range: ").AppendLine(villager.AttackRange.ToString("0.##"));
        text.Append("Attack windup: ").Append(villager.AttackWindupSeconds.ToString("0.##")).AppendLine("s");
        text.Append("Attack recovery: ").Append(villager.AttackRecoverySeconds.ToString("0.##")).AppendLine("s");
        text.Append("Weapon: ").AppendLine(villager.EquippedWeapon.ToString());
        text.AppendLine();
        text.AppendLine(FormatPersonality(person));
        text.AppendLine();
        text.AppendLine(FormatInventory(villager));
        text.AppendLine();
        if (villager.TryGetHomeCoordinates(out Vector2Int homeCoordinates))
        {
            text.AppendLine("Housing: Housed");
            text.Append("Home cell: (").Append(homeCoordinates.x).Append(", ")
                .Append(homeCoordinates.y).AppendLine(")");
        }
        else
        {
            text.AppendLine("Housing: Homeless");
        }

        detailsText.text = text.ToString();
    }

    private static string FormatPersonality(Person person)
    {
        StringBuilder text = new StringBuilder("Personality:");
        if (person == null || person.personalityTypes == null || person.personalityTypes.Length == 0)
        {
            return text.Append(" None").ToString();
        }

        VillagerPersonalityValue[] values = GameData.Instance.villagerPersonalityValues;
        if (values == null)
        {
            Debug.LogError("Villager personality values could not be loaded from GameData.");
            return text.Append(" Unavailable").ToString();
        }

        foreach (VillagerPersonalityType personalityType in person.personalityTypes)
        {
            text.Append('\n').Append(personalityType);
            foreach (VillagerPersonalityValue value in values)
            {
                if (value != null && value.type == personalityType)
                {
                    if (!string.IsNullOrWhiteSpace(value.description))
                    {
                        text.Append(" - ").Append(value.description);
                    }

                    break;
                }
            }
        }

        return text.ToString();
    }

    private static string FormatInventory(Villager selectedVillager)
    {
        StringBuilder text = new StringBuilder("Inventory:");
        bool hasItems = false;
        foreach (VillagerInventoryEntry entry in selectedVillager.Inventory)
        {
            text.Append("\n").Append(entry.itemType).Append(" x").Append(entry.amount);
            if (selectedVillager.EquippedWeapon == VillagerWeaponType.Axe
                && entry.itemType == VillagerItemType.Axe)
            {
                text.Append(" (equipped)");
            }

            hasItems = true;
        }

        foreach (VillagerResourceEntry entry in selectedVillager.ResourceInventory)
        {
            text.Append("\n").Append(entry.resourceType).Append(" x").Append(entry.amount);
            hasItems = true;
        }

        if (!hasItems)
        {
            text.Append("\nNone");
        }

        return text.ToString();
    }
}
