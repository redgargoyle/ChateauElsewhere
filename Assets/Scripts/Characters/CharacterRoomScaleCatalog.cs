using System;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterScaleProfile
{
    Auto = 0,
    Butler = 1,
    Guest = 2
}

[Serializable]
public sealed class CharacterRoomScaleEntry
{
    private const float MinimumScale = 0.001f;
    private const float MinimumEndpointDistance = 0.01f;

    public string roomId;
    public bool enabled = true;

    [Header("Room-local foot Y endpoints")]
    public float frontRoomLocalFootY;
    public float backRoomLocalFootY;

    [Header("Butler-calibrated final displayed local scale Y")]
    [Min(MinimumScale)] public float frontFinalLocalScaleY = 1f;
    [Min(MinimumScale)] public float backFinalLocalScaleY = 1f;

    [Tooltip("Maps the normalized front-to-back room depth before the endpoint sizes are interpolated.")]
    public AnimationCurve scaleFunction = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Room-stage scale at the time these endpoint sizes were calibrated. Used only to keep displayed size stable while the room stage zoom changes.")]
    public bool hasReferenceRoomStageScale;
    [Min(0.0001f)] public float referenceRoomStageScale = 1f;

    public CharacterRoomScaleEntry()
    {
        roomId = string.Empty;
    }

    public CharacterRoomScaleEntry(string id)
    {
        roomId = CharacterRoomScaleCatalog.CleanRoomId(id);
    }

    public bool Matches(string otherRoomId)
    {
        return CharacterRoomScaleCatalog.SameRoom(roomId, otherRoomId);
    }

    public bool HasUsableEndpoints =>
        Mathf.Abs(frontRoomLocalFootY - backRoomLocalFootY) >= MinimumEndpointDistance;

    public float GetFrontScale()
    {
        return Mathf.Max(MinimumScale, frontFinalLocalScaleY);
    }

    public float GetBackScale()
    {
        return Mathf.Max(MinimumScale, backFinalLocalScaleY);
    }

    public void SetFront(float roomLocalFootY, float finalLocalScaleY)
    {
        frontRoomLocalFootY = roomLocalFootY;
        frontFinalLocalScaleY = Mathf.Max(MinimumScale, Mathf.Abs(finalLocalScaleY));
    }

    public void SetBack(float roomLocalFootY, float finalLocalScaleY)
    {
        backRoomLocalFootY = roomLocalFootY;
        backFinalLocalScaleY = Mathf.Max(MinimumScale, Mathf.Abs(finalLocalScaleY));
    }

    public bool TryEvaluate(
        float roomLocalFootY,
        out float finalLocalScaleY,
        out float frontToBack01)
    {
        finalLocalScaleY = 1f;
        frontToBack01 = 0f;

        if (!enabled || !HasUsableEndpoints)
        {
            return false;
        }

        frontToBack01 = Mathf.Clamp01(
            Mathf.InverseLerp(frontRoomLocalFootY, backRoomLocalFootY, roomLocalFootY));
        float curvedDepth = scaleFunction != null && scaleFunction.length > 0
            ? Mathf.Clamp01(scaleFunction.Evaluate(frontToBack01))
            : frontToBack01;
        finalLocalScaleY = Mathf.Lerp(GetFrontScale(), GetBackScale(), curvedDepth);
        finalLocalScaleY = Mathf.Max(MinimumScale, finalLocalScaleY);
        return true;
    }

    public void Sanitize()
    {
        roomId = CharacterRoomScaleCatalog.CleanRoomId(roomId);
        frontFinalLocalScaleY = Mathf.Max(MinimumScale, Mathf.Abs(frontFinalLocalScaleY));
        backFinalLocalScaleY = Mathf.Max(MinimumScale, Mathf.Abs(backFinalLocalScaleY));
        referenceRoomStageScale = hasReferenceRoomStageScale
            ? Mathf.Max(0.0001f, referenceRoomStageScale)
            : 1f;
        scaleFunction ??= AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
}

public readonly struct CharacterRoomScaleSample
{
    public CharacterRoomScaleSample(
        string roomId,
        CharacterScaleProfile profile,
        float roomLocalFootY,
        float frontToBack01,
        float finalLocalScaleY)
    {
        RoomId = CharacterRoomScaleCatalog.CleanRoomId(roomId);
        Profile = profile;
        RoomLocalFootY = roomLocalFootY;
        FrontToBack01 = Mathf.Clamp01(frontToBack01);
        FinalLocalScaleY = Mathf.Max(0.001f, finalLocalScaleY);
    }

    public string RoomId { get; }
    public CharacterScaleProfile Profile { get; }
    public float RoomLocalFootY { get; }
    public float FrontToBack01 { get; }
    public float FinalLocalScaleY { get; }
}

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Character Room Scale Catalog")]
public sealed class CharacterRoomScaleCatalog : MonoBehaviour
{
    [SerializeField] private bool enableCharacterRoomScaling = true;
    [SerializeField] private List<CharacterRoomScaleEntry> rooms = new List<CharacterRoomScaleEntry>();
    [NonSerialized] private int runtimeRevision;

    public bool EnableCharacterRoomScaling => enableCharacterRoomScaling;
    public List<CharacterRoomScaleEntry> Rooms => rooms;
    public int RuntimeRevision => runtimeRevision;

    private void OnEnable()
    {
        SanitizeRooms();
    }

    private void OnValidate()
    {
        SanitizeRooms();
        MarkChanged();
    }

    public void SetCharacterRoomScalingEnabled(bool value)
    {
        if (enableCharacterRoomScaling == value)
        {
            return;
        }

        enableCharacterRoomScaling = value;
        MarkChanged();
    }

    public CharacterRoomScaleEntry GetOrCreateRoom(string roomId)
    {
        string cleanRoomId = CleanRoomId(roomId);

        if (string.IsNullOrWhiteSpace(cleanRoomId))
        {
            cleanRoomId = "Room";
        }

        rooms ??= new List<CharacterRoomScaleEntry>();

        for (int i = 0; i < rooms.Count; i++)
        {
            CharacterRoomScaleEntry entry = rooms[i];

            if (entry != null && entry.Matches(cleanRoomId))
            {
                entry.Sanitize();
                return entry;
            }
        }

        CharacterRoomScaleEntry created = new CharacterRoomScaleEntry(cleanRoomId);
        rooms.Add(created);
        MarkChanged();
        return created;
    }

    public bool TryGetRoom(string roomId, out CharacterRoomScaleEntry entry)
    {
        entry = null;

        if (rooms == null || string.IsNullOrWhiteSpace(roomId))
        {
            return false;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            CharacterRoomScaleEntry candidate = rooms[i];

            if (candidate != null && candidate.Matches(roomId))
            {
                candidate.Sanitize();
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryEvaluate(
        string roomId,
        CharacterScaleProfile profile,
        float roomLocalFootY,
        out CharacterRoomScaleSample sample)
    {
        sample = default;

        if (!enableCharacterRoomScaling ||
            !TryGetRoom(roomId, out CharacterRoomScaleEntry entry) ||
            !entry.TryEvaluate(roomLocalFootY, out float scaleY, out float depth01))
        {
            return false;
        }

        sample = new CharacterRoomScaleSample(
            entry.roomId,
            ResolveConcreteProfile(profile),
            roomLocalFootY,
            depth01,
            scaleY);
        return true;
    }

    public void SetFront(
        string roomId,
        float roomLocalFootY,
        float finalLocalScaleY)
    {
        CharacterRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.SetFront(roomLocalFootY, finalLocalScaleY);
        entry.enabled = true;
        MarkChanged();
    }

    public void SetBack(
        string roomId,
        float roomLocalFootY,
        float finalLocalScaleY)
    {
        CharacterRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.SetBack(roomLocalFootY, finalLocalScaleY);
        entry.enabled = true;
        MarkChanged();
    }

    public void SetReferenceRoomStageScale(string roomId, float stageScale)
    {
        CharacterRoomScaleEntry entry = GetOrCreateRoom(roomId);
        entry.hasReferenceRoomStageScale = true;
        entry.referenceRoomStageScale = Mathf.Max(0.0001f, stageScale);
        MarkChanged();
    }

    public bool TryGetReferenceRoomStageScale(string roomId, out float stageScale)
    {
        stageScale = 1f;

        if (!TryGetRoom(roomId, out CharacterRoomScaleEntry entry) ||
            !entry.hasReferenceRoomStageScale)
        {
            return false;
        }

        stageScale = Mathf.Max(0.0001f, entry.referenceRoomStageScale);
        return true;
    }

    public void GetRoomIds(List<string> results)
    {
        if (results == null || rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            CharacterRoomScaleEntry entry = rooms[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.roomId))
            {
                continue;
            }

            bool alreadyAdded = false;

            for (int resultIndex = 0; resultIndex < results.Count; resultIndex++)
            {
                if (SameRoom(results[resultIndex], entry.roomId))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                results.Add(entry.roomId);
            }
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
            CharacterRoomScaleEntry entry = rooms[i];

            if (entry != null && entry.Matches(roomId))
            {
                rooms.RemoveAt(i);
                MarkChanged();
                return true;
            }
        }

        return false;
    }

    public void MarkChanged()
    {
        unchecked
        {
            runtimeRevision++;
        }
    }

    public static CharacterRoomScaleCatalog FindInScene()
    {
        return FindAnyObjectByType<CharacterRoomScaleCatalog>(FindObjectsInactive.Include);
    }

    public static CharacterScaleProfile ResolveConcreteProfile(CharacterScaleProfile profile)
    {
        return profile == CharacterScaleProfile.Guest
            ? CharacterScaleProfile.Guest
            : CharacterScaleProfile.Butler;
    }

    public static bool SameRoom(string left, string right)
    {
        return string.Equals(
            NormalizeRoomName(left),
            NormalizeRoomName(right),
            StringComparison.OrdinalIgnoreCase);
    }

    public static string CleanRoomId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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

    private void SanitizeRooms()
    {
        rooms ??= new List<CharacterRoomScaleEntry>();

        for (int i = rooms.Count - 1; i >= 0; i--)
        {
            CharacterRoomScaleEntry entry = rooms[i];

            if (entry == null)
            {
                rooms.RemoveAt(i);
                continue;
            }

            entry.Sanitize();
        }
    }
}
