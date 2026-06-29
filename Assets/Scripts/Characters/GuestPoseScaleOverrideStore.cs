using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GuestPoseScaleOverrideEntry
{
    public bool enabled = true;
    public string roomId;
    public string guestKey;
    public CharacterPoseKind pose = CharacterPoseKind.Auto;
    public float manualPoseHeightRatio = 1f;
    public float manualFineTuneMultiplier = 1f;
    public Transform scaleRoot;
    public Transform boundsRoot;
    public Component guestComponent;
    public RoomProjectedEntity projectedEntity;
    public RoomPersonWalker2D walker;
    public ActorRoomState actor;
}

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Guest Pose Scale Override Store")]
public sealed class GuestPoseScaleOverrideStore : MonoBehaviour
{
    [SerializeField] private List<GuestPoseScaleOverrideEntry> entries = new List<GuestPoseScaleOverrideEntry>();

    public IReadOnlyList<GuestPoseScaleOverrideEntry> Entries
    {
        get
        {
            EnsureEntries();
            return entries;
        }
    }

    public bool TryGetOverride(
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor,
        string roomId,
        Transform scaleRoot,
        out GuestPoseScaleOverrideEntry entry)
    {
        EnsureEntries();
        entry = null;
        string cleanRoomId = Normalize(roomId);
        string actorId = actor != null ? Normalize(actor.ActorId) : string.Empty;
        string scalePath = Normalize(GetObjectPath(scaleRoot));

        for (int i = 0; i < entries.Count; i++)
        {
            GuestPoseScaleOverrideEntry candidate = entries[i];

            if (candidate == null ||
                !candidate.enabled ||
                !RoomMatches(candidate.roomId, cleanRoomId))
            {
                continue;
            }

            if (candidate.projectedEntity != null && candidate.projectedEntity == projectedEntity ||
                candidate.walker != null && candidate.walker == walker ||
                candidate.actor != null && candidate.actor == actor ||
                candidate.guestComponent != null && ComponentMatches(candidate.guestComponent, projectedEntity, walker, actor) ||
                candidate.scaleRoot != null && candidate.scaleRoot == scaleRoot ||
                !string.IsNullOrWhiteSpace(candidate.guestKey) && GuestKeyMatches(candidate.guestKey, actorId, scalePath))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    public static float SanitizePoseHeightRatio(float ratio)
    {
        return Mathf.Clamp(ratio <= 0f ? 1f : ratio, 0.25f, 1.25f);
    }

    public static float SanitizeFineTuneMultiplier(float multiplier)
    {
        return Mathf.Clamp(multiplier <= 0f ? 1f : multiplier, 0.25f, 4f);
    }

    private void OnValidate()
    {
        EnsureEntries();

        for (int i = 0; i < entries.Count; i++)
        {
            GuestPoseScaleOverrideEntry entry = entries[i];

            if (entry == null)
            {
                continue;
            }

            entry.roomId = string.IsNullOrWhiteSpace(entry.roomId) ? string.Empty : entry.roomId.Trim();
            entry.guestKey = string.IsNullOrWhiteSpace(entry.guestKey) ? string.Empty : entry.guestKey.Trim();
            entry.manualPoseHeightRatio = SanitizePoseHeightRatio(entry.manualPoseHeightRatio);
            entry.manualFineTuneMultiplier = SanitizeFineTuneMultiplier(entry.manualFineTuneMultiplier);
        }
    }

    private void EnsureEntries()
    {
        if (entries == null)
        {
            entries = new List<GuestPoseScaleOverrideEntry>();
        }
    }

    private static bool ComponentMatches(
        Component component,
        RoomProjectedEntity projectedEntity,
        RoomPersonWalker2D walker,
        ActorRoomState actor)
    {
        return component != null &&
            (component == projectedEntity ||
            component == walker ||
            component == actor);
    }

    private static bool RoomMatches(string configuredRoom, string cleanRoom)
    {
        string configured = Normalize(configuredRoom);
        return string.IsNullOrWhiteSpace(configured) || configured == cleanRoom;
    }

    private static bool GuestKeyMatches(string configuredKey, string actorId, string scalePath)
    {
        string configured = Normalize(configuredKey);
        return !string.IsNullOrWhiteSpace(configured) &&
            (configured == actorId ||
            scalePath.IndexOf(configured, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim()
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
    }

    private static string GetObjectPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
