using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DoorCameraSequence", menuName = "Dreadforge/Navigation/Door Camera Sequence")]
public class DoorCameraSequence : ScriptableObject
{
    public string promptText = "Open Door";
    public string startingRoom = "StorageCloset";
    public string loopStartRoom = "Hallway";
    public string[] roomOrder;

    public string StartingRoom => Clean(startingRoom);
    public string PromptText => string.IsNullOrWhiteSpace(promptText) ? "Open Door" : promptText.Trim();

    public bool TryGetNextRoom(string currentRoom, out string nextRoom)
    {
        nextRoom = string.Empty;

        if (roomOrder == null || roomOrder.Length == 0)
        {
            return false;
        }

        int currentIndex = FindRoomIndex(currentRoom);

        if (currentIndex < 0)
        {
            nextRoom = FirstValidRoom();
            return !string.IsNullOrEmpty(nextRoom);
        }

        if (currentIndex < roomOrder.Length - 1)
        {
            nextRoom = Clean(roomOrder[currentIndex + 1]);
            return !string.IsNullOrEmpty(nextRoom);
        }

        int loopIndex = FindRoomIndex(loopStartRoom);
        nextRoom = loopIndex >= 0 ? Clean(roomOrder[loopIndex]) : FirstValidRoom();
        return !string.IsNullOrEmpty(nextRoom);
    }

    public bool ContainsRoom(string roomName)
    {
        return FindRoomIndex(roomName) >= 0;
    }

    private int FindRoomIndex(string roomName)
    {
        string cleanRoomName = Clean(roomName);

        if (string.IsNullOrEmpty(cleanRoomName) || roomOrder == null)
        {
            return -1;
        }

        for (int i = 0; i < roomOrder.Length; i++)
        {
            if (string.Equals(Clean(roomOrder[i]), cleanRoomName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private string FirstValidRoom()
    {
        if (roomOrder == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < roomOrder.Length; i++)
        {
            string roomName = Clean(roomOrder[i]);

            if (!string.IsNullOrEmpty(roomName))
            {
                return roomName;
            }
        }

        return string.Empty;
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
