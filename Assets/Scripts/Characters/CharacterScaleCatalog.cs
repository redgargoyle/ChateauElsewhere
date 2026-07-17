using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterScaleRoomDefinition
{
    [SerializeField] private string roomName;
    [SerializeField] private float frontY;
    [SerializeField] private float frontScale = 1f;
    [SerializeField] private float backY;
    [SerializeField] private float backScale = 1f;

    public string RoomName => roomName;
    public float FrontY => frontY;
    public float FrontScale => frontScale;
    public float BackY => backY;
    public float BackScale => backScale;

    public CharacterScaleRoomDefinition()
    {
    }

    public CharacterScaleRoomDefinition(
        string definitionRoomName,
        float definitionFrontY,
        float definitionFrontScale,
        float definitionBackY,
        float definitionBackScale)
    {
        roomName = string.IsNullOrWhiteSpace(definitionRoomName)
            ? string.Empty
            : definitionRoomName.Trim();
        frontY = definitionFrontY;
        frontScale = definitionFrontScale;
        backY = definitionBackY;
        backScale = definitionBackScale;
    }

    public CharacterScaleRoomDefinition Copy()
    {
        return new CharacterScaleRoomDefinition(roomName, frontY, frontScale, backY, backScale);
    }

    public bool IsConfigured(out string reason)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            reason = "Room name is missing.";
            return false;
        }

        if (!IsFinite(frontY) || !IsFinite(backY))
        {
            reason = "Front and Back Y values must be finite.";
            return false;
        }

        if (Mathf.Approximately(frontY, backY))
        {
            reason = "Front and Back need different Y values.";
            return false;
        }

        if (!IsFinite(frontScale) || !IsFinite(backScale) || frontScale <= 0f || backScale <= 0f)
        {
            reason = "Front and Back scales must be finite and positive.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[CreateAssetMenu(
    fileName = "CharacterScaleCatalog",
    menuName = "Dreadforge/Characters/Character Scale Catalog")]
public sealed class CharacterScaleCatalog : ScriptableObject
{
    public const string DefaultResourcePath = "CharacterScaleCatalog";
    private const float MinimumScale = 0.0001f;

    [SerializeField, HideInInspector]
    private CharacterScaleRoomDefinition[] rooms = Array.Empty<CharacterScaleRoomDefinition>();

    private readonly Dictionary<string, CharacterScaleRoomDefinition> roomsByName =
        new Dictionary<string, CharacterScaleRoomDefinition>(StringComparer.OrdinalIgnoreCase);
    private static CharacterScaleCatalog defaultCatalog;

    public IReadOnlyList<CharacterScaleRoomDefinition> Rooms =>
        Array.AsReadOnly(rooms ?? Array.Empty<CharacterScaleRoomDefinition>());

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public static CharacterScaleCatalog LoadDefault()
    {
        if (defaultCatalog == null)
        {
            defaultCatalog = Resources.Load<CharacterScaleCatalog>(DefaultResourcePath);
        }

        return defaultCatalog;
    }

#if UNITY_EDITOR
    public void SetRooms(CharacterScaleRoomDefinition[] roomDefinitions)
    {
        if (roomDefinitions == null)
        {
            rooms = Array.Empty<CharacterScaleRoomDefinition>();
            RebuildLookup();
            return;
        }

        rooms = new CharacterScaleRoomDefinition[roomDefinitions.Length];

        for (int i = 0; i < roomDefinitions.Length; i++)
        {
            rooms[i] = roomDefinitions[i]?.Copy();
        }

        RebuildLookup();
    }

    public void SetRoom(CharacterScaleRoomDefinition roomDefinition)
    {
        if (roomDefinition == null)
        {
            return;
        }

        string normalizedName = NormalizeRoomName(roomDefinition.RoomName);

        for (int i = 0; i < rooms.Length; i++)
        {
            CharacterScaleRoomDefinition existing = rooms[i];

            if (existing != null && NormalizeRoomName(existing.RoomName) == normalizedName)
            {
                rooms[i] = roomDefinition.Copy();
                RebuildLookup();
                return;
            }
        }

        Array.Resize(ref rooms, rooms.Length + 1);
        rooms[rooms.Length - 1] = roomDefinition.Copy();
        RebuildLookup();
    }
#endif

    public bool TryGetRoom(string roomName, out CharacterScaleRoomDefinition roomDefinition)
    {
        if (roomsByName.Count == 0)
        {
            RebuildLookup();
        }

        return roomsByName.TryGetValue(NormalizeRoomName(roomName), out roomDefinition) &&
            roomDefinition != null;
    }

    public bool TryEvaluateScaleAtRoomY(string roomName, float characterRoomY, out float scale)
    {
        return TryEvaluateScaleAtRoomY(roomName, characterRoomY, 1f, out scale);
    }

    public bool TryEvaluateScaleAtRoomY(
        string roomName,
        float characterRoomY,
        float currentRoomStageScale,
        out float scale)
    {
        scale = 1f;

        if (!IsFinite(characterRoomY) ||
            !IsFinite(currentRoomStageScale) ||
            !TryGetRoom(roomName, out CharacterScaleRoomDefinition roomDefinition) ||
            !roomDefinition.IsConfigured(out _))
        {
            return false;
        }

        float authoredScale = CharacterScaleFunction.Evaluate(
            characterRoomY,
            roomDefinition.FrontY,
            roomDefinition.FrontScale,
            roomDefinition.BackY,
            roomDefinition.BackScale);
        float stageScale = Mathf.Max(MinimumScale, Mathf.Abs(currentRoomStageScale));
        scale = Mathf.Max(MinimumScale, authoredScale * stageScale);
        return true;
    }

    public bool ValidateCatalog(out string report)
    {
        RebuildLookup();
        List<string> problems = new List<string>();
        HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (rooms.Length == 0)
        {
            problems.Add("The catalog has no room definitions.");
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            CharacterScaleRoomDefinition roomDefinition = rooms[i];

            if (roomDefinition == null)
            {
                problems.Add($"Room slot {i + 1} is empty.");
                continue;
            }

            string normalizedName = NormalizeRoomName(roomDefinition.RoomName);

            if (string.IsNullOrEmpty(normalizedName))
            {
                problems.Add($"Room slot {i + 1} has no room name.");
            }
            else if (!seenNames.Add(normalizedName))
            {
                problems.Add($"Room '{roomDefinition.RoomName}' appears more than once.");
            }

            if (!roomDefinition.IsConfigured(out string reason))
            {
                problems.Add($"Room '{roomDefinition.RoomName}': {reason}");
            }
        }

        report = problems.Count == 0
            ? $"{rooms.Length} room scale definitions are valid."
            : string.Join("\n", problems);
        return problems.Count == 0;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static string NormalizeRoomName(string value)
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

    private void RebuildLookup()
    {
        roomsByName.Clear();

        if (rooms == null)
        {
            rooms = Array.Empty<CharacterScaleRoomDefinition>();
            return;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            CharacterScaleRoomDefinition roomDefinition = rooms[i];

            if (roomDefinition == null)
            {
                continue;
            }

            string normalizedName = NormalizeRoomName(roomDefinition.RoomName);

            if (!string.IsNullOrEmpty(normalizedName) && !roomsByName.ContainsKey(normalizedName))
            {
                roomsByName.Add(normalizedName, roomDefinition);
            }
        }
    }
}
