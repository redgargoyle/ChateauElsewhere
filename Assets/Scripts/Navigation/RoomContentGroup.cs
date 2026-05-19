using System;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomContentGroup : MonoBehaviour
{
    // Attach this to one root GameObject per room. When RoomNavigationManager's
    // currentRoom changes, it activates the matching RoomContentGroup and
    // deactivates the others, so each room can carry its own images, doors,
    // animators, sounds, and other preloaded content.
    [Header("Room")]
    [SerializeField] private string roomName;
    [SerializeField] private Texture roomBackgroundTexture;

    public string RoomName => GetEffectiveRoomName();
    public Texture RoomBackgroundTexture => roomBackgroundTexture;

    private void Reset()
    {
        FillRoomNameFromObject();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            FillRoomNameFromObject();
        }
    }

    public void RefreshInferredRoomName()
    {
        FillRoomNameFromObject();
    }

    public void SetRoomName(string value)
    {
        roomName = string.IsNullOrWhiteSpace(value) ? ParseRoomNameFromObject(gameObject.name) : value.Trim();
    }

    public void SetRoomBackgroundTexture(Texture texture)
    {
        roomBackgroundTexture = texture;
    }

    public bool TryGetRoomBackgroundTexture(out Texture texture)
    {
        texture = roomBackgroundTexture;
        return texture != null;
    }

    private string GetEffectiveRoomName()
    {
        if (!string.IsNullOrWhiteSpace(roomName))
        {
            return roomName.Trim();
        }

        return ParseRoomNameFromObject(gameObject.name);
    }

    private void FillRoomNameFromObject()
    {
        roomName = ParseRoomNameFromObject(gameObject.name);
    }

    private static string ParseRoomNameFromObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return string.Empty;
        }

        string cleanName = objectName.Trim();

        // This lets a room object be named either "Room_StorageCloset" or just
        // "StorageCloset". "Cam_" is accepted only for old scenes from before
        // rooms were split away from map camera buttons.
        if (cleanName.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Room_".Length);
        }
        else if (cleanName.StartsWith("Cam_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Cam_".Length);
        }

        return cleanName.Replace('_', ' ').Trim();
    }
}
