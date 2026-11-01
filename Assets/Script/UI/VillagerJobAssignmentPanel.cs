using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VillagerJobAssignmentPanel : MonoBehaviour
{
    private const float FirstJobRowY = 194f;
    private const float JobRowSpacing = 36f;

    private Villager villager;
    private TMP_Text titleText;
    private bool initialized;

    private void Awake()
    {
        Initialize();
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
            gameObject.SetActive(false);
        }
    }

    public void SetVillager(Villager selectedVillager)
    {
        if (selectedVillager == null)
        {
            Debug.LogError("Cannot assign a job to a null villager.", this);
            return;
        }

        Initialize();
        villager = selectedVillager;
        Person person = villager.Person;
        string fullName = person != null
            ? ((person.name ?? string.Empty) + " " + (person.familyName ?? string.Empty)).Trim()
            : string.Empty;
        titleText.text = string.IsNullOrEmpty(fullName)
            ? "Assign Job"
            : "Assign Job: " + fullName;
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        titleText = CreateText(
            "Title",
            transform,
            new Vector2(0f, 220f),
            new Vector2(340f, 38f),
            24f,
            TextAlignmentOptions.Center);
        titleText.text = "Assign Job";

        Array jobTypes = Enum.GetValues(typeof(JobType));
        int rowIndex = 0;
        foreach (JobType jobType in jobTypes)
        {
            float rowY = FirstJobRowY - rowIndex * JobRowSpacing;
            Button jobButton = CreateButton(
                transform,
                jobType.ToString(),
                new Vector2(0f, rowY),
                new Vector2(300f, 30f),
                jobType == JobType.Forager || jobType == JobType.None);

            TMP_Text label = jobButton.GetComponentInChildren<TMP_Text>();
            label.text = jobType == JobType.None
                ? "No Job"
                : (jobType == JobType.Forager ? "Forager" : jobType + " (Coming Soon)");

            JobType selectedJob = jobType;
            jobButton.onClick.AddListener(() => SelectJob(selectedJob));
            rowIndex++;
        }

        Button closeButton = CreateButton(
            transform,
            "Close",
            new Vector2(0f, -235f),
            new Vector2(140f, 34f),
            true);
        closeButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void SelectJob(JobType jobType)
    {
        if (villager == null || !villager.isActiveAndEnabled)
        {
            Debug.LogError("Cannot assign a job because the selected villager is unavailable.", this);
            gameObject.SetActive(false);
            return;
        }

        if (!villager.AssignJob(jobType))
        {
            Debug.LogError("The selected job is not currently assignable: " + jobType + ".", this);
            return;
        }

        gameObject.SetActive(false);
    }

    private static Button CreateButton(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        bool interactable)
    {
        GameObject buttonObject = new GameObject(
            objectName + "Button",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform buttonTransform = buttonObject.GetComponent<RectTransform>();
        buttonTransform.SetParent(parent, false);
        buttonTransform.anchorMin = new Vector2(0.5f, 0.5f);
        buttonTransform.anchorMax = new Vector2(0.5f, 0.5f);
        buttonTransform.anchoredPosition = position;
        buttonTransform.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = interactable
            ? new Color(0.22f, 0.39f, 0.58f, 1f)
            : new Color(0.24f, 0.25f, 0.28f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = interactable;

        TMP_Text label = CreateText(
            "Label",
            buttonTransform,
            Vector2.zero,
            size,
            16f,
            TextAlignmentOptions.Center);
        label.text = objectName;
        label.color = interactable
            ? Color.white
            : new Color(0.68f, 0.68f, 0.68f, 1f);
        return button;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.SetParent(parent, false);
        textTransform.anchorMin = new Vector2(0.5f, 0.5f);
        textTransform.anchorMax = new Vector2(0.5f, 0.5f);
        textTransform.anchoredPosition = position;
        textTransform.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
