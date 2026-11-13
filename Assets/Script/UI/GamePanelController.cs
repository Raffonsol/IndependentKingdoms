using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GamePanelController : MonoBehaviour
{
    private const string MainGameSceneName = "MainGame";
    private const string MainMenuSceneName = "MainMenu";

    [SerializeField] private Button newGameButton;
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button exitGameButton;

    private void Awake()
    {
        if (newGameButton == null || saveGameButton == null
            || mainMenuButton == null || closeButton == null || exitGameButton == null)
        {
            Debug.LogError(
                "Game Panel requires New Game, Save Game, Main Menu, Close, and Exit Game buttons.",
                this);
            return;
        }

        newGameButton.onClick.AddListener(StartNewGame);
        saveGameButton.interactable = true;
        saveGameButton.onClick.AddListener(SaveGame);
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        closeButton.onClick.AddListener(ClosePanel);
        exitGameButton.onClick.AddListener(ExitGame);
    }

    private void OnEnable()
    {
        if (newGameButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(newGameButton.gameObject);
        }
    }

    private void OnDisable()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selectedObject = eventSystem != null
            ? eventSystem.currentSelectedGameObject
            : null;
        if (selectedObject != null && selectedObject.transform.IsChildOf(transform))
        {
            eventSystem.SetSelectedGameObject(null);
        }
    }

    private void OnDestroy()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(StartNewGame);
        }

        if (saveGameButton != null)
        {
            saveGameButton.onClick.RemoveListener(SaveGame);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
        }

        if (exitGameButton != null)
        {
            exitGameButton.onClick.RemoveListener(ExitGame);
        }
    }

    private void StartNewGame()
    {
        GameSaveManager.ClearPendingLoad();
        LoadScene(MainGameSceneName);
    }

    private void SaveGame()
    {
        if (GameSaveManager.TrySaveCurrentGame(GameSaveManager.DefaultSlotName))
        {
            Debug.Log("Game saved.", this);
        }
    }

    private void ReturnToMainMenu()
    {
        LoadScene(MainMenuSceneName);
    }

    private void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    private void ExitGame()
    {
        Application.Quit();
    }

    private void LoadScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                "The scene '" + sceneName + "' is not enabled in the build settings.",
                this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
