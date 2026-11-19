using System.Collections.Generic;
using UnityEngine;

public class OutcropInstance : MonoBehaviour
{
    private readonly List<ResourceObject> resourceObjects = new List<ResourceObject>();
    private readonly Dictionary<ResourceObject, string> resourceObjectPaths =
        new Dictionary<ResourceObject, string>();

    public OutcropDefinition Definition { get; private set; }

    public string DefinitionId
    {
        get { return Definition != null ? Definition.id : null; }
    }

    public bool Initialize(OutcropDefinition definition)
    {
        if (definition == null
            || string.IsNullOrWhiteSpace(definition.id)
            || definition.resourceObjects == null
            || definition.resourceObjects.Length == 0)
        {
            Debug.LogError("Cannot initialize an outcrop without a valid definition.", this);
            return false;
        }

        Definition = definition;
        resourceObjects.Clear();
        resourceObjectPaths.Clear();
        foreach (OutcropResourceObjectDefinition childDefinition in definition.resourceObjects)
        {
            Transform childTransform = childDefinition != null
                ? transform.Find(childDefinition.hierarchyPath)
                : null;
            ResourceObject resourceObject = childTransform != null
                ? childTransform.GetComponent<ResourceObject>()
                : null;
            if (resourceObject == null
                || !resourceObject.Initialize(
                    childDefinition.resourceObjectDefinition,
                    0,
                    0f,
                    childDefinition.visualSprite))
            {
                Debug.LogError(
                    "Outcrop '" + definition.id + "' could not initialize child '"
                    + (childDefinition != null ? childDefinition.hierarchyPath : "")
                    + "'.",
                    this);
                return false;
            }

            resourceObject.SetOutcropOwner(this, childDefinition.hierarchyPath);
            resourceObjects.Add(resourceObject);
            resourceObjectPaths.Add(resourceObject, childDefinition.hierarchyPath);
        }

        return true;
    }

    public ResourceObject[] GetResourceObjects()
    {
        return resourceObjects.ToArray();
    }

    public bool TryGetResourceObjectPath(ResourceObject resourceObject, out string hierarchyPath)
    {
        return resourceObjectPaths.TryGetValue(resourceObject, out hierarchyPath);
    }

    public bool TryGetResourceObject(
        string hierarchyPath,
        out ResourceObject resourceObject)
    {
        foreach (KeyValuePair<ResourceObject, string> entry in resourceObjectPaths)
        {
            if (string.Equals(entry.Value, hierarchyPath, System.StringComparison.Ordinal))
            {
                resourceObject = entry.Key;
                return true;
            }
        }

        resourceObject = null;
        return false;
    }

    internal bool RemoveResourceObject(ResourceObject resourceObject)
    {
        if (!resourceObjectPaths.Remove(resourceObject))
        {
            return false;
        }

        resourceObjects.Remove(resourceObject);
        return resourceObjects.Count == 0;
    }
}
