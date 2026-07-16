using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Dreadforge/Characters/Character Scale Catalog")]
public sealed class CharacterScaleCatalog : MonoBehaviour
{
    [SerializeField] private CharacterScaleRoom[] rooms = Array.Empty<CharacterScaleRoom>();

    private readonly Dictionary<string, CharacterScaleRoom> roomsByName =
        new Dictionary<string, CharacterScaleRoom>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<CharacterScaleRoom> Rooms => rooms;

    private void Awake()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public void SetRooms(CharacterScaleRoom[] roomDefinitions)
    {
        rooms = roomDefinitions ?? Array.Empty<CharacterScaleRoom>();
        RebuildLookup();
    }

    public bool TryGetRoom(string roomName, out CharacterScaleRoom roomDefinition)
    {
        if (roomsByName.Count == 0)
        {
            RebuildLookup();
        }

        return roomsByName.TryGetValue(NormalizeRoomName(roomName), out roomDefinition) &&
            roomDefinition != null;
    }

    public bool TryEvaluateScale(string roomName, Vector3 characterWorldPosition, out float scale)
    {
        scale = 1f;
        return TryGetRoom(roomName, out CharacterScaleRoom roomDefinition) &&
            roomDefinition.TryEvaluateScale(characterWorldPosition, out scale);
    }

    public bool TryEvaluateScaleAtRoomY(string roomName, float characterRoomY, out float scale)
    {
        scale = 1f;
        return TryGetRoom(roomName, out CharacterScaleRoom roomDefinition) &&
            roomDefinition.TryEvaluateScaleAtRoomY(characterRoomY, out scale);
    }

    public bool ValidateCatalog(out string report)
    {
        RebuildLookup();
        List<string> problems = new List<string>();
        HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < rooms.Length; i++)
        {
            CharacterScaleRoom roomDefinition = rooms[i];

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

    private void RebuildLookup()
    {
        roomsByName.Clear();

        if (rooms == null)
        {
            rooms = Array.Empty<CharacterScaleRoom>();
            return;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            CharacterScaleRoom roomDefinition = rooms[i];

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
}
