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
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string RoomContentGroupPath = "Assets/Scripts/Navigation/RoomContentGroup.cs";
    private const string DoorOpenSoundCatalogPath = "Assets/Resources/Audio/DoorOpenSoundCatalog.asset";
    private const string StairwaySoundCatalogPath = "Assets/Resources/Audio/StairwaySoundCatalog.asset";
    private const string DoorPromptSequenceControllerPath = "Assets/Scripts/Navigation/DoorPromptSequenceController.cs";
    private const string CameraManagerPath = "Assets/Map/CameraManager.cs";
    private const string NavigationEditorToolsPath = "Assets/Editor/NavigationEditorTools.cs";
    private const string BackgroundShaderGraphPath = "Assets/Shader/Background.shadergraph";
    private const string BackgroundMaterialPath = "Assets/Shader/BackgroundMaterial.mat";
    private const string RoomPrefabPath = "Assets/Prefabs/Room.prefab";
    private const string YSortSolidObstaclePath = "Assets/Scripts/Characters/YSortSolidObstacle2D.cs";
    private const string ChapterTimeSettingsUIPath = "Assets/Scripts/Story/ChapterTimeSettingsUI.cs";
    private const string ChapterManagerPath = "Assets/Scripts/Story/ChapterManager.cs";
    private const string Chapter1InteractionHUDPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1InteractionHUD.cs";
    private const string Chapter2InteractionHUDPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2InteractionHUD.cs";
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
        Assert.That(triggerText, Does.Contain("TryPlayNavigationSoundNow"), "Door sound should start immediately when the door is clicked, before navigation work finishes.");
        Assert.That(triggerText, Does.Contain("StopCurrentNavigationSound"), "Starting a door creak should stop any previous navigation sound.");
        Assert.That(triggerText, Does.Not.Contain("PlayDoorOpenSoundIfSuccessful"), "Door audio should not wait for the full navigation transition before starting.");
        Assert.That(catalogText, Does.Not.Contain("a599035f4d65f7614a7cb90bfb65c96d"), "The stair climb noise should not be part of door-open randomization.");
        Assert.That(catalogText, Does.Not.Contain("a7718dd1d7db61a4490bf5be4b919568"), "The pot clang should not be part of door-open randomization.");
        Assert.That(catalogText, Does.Not.Contain("2cda7eb569e05e4ae87de22b60ce4fcf"), "Wood tapping should not be part of door-open randomization.");
        Assert.That(catalogText, Does.Not.Contain("95d9163c9d40da015a0afa4a2e8cb915"), "Typo-spaced @hamzak woodcreak files should not be part of door-open randomization.");
        Assert.That(Regex.Matches(catalogText, "fileID: 8300000").Count, Is.EqualTo(7), "Door-open randomization should only use active @hamzak - woodcreak* clips.");

        string[] woodClipMetaPaths = Directory.GetFiles("Assets/Audio/Sound Exports", "@hamzak - woodcreak*.wav.meta");
        Assert.That(woodClipMetaPaths.Length, Is.EqualTo(7), "Flatline clips should stay outside the active door-open export folder.");
    }

    [Test]
    public void StairwayTriggersPlayStairwaySoundCatalog()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string catalogText = File.ReadAllText(StairwaySoundCatalogPath);

        Assert.That(triggerText, Does.Contain("DefaultStairwaySoundCatalogResourcePath"), "Stairways should load their own sound catalog.");
        Assert.That(triggerText, Does.Contain("ResolveStairwaySoundCatalog"), "Stairway triggers should resolve stairway audio separately from door audio.");
        Assert.That(triggerText, Does.Contain("lastStairwayClipIndex"), "Stairway randomization should not share door clip history.");
        Assert.That(catalogText, Does.Contain("a599035f4d65f7614a7cb90bfb65c96d"), "The stairway catalog should use @hamzak - stair_climb_noise.wav.");
        Assert.That(Regex.Matches(catalogText, "fileID: 8300000").Count, Is.EqualTo(1), "Only the active @hamzak - stair* clip should be in the stairway catalog.");

        string[] stairClipMetaPaths = Directory.GetFiles("Assets/Audio/Sound Exports", "@hamzak - stair*.wav.meta");
        Assert.That(stairClipMetaPaths.Length, Is.EqualTo(1), "Only exact-prefix @hamzak - stair* clips should be treated as stairway clips.");
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
    public void ChapterClockUsesSingleBottomLeftHudReadout()
    {
        string timeSettingsText = File.ReadAllText(ChapterTimeSettingsUIPath);
        string chapter1HudText = File.ReadAllText(Chapter1InteractionHUDPath);
        string chapter2HudText = File.ReadAllText(Chapter2InteractionHUDPath);

        Assert.That(timeSettingsText, Does.Contain("Text_CurrentGameTime"));
        Assert.That(timeSettingsText, Does.Contain("clockRect.anchorMin = new Vector2(0f, 0f)"));
        Assert.That(timeSettingsText, Does.Contain("clockRect.anchorMax = new Vector2(0f, 0f)"));
        Assert.That(timeSettingsText, Does.Contain("clockRect.pivot = new Vector2(0f, 0f)"));
        Assert.That(timeSettingsText, Does.Contain("clockRect.anchoredPosition = new Vector2(18f, 18f)"));
        Assert.That(timeSettingsText, Does.Contain("TextAlignmentOptions.BottomLeft"));

        Assert.That(chapter1HudText, Does.Not.Contain("BuildShortHudState(chapterClock.CurrentTimeLabel)"), "Chapter 1 status should not render a second clock label.");
        Assert.That(chapter2HudText, Does.Not.Contain("chapterClock.CurrentTimeLabel"), "Chapter 2 status should not render a second clock label.");
        Assert.That(chapter2HudText, Does.Not.Contain("$\"{timeLabel}\\n{phaseLabel}\""), "Chapter 2 status should not combine time and phase in the top-left HUD.");
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
    public void DoorAndStairwayTriggersRequirePlayerApproach()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);

        Assert.That(triggerText, Does.Contain("requirePlayerProximity"), "Door and stairway triggers should check player distance before navigating.");
        Assert.That(triggerText, Does.Contain("walkPlayerToTriggerWhenFar"), "Far trigger clicks should walk the player toward the trigger instead of instantly navigating.");
        Assert.That(triggerText, Does.Contain("TryStartPlayerApproach"), "Door triggers should share one approach flow for doors and stairways.");
        Assert.That(triggerText, Does.Contain("GetClosestTriggerScreenPoint"), "Wide hitboxes should measure to the closest trigger edge, not only the center.");
        Assert.That(triggerText, Does.Contain("TryFindBestApproachDestination"), "Door approaches should sample the trigger and choose the closest reachable floor point.");
        Assert.That(triggerText, Does.Contain("CollectTriggerApproachSamples"), "Door approaches should consider trigger edges and bottom points, not a single screen point.");
        Assert.That(triggerText, Does.Contain("MovementStopped"), "Pending door approaches should clean up whether the player arrives or gets blocked.");
        Assert.That(triggerText, Does.Contain("ResetStaticState"), "Door trigger static state should reset between Play Mode sessions, including when domain reload is disabled.");
        Assert.That(triggerText, Does.Contain("LogApproachFailure"), "Failed door approaches should leave a useful console reason.");
        Assert.That(triggerText, Does.Contain("UpdateFallbackPointerHoverAndClick"), "Door triggers need a RectTransform fallback when UI pointer enter/click events are blocked.");
        Assert.That(triggerText, Does.Contain("ContainsScreenPoint"), "The fallback should test the authored door hitbox rect directly.");
        Assert.That(triggerText, Does.Contain("activeTriggers"), "The fallback should scan only active room triggers.");
        Assert.That(triggerText, Does.Match(@"GameObject\.Find\(cleanPlayerObjectName\)[\s\S]*FindObjectsByType<PointClickPlayerMovement>"), "Door triggers should prefer the named Player before scanning fallback movement components from guest clones.");
        Assert.That(triggerText, Does.Contain("IsLikelyChapterGuest"), "Door triggers should not cache active Chapter 1 guests as the player.");
        Assert.That(playerText, Does.Contain("TrySetDestinationFromScreenPoint"), "Navigation triggers need a public way to ask the player to walk toward a screen-space hitbox.");
        Assert.That(playerText, Does.Contain("TryEvaluateMovementAtScreenPoint"), "Cursor feedback and door approaches should use the same movement reachability query.");
        Assert.That(playerText, Does.Contain("TryGetScreenPointFromLogicalPosition"), "Door approaches need to score clamped floor points in screen space.");
        Assert.That(playerText, Does.Contain("IsPointerOverUi"), "Door UI clicks should not be overwritten by the floor click handler on the same frame.");
        Assert.That(playerText, Does.Contain("WalkableInsetAttempts"), "Clamped approach targets should move just inside the walkable polygon instead of sitting exactly on the collider edge.");
    }

    [Test]
    public void DoorTransitionsPlacePlayerAtDestinationDoor()
    {
        string navigationManagerText = File.ReadAllText(NavigationManagerPath);
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);

        Assert.That(navigationManagerText, Does.Contain("PlacePlayerAtDestinationDoor"), "Room transitions should move the player to the matching destination doorway.");
        Assert.That(navigationManagerText, Does.Contain("FindArrivalDoorTrigger"), "Destination placement should use the reverse trigger already authored in the room.");
        Assert.That(triggerText, Does.Contain("TryFindArrivalDestination"), "Door hitboxes should expose the same reachable floor sampling for arrivals.");
        Assert.That(triggerText, Does.Contain("TryFindClosestReachableArrivalDestination"), "Door arrivals should fall back to the nearest reachable floor point around the destination trigger.");
        Assert.That(triggerText, Does.Contain("TryFindClosestReachableDestinationToWorldPoint"), "Door arrivals should reuse the player movement boundary search when trigger screen samples miss the floor.");
        Assert.That(playerText, Does.Contain("TryWarpTo"), "Navigation needs an explicit non-walking placement path after a room change.");
        Assert.That(playerText, Does.Contain("RefreshWalkableFloorForCurrentRoom"), "Door arrivals must refresh the active room boundary before evaluating placement.");
    }

    [Test]
    public void PlayerCursorShowsWalkability()
    {
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);

        Assert.That(playerText, Does.Contain("UpdateWalkCursor"), "The player movement script should continuously describe what a floor click would do.");
        Assert.That(playerText, Does.Contain("SetWalkHover"), "Valid and invalid floor clicks should drive the shared cursor controller.");
        Assert.That(playerText, Does.Contain("movementQuery.ExactPointWalkable && movementQuery.HasReachableDestination"), "The walk cursor should describe exact floor validity, not the player's current distance from that point.");
        Assert.That(cameraManagerText, Does.Contain("CreateWalkCursor"), "The cursor controller should generate a walk cursor without needing imported art.");
        Assert.That(cameraManagerText, Does.Contain("Cursor_WalkBlocked"), "Invalid movement should show a distinct blocked-walk cursor.");
        Assert.That(cameraManagerText, Does.Contain("private const int CursorSize = 48"), "Movement cursors should be large enough to read quickly.");
        Assert.That(cameraManagerText, Does.Contain("ScaleCursorHotspot"), "Generated cursor art and hotspots should scale together.");
    }

    [Test]
    public void PlayerMovementUsesOnlyFloorBoundaryForWalkability()
    {
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);
        string obstacleText = File.ReadAllText(YSortSolidObstaclePath);

        Assert.That(playerText, Does.Contain("TryEvaluateMovementAtScreenPoint(screenPosition, false"), "Regular floor clicks should test the exact hovered floor point.");
        Assert.That(playerText, Does.Contain("LogicalToWalkableWorldPoint"), "Walkability should follow the visible room stage while edge panning moves it.");
        Assert.That(playerText, Does.Not.Contain("IsPickupObjectAtPoint"), "Pickup or prop colliders should not decide whether the floor is walkable.");
        Assert.That(playerText, Does.Not.Contain("IsMovementPointBlocked"), "Object footprints should not block point-click movement.");
        Assert.That(playerText, Does.Not.Contain("TryRestartPathFrom"), "Movement should not rebuild obstacle routes based on the butler's current position.");
        Assert.That(playerText, Does.Not.Contain("pathProbeStep"), "Movement should not sample a heavyweight path segment to reject floor clicks.");
        Assert.That(obstacleText, Does.Not.Contain("BlockPlayerMovement"), "Prop footprint components should not expose movement-blocking controls.");
        Assert.That(obstacleText, Does.Not.Contain("TryGetMovementBounds"), "Prop footprint components should not provide movement blockers.");
    }

    [Test]
    public void RoomPropDepthDoesNotDependOnViewportBounds()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string roomContentGroupText = File.ReadAllText(RoomContentGroupPath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);

        Assert.That(roomContentGroupText, Does.Contain("flattenChildRendererDepthAtRuntime"), "Room renderers should be flattened at runtime instead of relying on deep authored Z values.");
        Assert.That(roomContentGroupText, Does.Contain("NormalizeRendererDepth"), "Room renderer depth normalization should be centralized on RoomContentGroup.");
        Assert.That(roomContentGroupText, Does.Not.Contain("ApplyDynamicPropSorting"), "Room prop sorting should stay authored, not recomputed from viewport-dependent renderer bounds.");
        Assert.That(roomContentGroupText, Does.Not.Contain("spriteRenderer.bounds.min.y"), "Room prop sorting must not use world bounds that change with Free Aspect scaling.");
        Assert.That(sceneText, Does.Contain("far clip plane: 1000"), "The camera should use a normal clip range; deep prop Z is fixed at the room renderer level.");
        Assert.That(sceneText, Does.Not.Contain("minimumRoomRenderFarClipPlane"), "Room rendering should not depend on a giant far-clip workaround.");
        Assert.That(cameraManagerText, Does.Not.Contain("EnsureRoomRenderCameraClipRange"), "CameraManager should not hide room prop depth bugs by stretching the far clip plane.");
    }

    [Test]
    public void ServiceCorridorKeepsAuthoredProps()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(File.Exists("Assets/Art/Objects/service_corridor_left_table.png"), Is.True, "The service corridor left table sprite asset should stay in the project.");
        Assert.That(File.Exists("Assets/Art/Objects/service_corridor_right_desk.png"), Is.True, "The service corridor right desk sprite asset should stay in the project.");
        AssertScenePropSorting(sceneText, "service_corridor_left_table_0", 1480);
        AssertScenePropSorting(sceneText, "service_corridor_right_desk_0", 1512);
    }

    [Test]
    public void BilliardRoomKeepsForegroundCutouts()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(File.Exists("Assets/Art/Objects/green_armchair.png"), Is.True, "The billiard left armchair cutout should stay in the project.");
        Assert.That(File.Exists("Assets/Art/Objects/round_lamp_table.png"), Is.True, "The billiard left lamp table cutout should stay in the project.");
        AssertScenePropSorting(sceneText, "billiard_left_armchair", 1685);
        AssertScenePropSprite(sceneText, "billiard_left_armchair", "8017e9546fbe40beaaa61662f1e8191a");
        AssertScenePropSorting(sceneText, "billiard_left_lamp_table", 1695);
        AssertScenePropSprite(sceneText, "billiard_left_lamp_table", "dfcaa33b0d194eb0a42a334d83b90fb8");
    }

    [Test]
    public void DrawingRoomDoesNotKeepStaleGreenChairReference()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Not.Contain("353851a98d8c825dead3a8bdc3654973"), "The deleted drawingroomchair3 sprite should not remain in the scene.");
        Assert.That(sceneText, Does.Not.Contain("m_Name: green_armchair"), "The stale duplicate Drawing Room chair object should stay removed.");
        AssertScenePropSorting(sceneText, "drawingroomgreenchair_0", 1614);
        AssertScenePropSprite(sceneText, "drawingroomgreenchair_0", "5d2f4c79f6c75e4c69dbbafeaff8b2c2");
    }

    [Test]
    public void ImportantRoomPropsKeepStableAuthoredSorting()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        AssertScenePropSorting(sceneText, "nursery_chair_0", 1517);
        AssertScenePropSorting(sceneText, "nursery_chest_0", 1557);
        AssertScenePropSorting(sceneText, "nursery_table_0", 1524);
        AssertScenePropSorting(sceneText, "dog_toy_nursery_0", 1605);
        AssertScenePropSorting(sceneText, "Grand_entrance_railing_left_0", 1601);
        AssertScenePropSorting(sceneText, "grand_entrance_railing_right_0", 1616);
    }

    [Test]
    public void GameplayDoesNotKeepMapDropdownUi()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Not.Match(@"(?m)^\s*m_Name: Map$"), "Gameplay should not keep the old map dropdown panel.");
        Assert.That(sceneText, Does.Not.Match(@"(?m)^\s*m_Name: MapTrigger$"), "Gameplay should not keep the old map dropdown opener.");
        Assert.That(sceneText, Does.Not.Contain("MapAnimator"), "Gameplay should not reference the old map dropdown animator.");
        Assert.That(sceneText, Does.Not.Contain("CameraAreaController"), "Gameplay should not reference old map camera-area buttons.");
        Assert.That(File.Exists("Assets/Map/MapAnimator.cs"), Is.False, "The old map dropdown animator script should stay deleted.");
        Assert.That(File.Exists("Assets/Map/CameraAreaController.cs"), Is.False, "The old map camera-area button script should stay deleted.");
        Assert.That(File.Exists("Assets/Art/UI/map_labeled_transparent.png"), Is.False, "The old map dropdown art should stay deleted.");
    }

    [Test]
    public void ChapterManagerHasChapter2DebugSkipButton()
    {
        string chapterManagerText = File.ReadAllText(ChapterManagerPath);

        Assert.That(chapterManagerText, Does.Contain("showSkipToChapter2Button"));
        Assert.That(chapterManagerText, Does.Contain("Button_SkipToChapter2"));
        Assert.That(chapterManagerText, Does.Contain("SkipToChapter2ForTesting"));
        Assert.That(chapterManagerText, Does.Contain("ResolveChapter2Controller(true)"));
        Assert.That(chapterManagerText, Does.Contain("BeginChapter2(this)"));
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
        Assert.That(sceneText, Does.Not.Contain("m_Name: Button_Grand_Entrance_Hall"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: Button_Library"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: Button_Ballroom"));
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
        Assert.That(sceneText, Does.Contain("cropBackgroundToFill: 0"), "The legacy RawImage crop toggle can stay off because active room stages cover the viewport in code.");
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
        Assert.That(cameraManagerText, Does.Contain("GetRoomStageViewportScale"), "Room-stage framing should be computed from the room image and viewport.");
        Assert.That(cameraManagerText, Does.Contain("return Mathf.Max(widthScale, heightScale);"), "Room stages should always cover the whole Game view so no gray outline is visible.");
        Assert.That(cameraManagerText, Does.Contain("GetCurrentHorizontalPanSpeed"), "Edge panning should accelerate while the player holds the cursor at the edge.");
        Assert.That(cameraManagerText, Does.Contain("SmoothRoomZoom"), "Mouse-wheel zoom should be damped instead of stepping between crop values.");
        Assert.That(cameraManagerText, Does.Contain("activeRoomStage.localScale = new Vector3(stageScale, stageScale, 1f)"), "Regular zoom should scale the whole room stage so hitboxes and art share one transform.");
        Assert.That(cameraManagerText, Does.Contain("ResetRoomLookForRoomChange"), "Each new room should enter from a centered default view instead of inheriting the previous room's pan/zoom.");
        Assert.That(cameraManagerText, Does.Contain("Canvas.willRenderCanvases"), "The room stage must get a final pre-render layout pass after the Canvas resolves its true viewport size.");
        Assert.That(cameraManagerText, Does.Contain("HasRoomViewportSizeChanged"), "Room-stage layout must react to Canvas viewport changes, not only Screen.width and Screen.height.");
        Assert.That(cameraManagerText, Does.Match(@"applyingCanvasPreRenderLayout = true;\r?\n\s*try\r?\n\s*\{\r?\n\s*if \(!roomLayoutDirty && !HasRoomViewportSizeChanged\(\)\)"), "The pre-render recursion guard must be active before viewport checks can force another canvas update.");
        Assert.That(cameraManagerText, Does.Contain("if (!applyingCanvasPreRenderLayout)"), "GetUsableRectSize must not call Canvas.ForceUpdateCanvases from inside Canvas.willRenderCanvases.");
        Assert.That(cameraManagerText, Does.Match(@"EnsureBackgroundMaterialAssigned\(\);\r?\n\s*if \(updateBackground"), "Switching rooms should restore the correct background material before applying room-stage layout.");
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

    private static void AssertScenePropSorting(string sceneText, string propName, int sortingOrder)
    {
        string escapedName = Regex.Escape(propName);
        string pattern = $@"m_Name: {escapedName}[\s\S]*?m_SortingLayer: 2[\s\S]*?m_SortingOrder: {sortingOrder}\b";

        Assert.That(sceneText, Does.Match(pattern), $"{propName} should keep its authored People-layer sorting order.");
    }

    private static void AssertScenePropSprite(string sceneText, string propName, string spriteGuid)
    {
        string escapedName = Regex.Escape(propName);
        string escapedGuid = Regex.Escape(spriteGuid);
        string pattern = $@"m_Name: {escapedName}[\s\S]*?m_Sprite: \{{fileID: [-\d]+, guid: {escapedGuid}, type: 3\}}";

        Assert.That(sceneText, Does.Match(pattern), $"{propName} should keep its intended sprite asset.");
    }

}
