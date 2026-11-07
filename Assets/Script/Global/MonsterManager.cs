using System.Collections.Generic;
using UnityEngine;

public class MonsterManager : MonoBehaviour
{
    private static MonsterManager instance;

    public static MonsterManager Instance
    {
        get { return instance; }
    }

    [SerializeField] private GameObject monsterPrefab;

    private readonly List<Monster> spawnedMonsters = new List<Monster>();
    private MonsterDefinition[] definitions;
    private MonsterSpawnSettings spawnSettings;
    private float spawnTimer;

    public IReadOnlyList<Monster> Monsters
    {
        get { return spawnedMonsters; }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Only one MonsterManager can be active at a time.", this);
            enabled = false;
            return;
        }

        instance = this;
        GameData gameData = GameData.Instance;
        definitions = gameData.monsterDefinitions;
        spawnSettings = gameData.monsterSpawnSettings;
    }

    private void Start()
    {
        if (monsterPrefab == null
            || monsterPrefab.GetComponent<Monster>() == null
            || definitions == null
            || definitions.Length == 0
            || spawnSettings == null)
        {
            Debug.LogError(
                "MonsterManager requires a Monster prefab and valid monster data from GameData.",
                this);
            enabled = false;
            return;
        }

        if (MapManager.Instance == null)
        {
            Debug.LogError("MonsterManager requires an active MapManager.", this);
            enabled = false;
            return;
        }

        if (Player.Instance == null)
        {
            Debug.LogError("MonsterManager requires an active Player.", this);
            enabled = false;
            return;
        }

        if (UnityEngine.Camera.main == null)
        {
            Debug.LogError("Monster spawning requires an active camera tagged MainCamera.", this);
            enabled = false;
            return;
        }

        spawnTimer = spawnSettings.spawnIntervalSeconds;
    }

    private void Update()
    {
        PruneInactiveMonsters();
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
        {
            return;
        }

        spawnTimer = spawnSettings.spawnIntervalSeconds;
        TrySpawnOneMonster();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void TrySpawnOneMonster()
    {
        if (spawnedMonsters.Count >= spawnSettings.maximumTotalAmount)
        {
            return;
        }

        MonsterDefinition definition = ChooseSpawnDefinition();
        if (definition == null)
        {
            return;
        }

        if (!TryFindSpawnPosition(out Vector3 spawnPosition))
        {
            Debug.LogWarning(
                "MonsterManager could not find a valid spawn position for '" + definition.id
                + "' after " + spawnSettings.spawnPositionAttemptsPerInterval + " attempts.",
                this);
            return;
        }

        GameObject monsterObject = Instantiate(monsterPrefab, spawnPosition, Quaternion.identity);
        Monster monster = monsterObject.GetComponent<Monster>();
        if (monster == null || !monster.Configure(definition))
        {
            Debug.LogError(
                "Could not configure a spawned monster using definition '" + definition.id + "'.",
                monsterObject);
            Destroy(monsterObject);
            return;
        }

        spawnedMonsters.Add(monster);
    }

    private MonsterDefinition ChooseSpawnDefinition()
    {
        float totalWeight = 0f;
        foreach (MonsterDefinition definition in definitions)
        {
            if (definition != null
                && definition.spawnWeight > 0f
                && definition.maximumAmount > CountMonstersOfType(definition.id))
            {
                totalWeight += definition.spawnWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float selection = UnityEngine.Random.value * totalWeight;
        foreach (MonsterDefinition definition in definitions)
        {
            if (definition == null
                || definition.spawnWeight <= 0f
                || definition.maximumAmount <= CountMonstersOfType(definition.id))
            {
                continue;
            }

            selection -= definition.spawnWeight;
            if (selection <= 0f)
            {
                return definition;
            }
        }

        return null;
    }

    private bool TryFindSpawnPosition(out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;
        MapManager mapManager = MapManager.Instance;
        UnityEngine.Camera activeCamera = UnityEngine.Camera.main;
        if (mapManager == null || activeCamera == null || Player.Instance == null)
        {
            return false;
        }

        for (int attempt = 0; attempt < spawnSettings.spawnPositionAttemptsPerInterval; attempt++)
        {
            if (!mapManager.TryGetRandomMapCellPosition(out Vector3 candidate))
            {
                continue;
            }

            if (IsInsideCameraView(activeCamera, candidate)
                || IsTooCloseToVillager(candidate))
            {
                continue;
            }

            spawnPosition = candidate;
            return true;
        }

        return false;
    }

    private bool IsTooCloseToVillager(Vector3 position)
    {
        foreach (VillagerRecord record in Player.Instance.Villagers)
        {
            Villager villager = record != null ? record.Villager : null;
            if (villager != null
                && villager.IsAlive
                && Vector2.Distance(position, villager.transform.position)
                    < spawnSettings.minimumVillagerDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideCameraView(UnityEngine.Camera activeCamera, Vector3 position)
    {
        Vector3 viewportPosition = activeCamera.WorldToViewportPoint(position);
        return viewportPosition.z >= 0f
            && viewportPosition.x >= 0f
            && viewportPosition.x <= 1f
            && viewportPosition.y >= 0f
            && viewportPosition.y <= 1f;
    }

    private int CountMonstersOfType(string definitionId)
    {
        int count = 0;
        foreach (Monster monster in spawnedMonsters)
        {
            if (monster != null
                && monster.IsAlive
                && monster.DefinitionId == definitionId)
            {
                count++;
            }
        }

        return count;
    }

    private void PruneInactiveMonsters()
    {
        for (int index = spawnedMonsters.Count - 1; index >= 0; index--)
        {
            Monster monster = spawnedMonsters[index];
            if (monster == null || !monster.IsAlive)
            {
                spawnedMonsters.RemoveAt(index);
            }
        }
    }
}
