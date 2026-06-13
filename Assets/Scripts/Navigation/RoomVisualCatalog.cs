using System;
using UnityEngine;

[CreateAssetMenu(fileName = "RoomVisualCatalog", menuName = "ChataeuChatilly/Navigation/Room Visual Catalog")]
public class RoomVisualCatalog : ScriptableObject
{
    public RoomVisualEntry[] rooms;

    public bool TryGetRoomTexture(string roomName, out Texture texture)
    {
        texture = null;

        if (string.IsNullOrWhiteSpace(roomName) || rooms == null)
        {
            return false;
        }

        string normalizedRoomName = roomName.Trim();

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomVisualEntry entry = rooms[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.roomName))
            {
                continue;
            }

            if (string.Equals(entry.roomName.Trim(), normalizedRoomName, StringComparison.OrdinalIgnoreCase))
            {
                texture = entry.backgroundTexture;
                return texture != null;
            }
        }

        return false;
    }
}

[Serializable]
public class RoomVisualEntry
{
    public string roomName;
    public Texture backgroundTexture;
}
