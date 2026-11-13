using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const string MainGameSceneName = "MainGame";

    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button exitGameButton;

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
            loadGameButton.onClick.AddListener(LoadSavedGame);
        }

        if (exitGameButton != null)
        {
            exitGameButton.onClick.AddListener(ExitGame);
        }
        else
        {
            Debug.LogError("Main Menu requires an Exit Game button.", this);
        }
    }

    private void OnEnable()
    {
        if (loadGameButton != null)
        {
            loadGameButton.interactable = GameSaveManager.HasLoadableSave();
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

        if (loadGameButton != null)
        {
            loadGameButton.onClick.RemoveListener(LoadSavedGame);
        }

        if (exitGameButton != null)
        {
            exitGameButton.onClick.RemoveListener(ExitGame);
        }
    }

    private void LoadMainGame()
    {
        GameSaveManager.ClearPendingLoad();
        LoadGameScene();
    }

    private void LoadSavedGame()
    {
        if (!GameSaveManager.TryPrepareLoad())
        {
            loadGameButton.interactable = false;
            return;
        }

        if (!LoadGameScene())
        {
            GameSaveManager.ClearPendingLoad();
        }
    }

    private void ExitGame()
    {
        Application.Quit();
    }

    private bool LoadGameScene()
    {
        if (!Application.CanStreamedLevelBeLoaded(MainGameSceneName))
        {
            Debug.LogError(
                "The main game scene '" + MainGameSceneName
                + "' is not enabled in the build settings.",
                this);
            return false;
        }

        SceneManager.LoadScene(MainGameSceneName);
        return true;
    }
}
