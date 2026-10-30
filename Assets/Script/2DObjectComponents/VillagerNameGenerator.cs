using System.Collections.Generic;
using UnityEngine;

public class VillagerNameGenerator : MonoBehaviour
{
    public bool TryGenerateName(Person person)
    {
        if (person == null)
        {
            Debug.LogError("Cannot generate a villager name without a Person record.", this);
            return false;
        }

        NamePart[] nameParts = GameData.Instance.nameParts;
        if (nameParts == null || nameParts.Length == 0)
        {
            Debug.LogError("Cannot generate a villager name because no name parts are configured.", this);
            return false;
        }

        List<NamePart> eligibleParts = GetEligibleParts(nameParts, person.isMale, null);
        if (eligibleParts.Count == 0)
        {
            Debug.LogError(
                "No valid name parts are configured for " + (person.isMale ? "male" : "female") + " villagers.",
                this);
            return false;
        }

        NamePart selectedPart = SelectRandomPart(eligibleParts);
        string generatedName;
        switch (selectedPart.type)
        {
            case NamePartType.FullName:
                generatedName = selectedPart.value;
                break;
            case NamePartType.FirstPart:
                generatedName = CombineNameParts(
                    selectedPart,
                    SelectMatchingPart(nameParts, person.isMale, NamePartType.LastPart));
                break;
            case NamePartType.LastPart:
                generatedName = CombineNameParts(
                    SelectMatchingPart(nameParts, person.isMale, NamePartType.FirstPart),
                    selectedPart);
                break;
            default:
                Debug.LogError("The selected name part has an unsupported type.", this);
                return false;
        }

        if (string.IsNullOrWhiteSpace(generatedName))
        {
            Debug.LogError("The selected name parts do not produce a valid villager name.", this);
            return false;
        }

        person.name = generatedName;
        gameObject.name = generatedName;
        return true;
    }

    private List<NamePart> GetEligibleParts(
        NamePart[] nameParts,
        bool isMale,
        NamePartType? requiredType)
    {
        List<NamePart> eligibleParts = new List<NamePart>();
        foreach (NamePart namePart in nameParts)
        {
            if (namePart == null
                || string.IsNullOrWhiteSpace(namePart.value)
                || (requiredType.HasValue && namePart.type != requiredType.Value)
                || !IsValidNamePartType(namePart.type)
                || !MatchesGender(namePart, isMale))
            {
                continue;
            }

            eligibleParts.Add(namePart);
        }

        return eligibleParts;
    }

    private NamePart SelectMatchingPart(NamePart[] nameParts, bool isMale, NamePartType requiredType)
    {
        List<NamePart> eligibleParts = GetEligibleParts(nameParts, isMale, requiredType);
        if (eligibleParts.Count == 0)
        {
            Debug.LogError(
                "No " + requiredType + " name part is configured for "
                + (isMale ? "male" : "female") + " villagers.",
                this);
            return null;
        }

        return SelectRandomPart(eligibleParts);
    }

    private NamePart SelectRandomPart(List<NamePart> eligibleParts)
    {
        return eligibleParts[Random.Range(0, eligibleParts.Count)];
    }

    private string CombineNameParts(NamePart firstPart, NamePart lastPart)
    {
        if (firstPart == null || lastPart == null)
        {
            return null;
        }

        return firstPart.value + lastPart.value;
    }

    private bool MatchesGender(NamePart namePart, bool isMale)
    {
        return isMale ? namePart.worksForMen : namePart.worksForWomen;
    }

    private bool IsValidNamePartType(NamePartType type)
    {
        return type == NamePartType.FullName
            || type == NamePartType.FirstPart
            || type == NamePartType.LastPart;
    }
}
