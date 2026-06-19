using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SubtitleLineBank", menuName = "Dreadforge/UI/Subtitle Line Bank")]
public sealed class SubtitleLineBank : ScriptableObject
{
    public List<SubtitleLine> lines = new List<SubtitleLine>();

    public bool TryGetLine(string lineId, out SubtitleLine line)
    {
        line = null;

        if (string.IsNullOrWhiteSpace(lineId) || lines == null)
        {
            return false;
        }

        string cleanLineId = lineId.Trim();

        for (int i = 0; i < lines.Count; i++)
        {
            SubtitleLine candidate = lines[i];

            if (candidate != null && string.Equals(candidate.lineId, cleanLineId, System.StringComparison.OrdinalIgnoreCase))
            {
                line = candidate;
                return true;
            }
        }

        return false;
    }
}
