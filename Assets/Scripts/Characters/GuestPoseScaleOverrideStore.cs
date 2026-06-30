using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Guest Pose Scale Override Store")]
public sealed class GuestPoseScaleOverrideStore : MonoBehaviour
{
    [SerializeField] private List<GuestPoseScaleOverrideEntry> entries = new List<GuestPoseScaleOverrideEntry>();

    public List<GuestPoseScaleOverrideEntry> Entries => entries;

    public bool TryGetOverride(
        string roomId,
        string characterId,
        out CharacterPose pose,
        out float poseRatio,
        out float fineTuneMultiplier)
    {
        pose = CharacterPose.Auto;
        poseRatio = 0f;
        fineTuneMultiplier = 1f;

        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            GuestPoseScaleOverrideEntry entry = entries[i];

            if (entry == null || !entry.Matches(roomId, characterId))
            {
                continue;
            }

            pose = entry.pose;
            poseRatio = entry.poseRatio;
            fineTuneMultiplier = Mathf.Max(0.001f, entry.fineTuneMultiplier);
            return true;
        }

        return false;
    }

    public GuestPoseScaleOverrideEntry GetOrCreate(string roomId, string characterId)
    {
        if (entries == null)
        {
            entries = new List<GuestPoseScaleOverrideEntry>();
        }

        for (int i = 0; i < entries.Count; i++)
        {
            GuestPoseScaleOverrideEntry entry = entries[i];

            if (entry != null && entry.Matches(roomId, characterId))
            {
                return entry;
            }
        }

        GuestPoseScaleOverrideEntry created = new GuestPoseScaleOverrideEntry
        {
            roomId = GuestRoomScaleCalibration.CleanRoomId(roomId),
            characterId = string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim(),
            pose = CharacterPose.Seated,
            poseRatio = 0.68f,
            fineTuneMultiplier = 1f
        };
        entries.Add(created);
        return created;
    }
}

[Serializable]
public sealed class GuestPoseScaleOverrideEntry
{
    public string roomId;
    public string characterId;
    public CharacterPose pose = CharacterPose.Seated;
    [Range(0.1f, 2f)] public float poseRatio = 0.68f;
    [Min(0.001f)] public float fineTuneMultiplier = 1f;

    public bool Matches(string otherRoomId, string otherCharacterId)
    {
        return GuestRoomScaleCalibration.SameRoom(roomId, otherRoomId) &&
            SameCharacter(characterId, otherCharacterId);
    }

    private static bool SameCharacter(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
        {
            return true;
        }

        return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty);
    }
}
