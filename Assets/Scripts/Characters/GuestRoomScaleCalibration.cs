using System;
using System.Collections.Generic;
using UnityEngine;

public enum GuestRoomScaleMode
{
    Inferred = 0,
    ButlerCurve = 1,
    Fixed = 2,
    DepthCurve = 3
}

[Serializable]
public sealed class GuestRoomScaleEntry
{
    public string roomId;
    public bool enabled = true;
    public GuestRoomScaleMode scaleMode = GuestRoomScaleMode.Inferred;
    [Min(0.001f)] public float roomGuestScaleMultiplier = 1f;
    public bool useButlerRoomCurve = true;
    public bool useFixedGuestScale;
    [Min(0.001f)] public float fixedGuestScale = 1f;
    public bool useCustomGuestCurve;
    public bool hasFront;
    public float frontRoomLocalY;
    [Min(0.001f)] public float frontGuestScale = 1f;
    public bool hasBack;
    public float backRoomLocalY;
    [Min(0.001f)] public float backGuestScale = 1f;
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

    public bool HasCompleteCustomCurve =>
        hasFront &&
        hasBack &&
        Mathf.Abs(frontRoomLocalY - backRoomLocalY) >= PointClickPlayerMovement.ButlerRoomScaleEndpointEpsilon;

    public GuestRoomScaleMode ResolvedScaleMode
    {
        get
        {
            if (scaleMode != GuestRoomScaleMode.Inferred)
            {
                return scaleMode;
            }

            if (useCustomGuestCurve && HasCompleteCustomCurve)
            {
                return GuestRoomScaleMode.DepthCurve;
            }

            if (useFixedGuestScale)
            {
                return GuestRoomScaleMode.Fixed;
            }

            return GuestRoomScaleMode.ButlerCurve;
        }
    }

    public bool Matches(string otherRoomId)
    {
        return SameRoom(roomId, otherRoomId);
    }

    internal void Sanitize()
    {
        roomId = CleanRoomId(roomId);
        if (!Enum.IsDefined(typeof(GuestRoomScaleMode), scaleMode))
        {
            scaleMode = GuestRoomScaleMode.Inferred;
        }

        roomGuestScaleMultiplier = Mathf.Max(0.001f, roomGuestScaleMultiplier);
        fixedGuestScale = Mathf.Max(0.001f, fixedGuestScale);
        frontGuestScale = Mathf.Max(0.001f, frontGuestScale);
        backGuestScale = Mathf.Max(0.001f, backGuestScale);
        referenceRoomStageScale = hasReferenceRoomStageScale
            ? Mathf.Max(0.0001f, referenceRoomStageScale)
            : 1f;
    }

    internal void UseButlerCurve(float multiplier)
    {
        scaleMode = GuestRoomScaleMode.ButlerCurve;
        useButlerRoomCurve = true;
        useFixedGuestScale = false;
        fixedGuestScale = 1f;
        useCustomGuestCurve = false;
        roomGuestScaleMultiplier = Mathf.Max(0.001f, multiplier);
    }

    internal void UseFixedScale(float guestScale)
    {
        scaleMode = GuestRoomScaleMode.Fixed;
        useButlerRoomCurve = false;
        useFixedGuestScale = true;
        fixedGuestScale = Mathf.Max(0.001f, guestScale);
        useCustomGuestCurve = false;
    }

    internal void UseDepthCurve()
    {
        scaleMode = GuestRoomScaleMode.DepthCurve;
        useButlerRoomCurve = false;
        useFixedGuestScale = false;
        useCustomGuestCurve = true;
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
                candidate.Sanitize();
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

        GuestRoomScaleMode mode = entry.ResolvedScaleMode;

        if (mode == GuestRoomScaleMode.DepthCurve)
        {
            if (!entry.HasCompleteCustomCurve)
            {
                diagnostic = $"Depth guest curve for '{entry.roomId}' is incomplete.";
                return false;
            }

            depth01 = Mathf.Clamp01(Mathf.InverseLerp(entry.frontRoomLocalY, entry.backRoomLocalY, roomLocalY));
            scale = Mathf.Lerp(entry.frontGuestScale, entry.backGuestScale, depth01);
            diagnostic = $"Depth guest curve {entry.roomId} depth={depth01:0.###}.";
            return true;
        }

        if (mode == GuestRoomScaleMode.Fixed)
        {
            scale = Mathf.Max(0.001f, entry.fixedGuestScale);
            diagnostic = $"Fixed manual guest scale for '{entry.roomId}'.";
            return true;
        }

        PointClickPlayerMovement source = ResolveButlerScaleSource();

        if (mode == GuestRoomScaleMode.ButlerCurve &&
            source != null &&
            source.TryEvaluateButlerCharacterScale(
                entry.roomId,
                new Vector2(0f, roomLocalY),
                out PointClickPlayerMovement.ButlerCharacterScaleSample sample))
        {
            depth01 = sample.Depth01;
            scale = Mathf.Max(0.001f, sample.ButlerFinalLocalScaleY);
            scale *= Mathf.Max(0.001f, entry.roomGuestScaleMultiplier);
            diagnostic = $"Butler final local scale {sample.Source} depth={depth01:0.###}.";
            return true;
        }

        diagnostic = $"Butler guest scale unavailable for '{entry.roomId}'.";
        return false;
    }

    public void SetRoomMultiplier(string roomId, float multiplier)
    {
        GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.roomGuestScaleMultiplier = Mathf.Max(0.001f, multiplier);
    }

    public void SetRoomScaleMode(string roomId, GuestRoomScaleMode mode)
    {
        GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);

        switch (mode)
        {
            case GuestRoomScaleMode.Fixed:
                entry.UseFixedScale(entry.fixedGuestScale);
                break;
            case GuestRoomScaleMode.DepthCurve:
                entry.UseDepthCurve();
                break;
            case GuestRoomScaleMode.ButlerCurve:
            case GuestRoomScaleMode.Inferred:
            default:
                entry.UseButlerCurve(entry.roomGuestScaleMultiplier);
                break;
        }
    }

    public void UseButlerRoomCurve(string roomId, float multiplier = 1f)
    {
        GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.UseButlerCurve(multiplier);
    }

    public void SetFixedGuestScale(string roomId, float guestScale)
    {
        GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.UseFixedScale(guestScale);
    }

    public void ClearFixedGuestScale(string roomId)
    {
        GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.useFixedGuestScale = false;
        entry.fixedGuestScale = 1f;

        if (entry.ResolvedScaleMode == GuestRoomScaleMode.Fixed)
        {
            entry.UseButlerCurve(entry.roomGuestScaleMultiplier);
        }
    }

    public bool LoadCustomCurveFromButlerScale(PointClickPlayerMovement butler, string roomId)
    {
        string cleanRoomId = CleanRoomId(roomId);

        if (butler == null ||
            string.IsNullOrWhiteSpace(cleanRoomId) ||
            !butler.TryGetButlerRoomScaleOverride(cleanRoomId, out PointClickPlayerMovement.ButlerRoomScaleOverrideData data) ||
            !data.IsComplete)
        {
            return false;
        }

        GuestRoomScaleEntry entry = GetOrCreateRoom(data.RoomId);
        entry.UseDepthCurve();
        entry.hasFront = true;
        entry.frontRoomLocalY = data.FrontRoomLocalFootY;
        entry.frontGuestScale = Mathf.Max(0.001f, data.FrontFinalLocalScaleY);
        entry.hasBack = true;
        entry.backRoomLocalY = data.BackRoomLocalFootY;
        entry.backGuestScale = Mathf.Max(0.001f, data.BackFinalLocalScaleY);
        return true;
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

    public void SetFront(string roomId, float roomLocalY, float guestScale)
    {
        GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.UseDepthCurve();
        entry.hasFront = true;
        entry.frontRoomLocalY = roomLocalY;
        entry.frontGuestScale = Mathf.Max(0.001f, guestScale);
    }

    public void SetBack(string roomId, float roomLocalY, float guestScale)
    {
        GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.UseDepthCurve();
        entry.hasBack = true;
        entry.backRoomLocalY = roomLocalY;
        entry.backGuestScale = Mathf.Max(0.001f, guestScale);
    }

    public void ClearCustomCurve(string roomId)
    {
        GuestRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.useCustomGuestCurve = false;
        entry.hasFront = false;
        entry.frontRoomLocalY = 0f;
        entry.frontGuestScale = 1f;
        entry.hasBack = false;
        entry.backRoomLocalY = 0f;
        entry.backGuestScale = 1f;

        if (entry.ResolvedScaleMode == GuestRoomScaleMode.DepthCurve)
        {
            entry.UseButlerCurve(entry.roomGuestScaleMultiplier);
        }
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
