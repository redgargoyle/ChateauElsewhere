using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class SubtitleSpeakerPortraitBinding
{
    public string speakerId;
    public Sprite portrait;
}

[CreateAssetMenu(fileName = "SubtitleLineBank", menuName = "Dreadforge/UI/Subtitle Line Bank")]
public sealed class SubtitleLineBank : ScriptableObject
{
    public List<SubtitleLine> lines = new List<SubtitleLine>();
    public List<SubtitleSpeakerPortraitBinding> speakerPortraits = new List<SubtitleSpeakerPortraitBinding>();

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

    public bool TryGetSpeakerPortrait(string speakerId, out Sprite portrait)
    {
        portrait = null;

        if (string.IsNullOrWhiteSpace(speakerId) || speakerPortraits == null)
        {
            return false;
        }

        string cleanSpeakerId = speakerId.Trim();

        for (int i = 0; i < speakerPortraits.Count; i++)
        {
            SubtitleSpeakerPortraitBinding binding = speakerPortraits[i];

            if (binding != null &&
                binding.portrait != null &&
                string.Equals(binding.speakerId?.Trim(), cleanSpeakerId, System.StringComparison.OrdinalIgnoreCase))
            {
                portrait = binding.portrait;
                return true;
            }
        }

        return false;
    }
}
