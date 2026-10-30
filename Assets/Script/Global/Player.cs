
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    private static Player instance;

    public static Player Instance
    {
        get { return instance; }
    }

    [NonSerialized] public ResourceRecord[] resources;
    public List<VillagerRecord> Villagers { get; private set; } = new List<VillagerRecord>();

    private int manaResourceIndex = -1;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogError("Only one Player can be active at a time.", this);
            enabled = false;
            return;
        }

        instance = this;
        GameData gameData = GameData.Instance;
        if (!InitializeResources(gameData.resourceDefinitions))
        {
            return;
        }

        PlayerSettings settings = gameData.playerSettings;
        if (settings == null)
        {
            Debug.LogError("Player settings could not be loaded from GameData.", this);
            return;
        }

        if (settings.manaGainIntervalSeconds <= 0f || settings.manaGainAmount <= 0)
        {
            Debug.LogError("Mana gain interval and amount must both be greater than zero.", this);
            return;
        }

        if (manaResourceIndex < 0)
        {
            Debug.LogError("The ResourceType enum must include Mana for mana regeneration.", this);
            return;
        }

        StartCoroutine(RegenerateMana(settings));
    }

    public void RegisterVillager(Person person, Villager villager)
    {
        if (person == null || villager == null)
        {
            Debug.LogError("Cannot register a villager without both person data and a Villager component.", this);
            return;
        }

        Villagers.Add(new VillagerRecord(person, villager));
    }

    private bool InitializeResources(ResourceDefinition[] resourceDefinitions)
    {
        Array resourceTypes = Enum.GetValues(typeof(ResourceType));
        if (resourceDefinitions == null || resourceDefinitions.Length != resourceTypes.Length)
        {
            Debug.LogError("Resource definitions could not be loaded from GameData.", this);
            return false;
        }

        resources = new ResourceRecord[resourceTypes.Length];
        foreach (ResourceDefinition definition in resourceDefinitions)
        {
            if (definition == null || !Enum.IsDefined(typeof(ResourceType), definition.type))
            {
                Debug.LogError("GameData contains an invalid resource definition.", this);
                resources = null;
                return false;
            }

            int resourceIndex = Convert.ToInt32(definition.type);
            if (resourceIndex < 0
                || resourceIndex >= resources.Length
                || resources[resourceIndex] != null
                || definition.initialAmount < 0)
            {
                Debug.LogError("GameData contains an invalid or duplicate resource definition.", this);
                resources = null;
                return false;
            }

            resources[resourceIndex] = new ResourceRecord
            {
                type = definition.type,
                amount = definition.initialAmount
            };

            if (definition.type == ResourceType.Mana)
            {
                manaResourceIndex = resourceIndex;
            }
        }

        return true;
    }

    public bool TryGetResourceAmount(ResourceType resourceType, out int amount)
    {
        if (resources != null)
        {
            foreach (ResourceRecord resource in resources)
            {
                if (resource != null && resource.type == resourceType)
                {
                    amount = resource.amount;
                    return true;
                }
            }
        }

        amount = 0;
        return false;
    }

    private IEnumerator RegenerateMana(PlayerSettings settings)
    {
        WaitForSeconds interval = new WaitForSeconds(settings.manaGainIntervalSeconds);
        while (true)
        {
            yield return interval;
            resources[manaResourceIndex].amount += settings.manaGainAmount;
        }
    }
}
