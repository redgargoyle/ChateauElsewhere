using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CoatCloset : MonoBehaviour
{
    [SerializeField] private string closetId = "coat_closet";
    [SerializeField] private List<string> storedCoats = new List<string>();

    public string ClosetId => closetId;
    public int StoredCoatCount => storedCoats.Count;
    public IReadOnlyList<string> StoredCoats => storedCoats;

    public void StoreCoat(string coatId)
    {
        string cleanCoatId = string.IsNullOrWhiteSpace(coatId) ? "unknown_coat" : coatId.Trim();
        storedCoats.Add(cleanCoatId);
        Debug.Log($"Coat placed in closet: {cleanCoatId}", this);
    }

    public bool ContainsCoat(string coatId)
    {
        if (string.IsNullOrWhiteSpace(coatId))
        {
            return false;
        }

        for (int i = 0; i < storedCoats.Count; i++)
        {
            if (string.Equals(storedCoats[i], coatId.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void ClearStoredCoats()
    {
        storedCoats.Clear();
    }
}
