using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class NavigationRegressionTests
{
    private const string DoorDataPath = "Assets/Resources/Navigation/doors.txt";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string DoorTriggerNavigationGuid = "7e419b0f8f26d4f2d8d03e567fef4c52";

    [Test]
    public void GameplayDoorTriggersMatchDoorData()
    {
        DoorDataParseResult doorData = DoorDataParser.Parse(File.ReadAllText(DoorDataPath));
        Assert.That(doorData.Errors, Is.Empty, string.Join(Environment.NewLine, doorData.Errors));

        Dictionary<string, SerializedDoorTrigger> triggersByDoorId = ReadDoorTriggersByDoorId(File.ReadAllText(GameplayScenePath));

        foreach (DoorRoute route in doorData.RoutesByDoorId.Values)
        {
            Assert.That(triggersByDoorId.ContainsKey(route.DoorId), Is.True, $"Missing DoorTriggerNavigation for door '{route.DoorId}'.");

            SerializedDoorTrigger trigger = triggersByDoorId[route.DoorId];
            Assert.That(trigger.SourceRoom, Is.EqualTo(route.SourceRoom).IgnoreCase, $"Door '{route.DoorId}' source room drifted.");
            Assert.That(trigger.DestinationRoom, Is.EqualTo(route.DestinationRoom).IgnoreCase, $"Door '{route.DoorId}' destination room drifted.");
            Assert.That(trigger.UsesCameraSequence, Is.False, $"Door '{route.DoorId}' should use doors.txt navigation, not the old camera sequence.");
        }
    }

    [Test]
    public void GameplayDoorTriggerLayerCanReceiveClicks()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Contain("m_Name: Canvas_Background"));
        Assert.That(sceneText, Does.Contain("m_Name: EventSystem"));
        Assert.That(sceneText, Does.Contain("m_Name: RoomDoorTriggers_Edit"));

        // A zero-scale UI transform is the exact regression that makes visible
        // door triggers stop receiving clicks, so keep it out of the gameplay scene.
        Assert.That(sceneText, Does.Not.Contain("m_LocalScale: {x: 0, y: 0, z: 0}"));
    }

    [Test]
    public void GameplayHasRoomGroupsForEveryDoorDataRoom()
    {
        DoorDataParseResult doorData = DoorDataParser.Parse(File.ReadAllText(DoorDataPath));
        Assert.That(doorData.Errors, Is.Empty, string.Join(Environment.NewLine, doorData.Errors));

        string sceneText = File.ReadAllText(GameplayScenePath);

        foreach (RoomDefinition room in doorData.RoomsByName.Values)
        {
            string expectedRoomGroupName = $"m_Name: Room_{SafeObjectName(room.RoomName)}";
            Assert.That(sceneText, Does.Contain(expectedRoomGroupName), $"Missing door trigger room group for '{room.RoomName}'.");
        }
    }

    private static Dictionary<string, SerializedDoorTrigger> ReadDoorTriggersByDoorId(string sceneText)
    {
        Dictionary<string, SerializedDoorTrigger> triggersByDoorId =
            new Dictionary<string, SerializedDoorTrigger>(StringComparer.OrdinalIgnoreCase);
        string[] blocks = Regex.Split(sceneText, @"(?m)^--- !u!114 ");

        for (int i = 0; i < blocks.Length; i++)
        {
            string block = blocks[i];

            if (!block.Contains(DoorTriggerNavigationGuid))
            {
                continue;
            }

            SerializedDoorTrigger trigger = new SerializedDoorTrigger
            {
                SourceRoom = ReadSerializedString(block, "sourceRoom"),
                DoorId = ReadSerializedString(block, "doorName"),
                DestinationRoom = ReadSerializedString(block, "destinationRoom"),
                UsesCameraSequence = ReadSerializedBool(block, "useCameraSequence")
            };

            if (!string.IsNullOrWhiteSpace(trigger.DoorId) && !triggersByDoorId.ContainsKey(trigger.DoorId))
            {
                triggersByDoorId.Add(trigger.DoorId, trigger);
            }
        }

        return triggersByDoorId;
    }

    private static string ReadSerializedString(string block, string fieldName)
    {
        Match match = Regex.Match(block, $@"(?m)^\s*{Regex.Escape(fieldName)}:\s*(.*)$");
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static bool ReadSerializedBool(string block, string fieldName)
    {
        return ReadSerializedString(block, fieldName) == "1";
    }

    private static string SafeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        char[] chars = value.Trim().ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    private struct SerializedDoorTrigger
    {
        public string SourceRoom;
        public string DoorId;
        public string DestinationRoom;
        public bool UsesCameraSequence;
    }
}
