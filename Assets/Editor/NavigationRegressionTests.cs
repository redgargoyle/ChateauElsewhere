using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class NavigationRegressionTests
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string NavigationManagerPath = "Assets/Scripts/Navigation/RoomNavigationManager.cs";
    private const string NavigationBootstrapPath = "Assets/Scripts/Navigation/RoomNavigationBootstrap.cs";
    private const string DoorTriggerNavigationPath = "Assets/Scripts/Navigation/DoorTriggerNavigation.cs";
    private const string DoorOpenSoundCatalogPath = "Assets/Resources/Audio/DoorOpenSoundCatalog.asset";
    private const string DoorPromptSequenceControllerPath = "Assets/Scripts/Navigation/DoorPromptSequenceController.cs";
    private const string CameraManagerPath = "Assets/Map/CameraManager.cs";
    private const string NavigationEditorToolsPath = "Assets/Editor/NavigationEditorTools.cs";
    private const string BackgroundShaderGraphPath = "Assets/Shader/Background.shadergraph";
    private const string BackgroundMaterialPath = "Assets/Shader/BackgroundMaterial.mat";
    private const string RoomPrefabPath = "Assets/Prefabs/Room.prefab";
    private const string RoomContentGroupGuid = "d0ea47fd950844bcacb0fd5556a9d880";

    [Test]
    public void DoorTriggersUseInspectorDestinationsOnly()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);

        Assert.That(triggerText, Does.Contain("MoveThroughInspectorDoor"), "Door triggers should navigate through their Inspector destination.");
        Assert.That(triggerText, Does.Not.Contain("TryMoveThroughDoor"), "Door hitboxes should not consult doors.txt.");
    }

    [Test]
    public void DoorTriggersPlayRandomWoodCreaksFromCatalog()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string catalogText = File.ReadAllText(DoorOpenSoundCatalogPath);

        Assert.That(triggerText, Does.Contain("DoorOpenSoundCatalog"), "Door triggers should use the shared randomized door sound catalog.");
        Assert.That(triggerText, Does.Contain("TryGetRandomClip"), "Door clicks should pick one door-open clip at random.");
        Assert.That(triggerText, Does.Contain("TryPlayDoorOpenSoundNow"), "Door sound should start immediately when the door is clicked, before navigation work finishes.");
        Assert.That(triggerText, Does.Contain("StopCurrentDoorOpenSound"), "Starting a door creak should stop any previous door creak.");
        Assert.That(triggerText, Does.Not.Contain("PlayDoorOpenSoundIfSuccessful"), "Door audio should not wait for the full navigation transition before starting.");
        Assert.That(catalogText, Does.Not.Contain("a7718dd1d7db61a4490bf5be4b919568"), "The pot clang should not be part of door-open randomization.");
        Assert.That(catalogText, Does.Not.Contain("2cda7eb569e05e4ae87de22b60ce4fcf"), "Wood tapping should not be part of door-open randomization.");
        Assert.That(catalogText, Does.Not.Contain("95d9163c9d40da015a0afa4a2e8cb915"), "Typo-spaced @hamzak woodcreak files should not be part of door-open randomization.");
        Assert.That(Regex.Matches(catalogText, "fileID: 8300000").Count, Is.EqualTo(7), "Door-open randomization should only use active @hamzak - woodcreak* clips.");

        string[] woodClipMetaPaths = Directory.GetFiles("Assets/Audio/Sound Exports", "@hamzak - woodcreak*.wav.meta");
        Assert.That(woodClipMetaPaths.Length, Is.EqualTo(7), "Flatline clips should stay outside the active door-open export folder.");
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
    public void StairwayTriggersUseStairwayNamesAndCursor()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);
        string promptText = File.ReadAllText(DoorPromptSequenceControllerPath);

        Assert.That(Regex.Matches(sceneText, "m_Name: StairwayTrigger_").Count, Is.EqualTo(4), "Only the hand-placed hitboxes over visible stairs should be named StairwayTrigger.");
        Assert.That(sceneText, Does.Contain("m_Name: StairwayTrigger_GEH_UpperGalleryLeft"));
        Assert.That(sceneText, Does.Contain("m_Name: StairwayTrigger_GEH_UpperGalleryRight"));
        Assert.That(sceneText, Does.Contain("m_Name: StairwayTrigger_UpperGallery_GEH"));
        Assert.That(sceneText, Does.Contain("m_Name: StairwayTrigger_SideStairMudroom_UpperSittingHall"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: DoorTrigger_GEH_UpperGalleryLeft_entrance"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: DoorTrigger_GEH_UpperGalleryRight_entrance"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: DoorTrigger_UpperGallery_GEH"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: DoorTrigger_SideStairMudroom_UpperSittingHall"));
        Assert.That(sceneText, Does.Contain("doorName: GEH_Stairway_UpperGalleryLeft"));
        Assert.That(sceneText, Does.Contain("doorName: GEH_Stairway_UpperGalleryRight"));
        Assert.That(sceneText, Does.Contain("doorName: UpperGallery_Stairway_GEH"));
        Assert.That(sceneText, Does.Contain("doorName: SideStairMudroom_Stairway_UpperSittingHall"));

        Assert.That(triggerText, Does.Contain("InteractionLabel"), "Stairway triggers should identify themselves to shared UI without a separate hitbox class.");
        Assert.That(triggerText, Does.Contain("HoverIcon.Stairway"), "Stairway triggers should request the stairway cursor through the existing cursor controller.");
        Assert.That(cameraManagerText, Does.Contain("CreateStairwayCursor"), "The cursor controller should generate a stairway cursor icon.");
        Assert.That(promptText, Does.Contain("Use Stairway"), "Hover prompt text should match stairway interactions.");
    }

    [Test]
    public void GameplayHasManualRoomStageRoot()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Contain("m_Name: Rooms"));
        Assert.That(sceneText, Does.Contain("m_Name: Room_Grand_Entrance_Hall"));
        Assert.That(sceneText, Does.Contain("m_Name: Doors"));
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
        Assert.That(triggerText, Does.Contain("InferSourceRoomFromHierarchy(transform)"), "Door source rooms should come from the Room_* hierarchy by default.");
        Assert.That(cameraManagerText, Does.Contain("AttachBackgroundToRoomStage"), "CameraManager should put the background under the same room stage as the hitboxes.");
        Assert.That(cameraManagerText, Does.Not.Contain("TryCaptureShaderAnchoredRect"), "CameraManager should not expose old capture APIs.");
        Assert.That(cameraManagerText, Does.Not.Contain("TryApplySourceImageRect"), "CameraManager should not expose a projection bridge for door hitboxes.");
        Assert.That(editorToolsText, Does.Not.Contain("CaptureVisibleDoorTriggerAnchorsForCurrentPreview"), "Editor previews should not save hitbox locations as a side effect.");
        Assert.That(editorToolsText, Does.Not.Contain("AutoSyncDoorTriggers"), "Door trigger sync should be an explicit menu action, not an automatic editor task.");
        Assert.That(editorToolsText, Does.Contain("Door trigger sync from doors.txt is disabled"), "doors.txt must not be able to move or recreate hand-placed door triggers.");
        Assert.That(editorToolsText, Does.Not.Contain("SetTransformParent(trigger.transform"), "Editor preview/sync code should not move existing door triggers between rooms.");
        Assert.That(editorToolsText, Does.Contain("FitToTextureWithUndo"), "Editor room previews should show the source image at native size so door placement matches runtime UVs.");
        Assert.That(gameplaySceneText, Does.Not.Contain("backgroundShaderUvRect"), "Gameplay scene should not carry stale hidden hitbox anchors.");
        Assert.That(mainMenuSceneText, Does.Not.Contain("backgroundShaderUvRect"), "MainMenu scene should not carry stale hidden hitbox anchors.");
    }

}
