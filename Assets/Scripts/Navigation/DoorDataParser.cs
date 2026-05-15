using System;
using System.Collections.Generic;

public sealed class RoomDefinition
{
    private readonly List<DoorRoute> doors = new List<DoorRoute>();

    public RoomDefinition(string roomName)
    {
        RoomName = roomName;
    }

    public string RoomName { get; private set; }
    public IReadOnlyList<DoorRoute> Doors => doors;

    public void AddDoor(DoorRoute route)
    {
        if (route != null)
        {
            doors.Add(route);
        }
    }
}

public sealed class DoorRoute
{
    public DoorRoute(string doorId, string sourceRoom, string destinationRoom)
    {
        DoorId = doorId;
        SourceRoom = sourceRoom;
        DestinationRoom = destinationRoom;
    }

    public string DoorId { get; private set; }
    public string SourceRoom { get; private set; }
    public string DestinationRoom { get; private set; }
}

public sealed class DoorDataParseResult
{
    public DoorDataParseResult()
    {
        RoomsByName = new Dictionary<string, RoomDefinition>(StringComparer.OrdinalIgnoreCase);
        RoutesByDoorId = new Dictionary<string, DoorRoute>(StringComparer.OrdinalIgnoreCase);
        Errors = new List<string>();
        Warnings = new List<string>();
    }

    public Dictionary<string, RoomDefinition> RoomsByName { get; private set; }
    public Dictionary<string, DoorRoute> RoutesByDoorId { get; private set; }
    public List<string> Errors { get; private set; }
    public List<string> Warnings { get; private set; }
    public bool IsValid => Errors.Count == 0 && RoomsByName.Count > 0;
}

public static class DoorDataParser
{
    public static DoorDataParseResult Parse(string text)
    {
        DoorDataParseResult result = new DoorDataParseResult();

        if (string.IsNullOrWhiteSpace(text))
        {
            result.Errors.Add("Door data is empty.");
            return result;
        }

        RoomDefinition currentRoom = null;
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            int lineNumber = i + 1;
            string line = StripInlineComment(lines[i]).Trim();

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            int separatorIndex = line.IndexOf(':');

            if (separatorIndex < 0)
            {
                result.Errors.Add($"Line {lineNumber}: expected either 'Room:' or 'DoorId: Destination Room'.");
                continue;
            }

            if (separatorIndex == line.Length - 1)
            {
                string roomName = line.Substring(0, separatorIndex).Trim();

                if (string.IsNullOrEmpty(roomName))
                {
                    result.Errors.Add($"Line {lineNumber}: room name is empty.");
                    currentRoom = null;
                    continue;
                }

                if (!result.RoomsByName.TryGetValue(roomName, out currentRoom))
                {
                    currentRoom = new RoomDefinition(roomName);
                    result.RoomsByName.Add(roomName, currentRoom);
                }
                else
                {
                    result.Warnings.Add($"Line {lineNumber}: room '{roomName}' appears more than once; entries will be merged.");
                }

                continue;
            }

            if (currentRoom == null)
            {
                result.Errors.Add($"Line {lineNumber}: door entry appears before any room header.");
                continue;
            }

            string doorId = line.Substring(0, separatorIndex).Trim();
            string destinationRoom = line.Substring(separatorIndex + 1).Trim();

            if (string.IsNullOrEmpty(doorId))
            {
                result.Errors.Add($"Line {lineNumber}: door ID is empty.");
                continue;
            }

            if (string.IsNullOrEmpty(destinationRoom))
            {
                result.Errors.Add($"Line {lineNumber}: destination room is empty for door '{doorId}'.");
                continue;
            }

            if (result.RoutesByDoorId.ContainsKey(doorId))
            {
                DoorRoute existing = result.RoutesByDoorId[doorId];
                result.Errors.Add(
                    $"Line {lineNumber}: duplicate door ID '{doorId}'. First seen in '{existing.SourceRoom}', repeated in '{currentRoom.RoomName}'.");
                continue;
            }

            DoorRoute route = new DoorRoute(doorId, currentRoom.RoomName, destinationRoom);
            currentRoom.AddDoor(route);
            result.RoutesByDoorId.Add(doorId, route);
        }

        return result;
    }

    private static string StripInlineComment(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        int hashIndex = line.IndexOf('#');
        int slashIndex = line.IndexOf("//", StringComparison.Ordinal);
        int commentIndex = -1;

        if (hashIndex >= 0)
        {
            commentIndex = hashIndex;
        }

        if (slashIndex >= 0 && (commentIndex < 0 || slashIndex < commentIndex))
        {
            commentIndex = slashIndex;
        }

        return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
    }
}
