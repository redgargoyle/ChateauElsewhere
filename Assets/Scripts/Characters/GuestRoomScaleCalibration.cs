using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GuestRoomScaleEntry
{
    public string roomId;
    public bool enabled = true;
    [Min(0.001f)] public float roomGuestScaleMultiplier = 1f;
    public bool useButlerRoomCurve = true;
    public bool hasReferenceRoomStageScale;
    [Min(0.0001f)] public float referenceRoomStageScale = 1f;

    public GuestRoomScaleEntry()
    {
        roomId = string.Empty;
    }

    public GuestRoomScaleEntry(string roomId)
    {
        this.roomId = CleanRoomId(roomId);
    }

    public bool Matches(string otherRoomId)
    {
        return SameRoom(roomId, otherRoomId);
    }

    internal void Sanitize()
    {
        roomId = CleanRoomId(roomId);
        roomGuestScaleMultiplier = Mathf.Max(0.001f, roomGuestScaleMultiplier);
        referenceRoomStageScale = hasReferenceRoomStageScale
            ? Mathf.Max(0.0001f, referenceRoomStageScale)
            : 1f;
    }

    private static string CleanRoomId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(NormalizeRoomName(left), NormalizeRoomName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoomName(string value)
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

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Guest Room Scale Calibration")]
public sealed class GuestRoomScaleCalibration : MonoBehaviour
{
    [SerializeField] private bool enableGuestRoomScaling = true;
    [SerializeField] private PointClickPlayerMovement butlerScaleSource;
    [SerializeField] private List<GuestRoomScaleEntry> rooms = new List<GuestRoomScaleEntry>();

    public bool EnableGuestRoomScaling => enableGuestRoomScaling;
    public List<GuestRoomScaleEntry> Rooms => rooms;
    public PointClickPlayerMovement ButlerScaleSource => butlerScaleSource;

    private void OnValidate()
    {
        SanitizeRooms();
    }

    public void SetGuestRoomScalingEnabled(bool value)
    {
        enableGuestRoomScaling = value;
    }

    public void SetButlerScaleSource(PointClickPlayerMovement source)
    {
        butlerScaleSource = source;
    }

    public GuestRoomScaleEntry GetOrCreateRoom(string roomId)
    {
        string cleanRoomId = CleanRoomId(roomId);

        if (string.IsNullOrWhiteSpace(cleanRoomId))
        {
            cleanRoomId = "Room";
        }

        if (rooms == null)
        {
            rooms = new List<GuestRoomScaleEntry>();
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            GuestRoomScaleEntry entry = rooms[i];

            if (entry != null && entry.Matches(cleanRoomId))
            {
                entry.Sanitize();
                return entry;
            }
        }

        GuestRoomScaleEntry created = new GuestRoomScaleEntry(cleanRoomId);
        rooms.Add(created);
        return created;
    }

    public bool TryGetRoom(string roomId, out GuestRoomScaleEntry entry)
    {
        if (!TryGetRoomReadOnly(roomId, out entry))
        {
            return false;
        }

        entry.Sanitize();
        return true;
    }

    public bool TryGetRoomReadOnly(string roomId, out GuestRoomScaleEntry entry)
    {
        entry = null;

        if (rooms == null || string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            GuestRoomScaleEntry candidate = rooms[i];

            if (candidate != null && candidate.Matches(roomId))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryEvaluateGuestScale(
        string roomId,
        float roomLocalY,
        out float scale,
        out float depth01,
        out string diagnostic)
    {
        scale = 1f;
        depth01 = 0f;
        diagnostic = string.Empty;

        if (!enableGuestRoomScaling)
        {
            diagnostic = "Guest room scaling disabled.";
            return true;
        }

        if (!TryGetRoom(roomId, out GuestRoomScaleEntry entry))
        {
            diagnostic = $"No guest room scale entry for '{roomId}'.";
            return false;
        }

        if (!entry.enabled)
        {
            diagnostic = $"Guest room scale entry disabled for '{entry.roomId}'.";
            return true;
        }

        PointClickPlayerMovement source = ResolveButlerScaleSource();
        float roomMultiplier = Mathf.Max(0.001f, entry.roomGuestScaleMultiplier);

        if (source != null &&
            source.TryEvaluateButlerCharacterScale(
                entry.roomId,
                new Vector2(0f, roomLocalY),
                out PointClickPlayerMovement.ButlerCharacterScaleSample sample))
        {
            depth01 = sample.Depth01;
            scale = Mathf.Max(0.001f, sample.ButlerFinalLocalScaleY);
            scale *= roomMultiplier;
            diagnostic = $"Butler depth scale {sample.Source} depth={depth01:0.###}; room multiplier {roomMultiplier:0.###}.";
            return true;
        }

        scale = roomMultiplier;
        diagnostic = $"Fallback room multiplier for '{entry.roomId}'.";
        return true;
    }

    public void SetRoomMultiplier(string roomId, float multiplier)
    {
        GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.roomGuestScaleMultiplier = Mathf.Max(0.001f, multiplier);
    }

    public void SetReferenceRoomStageScale(string roomId, float stageScale)
    {
        GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.hasReferenceRoomStageScale = true;
        entry.referenceRoomStageScale = SanitizeRoomStageScale(stageScale);
    }

    public bool TryGetReferenceRoomStageScale(string roomId, out float stageScale)
    {
        stageScale = 1f;

        if (!TryGetRoom(roomId, out GuestRoomScaleEntry entry) ||
            !entry.hasReferenceRoomStageScale)
        {
            return false;
        }

        stageScale = SanitizeRoomStageScale(entry.referenceRoomStageScale);
        return true;
    }

    public bool TryGetReferenceRoomStageScaleReadOnly(string roomId, out float stageScale)
    {
        stageScale = 1f;

        if (!TryGetRoomReadOnly(roomId, out GuestRoomScaleEntry entry) ||
            !entry.hasReferenceRoomStageScale)
        {
            return false;
        }

        stageScale = SanitizeRoomStageScale(entry.referenceRoomStageScale);
        return true;
    }

    public bool RemoveRoom(string roomId)
    {
        if (rooms == null)
        {
            return false;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            GuestRoomScaleEntry entry = rooms[i];

            if (entry != null && entry.Matches(roomId))
            {
                rooms.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public void InitializeMissingRoomsFromButler(PointClickPlayerMovement butler)
    {
        if (butler == null)
        {
            return;
        }

        butlerScaleSource = butler;
        List<string> butlerRooms = new List<string>();
        butler.GetButlerScaleOverrideRoomIds(butlerRooms);

        float referenceRoomStageScale = GuestRoomStageScaleUtility.TryGetActiveRoomStageScale(out float activeStageScale)
            ? activeStageScale
            : 1f;

        for (int i = 0; i < butlerRooms.Count; i++)
        {
            string roomId = butlerRooms[i];

            if (string.IsNullOrWhiteSpace(roomId))
            {
                continue;
            }

            GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);

            if (!entry.hasReferenceRoomStageScale)
            {
                entry.hasReferenceRoomStageScale = true;
                entry.referenceRoomStageScale = SanitizeRoomStageScale(referenceRoomStageScale);
            }
        }
    }

    public PointClickPlayerMovement ResolveButlerScaleSource()
    {
        if (butlerScaleSource != null)
        {
            return butlerScaleSource;
        }

        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Include);
        PointClickPlayerMovement firstActive = null;
        PointClickPlayerMovement firstInactive = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate == null || candidate.gameObject == null || NameLooksLikeGuest(candidate.gameObject.name))
            {
                continue;
            }

            if (candidate.gameObject.activeInHierarchy)
            {
                firstActive ??= candidate;

                if (string.Equals(candidate.gameObject.tag, "Player", StringComparison.OrdinalIgnoreCase) ||
                    NameLooksLikePlayerOrButler(candidate.gameObject.name))
                {
                    butlerScaleSource = candidate;
                    return butlerScaleSource;
                }
            }
            else
            {
                firstInactive ??= candidate;
            }
        }

        butlerScaleSource = firstActive != null ? firstActive : firstInactive;
        return butlerScaleSource;
    }

    private void SanitizeRooms()
    {
        if (rooms == null)
        {
            rooms = new List<GuestRoomScaleEntry>();
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            rooms[i]?.Sanitize();
        }
    }

    public static bool SameRoom(string left, string right)
    {
        return string.Equals(NormalizeRoomName(left), NormalizeRoomName(right), StringComparison.OrdinalIgnoreCase);
    }

    public static string CleanRoomId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public static float SanitizeRoomStageScale(float value)
    {
        return Mathf.Max(0.0001f, value);
    }

    public static string NormalizeRoomName(string value)
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

    private static bool NameLooksLikePlayerOrButler(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
            value.IndexOf("Butler", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool NameLooksLikeGuest(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf("Guest", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
