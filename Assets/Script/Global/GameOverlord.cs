using UnityEngine;

public class GameOverlord : MonoBehaviour
{
    private static GameOverlord instance;

    public static GameOverlord Instance
    {
        get { return instance; }
    }

    [SerializeField] private GameObject roadTilePrefab;

    public GameObject RoadTilePrefab
    {
        get { return roadTilePrefab; }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Only one GameOverlord can be active at a time.", this);
            enabled = false;
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
