using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class NavigationRegressionTests
{
    private const string DoorDataPath = "Assets/Resources/Navigation/doors.txt";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string NavigationManagerPath = "Assets/Scripts/Navigation/RoomNavigationManager.cs";
    private const string NavigationBootstrapPath = "Assets/Scripts/Navigation/RoomNavigationBootstrap.cs";
    private const string DoorTriggerNavigationPath = "Assets/Scripts/Navigation/DoorTriggerNavigation.cs";
    private const string CameraManagerPath = "Assets/Map/CameraManager.cs";
    private const string NavigationEditorToolsPath = "Assets/Editor/NavigationEditorTools.cs";
    private const string BackgroundShaderGraphPath = "Assets/Shader/Background.shadergraph";
    private const string BackgroundMaterialPath = "Assets/Shader/BackgroundMaterial.mat";
    private const string RoomPrefabPath = "Assets/Prefabs/Room.prefab";
    private const string DoorTriggerNavigationGuid = "7e419b0f8f26d4f2d8d03e567fef4c52";
    private const string RoomContentGroupGuid = "d0ea47fd950844bcacb0fd5556a9d880";

    [Test]
    public void GameplayDoorTriggersMatchDoorData()
    {
        DoorDataParseResult doorData = DoorDataParser.Parse(File.ReadAllText(DoorDataPath));
        Assert.That(doorData.Errors, Is.Empty, string.Join(Environment.NewLine, doorData.Errors));

        Dictionary<string, List<SerializedDoorTrigger>> triggersByDoorId = ReadDoorTriggersByDoorId(File.ReadAllText(GameplayScenePath));

        foreach (DoorRoute route in doorData.RoutesByDoorId.Values)
        {
            Assert.That(triggersByDoorId.ContainsKey(route.DoorId), Is.True, $"Missing DoorTriggerNavigation for door '{route.DoorId}'.");

            foreach (SerializedDoorTrigger trigger in triggersByDoorId[route.DoorId])
            {
                Assert.That(trigger.SourceRoom, Is.EqualTo(route.SourceRoom).IgnoreCase, $"Door '{route.DoorId}' source room drifted.");
                Assert.That(trigger.DestinationRoom, Is.EqualTo(route.DestinationRoom).IgnoreCase, $"Door '{route.DoorId}' destination room drifted.");
                Assert.That(trigger.UsesCameraSequence, Is.False, $"Door '{route.DoorId}' should use doors.txt navigation, not the old camera sequence.");
            }
        }
    }

    [Test]
    public void GameplayDoorTriggerLayerCanReceiveClicks()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Contain("m_Name: Canvas_Background"));
        Assert.That(sceneText, Does.Contain("m_Name: EventSystem"));
        Assert.That(sceneText, Does.Contain("m_Name: Rooms"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: RoomDoorTriggers_Edit"));

        // A zero-scale UI transform is the exact regression that makes visible
        // door triggers stop receiving clicks, so keep it out of the gameplay scene.
        Assert.That(sceneText, Does.Not.Contain("m_LocalScale: {x: 0, y: 0, z: 0}"));
        Assert.That(sceneText, Does.Not.Contain("m_UiScaleMode: 0"), "Gameplay canvases should scale consistently between Edit and Play mode.");
        Assert.That(sceneText, Does.Not.Contain("m_ReferenceResolution: {x: 800, y: 600}"), "Gameplay canvases should use the project reference resolution, not Unity defaults.");
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
    public void GameplayRoomsOwnBackgroundsAndDoorGroups()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string navigationManagerText = File.ReadAllText(NavigationManagerPath);
        string roomPrefabText = File.ReadAllText(RoomPrefabPath);

        Assert.That(sceneText, Does.Contain($"guid: {RoomContentGroupGuid}"), "Gameplay room objects should have RoomContentGroup components.");
        Assert.That(sceneText, Does.Contain("roomBackgroundTexture: {fileID: 2800000"), "RoomContentGroup should own each room background texture.");
        Assert.That(sceneText, Does.Contain("m_Name: Button_Grand_Entrance_Hall"));
        Assert.That(sceneText, Does.Contain("m_Name: Button_Library"));
        Assert.That(sceneText, Does.Contain("m_Name: Button_Ballroom"));
        Assert.That(Regex.Matches(sceneText, @"m_Name: Doors").Count, Is.GreaterThanOrEqualTo(18), "Each room object should have a Doors child.");
        Assert.That(sceneText, Does.Not.Contain("m_Name: Cam_"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: MapButton_"));
        Assert.That(sceneText, Does.Not.Contain("DoorTrigger_K1"));
        Assert.That(sceneText, Does.Not.Contain("DoorTrigger_K2"));
        Assert.That(sceneText, Does.Not.Contain("006acc238c9c2e26f8e9e7ec33e82a09"));
        Assert.That(sceneText, Does.Not.Contain("a8335d5d820eabc44a82824f60fc64c6"));
        Assert.That(navigationManagerText, Does.Contain("NormalizeComparableName"), "Clean hierarchy names still need to match display room names with apostrophes or ampersands.");

        Assert.That(roomPrefabText, Does.Contain("m_Name: Room_NewRoom"));
        Assert.That(roomPrefabText, Does.Contain("m_Name: Doors"));
        Assert.That(roomPrefabText, Does.Contain($"guid: {RoomContentGroupGuid}"));
        Assert.That(roomPrefabText, Does.Contain("roomBackgroundTexture: {fileID: 0}"));
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
        string shaderGraphText = File.ReadAllText(BackgroundShaderGraphPath);
        string backgroundMaterialText = File.ReadAllText(BackgroundMaterialPath);

        Assert.That(sceneText, Does.Contain("edgePanActivationPixels: 24"), "Gameplay should use a tiny pixel edge zone, not broad screen regions.");
        Assert.That(sceneText, Does.Contain("zoomRoomWithMouseWheel: 1"), "Mouse wheel should use a regular image zoom, not the old vertical shader distortion.");
        Assert.That(sceneText, Does.Contain("defaultRoomFov: 0.8"), "Room art should start less cropped than the old tutorial placeholder framing.");
        Assert.That(sceneText, Does.Contain("fitBackgroundToRoomAspect: 1"), "Room art should keep a stable aspect plane when the Game view is resized.");
        Assert.That(sceneText, Does.Contain("cropBackgroundToFill: 0"), "Window resizing should letterbox the room plane instead of changing the visible source-image crop.");
        Assert.That(sceneText, Does.Contain("roomPanStartSpeed: 0.45"), "Edge panning should start gently before accelerating.");
        Assert.That(sceneText, Does.Contain("defaultRoomZoom: 1.06"), "The room should start slightly zoomed so edge panning has room to move.");
        Assert.That(sceneText, Does.Contain("maxRoomZoom: 1.22"), "Wheel zoom should stay strong enough for panning but not feel like teleporting.");
        Assert.That(sceneText, Does.Contain("roomZoomFocus: {x: 0.5, y: 0.56}"), "Regular zoom should aim near the room vanishing point so it reads as stepping closer.");
        Assert.That(sceneText, Does.Not.Contain("scrollRoomVerticallyWithMouseWheel"), "Mouse wheel should not drive vertical shader strength; that smeared room art into stripes.");
        Assert.That(sceneText, Does.Not.Contain("scrollRoomFovWithMouseWheel"), "Mouse wheel should not drive FOV zoom; that caused the sideways drift regression.");
        Assert.That(cameraManagerText, Does.Contain("return currentRoomPan;"), "Leaving the edge should hold the current pan instead of recentering.");
        Assert.That(cameraManagerText, Does.Contain("NavigationCursorController.SetEdgePanDirection"), "Edge panning should update the cursor state.");
        Assert.That(cameraManagerText, Does.Contain("SetActiveRoomContent"), "CameraManager should know which room stage owns the current background and hitboxes.");
        Assert.That(cameraManagerText, Does.Contain("TryApplyRoomStageLayout"), "Runtime panning must move the active room stage, not reproject door rectangles.");
        Assert.That(cameraManagerText, Does.Contain("AttachBackgroundToRoomStage"), "The background image should become a child of the active room stage in Play mode.");
        Assert.That(cameraManagerText, Does.Contain("GetCurrentHorizontalPanSpeed"), "Edge panning should accelerate while the player holds the cursor at the edge.");
        Assert.That(cameraManagerText, Does.Contain("SmoothRoomZoom"), "Mouse-wheel zoom should be damped instead of stepping between crop values.");
        Assert.That(cameraManagerText, Does.Contain("activeRoomStage.localScale = new Vector3(stageScale, stageScale, 1f)"), "Regular zoom should scale the whole room stage so hitboxes and art share one transform.");
        Assert.That(cameraManagerText, Does.Contain("ResetRoomLookForRoomChange"), "Each new room should enter from a centered default view instead of inheriting the previous room's pan/zoom.");
        Assert.That(cameraManagerText, Does.Contain("roomStageOwnsMotion ? 0f"), "The shader must stay neutral when the room stage owns panning and zooming.");
        Assert.That(cameraManagerText, Does.Not.Contain("TryApplySourceImageRect"), "Door hitboxes should not be reprojected separately from the room image.");
        Assert.That(cameraManagerText, Does.Not.Contain("return 1.5f - curvedScale"), "The old signed vertical projection jumped across zero on a single mouse-wheel tick.");
        Assert.That(shaderGraphText, Does.Not.Match(@"(?s)""m_OutputSlot""\s*:\s*\{\s*""m_Node""\s*:\s*\{\s*""m_Id"": ""f02995c1a6a74ca897aad1adcdadc881""\s*\}\s*,\s*""m_SlotId"": 2\s*\}\s*,\s*""m_InputSlot""\s*:\s*\{\s*""m_Node""\s*:\s*\{\s*""m_Id"": ""3b2c5930216346678c0347817b27b12a""\s*\}\s*,\s*""m_SlotId"": 2"), "The shader Step node must not switch vertical projection branches at zero.");
        Assert.That(backgroundMaterialText, Does.Contain("- _verticle_strength: 0"), "The shared background material should not save a warped zoom state.");
        Assert.That(backgroundMaterialText, Does.Contain("- _MainTex:\n        m_Texture: {fileID: 2800000, guid: f233ee9a18ce3e78bb0a642637f2d2d0, type: 3}"), "The shared background material should keep its preview texture assigned.");
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
        Assert.That(cameraManagerText, Does.Not.Contain("SourceUvXMin"), "Door hitboxes should not depend on a separate source-UV projection bridge.");
        Assert.That(cameraManagerText, Does.Not.Contain("SourceUvXRange"), "Door hitboxes should not depend on a separate source-UV projection bridge.");
    }

    [Test]
    public void DoorHitboxesUseVisibleRectTransformsAsSourceOfTruth()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);
        string editorToolsText = File.ReadAllText(NavigationEditorToolsPath);
        string gameplaySceneText = File.ReadAllText(GameplayScenePath);
        string mainMenuSceneText = File.ReadAllText(MainMenuScenePath);

        Assert.That(triggerText, Does.Not.Contain("[ExecuteAlways]"), "Door hitboxes should not run edit-mode scripts that silently rewrite placement.");
        Assert.That(triggerText, Does.Not.Contain("backgroundShaderUvRect"), "Door hitboxes should not keep a second hidden placement coordinate.");
        Assert.That(triggerText, Does.Not.Contain("CaptureCurrentShaderAnchor"), "Manual RectTransforms should be the authoring data, not captured anchors.");
        Assert.That(triggerText, Does.Not.Contain("TryCaptureAuthoredSourceImageRect"), "Runtime should not derive a second UV coordinate from the visible RectTransform.");
        Assert.That(triggerText, Does.Not.Contain("LateUpdate"), "Door hitboxes should not chase the camera every frame.");
        Assert.That(cameraManagerText, Does.Contain("AttachBackgroundToRoomStage"), "CameraManager should put the background under the same room stage as the hitboxes.");
        Assert.That(cameraManagerText, Does.Not.Contain("TryCaptureShaderAnchoredRect"), "CameraManager should not expose old capture APIs.");
        Assert.That(cameraManagerText, Does.Not.Contain("TryApplySourceImageRect"), "CameraManager should not expose a projection bridge for door hitboxes.");
        Assert.That(editorToolsText, Does.Not.Contain("CaptureVisibleDoorTriggerAnchorsForCurrentPreview"), "Editor previews should not save hitbox locations as a side effect.");
        Assert.That(editorToolsText, Does.Not.Contain("AutoSyncDoorTriggers"), "Door trigger sync should be an explicit menu action, not an automatic editor task.");
        Assert.That(editorToolsText, Does.Contain("FitToTextureWithUndo"), "Editor room previews should show the source image at native size so door placement matches runtime UVs.");
        Assert.That(gameplaySceneText, Does.Not.Contain("backgroundShaderUvRect"), "Gameplay scene should not carry stale hidden hitbox anchors.");
        Assert.That(mainMenuSceneText, Does.Not.Contain("backgroundShaderUvRect"), "MainMenu scene should not carry stale hidden hitbox anchors.");
    }

    private static Dictionary<string, List<SerializedDoorTrigger>> ReadDoorTriggersByDoorId(string sceneText)
    {
        Dictionary<string, List<SerializedDoorTrigger>> triggersByDoorId =
            new Dictionary<string, List<SerializedDoorTrigger>>(StringComparer.OrdinalIgnoreCase);
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

            if (!string.IsNullOrWhiteSpace(trigger.DoorId))
            {
                if (!triggersByDoorId.TryGetValue(trigger.DoorId, out List<SerializedDoorTrigger> triggers))
                {
                    triggers = new List<SerializedDoorTrigger>();
                    triggersByDoorId.Add(trigger.DoorId, triggers);
                }

                triggers.Add(trigger);
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

        string safeName = Regex.Replace(value.Trim().Replace("'", string.Empty), @"[^A-Za-z0-9]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(safeName) ? "Unnamed" : safeName;
    }

    private struct SerializedDoorTrigger
    {
        public string SourceRoom;
        public string DoorId;
        public string DestinationRoom;
        public bool UsesCameraSequence;
    }
}
