using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterDisplayScaleOverride
{
    [SerializeField] private CharacterDisplayId characterId;
    [SerializeField] private Vector2 frontPreviewPosition;
    [SerializeField] private Vector2 backPreviewPosition;
    [SerializeField] private float frontScale = 1f;
    [SerializeField] private float backScale = 1f;
    [SerializeField] private AnimationCurve yToScaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private bool enabled = true;

    public CharacterDisplayId CharacterId => characterId;
    public Vector2 FrontPreviewPosition => frontPreviewPosition;
    public Vector2 BackPreviewPosition => backPreviewPosition;
    public float FrontScale => frontScale;
    public float BackScale => backScale;
    public AnimationCurve YToScaleCurve => yToScaleCurve;
    public bool Enabled => enabled;

    public CharacterDisplayScaleOverride()
    {
    }

    public CharacterDisplayScaleOverride(
        CharacterDisplayId id,
        Vector2 frontPosition,
        float authoredFrontScale,
        Vector2 backPosition,
        float authoredBackScale,
        AnimationCurve curve,
        bool isEnabled = true)
    {
        characterId = id;
        frontPreviewPosition = frontPosition;
        frontScale = authoredFrontScale;
        backPreviewPosition = backPosition;
        backScale = authoredBackScale;
        yToScaleCurve = CharacterDisplayScaleCatalog.CopyCurve(curve);
        enabled = isEnabled;
    }

    public CharacterDisplayScaleOverride Copy()
    {
        return new CharacterDisplayScaleOverride(
            characterId,
            frontPreviewPosition,
            frontScale,
            backPreviewPosition,
            backScale,
            yToScaleCurve,
            enabled);
    }

    public bool IsConfigured(out string reason)
    {
        return CharacterDisplayScaleCatalog.ValidateCalibration(
            frontPreviewPosition,
            frontScale,
            backPreviewPosition,
            backScale,
            yToScaleCurve,
            out reason);
    }
}

[Serializable]
public sealed class CharacterDisplayStateScaleOverride
{
    [SerializeField] private bool enabled;
    [SerializeField] private float scale = 1f;

    public bool Enabled => enabled;
    public float Scale => scale;

    public CharacterDisplayStateScaleOverride()
    {
    }

    public CharacterDisplayStateScaleOverride(bool isEnabled, float displayScale)
    {
        enabled = isEnabled;
        scale = displayScale;
    }

    public CharacterDisplayStateScaleOverride Copy()
    {
        return new CharacterDisplayStateScaleOverride(enabled, scale);
    }

    public bool IsConfigured(out string reason)
    {
        if (!enabled)
        {
            reason = string.Empty;
            return true;
        }

        if (!CharacterDisplayScaleCatalog.IsFinitePositive(scale))
        {
            reason = "The enabled seated scale must be finite and positive.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class RoomStateScaleOverrides
{
    [SerializeField] private CharacterDisplayStateScaleOverride drawingRoomSeated =
        new CharacterDisplayStateScaleOverride(false, 1f);
    [SerializeField] private CharacterDisplayStateScaleOverride diningRoomSeated =
        new CharacterDisplayStateScaleOverride(false, 1f);

    public CharacterDisplayStateScaleOverride DrawingRoomSeated =>
        drawingRoomSeated ?? (drawingRoomSeated = new CharacterDisplayStateScaleOverride(false, 1f));
    public CharacterDisplayStateScaleOverride DiningRoomSeated =>
        diningRoomSeated ?? (diningRoomSeated = new CharacterDisplayStateScaleOverride(false, 1f));

    public RoomStateScaleOverrides()
    {
    }

    public RoomStateScaleOverrides(
        CharacterDisplayStateScaleOverride drawingOverride,
        CharacterDisplayStateScaleOverride diningOverride)
    {
        drawingRoomSeated = drawingOverride?.Copy() ?? new CharacterDisplayStateScaleOverride(false, 1f);
        diningRoomSeated = diningOverride?.Copy() ?? new CharacterDisplayStateScaleOverride(false, 1f);
    }

    public RoomStateScaleOverrides Copy()
    {
        return new RoomStateScaleOverrides(DrawingRoomSeated, DiningRoomSeated);
    }
}

[Serializable]
public sealed class RoomDisplayScaleEntry
{
    [SerializeField] private string roomId;
    [SerializeField] private Vector2 frontPreviewPosition;
    [SerializeField] private Vector2 backPreviewPosition;
    [SerializeField] private float frontScale = 1f;
    [SerializeField] private float backScale = 1f;
    [SerializeField] private AnimationCurve yToScaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private List<CharacterDisplayScaleOverride> characterOverrides =
        new List<CharacterDisplayScaleOverride>();
    [SerializeField] private RoomStateScaleOverrides stateOverrides = new RoomStateScaleOverrides();

    public string RoomId => roomId;
    public Vector2 FrontPreviewPosition => frontPreviewPosition;
    public Vector2 BackPreviewPosition => backPreviewPosition;
    public float FrontScale => frontScale;
    public float BackScale => backScale;
    public AnimationCurve YToScaleCurve => yToScaleCurve;
    public IReadOnlyList<CharacterDisplayScaleOverride> CharacterOverrides =>
        (characterOverrides ?? (characterOverrides = new List<CharacterDisplayScaleOverride>())).AsReadOnly();
    public RoomStateScaleOverrides StateOverrides =>
        stateOverrides ?? (stateOverrides = new RoomStateScaleOverrides());

    public RoomDisplayScaleEntry()
    {
    }

    public RoomDisplayScaleEntry(
        string id,
        Vector2 frontPosition,
        float authoredFrontScale,
        Vector2 backPosition,
        float authoredBackScale,
        AnimationCurve curve,
        IEnumerable<CharacterDisplayScaleOverride> overrides = null,
        RoomStateScaleOverrides authoredStateOverrides = null)
    {
        roomId = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        frontPreviewPosition = frontPosition;
        frontScale = authoredFrontScale;
        backPreviewPosition = backPosition;
        backScale = authoredBackScale;
        yToScaleCurve = CharacterDisplayScaleCatalog.CopyCurve(curve);
        characterOverrides = new List<CharacterDisplayScaleOverride>();

        if (overrides != null)
        {
            foreach (CharacterDisplayScaleOverride scaleOverride in overrides)
            {
                if (scaleOverride != null)
                {
                    characterOverrides.Add(scaleOverride.Copy());
                }
            }
        }

        stateOverrides = authoredStateOverrides?.Copy() ?? new RoomStateScaleOverrides();
    }

    public RoomDisplayScaleEntry Copy()
    {
        return new RoomDisplayScaleEntry(
            roomId,
            frontPreviewPosition,
            frontScale,
            backPreviewPosition,
            backScale,
            yToScaleCurve,
            characterOverrides,
            StateOverrides);
    }

    public bool TryGetCharacterOverride(
        CharacterDisplayId characterId,
        out CharacterDisplayScaleOverride scaleOverride)
    {
        if (characterOverrides != null)
        {
            for (int i = 0; i < characterOverrides.Count; i++)
            {
                CharacterDisplayScaleOverride candidate = characterOverrides[i];

                if (candidate != null && candidate.CharacterId == characterId)
                {
                    scaleOverride = candidate;
                    return true;
                }
            }
        }

        scaleOverride = null;
        return false;
    }

    public bool IsConfigured(out string reason)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            reason = "Room id is missing.";
            return false;
        }

        if (!CharacterDisplayScaleCatalog.ValidateCalibration(
                frontPreviewPosition,
                frontScale,
                backPreviewPosition,
                backScale,
                yToScaleCurve,
                out reason))
        {
            return false;
        }

        HashSet<CharacterDisplayId> seenIds = new HashSet<CharacterDisplayId>();

        if (characterOverrides != null)
        {
            for (int i = 0; i < characterOverrides.Count; i++)
            {
                CharacterDisplayScaleOverride scaleOverride = characterOverrides[i];

                if (scaleOverride == null)
                {
                    reason = $"Character override slot {i + 1} is empty.";
                    return false;
                }

                if (!seenIds.Add(scaleOverride.CharacterId))
                {
                    reason = $"Character override '{scaleOverride.CharacterId}' appears more than once.";
                    return false;
                }

                if (scaleOverride.Enabled && !scaleOverride.IsConfigured(out string overrideReason))
                {
                    reason = $"Character override '{scaleOverride.CharacterId}': {overrideReason}";
                    return false;
                }
            }
        }

        if (!StateOverrides.DrawingRoomSeated.IsConfigured(out string drawingReason))
        {
            reason = $"Drawing Room seated override: {drawingReason}";
            return false;
        }

        if (!StateOverrides.DiningRoomSeated.IsConfigured(out string diningReason))
        {
            reason = $"Dining Room seated override: {diningReason}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void SetDefaultCalibration(
        Vector2 frontPosition,
        float authoredFrontScale,
        Vector2 backPosition,
        float authoredBackScale,
        AnimationCurve curve)
    {
        frontPreviewPosition = frontPosition;
        frontScale = authoredFrontScale;
        backPreviewPosition = backPosition;
        backScale = authoredBackScale;
        yToScaleCurve = CharacterDisplayScaleCatalog.CopyCurve(curve);
    }

    public void SetCharacterOverride(CharacterDisplayScaleOverride replacement)
    {
        if (replacement == null)
        {
            return;
        }

        if (characterOverrides == null)
        {
            characterOverrides = new List<CharacterDisplayScaleOverride>();
        }

        for (int i = 0; i < characterOverrides.Count; i++)
        {
            CharacterDisplayScaleOverride existing = characterOverrides[i];

            if (existing != null && existing.CharacterId == replacement.CharacterId)
            {
                characterOverrides[i] = replacement.Copy();
                return;
            }
        }

        characterOverrides.Add(replacement.Copy());
    }

    public bool RemoveCharacterOverride(CharacterDisplayId characterId)
    {
        if (characterOverrides == null)
        {
            return false;
        }

        for (int i = 0; i < characterOverrides.Count; i++)
        {
            CharacterDisplayScaleOverride existing = characterOverrides[i];

            if (existing != null && existing.CharacterId == characterId)
            {
                characterOverrides.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public void SetStateOverrides(RoomStateScaleOverrides overrides)
    {
        stateOverrides = overrides?.Copy() ?? new RoomStateScaleOverrides();
    }
}

[CreateAssetMenu(
    fileName = "CharacterDisplayScaleCatalog",
    menuName = "Dreadforge/Characters/Character Display Scale Catalog")]
public sealed class CharacterDisplayScaleCatalog : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/_Chateau/Data/Resources/CharacterDisplayScaleCatalog.asset";
    public const string DefaultResourcePath = "CharacterDisplayScaleCatalog";
    public const string DrawingRoomId = "Drawing Room";
    public const string DiningRoomId = "Dining Room";
    private const float MinimumScale = 0.0001f;

    [SerializeField] private List<RoomDisplayScaleEntry> rooms = new List<RoomDisplayScaleEntry>();

    private readonly Dictionary<string, RoomDisplayScaleEntry> roomsById =
        new Dictionary<string, RoomDisplayScaleEntry>(StringComparer.OrdinalIgnoreCase);
    private static CharacterDisplayScaleCatalog defaultCatalog;

    public IReadOnlyList<RoomDisplayScaleEntry> Rooms =>
        (rooms ?? (rooms = new List<RoomDisplayScaleEntry>())).AsReadOnly();

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public static CharacterDisplayScaleCatalog LoadDefault()
    {
        if (defaultCatalog == null)
        {
            defaultCatalog = Resources.Load<CharacterDisplayScaleCatalog>(DefaultResourcePath);
        }

        return defaultCatalog;
    }

    public bool TryGetRoom(string roomId, out RoomDisplayScaleEntry room)
    {
        if (roomsById.Count == 0)
        {
            RebuildLookup();
        }

        return roomsById.TryGetValue(NormalizeRoomId(roomId), out room) && room != null;
    }

    public bool TryEvaluateScale(
        string roomId,
        CharacterDisplayId characterId,
        CharacterDisplayState displayState,
        float currentRoomLocalFootY,
        out float scale)
    {
        scale = 1f;

        if (!IsFinite(currentRoomLocalFootY) ||
            !TryGetRoom(roomId, out RoomDisplayScaleEntry room) ||
            !room.IsConfigured(out _))
        {
            return false;
        }

        if (TryEvaluateAllowedStateOverride(room, roomId, displayState, out float stateScale))
        {
            scale = stateScale;
            return true;
        }

        if (room.TryGetCharacterOverride(characterId, out CharacterDisplayScaleOverride scaleOverride) &&
            scaleOverride.Enabled)
        {
            return TryEvaluateCalibration(
                scaleOverride.FrontPreviewPosition,
                scaleOverride.FrontScale,
                scaleOverride.BackPreviewPosition,
                scaleOverride.BackScale,
                scaleOverride.YToScaleCurve,
                currentRoomLocalFootY,
                out scale);
        }

        return TryEvaluateCalibration(
            room.FrontPreviewPosition,
            room.FrontScale,
            room.BackPreviewPosition,
            room.BackScale,
            room.YToScaleCurve,
            currentRoomLocalFootY,
            out scale);
    }

    public static bool TryEvaluateCalibration(
        Vector2 frontPreviewPosition,
        float frontScale,
        Vector2 backPreviewPosition,
        float backScale,
        AnimationCurve yToScaleCurve,
        float currentRoomLocalFootY,
        out float scale)
    {
        scale = 1f;

        if (!IsFinite(currentRoomLocalFootY) ||
            !ValidateCalibration(
                frontPreviewPosition,
                frontScale,
                backPreviewPosition,
                backScale,
                yToScaleCurve,
                out _))
        {
            return false;
        }

        float depth01 = Mathf.Clamp01(Mathf.InverseLerp(
            frontPreviewPosition.y,
            backPreviewPosition.y,
            currentRoomLocalFootY));
        float curvedT = Mathf.Clamp01(yToScaleCurve.Evaluate(depth01));
        scale = Mathf.Max(MinimumScale, Mathf.Lerp(frontScale, backScale, curvedT));
        return true;
    }

    public bool ValidateCatalog(out string report)
    {
        RebuildLookup();
        List<string> problems = new List<string>();
        HashSet<string> seenRoomIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (rooms == null || rooms.Count == 0)
        {
            problems.Add("The catalog has no room entries.");
        }
        else
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomDisplayScaleEntry room = rooms[i];

                if (room == null)
                {
                    problems.Add($"Room slot {i + 1} is empty.");
                    continue;
                }

                string normalizedId = NormalizeRoomId(room.RoomId);

                if (string.IsNullOrEmpty(normalizedId))
                {
                    problems.Add($"Room slot {i + 1} has no room id.");
                }
                else if (!seenRoomIds.Add(normalizedId))
                {
                    problems.Add($"Room '{room.RoomId}' appears more than once.");
                }

                if (!room.IsConfigured(out string reason))
                {
                    problems.Add($"Room '{room.RoomId}': {reason}");
                }

                if (room.StateOverrides.DrawingRoomSeated.Enabled &&
                    normalizedId != NormalizeRoomId(DrawingRoomId))
                {
                    problems.Add($"Room '{room.RoomId}' enables the Drawing Room-only seated override.");
                }

                if (room.StateOverrides.DiningRoomSeated.Enabled &&
                    normalizedId != NormalizeRoomId(DiningRoomId))
                {
                    problems.Add($"Room '{room.RoomId}' enables the Dining Room-only seated override.");
                }
            }
        }

        report = problems.Count == 0
            ? $"{rooms.Count} room display-scale entries are valid."
            : string.Join("\n", problems);
        return problems.Count == 0;
    }

    public void SetRooms(IEnumerable<RoomDisplayScaleEntry> roomEntries)
    {
        rooms = new List<RoomDisplayScaleEntry>();

        if (roomEntries != null)
        {
            foreach (RoomDisplayScaleEntry room in roomEntries)
            {
                if (room != null)
                {
                    rooms.Add(room.Copy());
                }
            }
        }

        RebuildLookup();
    }

    public void SetRoom(RoomDisplayScaleEntry replacement)
    {
        if (replacement == null)
        {
            return;
        }

        if (rooms == null)
        {
            rooms = new List<RoomDisplayScaleEntry>();
        }

        string replacementId = NormalizeRoomId(replacement.RoomId);

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomDisplayScaleEntry existing = rooms[i];

            if (existing != null && NormalizeRoomId(existing.RoomId) == replacementId)
            {
                rooms[i] = replacement.Copy();
                RebuildLookup();
                return;
            }
        }

        rooms.Add(replacement.Copy());
        RebuildLookup();
    }

    public static bool ValidateCalibration(
        Vector2 frontPreviewPosition,
        float frontScale,
        Vector2 backPreviewPosition,
        float backScale,
        AnimationCurve yToScaleCurve,
        out string reason)
    {
        if (!IsFinite(frontPreviewPosition.x) ||
            !IsFinite(frontPreviewPosition.y) ||
            !IsFinite(backPreviewPosition.x) ||
            !IsFinite(backPreviewPosition.y))
        {
            reason = "Front and Back preview positions must be finite.";
            return false;
        }

        if (Mathf.Approximately(frontPreviewPosition.y, backPreviewPosition.y))
        {
            reason = "Front and Back preview Y values must be different.";
            return false;
        }

        if (!IsFinitePositive(frontScale) || !IsFinitePositive(backScale))
        {
            reason = "Front and Back scales must be finite and positive.";
            return false;
        }

        if (!IsValidCurve(yToScaleCurve))
        {
            reason = "The Y-to-scale curve needs at least two finite keys.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static string NormalizeRoomId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] normalized = new char[value.Length];
        int count = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (char.IsLetterOrDigit(character))
            {
                normalized[count++] = char.ToLowerInvariant(character);
            }
        }

        return new string(normalized, 0, count);
    }

    internal static AnimationCurve CopyCurve(AnimationCurve source)
    {
        AnimationCurve copy = source != null
            ? new AnimationCurve(source.keys)
            : AnimationCurve.Linear(0f, 0f, 1f, 1f);

        if (source != null)
        {
            copy.preWrapMode = source.preWrapMode;
            copy.postWrapMode = source.postWrapMode;
        }

        return copy;
    }

    internal static bool IsFinitePositive(float value)
    {
        return IsFinite(value) && value > 0f;
    }

    private static bool TryEvaluateAllowedStateOverride(
        RoomDisplayScaleEntry room,
        string roomId,
        CharacterDisplayState displayState,
        out float scale)
    {
        scale = 1f;
        string normalizedRoomId = NormalizeRoomId(roomId);
        CharacterDisplayStateScaleOverride stateOverride = null;

        if (displayState == CharacterDisplayState.DrawingRoomSeated &&
            normalizedRoomId == NormalizeRoomId(DrawingRoomId))
        {
            stateOverride = room.StateOverrides.DrawingRoomSeated;
        }
        else if (displayState == CharacterDisplayState.DiningRoomSeated &&
            normalizedRoomId == NormalizeRoomId(DiningRoomId))
        {
            stateOverride = room.StateOverrides.DiningRoomSeated;
        }

        if (stateOverride == null || !stateOverride.Enabled || !stateOverride.IsConfigured(out _))
        {
            return false;
        }

        scale = Mathf.Max(MinimumScale, stateOverride.Scale);
        return true;
    }

    private static bool IsValidCurve(AnimationCurve curve)
    {
        if (curve == null || curve.length < 2)
        {
            return false;
        }

        Keyframe[] keys = curve.keys;

        for (int i = 0; i < keys.Length; i++)
        {
            if (!IsFinite(keys[i].time) || !IsFinite(keys[i].value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void RebuildLookup()
    {
        roomsById.Clear();

        if (rooms == null)
        {
            rooms = new List<RoomDisplayScaleEntry>();
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomDisplayScaleEntry room = rooms[i];

            if (room == null)
            {
                continue;
            }

            string normalizedId = NormalizeRoomId(room.RoomId);

            if (!string.IsNullOrEmpty(normalizedId) && !roomsById.ContainsKey(normalizedId))
            {
                roomsById.Add(normalizedId, room);
            }
        }
    }
}
