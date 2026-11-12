using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const string MainGameSceneName = "MainGame";

    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;

    private void Awake()
    {
        if (newGameButton == null)
        {
            Debug.LogError("Main Menu requires a New Game button.", this);
        }
        else
        {
            newGameButton.onClick.AddListener(LoadMainGame);
        }

        if (loadGameButton != null)
        {
            loadGameButton.interactable = false;
        }
    }

    private void Start()
    {
        if (newGameButton == null)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            Debug.LogError("Main Menu requires an EventSystem.", this);
            return;
        }

        eventSystem.SetSelectedGameObject(newGameButton.gameObject);
    }

    private void OnDestroy()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(LoadMainGame);
        }
    }

    private void LoadMainGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(MainGameSceneName))
        {
            Debug.LogError(
                "The main game scene '" + MainGameSceneName
                + "' is not enabled in the build settings.",
                this);
            return;
        }

        SceneManager.LoadScene(MainGameSceneName);
    }
}
