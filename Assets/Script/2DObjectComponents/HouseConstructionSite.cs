using System.Collections.Generic;
using UnityEngine;

public class HouseConstructionSite : MonoBehaviour
{
    private readonly List<Villager> helpers = new List<Villager>();
    private Villager builder;

    public bool IsActive { get; private set; }
    public Vector3 WorkPosition { get; private set; }

    public int WorkerCount
    {
        get
        {
            if (!IsActive)
            {
                return 1;
            }

            int workerCount = builder != null ? 1 : 0;
            foreach (Villager helper in helpers)
            {
                if (helper != null && helper.IsActivelyHelpingConstruction)
                {
                    workerCount++;
                }
            }

            return Mathf.Max(1, workerCount);
        }
    }

    public bool BeginWorkStep(Villager owner, Vector3 workPosition)
    {
        if (owner == null)
        {
            Debug.LogError("A construction site requires a builder.", this);
            return false;
        }

        if (builder != null && builder != owner && IsActive)
        {
            Debug.LogError("An active construction site cannot change builders.", this);
            return false;
        }

        if (builder != null && builder != owner)
        {
            EndWorkStep();
        }

        builder = owner;
        WorkPosition = workPosition;
        IsActive = true;
        MapManager mapManager = MapManager.Instance;
        if (mapManager != null)
        {
            mapManager.RegisterActiveConstructionSite(this);
        }

        return true;
    }

    public void EndWorkStep()
    {
        if (!IsActive && helpers.Count == 0)
        {
            return;
        }

        IsActive = false;
        MapManager mapManager = MapManager.Instance;
        if (mapManager != null)
        {
            mapManager.UnregisterActiveConstructionSite(this);
        }

        Villager[] currentHelpers = helpers.ToArray();
        helpers.Clear();
        foreach (Villager helper in currentHelpers)
        {
            if (helper != null)
            {
                helper.StopConstructionAssistance(this);
            }
        }
    }

    public void Complete()
    {
        EndWorkStep();
        builder = null;
    }

    public bool CanAcceptHelper(Villager helper)
    {
        return IsActive
            && helper != null
            && helper != builder
            && !helpers.Contains(helper);
    }

    public bool TryJoin(Villager helper)
    {
        if (!CanAcceptHelper(helper))
        {
            return false;
        }

        helpers.Add(helper);
        return true;
    }

    public bool HasHelper(Villager helper)
    {
        return IsActive && helper != null && helpers.Contains(helper);
    }

    public void Leave(Villager helper)
    {
        if (helper != null)
        {
            helpers.Remove(helper);
        }
    }

    private void OnDestroy()
    {
        Complete();
    }
}
