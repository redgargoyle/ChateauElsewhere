using UnityEngine;

[CreateAssetMenu(menuName = "Noise/Static Set", fileName = "SS_StaticSet")]
public class StaticSet : ScriptableObject
{
    public string setName = "Static Set";

    [Tooltip("Order of groups to play.")]
    public StaticFrameGroup[] groups;

    public bool loop = true;

    public bool IsValid()
    {
        if (groups == null || groups.Length == 0)
        {
            return false;
        }

        foreach (StaticFrameGroup group in groups)
        {
            if (group != null && group.IsValid())
            {
                return true;
            }
        }

        return false;
    }

    public bool isValid()
    {
        return IsValid();
    }
}
