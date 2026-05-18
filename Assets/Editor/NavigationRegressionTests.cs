using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class NavigationRegressionTests
{
    private const string DoorDataPath = "Assets/Resources/Navigation/doors.txt";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string NavigationBootstrapPath = "Assets/Scripts/Navigation/RoomNavigationBootstrap.cs";
    private const string CameraManagerPath = "Assets/Map/CameraManager.cs";
    private const string BackgroundShaderGraphPath = "Assets/Shader/Background.shadergraph";
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

    [Test]
    public void NavigationBootstrapRunsAfterMainMenuLoadsGameplay()
    {
        string bootstrapText = File.ReadAllText(NavigationBootstrapPath);

        Assert.That(bootstrapText, Does.Contain("SceneManager.sceneLoaded"), "Navigation bootstrap must run again when MainMenu loads Gameplay.");
        Assert.That(bootstrapText, Does.Contain("HandleSceneLoaded"), "Navigation bootstrap needs a scene-loaded callback for non-initial gameplay loads.");
    }

    [Test]
    public void GameplayCameraUsesTutorialStyleRoomLook()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);

        Assert.That(sceneText, Does.Contain("edgePanActivationPixels: 24"), "Gameplay should use a tiny pixel edge zone, not broad screen regions.");
        Assert.That(sceneText, Does.Contain("scrollRoomVerticallyWithMouseWheel: 1"), "Tutorial 1's wheel control drives shader vertical strength for the 2.5D look.");
        Assert.That(sceneText, Does.Contain("defaultRoomFov: 0.8"), "Room art should start less cropped than the old tutorial placeholder framing.");
        Assert.That(sceneText, Does.Contain("roomPanStartSpeed: 0.45"), "Edge panning should start gently before accelerating.");
        Assert.That(sceneText, Does.Not.Contain("scrollRoomFovWithMouseWheel"), "Mouse wheel should not drive FOV zoom; that caused the sideways drift regression.");
        Assert.That(cameraManagerText, Does.Contain("return currentRoomPan;"), "Leaving the edge should hold the current pan instead of recentering.");
        Assert.That(cameraManagerText, Does.Contain("NavigationCursorController.SetEdgePanDirection"), "Edge panning should update the cursor state.");
        Assert.That(cameraManagerText, Does.Contain("GetSafeHorizontalPanLimit"), "Horizontal panning must clamp to the visible image to prevent edge streak artifacts.");
        Assert.That(cameraManagerText, Does.Contain("GetCurrentHorizontalPanSpeed"), "Edge panning should accelerate while the player holds the cursor at the edge.");
    }

    [Test]
    public void GameplayHasExplorationAudioHooks()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Contain("m_Name: Audio_ExplorationMusic"), "Gameplay should start house exploration ambience without depending on MainMenu.");
        Assert.That(sceneText, Does.Contain("m_Name: Audio_DoorOpen"), "Door triggers need a shared AudioSource for the door opening sound.");
        Assert.That(sceneText, Does.Contain("guid: 5cd6bd3d35aa8e1ebae11661918fd66a"), "Exploration music should use the existing dreadforge soundscape clip.");
        Assert.That(sceneText, Does.Contain("guid: 700538fbae21acc4dae7d01a518aad25"), "Door opening should use an existing short click-style clip until final door audio exists.");
    }

    [Test]
    public void BackgroundShaderUsesFullRoomArtWidth()
    {
        string shaderGraphText = File.ReadAllText(BackgroundShaderGraphPath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);

        Assert.That(shaderGraphText, Does.Not.Contain("0.20000000298023225"), "The old tutorial art crop cut off the new room images.");
        Assert.That(shaderGraphText, Does.Not.Contain("0.800000011920929"), "The old tutorial art crop cut off the new room images.");
        Assert.That(cameraManagerText, Does.Contain("SourceUvXMin = 0f"));
        Assert.That(cameraManagerText, Does.Contain("SourceUvXRange = 1f"));
    }

    [Test]
    public void KitchenHallwayDoorHasUsablePlacementAnchor()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string triggerBlock = FindDoorTriggerBlock(sceneText, "K2");

        Assert.That(triggerBlock, Is.Not.Empty, "Missing Kitchen -> Hallway trigger K2.");
        Assert.That(ReadSerializedString(triggerBlock, "sourceRoom"), Is.EqualTo("Kitchen"));
        Assert.That(ReadSerializedString(triggerBlock, "destinationRoom"), Is.EqualTo("Hallway"));

        float x = ReadSerializedFloat(triggerBlock, "x");
        float y = ReadSerializedFloat(triggerBlock, "y");
        float width = ReadSerializedFloat(triggerBlock, "width");
        float height = ReadSerializedFloat(triggerBlock, "height");

        Assert.That(x, Is.LessThan(0.2f), "K2 should sit on the left-side Kitchen doorway in source-image UV space.");
        Assert.That(y, Is.InRange(0.2f, 0.4f), "K2 vertical anchor should cover the Kitchen hallway opening.");
        Assert.That(width, Is.GreaterThan(0.1f), "K2 must not collapse back to the tiny accidental default.");
        Assert.That(height, Is.GreaterThan(0.4f), "K2 must cover enough of the doorway to be clickable.");
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

    private static float ReadSerializedFloat(string block, string fieldName)
    {
        string value = ReadSerializedString(block, fieldName);
        Assert.That(value, Is.Not.Empty, $"Missing serialized float '{fieldName}'.");
        return float.Parse(value, CultureInfo.InvariantCulture);
    }

    private static bool ReadSerializedBool(string block, string fieldName)
    {
        return ReadSerializedString(block, fieldName) == "1";
    }

    private static string FindDoorTriggerBlock(string sceneText, string doorId)
    {
        string[] blocks = Regex.Split(sceneText, @"(?m)^--- !u!114 ");

        for (int i = 0; i < blocks.Length; i++)
        {
            string block = blocks[i];

            if (block.Contains(DoorTriggerNavigationGuid) && ReadSerializedString(block, "doorName") == doorId)
            {
                return block;
            }
        }

        return string.Empty;
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
