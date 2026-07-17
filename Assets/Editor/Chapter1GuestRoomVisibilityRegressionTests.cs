using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class Chapter1GuestRoomVisibilityRegressionTests
{
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string Chapter1SceneActionPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string ActorRoomStatePath = "Assets/Scripts/Story/ActorRoomState.cs";

    [Test]
    public void MoveGroupToDrawingRoomDoesNotHideGuestsAtDoor()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string methodBody = ExtractMethodBody(controllerText, "CompleteGuestDrawingRoomArrival");

        Assert.That(methodBody, Does.Not.Contain("SetGuestVisibleAfterDrawingRoomExit(guest, false)"), "Guests who enter the Drawing Room should stay present for later room views.");
        Assert.That(methodBody, Does.Not.Contain("SetVisibleByChapterState(false)"), "Drawing Room guests should not be hidden by chapter state after leaving the entrance.");
        Assert.That(methodBody, Does.Not.Contain("disappeared at drawing room door"), "The Drawing Room transition should not be modeled as guests disappearing.");
    }

    [Test]
    public void MoveGroupToDrawingRoomKeepsGuestsVisibleThroughActorRoomState()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string methodBody = ExtractMethodBody(controllerText, "CompleteGuestDrawingRoomArrival");
        string groupCompletionBody = ExtractMethodBody(controllerText, "CompleteEntranceGroupDrawingRoomArrival");

        Assert.That(methodBody, Does.Contain("SetCurrentRoom(drawingRoomId)"), "Guests should logically move to the Drawing Room.");
        Assert.That(methodBody, Does.Contain("SetAvailableInCurrentChapter(true)"), "Guests in the Drawing Room should remain available in Chapter 1.");
        Assert.That(methodBody, Does.Contain("SetVisibleByChapterState(true)"), "Room visibility, not chapter invisibility, should decide whether Drawing Room guests render.");
        Assert.That(methodBody, Does.Contain("ApplyDrawingRoomWaitingPose(guest)"), "Guests should get their drawing-room waiting pose when they enter.");
        Assert.That(groupCompletionBody, Does.Contain("guest.Seated = true"), "Both guests should be marked waiting/seated together before Drawing Room presentation runs.");
        Assert.That(methodBody, Does.Contain("SetInteractable(false)"), "Guests should not become interactive just because they are visible in the Drawing Room.");
    }

    [Test]
    public void Chapter2SkipStagesGuestsVisibleInDrawingRoom()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string skipMethodBody = ExtractMethodBody(controllerText, "public void PrepareGuestsForChapter2Skip");
        string refreshMethodBody = ExtractMethodBody(controllerText, "public void RefreshChapter2SkipGuestVisibilityAfterRoomChange");
        string stageAllMethodBody = ExtractMethodBody(controllerText, "private int StageRequiredGuestsInDrawingRoomForChapter2");
        string stageMethodBody = ExtractMethodBody(controllerText, "private void StageGuestInDrawingRoomForChapter2");

        Assert.That(skipMethodBody, Does.Contain("StopAllCoroutines()"), "Debug skip should stop Chapter 1 guest movement before staging Chapter 2.");
        Assert.That(skipMethodBody, Does.Contain("DisableAllChapter1CoatPickupsForChapter2Skip()"), "Debug skip should disable old Chapter 1 coat targets before resetting guest state.");
        Assert.That(skipMethodBody, Does.Contain("ResetGuestStates(true)"), "Debug skip should build guest runtime state even if Chapter 1 was skipped early.");
        Assert.That(skipMethodBody, Does.Contain("coatCloset?.ClearStoredCoats()"), "Debug skip should rebuild the closet contents for a clean Chapter 2 handoff.");
        Assert.That(skipMethodBody, Does.Contain("StageRequiredGuestsInDrawingRoomForChapter2"), "Debug skip should place each required guest in the Drawing Room.");
        Assert.That(refreshMethodBody, Does.Match(@"ResetGuestStates\(true\)[\s\S]*StageRequiredGuestsInDrawingRoomForChapter2\(\)"), "Debug skip should force-build and stage the full required guest roster after Chapter 2 moves to the Drawing Room.");
        Assert.That(refreshMethodBody, Does.Not.Contain("IsGuestReadyForChapter2SkipRoomView"), "Debug skip should not preserve a partial Chapter 1 coat-progress state.");
        Assert.That(stageAllMethodBody, Does.Contain("GetRequiredGuestCountForCurrentRun()"), "Debug skip should stage the configured required guest count, not only already-arrived guests.");
        Assert.That(stageAllMethodBody, Does.Contain("ResetGuestStates(true)"), "Debug skip should rebuild guest state if the current runtime list is short.");
        Assert.That(stageAllMethodBody, Does.Match(@"StageGuestInDrawingRoomForChapter2\(guestStates\[i\]\)[\s\S]*HideGuestCoatsForChapter2Skip\(\)"), "Every staged guest should be placed before coat visuals are swept.");
        Assert.That(stageMethodBody, Does.Contain("SetGuestVisibleAfterDrawingRoomExit(guest, true)"), "Skipped guests should have their scene objects active.");
        Assert.That(stageMethodBody, Does.Contain("SetCurrentRoom(drawingRoomId)"), "Skipped guests should logically be in the Drawing Room.");
        Assert.That(stageMethodBody, Does.Contain("SetAvailableInCurrentChapter(true)"), "Skipped guests should be available for Chapter 2 setup.");
        Assert.That(stageMethodBody, Does.Contain("SetVisibleByChapterState(true)"), "Skipped guests should not remain hidden by Chapter 1 reset state.");
        Assert.That(stageMethodBody, Does.Contain("SetInteractable(false)"), "Drawing Room staging should not make guests clickable before Chapter 2 search begins.");
        Assert.That(stageMethodBody, Does.Contain("ApplyDrawingRoomWaitingPose(guest)"), "Skipped guests should match the normal Drawing Room handoff pose.");
        Assert.That(stageMethodBody, Does.Contain("guest.Seated = true"), "Skipped guests should still be marked as waiting/seated for chapter progression.");
        Assert.That(stageMethodBody, Does.Match(@"ApplyState\(\)[\s\S]*HideGuestCoatVisualsForChapter2Skip\(guest\)"), "Guest coat child renderers should be hidden after ActorRoomState refreshes guest visibility.");
        Assert.That(stageMethodBody, Does.Contain("StoreGuestCoatForChapter2Skip(guest)"), "Debug skip should put each staged guest coat into the closet.");
        Assert.That(stageMethodBody, Does.Contain("CoatStored = true"), "Debug skip should mark the Chapter 1 coat flow as resolved for staged guests.");
        Assert.That(controllerText, Does.Match(@"HideGuestCoatVisualsForChapter2Skip\s*\([^)]*\)\s*\{[\s\S]*IndexOf\(\""coat\"", StringComparison\.OrdinalIgnoreCase\)[\s\S]*HideCoatVisualObjectForChapter2Skip\(child\.gameObject\)"), "Debug skip should hide guest coat child visuals, not just pickup components.");
        Assert.That(controllerText, Does.Match(@"HideGuestCoatsForChapter2Skip\s*\([^)]*\)\s*\{[\s\S]*HideGuestCoatVisualsForChapter2Skip\(guestStates\[i\]\)[\s\S]*HideAllGuestCoatVisualsForChapter2Skip\(\)"), "Debug skip should hide both known guest coat children and scene coat visuals.");
        Assert.That(controllerText, Does.Match(@"HideAllGuestCoatVisualsForChapter2Skip\s*\([^)]*\)\s*\{[\s\S]*Resources\.FindObjectsOfTypeAll<GameObject>\(\)[\s\S]*IsChapter2SkipCoatVisualObject"), "Debug skip should sweep scene coat visuals that are not attached to the reset guest runtime state.");
        Assert.That(controllerText, Does.Match(@"IsChapter2SkipCoatVisualObject\s*\([^)]*\)\s*\{[\s\S]*IndexOf\(\""coatcutout\"", StringComparison\.OrdinalIgnoreCase\)[\s\S]*StartsWith\(\""Coat_\"", StringComparison\.OrdinalIgnoreCase\)"), "Debug skip should recognize authored coatcutout sprites and runtime coat objects.");
        Assert.That(controllerText, Does.Match(@"HideCoatVisualObjectForChapter2Skip\s*\([^)]*\)\s*\{[\s\S]*Collider2D[\s\S]*enabled = false[\s\S]*SetCoatPickupRenderersVisible\(coatObject, false\)[\s\S]*coatObject\.SetActive\(false\)"), "Debug skip should hide coat renderers and remove coat colliders from the scene.");
        Assert.That(controllerText, Does.Match(@"StoreGuestCoatForChapter2Skip\s*\([^)]*\)\s*\{[\s\S]*coatCloset\.StoreCoat\(coatId\)"), "Debug skip should use the closet storage system, not only guest flags.");
        Assert.That(controllerText, Does.Match(@"DisableAllChapter1CoatPickupsForChapter2Skip\s*\([^)]*\)\s*\{[\s\S]*FindObjectsByType<Chapter1CoatPickup>\(FindObjectsInactive\.Include\)"), "Debug skip should find stale coat pickup objects that no longer belong to the reset guest state.");
        Assert.That(controllerText, Does.Match(@"DisableChapter1CoatPickupForChapter2Skip\s*\([^)]*\)\s*\{[\s\S]*HideCoatVisualObjectForChapter2Skip\(coatPickup\.gameObject\)"), "Debug skip should remove leftover coat colliders so they cannot intercept Chapter 2 guest clicks.");
    }

    [Test]
    public void ActiveChapterGuestsKeepActorRoomStateEnabled()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);

        Assert.That(controllerText, Does.Not.Contain("actorState.enabled = false"), "Active Chapter 1 guests should use ActorRoomState for room visibility instead of disabling it.");
        Assert.That(controllerText, Does.Not.Contain("guestState.ActorState.enabled = false"), "Active Chapter 1 guests should use ActorRoomState for room visibility instead of disabling it.");
    }

    [Test]
    public void GuestDrawingRoomMovementDoesNotReparentIntoPresentationHierarchy()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string moveGuestObjectBody = ExtractDeclaredMethodBody(controllerText, "MoveGuestObjectToRoomContent");

        Assert.That(moveGuestObjectBody, Does.Match(@"guest\.ActorState != null \|\| IsChapterSceneGuest\(guest\.GuestObject\)[\s\S]*return;[\s\S]*SetParent"), "Authored/ActorRoomState guests should stay in the Hierarchy and rely on room state instead of room-content parenting.");
    }

    [Test]
    public void WorldSpaceGuestsUseWorldSpaceDrawingRoomTargets()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string sceneText = File.ReadAllText(GameplayScenePath);
        string entryMethodBody = ExtractMethodBody(controllerText, "GetWorldDrawingRoomEntryPosition");
        string entryBaseMethodBody = ExtractMethodBody(controllerText, "GetWorldDrawingRoomEntryBasePosition");
        string editableTargetMethodBody = ExtractMethodBody(controllerText, "TryGetGrandEntranceDrawingRoomGuestTargetPosition");
        string doorLookupMethodBody = ExtractMethodBody(controllerText, "TryGetGrandEntranceDrawingRoomDoorPosition");
        string spotMethodBody = ExtractMethodBody(controllerText, "ResolveDrawingRoomSpotForGuest");

        Assert.That(entryMethodBody, Does.Contain("GetWorldDrawingRoomEntryBasePosition(guestState)"), "World-space guest exit movement should use a converted visible entrance-hall doorway target.");
        Assert.That(entryBaseMethodBody, Does.Match(@"TryGetGrandEntranceDrawingRoomGuestTargetPosition[\s\S]*TryGetGrandEntranceDrawingRoomDoorPosition"), "Guest movement should prefer the hand-authored straight-line target before falling back to the door hitbox.");
        Assert.That(editableTargetMethodBody, Does.Contain("FindAnchor(DrawingRoomDoorTargetAnchorId, entryRoomId)"), "The Drawing Room door walking target should be editable as a RoomAnchor in the Entrance Hall.");
        Assert.That(editableTargetMethodBody, Does.Contain("FindSceneObjectByExactName(DrawingRoomDoorTargetAnchorId)"), "The editable target should still be found if RoomAnchor data is stale.");
        Assert.That(entryBaseMethodBody, Does.Contain("TryGetGrandEntranceDrawingRoomDoorPosition"), "World-space guests should walk to the visible Grand Entrance Hall Drawing Room door trigger.");
        Assert.That(entryBaseMethodBody, Does.Contain("GetWorldVisibleAnchorPosition"), "World-space guest movement should convert room-stage anchors into guest world coordinates.");
        Assert.That(doorLookupMethodBody, Does.Contain("activeInHierarchy"), "Drawing Room movement should prefer the active Grand Entrance Hall door trigger over inactive duplicate room views.");
        Assert.That(entryMethodBody, Does.Not.Contain("GetEntranceDrawingRoomExitPosition"), "World-space guest movement must not chase a UI/RectTransform Drawing Room door coordinate.");
        Assert.That(spotMethodBody, Does.Contain("FindDrawingRoomGuestPoint(guest.GuestIndex)"), "Every guest should use the same authored Drawing Room anchor source.");
        Assert.That(spotMethodBody, Does.Not.Contain("GetWorldDrawingRoomSeatPosition"), "Drawing Room placement should not synthesize a second world-space seat position.");
        Assert.That(sceneText, Does.Contain("m_Name: GuestDrawingRoomDoorTarget"), "Gameplay should expose an editable Entrance Hall target for the Drawing Room guest walk path.");
        Assert.That(sceneText, Does.Contain("anchorId: GuestDrawingRoomDoorTarget"), "The editable Drawing Room guest walk target should have a RoomAnchor id.");
        Assert.That(sceneText, Does.Contain("roomId: Grand Entrance Hall"), "The editable Drawing Room guest walk target should belong to the Entrance Hall.");
        Assert.That(sceneText, Does.Contain("guestGroupCount: 4"), "Chapter 1 should retain four arrival groups.");
        Assert.That(sceneText, Does.Contain("guestsPerArrivalGroup: 2"), "Every Chapter 1 arrival/departure group must remain a pair.");

        Match targetPositionMatch = Regex.Match(
            sceneText,
            @"m_Name: GuestDrawingRoomDoorTarget[\s\S]{0,600}?m_LocalPosition: \{x: (?<x>-?[\d.]+), y: (?<y>-?[\d.]+), z:");
        Assert.That(targetPositionMatch.Success, Is.True, "The Drawing Room departure target position should remain serialized and testable.");
        float targetX = float.Parse(targetPositionMatch.Groups["x"].Value, CultureInfo.InvariantCulture);
        float targetY = float.Parse(targetPositionMatch.Groups["y"].Value, CultureInfo.InvariantCulture);
        Assert.That(targetX, Is.EqualTo(-704f).Within(0.01f), "The departure route should still end at the left Drawing Room passage.");
        Assert.That(targetY, Is.EqualTo(-210f).Within(0.01f), "Guest feet should follow the lower floor route instead of crossing the rear wall.");
        Assert.That(targetY, Is.LessThan(-156.5f), "The route target must stay below the Drawing Room threshold and inside the Entrance Hall walkable floor.");
    }

    [Test]
    public void LiveDoorAnswerUsesStableEntranceWorldPositions()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string sceneText = File.ReadAllText(GameplayScenePath);
        string admitBody = ExtractDeclaredMethodBody(controllerText, "AdmitGuestToEntranceHall");
        string placeDoorBody = ExtractDeclaredMethodBody(controllerText, "PlaceGuestAtDoorArrival");
        string placeFeetBody = ExtractDeclaredMethodBody(controllerText, "PlaceGuestFeetAtPosition");
        string doorArrivalBaseBody = ExtractDeclaredMethodBody(controllerText, "GetWorldDoorArrivalBasePosition");
        string doorArrivalTargetBody = ExtractDeclaredMethodBody(controllerText, "GetWorldDoorArrivalTarget");
        string answerSpotBody = ExtractDeclaredMethodBody(controllerText, "TryGetWorldFrontDoorAnswerSpot");
        string waitSpotBody = ExtractDeclaredMethodBody(controllerText, "GetEntranceHallGuestSpot");
        string interactionTargetBody = ExtractDeclaredMethodBody(controllerText, "GetFrontDoorInteractionTransform");
        string conversionBody = ExtractDeclaredMethodBody(controllerText, "TryGetWorldPositionForGuestTarget");
        string guestArrivalDoorBlock = ExtractObjectBlock(sceneText, "GuestArrival_Door");
        string doorAnswerTriggerBlock = ExtractObjectBlock(sceneText, "Door_answer_trigger");
        string drawingRoomSideSpotBlock = ExtractObjectBlock(sceneText, "DrawingRoomSideButlerSpot");
        string drawingRoomDoorTriggerBlock = ExtractObjectBlock(sceneText, "DoorTrigger_GEH_DrawingRoom");

        Assert.That(controllerText, Does.Contain("FrontDoorGuestSpawnAnchorId"), "Front-door guest spawning should have a named anchor separate from drawing-room door targets.");
        Assert.That(admitBody, Does.Contain("PrepareGuestAtDoorArrival(guest)"), "The initial door spawn should use the shared feet-aware placement path.");
        Assert.That(admitBody, Does.Contain("GetEntranceHallGuestSpot(guest)"), "The walk inward should target the guest's authored physical anchor.");
        Assert.That(admitBody, Does.Not.Contain("PlaceGuestAt(guest, arrivalPoint"), "UI guests should not bypass feet-aware door spawning by placing their transform directly on the front-door anchor.");
        Assert.That(admitBody, Does.Not.Contain("CreateRuntimeAnchor"), "Entrance waiting should not calculate a replacement runtime target.");
        Assert.That(placeDoorBody, Does.Contain("PlaceGuestFeetAtPosition"), "Door arrival should use the shared room-anchor placement path.");
        Assert.That(placeDoorBody, Does.Not.Contain("RoomProjectedEntity"), "Door arrival should have one authored placement path, not a projection-side relocation path.");
        Assert.That(placeFeetBody, Does.Not.Contain("TryGetGuestFeetWorldPoint"), "Door arrival must not derive actor-root placement from animation renderer bounds.");
        Assert.That(placeFeetBody, Does.Not.Contain("feetOffset"), "Door arrival must not add a frame-dependent X/Y correction.");
        Assert.That(placeFeetBody, Does.Contain("guestTransform.position = targetPosition"), "Door arrival should assign the authored room anchor directly to the persistent Guest root.");
        Assert.That(placeFeetBody, Does.Contain("BindGuestToRoomStagePoint(guestState, roomStageTarget)"), "World guests should remain locked to the door anchor until movement begins.");
        Assert.That(placeDoorBody, Does.Contain("GetWorldDoorArrivalBasePosition(guestState)"), "World-space door spawning should begin at the same shared front-door foot point.");
        Assert.That(placeDoorBody, Does.Not.Contain("GetWorldGuestGridOffset"), "Door-answer spawning should not use batch/grid offsets.");
        Assert.That(controllerText, Does.Not.Contain("GetDoorArrivalPairSlotOffset"), "Door spawning should not retain a second pair-offset pathway.");
        Assert.That(doorArrivalBaseBody, Does.Match(@"GetWorldDoorArrivalTarget\(\)[\s\S]*TryGetWorldPositionForGuestTarget[\s\S]*TryGetWorldFrontDoorAnswerSpot"), "Front-door spawning should use GuestArrival_Door before the Butler answer-point fallback.");
        Assert.That(doorArrivalTargetBody, Does.Match(@"frontDoorArrivalPoint[\s\S]*return frontDoorArrivalPoint[\s\S]*FindAnchor\(FrontDoorGuestSpawnAnchorId, entryRoomId\)"), "GuestArrival_Door should be the serialized authority with a RoomAnchor lookup fallback.");
        Assert.That(doorArrivalTargetBody, Does.Not.Contain("drawingRoomSideButlerSpot"), "Front-door spawning must not use the left Drawing Room side-door marker.");
        Assert.That(answerSpotBody, Does.Contain("TryGetWorldPointFromLogicalPosition(frontDoorAnswerSpot"), "The cached Butler door-answer floor point should be converted back to world space for guest feet.");
        Assert.That(waitSpotBody, Does.Contain("entranceHallGuestSpots[guestIndex]"), "Entrance waiting should resolve the guest's stable authored spot directly.");
        Assert.That(interactionTargetBody, Does.Match(@"frontDoorArrivalPoint[\s\S]*return frontDoorArrivalPoint[\s\S]*FindDoorAnswerTriggerObject"), "The butler should walk to the centered front-door arrival point before answering the door.");
        Assert.That(interactionTargetBody, Does.Not.Contain("drawingRoomSideButlerSpot"), "Front-door click/approach logic must not fall back to the left Drawing Room side-door marker.");
        Assert.That(conversionBody, Does.Not.Contain("target.GetComponentInParent<Canvas>(true) == null"), "Visible anchor conversion must work for non-Canvas room-stage anchors as well as UI anchors.");
        Assert.That(conversionBody, Does.Contain("TryGetTargetScreenPosition"), "Visible anchor conversion should preserve what the player sees on screen.");
        Assert.That(conversionBody, Does.Contain("mainCamera.ScreenToWorldPoint"), "Drawing Room anchor conversion should land on the guest world plane instead of raw room-stage coordinates.");
        Assert.That(guestArrivalDoorBlock, Does.Contain("m_Name: GuestArrival_Door"), "Gameplay should retain the dedicated, movable guest-foot anchor.");
        Assert.That(doorAnswerTriggerBlock, Does.Contain("m_LocalPosition: {x: -7.216162, y: -13.4132805"), "The clickable door-answer trigger should remain separate from the guest spawn threshold.");
        Assert.That(drawingRoomSideSpotBlock, Does.Contain("m_LocalPosition: {x: -684"), "The old ButlerGreetingSpot marker is actually beside the left Drawing Room door and must stay labeled that way.");
        Assert.That(drawingRoomDoorTriggerBlock, Does.Contain("m_AnchoredPosition: {x: -687.8042"), "The Drawing Room trigger proves the side marker is not the front entrance.");
        Assert.That(sceneText, Does.Contain("drawingRoomSideButlerSpot: {fileID: 140767560}"), "The serialized scene reference should label the left-door marker as a Drawing Room side spot.");
        Assert.That(sceneText, Does.Not.Contain("butlerDoorSpot:"), "The scene should not serialize the left-door marker under the old front-door-adjacent name.");
        Assert.That(sceneText, Does.Not.Contain("anchorId: ButlerGreetingSpot"), "The scene anchor id should not label the left Drawing Room side marker as the Butler greeting/front-door spot.");
        Assert.That(sceneText, Does.Not.Contain("Placemark_guests_entrance"), "The obsolete spawn placemark should no longer compete with GuestArrival_Door.");
        Assert.That(sceneText, Does.Contain("entranceHallGuestSpots:"), "The Chapter 1 controller should serialize all eight editable wait spots.");
    }

    [Test]
    public void EntranceDoorSpawnAlignsEveryGuestToOneFootAnchorAtAuthoredScale()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string prepareMethodBody = ExtractDeclaredMethodBody(controllerText, "PrepareGuestAtDoorArrival");
        string placeMethodBody = ExtractDeclaredMethodBody(controllerText, "PlaceGuestAtDoorArrival");
        string basePositionMethodBody = ExtractDeclaredMethodBody(controllerText, "GetWorldDoorArrivalBasePosition");

        int firstPlacementIndex = prepareMethodBody.IndexOf("PlaceGuestAtDoorArrival(guest)", StringComparison.Ordinal);
        int finalPlacementIndex = prepareMethodBody.LastIndexOf("PlaceGuestAtDoorArrival(guest)", StringComparison.Ordinal);

        Assert.That(firstPlacementIndex, Is.GreaterThanOrEqualTo(0), "Entrance guests should be aligned to the authored door-foot anchor.");
        Assert.That(finalPlacementIndex, Is.EqualTo(firstPlacementIndex), "Static-scale guests need one placement pass, not a scale-and-realign cycle.");
        Assert.That(placeMethodBody, Does.Contain("GetWorldDoorArrivalTarget()"), "Door placement should resolve the one authoritative GuestArrival_Door anchor.");
        Assert.That(placeMethodBody, Does.Contain("GetWorldDoorArrivalBasePosition(guestState)"), "World-space guests should use that same shared door-foot point.");
        Assert.That(placeMethodBody, Does.Not.Contain("GetDoorArrivalPairSlotOffset"), "All guests should begin at the exact same door-foot point; spacing belongs only to their later wait formation.");
        Assert.That(prepareMethodBody, Does.Not.Match(@"\.localScale\s*="), "Door preparation must not resize a guest body.");
        Assert.That(controllerText, Does.Not.Contain("GetDoorArrivalPairSlotOffset"), "There should be one door spawn path, without a second pair-offset pathway.");
        Assert.That(basePositionMethodBody, Does.Contain("TryGetWorldPositionForGuestTarget"), "World placement should retain room-stage conversion so camera pan and zoom cannot move the spawn point relative to the door.");
    }

    [Test]
    public void GuestArrivalDoorIsTheOnlyEditableFootSpawnAndKeepsAuthoredScale()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string sceneText = File.ReadAllText(GameplayScenePath);
        string targetMethodBody = ExtractDeclaredMethodBody(controllerText, "GetWorldDoorArrivalTarget");
        string prepareMethodBody = ExtractDeclaredMethodBody(controllerText, "PrepareGuestAtDoorArrival");
        string placeFeetMethodBody = ExtractDeclaredMethodBody(controllerText, "PlaceGuestFeetAtPosition");

        int firstPlacementIndex = prepareMethodBody.IndexOf("PlaceGuestAtDoorArrival(guest)", StringComparison.Ordinal);
        int finalPlacementIndex = prepareMethodBody.LastIndexOf("PlaceGuestAtDoorArrival(guest)", StringComparison.Ordinal);

        Assert.That(targetMethodBody, Does.Match(@"frontDoorArrivalPoint[\s\S]*return frontDoorArrivalPoint[\s\S]*FindAnchor\(FrontDoorGuestSpawnAnchorId, entryRoomId\)"), "Door spawning should resolve the serialized GuestArrival_Door first and retain its RoomAnchor lookup fallback.");
        Assert.That(targetMethodBody, Does.Not.Contain("GetGuestEntranceSpawnPlacemark"), "A second placemark must not override GuestArrival_Door in play mode.");
        Assert.That(controllerText, Does.Not.Contain("GuestEntranceSpawnPlacemarkId"), "The removed door placemark must not remain as a legacy spawn pathway.");
        Assert.That(controllerText, Does.Not.Contain("guestEntranceSpawnPlacemark"), "The removed door placemark must not remain cached at runtime.");
        Assert.That(sceneText, Does.Not.Contain("m_Name: Placemark_guests_entrance"), "Gameplay should expose only GuestArrival_Door for guest foot spawning.");
        Assert.That(sceneText, Does.Match(@"anchorId: GuestArrival_Door\s+roomId: Grand Entrance Hall\s+showSceneGizmo: 1"), "GuestArrival_Door should be visible and easy to drag in the Scene view.");
        Assert.That(sceneText, Does.Not.Contain("roomGuestScaleMultiplier"), "Legacy per-room guest scale data should be removed with its component.");
        Assert.That(firstPlacementIndex, Is.GreaterThanOrEqualTo(0), "The guest must be placed at GuestArrival_Door.");
        Assert.That(finalPlacementIndex, Is.EqualTo(firstPlacementIndex), "The door flow should not repeat placement around a hidden scale mutation.");
        Assert.That(placeFeetMethodBody, Does.Contain("BindGuestToRoomStagePoint(guestState, roomStageTarget)"), "A spawned world guest should remain attached to GuestArrival_Door until walking begins.");
        Assert.That(placeFeetMethodBody, Does.Not.Match(@"\.localScale\s*="), "Feet-aware placement must leave authored body scale untouched.");
    }

    [Test]
    public void EntranceHallWaitingUsesEightAuthoredCameraStableAnchors()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string sceneText = File.ReadAllText(GameplayScenePath);
        string admitMethodBody = ExtractDeclaredMethodBody(controllerText, "AdmitGuestToEntranceHall");
        string lookupMethodBody = ExtractDeclaredMethodBody(controllerText, "GetEntranceHallGuestSpot");

        Assert.That(controllerText, Does.Contain("private const int EntranceHallGuestSpotCount = 8"), "The authored Entrance Hall formation should have one stable spot per guest.");
        Assert.That(controllerText, Does.Contain("private Transform[] entranceHallGuestSpots"), "The eight physical spots should be serialized and directly editable.");
        Assert.That(admitMethodBody, Does.Contain("Transform waitSpot = GetEntranceHallGuestSpot(guest)"), "Arrival movement should target the guest's authored physical spot.");
        Assert.That(admitMethodBody, Does.Not.Contain("CreateRuntimeAnchor"), "Entrance waiting should not recreate calculated runtime targets.");
        Assert.That(lookupMethodBody, Does.Contain("guestState.GuestIndex"), "A guest's stable roster index should select its authored spot.");
        Assert.That(lookupMethodBody, Does.Contain("entranceHallGuestSpots[guestIndex]"), "Each guest should map directly to one serialized anchor.");
        Assert.That(lookupMethodBody, Does.Not.Contain("FindAnchor"), "Entrance waiting must not repair or replace a manually authored spot at runtime.");
        Assert.That(lookupMethodBody, Does.Not.Contain("FindSceneObjectByExactName"), "Entrance waiting must not substitute a name-based scene object for a manually authored spot.");
        Assert.That(controllerText, Does.Not.Contain("ResolveEntranceHallGuestSpots"), "Runtime code must not rewrite the serialized entrance spot array.");
        Assert.That(controllerText, Does.Not.Contain("EntranceHallGuestSpotPrefix"), "Runtime code must not retain a name-based entrance spot repair path.");

        string[] legacyNames =
        {
            "EntranceHallGuestAnchor",
            "GetEntranceWaitPosition",
            "GetWorldEntranceWaitPosition",
            "GetEntranceGroupOffset",
            "GetWorldEntranceGroupOffset",
            "entranceGuestSpacing",
            "worldEntranceGuestSpacing",
            "snapGuestsIntoEntranceForFirstVisualPass"
        };

        for (int i = 0; i < legacyNames.Length; i++)
        {
            Assert.That(controllerText, Does.Not.Contain(legacyNames[i]), $"Legacy entrance formation path '{legacyNames[i]}' should be removed.");
        }

        MatchCollection sceneSpotNames = Regex.Matches(sceneText, @"m_Name: EntranceGuestSpot_(\d{2})");
        Assert.That(sceneSpotNames.Count, Is.EqualTo(8), "Gameplay should contain exactly eight Entrance Hall guest spot objects.");
        Assert.That(
            sceneText,
            Does.Contain(
                "entranceHallGuestSpots:\n" +
                "  - {fileID: 3501000031}\n" +
                "  - {fileID: 3501000034}\n" +
                "  - {fileID: 3501000037}\n" +
                "  - {fileID: 3501000040}\n" +
                "  - {fileID: 3501000043}\n" +
                "  - {fileID: 3501000046}\n" +
                "  - {fileID: 3501000049}\n" +
                "  - {fileID: 3501000052}"),
            "Guest roster order must keep its direct serialized reference to each hand-authored spot.");

        float[,] expectedPositions =
        {
            { 111.4f, -143.7f },
            { 159.9f, -143.6f },
            { 80.8f, -194.6f },
            { 130.6f, -194.6f },
            { 48.4f, -246.4f },
            { 101.3f, -245.5f },
            { 14.2f, -297.4f },
            { 66.5f, -297.4f }
        };
        HashSet<string> positions = new HashSet<string>();

        for (int i = 1; i <= 8; i++)
        {
            string spotName = $"EntranceGuestSpot_{i:00}";
            string spotBlock = ExtractObjectBlock(sceneText, spotName);
            Match position = Regex.Match(spotBlock, @"m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}");

            Assert.That(spotBlock, Does.Contain($"anchorId: {spotName}"), $"{spotName} should be a physical RoomAnchor.");
            Assert.That(spotBlock, Does.Contain("roomId: Grand Entrance Hall"), $"{spotName} should belong to the Entrance Hall stage.");
            Assert.That(spotBlock, Does.Contain("showSceneGizmo: 1"), $"{spotName} should be visible and draggable in the Scene view.");
            Assert.That(spotBlock, Does.Contain("m_LocalScale: {x: 1, y: 1, z: 1}"), $"{spotName} must keep its authored scale.");
            Assert.That(spotBlock, Does.Contain("m_Father: {fileID: 3501000001}"), $"{spotName} must remain under the Entrance Hall Anchors container.");
            Assert.That(position.Success, Is.True, $"{spotName} should have an authored local position.");
            Assert.That(positions.Add(position.Value), Is.True, $"{spotName} should not overlap another waiting spot.");
            Assert.That(float.Parse(position.Groups[1].Value, CultureInfo.InvariantCulture), Is.EqualTo(expectedPositions[i - 1, 0]).Within(0.001f), $"{spotName} x must remain at Hamza's recovered hand-authored value.");
            Assert.That(float.Parse(position.Groups[2].Value, CultureInfo.InvariantCulture), Is.EqualTo(expectedPositions[i - 1, 1]).Within(0.001f), $"{spotName} y must remain at Hamza's recovered hand-authored value.");
            Assert.That(float.Parse(position.Groups[3].Value, CultureInfo.InvariantCulture), Is.EqualTo(-7691.114f).Within(0.001f), $"{spotName} z must remain on the Entrance Hall anchor plane.");
        }

        Assert.That(sceneText, Does.Not.Contain("m_Name: EntranceHallGuestAnchor"), "The old single formation anchor should be removed from Gameplay.");
    }

    [Test]
    public void FrontDoorActionUsesArrivalControllerAnswerSpot()
    {
        string actionText = File.ReadAllText(Chapter1SceneActionPath);
        string startBody = ExtractMethodBody(actionText, "StartFrontDoorApproach");
        string stoppedBody = ExtractMethodBody(actionText, "HandleFrontDoorApproachStopped");
        string closeBody = ExtractMethodBody(actionText, "IsPlayerCloseToFrontDoor");
        int destinationLookupIndex = startBody.IndexOf(
            "arrivalController.TryGetFrontDoorApproachDestination(playerMovement, out Vector2 approachDestination)",
            StringComparison.Ordinal);
        int firstAnswerIndex = startBody.IndexOf("arrivalController.AnswerFrontDoor()", StringComparison.Ordinal);

        Assert.That(startBody, Does.Contain("arrivalController.TryGetFrontDoorApproachDestination(playerMovement, out Vector2 approachDestination)"), "Front-door clicks should use the arrival controller's reachable greeting spot.");
        Assert.That(startBody, Does.Not.Contain("TryFindClosestReachableDestinationToWorldPoint(transform.position"), "Front-door clicks should not fall back to the scene-action object's visual center.");
        Assert.That(destinationLookupIndex, Is.GreaterThanOrEqualTo(0), "Front-door clicks should attempt a door approach destination before any answer logic.");
        Assert.That(firstAnswerIndex, Is.GreaterThan(destinationLookupIndex), "The butler must not answer from the click handler just because he is close to the cached front-door spot.");
        Assert.That(startBody, Does.Not.Match(@"CancelPendingFrontDoorApproach\(\);\s*if \(IsPlayerCloseToFrontDoor\(playerMovement\)\)"), "Every available front-door click should route through a movement attempt before guests are admitted.");
        Assert.That(stoppedBody, Does.Contain("arrivalController.AnswerFrontDoor()"), "Guests should be admitted after the butler's front-door approach stops near the answer spot.");
        Assert.That(closeBody, Does.Contain("arrivalController.IsButlerCloseToFrontDoor(playerMovement)"), "Answering should use the same controller-level proximity check as the movement destination.");
    }

    [Test]
    public void DrawingRoomGuestMovementUsesEditableScenePoints()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string sceneText = File.ReadAllText(GameplayScenePath);
        string spotMethodBody = ExtractMethodBody(controllerText, "ResolveDrawingRoomSpotForGuest");
        string seatMethodBody = ExtractDeclaredMethodBody(controllerText, "ResolveSeatForGuest");
        string placeMethodBody = ExtractMethodBody(controllerText, "PlaceGuestAt");

        Assert.That(controllerText, Does.Contain("DrawingRoomGuestPointPrefix"), "Chapter 1 should name editable Drawing Room guest points consistently.");
        Assert.That(spotMethodBody, Does.Contain("FindDrawingRoomGuestPoint(guest.GuestIndex)"), "Guests should use the editable Drawing Room points directly.");
        Assert.That(spotMethodBody, Does.Not.Contain("IsWorldSpaceGuestObject"), "Anchor selection should not split into competing UI/world presentation paths.");
        Assert.That(seatMethodBody, Does.Contain("return FindDrawingRoomGuestPoint(index);"), "Assigned seats should use the same editable Drawing Room point source.");
        Assert.That(controllerText, Does.Match(@"FindDrawingRoomGuestPoint\s*\([^)]*\)\s*\{[\s\S]*FindAnchor\(pointName, drawingRoomId\)[\s\S]*FindSceneObjectByExactName\(pointName\)"), "Editable guest points should fall back to the physical scene object name if RoomAnchor data is stale.");
        Assert.That(placeMethodBody, Does.Match(@"TryGetWorldPositionForGuestTarget[\s\S]*ActorState\.PlaceAt"), "World-space guests should convert UI room-stage points before falling back to raw Transform placement.");
        Assert.That(placeMethodBody, Does.Match(@"TryGetWorldPositionForGuestTarget[\s\S]*PlaceGuestFeetAtPosition"), "Static world-space placement must use the shared stable room-anchor path.");
        Assert.That(placeMethodBody, Does.Not.Contain("guestState.GuestObject.transform.position = worldPosition"), "Static world-space placement must not bypass persistent room-stage binding.");
        Assert.That(controllerText, Does.Contain("RectTransformUtility.ScreenPointToLocalPointInRectangle"), "UI guests should convert Drawing Room points into their parent RectTransform space.");
        Assert.That(controllerText, Does.Contain("RectTransformUtility.WorldToScreenPoint"), "Drawing Room points should be interpreted by their visible screen position.");
        Assert.That(controllerText, Does.Contain("mainCamera.ScreenToWorldPoint"), "World-space guests should receive a camera-world position for visible room-stage points.");

        for (int i = 1; i <= 8; i++)
        {
            string anchorName = $"DrawingRoomGuestPoint_{i:00}";
            Assert.That(sceneText, Does.Contain($"m_Name: {anchorName}"), $"Gameplay should contain editable scene object {anchorName}.");
            Assert.That(sceneText, Does.Contain($"anchorId: {anchorName}"), $"{anchorName} should have a RoomAnchor id.");
        }
    }

    [Test]
    public void EntranceCoatHangerUsesAuthoredSceneObjectForClosetInteraction()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string actionText = File.ReadAllText(Chapter1SceneActionPath);
        string sceneText = File.ReadAllText(GameplayScenePath);
        string ensureBody = ExtractMethodBody(controllerText, "EnsureEntranceCoatHanger");
        string configureBody = ExtractMethodBody(controllerText, "ConfigureAuthoredCoatHangerObject");
        string colliderBody = ExtractMethodBody(controllerText, "EnsureCoatHangerCollider");
        string handleClosetBody = ExtractMethodBody(controllerText, "HandleClosetClicked");
        string walkClosetBody = ExtractMethodBody(controllerText, "WalkButlerToCloset");
        string completeClosetBody = ExtractMethodBody(controllerText, "CompletePendingClosetStorage");
        string closetDestinationBody = ExtractMethodBody(controllerText, "TryGetClosetApproachDestination");
        string closetScreenBody = ExtractMethodBody(controllerText, "TryGetClosetApproachScreenPosition");
        string walkCoatBody = ExtractMethodBody(controllerText, "WalkButlerToCoat");
        string closeCoatBody = ExtractMethodBody(controllerText, "IsButlerCloseToCoat");
        string actionBoundsBody = ExtractMethodBody(actionText, "IsPointerInsideActionBounds");
        string screenBoundsBody = ExtractMethodBody(actionText, "private bool IsPointerInsideScreenBounds(Vector2 screenPosition)");
        string actionUpdateBody = ExtractMethodBody(actionText, "private void Update");
        string performActionBody = ExtractMethodBody(actionText, "private void PerformAction");
        string resolveReferencesBody = ExtractMethodBody(controllerText, "ResolveReferences(bool createFallbacks)");
        string resolveAnchorsBody = ExtractMethodBody(controllerText, "ResolveAnchors");

        Assert.That(sceneText, Does.Contain("m_Name: entrance_coat_hanger_0"), "Gameplay should contain the authored entrance coat hanger object.");
        Assert.That(controllerText, Does.Contain("EntranceCoatHangerName = \"entrance_coat_hanger_0\""), "Chapter 1 should look up the authored entrance coat hanger by its scene name.");
        Assert.That(ensureBody, Does.Contain("FindSceneObjectByExactName(EntranceCoatHangerName)"), "Closet setup should start from the authored scene object.");
        Assert.That(ensureBody, Does.Not.Contain("new GameObject"), "Closet setup should not create a hard-coded runtime wardrobe object.");
        Assert.That(configureBody, Does.Contain("Chapter1SceneActionType.CoatCloset"), "The authored hanger should reuse the existing coat-closet action so cursor hover and click handling stay intact.");
        Assert.That(configureBody, Does.Contain("AddComponent<Chapter1SceneAction>"), "The authored hanger should gain the standard scene action if it is not already serialized.");
        Assert.That(configureBody, Does.Contain("AddComponent<CoatCloset>"), "The authored hanger should become the active coat storage container.");
        Assert.That(colliderBody, Does.Contain("BoxCollider2D"), "The authored hanger needs a trigger collider for pointer hit testing.");
        Assert.That(handleClosetBody, Does.Match(@"if \(!IsButlerCloseToCloset\(\)\)[\s\S]*WalkButlerToCloset\(\)[\s\S]*return;[\s\S]*StoreCarriedCoatInCloset\(\)"), "A valid coat-hanger click should walk the butler's feet to the hanger approach point before storing.");
        Assert.That(walkClosetBody, Does.Contain("TryGetClosetApproachDestination"), "Coat storage should use a computed reachable hanger approach destination.");
        Assert.That(walkClosetBody, Does.Contain("MovementStopped"), "Coat storage should finish after the butler reaches the hanger.");
        Assert.That(completeClosetBody, Does.Contain("IsButlerCloseToCloset()"), "Movement completion should re-check that the butler reached the hanger approach point.");
        Assert.That(closetDestinationBody, Does.Contain("TryGetClosetApproachScreenPosition"), "The hanger walk target should start from the visible lower hanger point, not the object center.");
        Assert.That(closetDestinationBody, Does.Contain("TryEvaluateMovementAtScreenPoint"), "The hanger target should be converted through the same screen-space movement mapping as player clicks.");
        Assert.That(closetScreenBody, Does.Contain("TryGetVisibleFeetWorldPoint"), "The hanger approach point should use the lower visible bounds.");
        Assert.That(walkCoatBody, Does.Contain("TryGetGuestFeetScreenPosition"), "Guest coat pickup should walk to the guest's feet, not the coat sprite transform.");
        Assert.That(closeCoatBody, Does.Contain("TryGetGuestFeetScreenPosition"), "Guest coat proximity should compare the butler's feet with the same guest-feet target used for movement.");
        Assert.That(controllerText, Does.Contain("IsCoatVisualTransform(renderer.transform)"), "Guest feet detection should ignore coat renderers before falling back to all visible renderers.");
        Assert.That(actionBoundsBody, Does.Contain("IsPointerInsideScreenBounds(screenPosition)"), "World-space scene actions should test the visible screen bounds, not raw world collider points.");
        Assert.That(screenBoundsBody, Does.Contain("TryGetActionScreenBounds"), "Coat-hanger hit testing should build screen-space bounds from the visible object.");
        Assert.That(screenBoundsBody, Does.Contain("GetMinimumScreenClickRadius"), "Coat-hanger hit testing should keep a minimum screen click radius for scaled layouts.");
        Assert.That(actionText, Does.Contain("CoatClosetClickScreenRadius"), "The coat hanger should have an explicit screen-space click radius.");
        Assert.That(actionText, Does.Not.Contain("ScreenToWorldPointAtActionDepth"), "The coat hanger should not convert mouse points through a raw world plane for hit testing.");
        Assert.That(actionText, Does.Not.Contain("OverlapPoint(worldPosition)"), "The coat hanger should not rely on world collider overlap for scaled room-stage hit testing.");
        Assert.That(actionUpdateBody, Does.Contain("RuntimeSettingsMenu.BlocksGameInput"), "Manual scene-action polling must stop while the settings modal blocks game input.");
        Assert.That(performActionBody, Does.Contain("RuntimeSettingsMenu.BlocksGameInput"), "Scene actions must not execute while the settings modal blocks game input.");
        Assert.That(resolveReferencesBody, Does.Contain("EnsureEntranceCoatHanger()"), "Runtime reference resolution should configure the authored hanger.");
        Assert.That(resolveAnchorsBody, Does.Contain("FindSceneObjectByExactName(EntranceCoatHangerName)"), "Anchor resolution should prefer the authored entrance hanger when the serialized closet still points elsewhere.");
        Assert.That(controllerText, Does.Not.Contain("Ensure" + "RuntimeCloset"), "The old hard-coded runtime closet setup must stay removed.");
        Assert.That(controllerText, Does.Not.Contain("Configure" + "Runtime" + "WardrobeObject"), "The old hard-coded runtime wardrobe configuration must stay removed.");
        Assert.That(controllerText, Does.Not.Contain("Wardrobe_" + "EntranceHall_" + "Runtime"), "The old runtime wardrobe object name must stay removed.");
        Assert.That(controllerText, Does.Not.Contain("CoatCloset_" + "EntranceHall_" + "Runtime"), "The old runtime closet object name must stay removed.");
        Assert.That(controllerText, Does.Not.Contain("Create" + "WardrobeSprite"), "The old generated wardrobe sprite path must stay removed.");
    }

    [Test]
    public void Chapter1GuestsKeepAuthoredStaticScaleWhileUsingRoomAnchors()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string playerMovementText = File.ReadAllText(PointClickPlayerMovementPath);
        string actorRoomStateText = File.ReadAllText(ActorRoomStatePath);
        string prepareMethodBody = ExtractMethodBody(controllerText, "PrepareSceneGuestObject");
        string disablePlayerMethodBody = ExtractMethodBody(controllerText, "DisablePlayerOnlyComponents");
        string placeMethodBody = ExtractMethodBody(controllerText, "PlaceGuestAt");
        string placeFeetMethodBody = ExtractMethodBody(controllerText, "PlaceGuestFeetAtPosition");

        Assert.That(playerMovementText, Does.Not.Match(@"\.localScale\s*="), "Point-click movement must not write the Butler body scale.");
        Assert.That(actorRoomStateText, Does.Not.Match(@"\.localScale\s*="), "Room-stage binding must not write guest body scale.");
        Assert.That(prepareMethodBody, Does.Not.Match(@"\.localScale\s*="), "Scene guest preparation must keep the authored scale.");
        Assert.That(prepareMethodBody, Does.Not.Contain("GuestScale"));
        Assert.That(disablePlayerMethodBody, Does.Contain("pointClickMovements[i].enabled = false"), "Guest clones should still disable player-only input.");
        Assert.That(disablePlayerMethodBody, Does.Not.Contain("PerspectiveScale"));
        Assert.That(placeMethodBody, Does.Contain("PlaceGuestFeetAtPosition"), "World guests should use the shared room-anchor placement path.");
        Assert.That(placeFeetMethodBody, Does.Contain("BindGuestToRoomStagePoint"), "World guests should remain attached to their authored room anchors.");
        Assert.That(placeFeetMethodBody, Does.Not.Contain("TryGetGuestFeetWorldPoint"), "Static room-anchor placement must not derive Guest root position from animation renderer bounds.");
        Assert.That(placeFeetMethodBody, Does.Contain("guestTransform.position = targetPosition"), "Static room-anchor placement should assign the mapped anchor directly to the persistent Guest root.");
        Assert.That(placeMethodBody, Does.Not.Match(@"\.localScale\s*="), "Authored placement must not resize guests.");
    }

    [Test]
    public void SceneGuestsPreserveAuthoredRendererOffsetsWhenContinuousYSortingStarts()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string playerMovementText = File.ReadAllText(PointClickPlayerMovementPath);
        string awakeMethodBody = ExtractMethodBody(playerMovementText, "private void Awake");
        string playerSortingMethodBody = ExtractMethodBody(playerMovementText, "private void ApplyPlayerSorting");
        string playerSortingSetterBody = ExtractMethodBody(playerMovementText, "public void SetPlayerSortingEnabled");
        string prepareMethodBody = ExtractMethodBody(controllerText, "PrepareSceneGuestObject");
        string disablePlayerMethodBody = ExtractMethodBody(controllerText, "DisablePlayerOnlyComponents");
        string preserveMethodBody = ExtractMethodBody(controllerText, "ShouldPreserveAuthoredGuestSorting");
        string ensureSorterBody = ExtractMethodBody(controllerText, "EnsureGuestYSorter");

        Assert.That(playerMovementText, Does.Contain("applyPlayerSorting"), "Player movement should have an explicit switch for runtime y-axis sorting.");
        Assert.That(awakeMethodBody, Does.Match(@"CacheReferences\(\);[\s\S]*CaptureAuthoredRendererSortingIfNeeded\(\);[\s\S]*InitializeVisualStateFromTransform\(\);"), "Player movement must capture Edit Mode sorting before its Awake-time y-sort can overwrite it.");
        Assert.That(playerSortingMethodBody, Does.Match(@"if \(!applyPlayerSorting\)[\s\S]*return;"), "Disabled player sorting should stop PointClickPlayerMovement from writing SpriteRenderer sorting.");
        Assert.That(playerSortingSetterBody, Does.Contain("RestoreAuthoredRendererSorting()"), "Guests cloned from the player prefab need their Edit Mode sorting restored after player sorting is disabled.");
        Assert.That(disablePlayerMethodBody, Does.Contain("SetPlayerSortingEnabled(false)"), "Scene guests should turn off inherited player y-sorting before player-only movement is disabled.");
        Assert.That(prepareMethodBody, Does.Contain("ShouldPreserveAuthoredGuestSorting(guestObject)"), "Scene-authored guests should not have their Edit Mode sprite order overwritten at runtime.");
        Assert.That(prepareMethodBody, Does.Match(@"if \(preserveAuthoredSorting\)[\s\S]*continue;[\s\S]*sortingLayerName = \""People\""[\s\S]*sortingOrder = 9000 \+ index"), "Only runtime fallback guests should receive generated People/9000 sorting.");
        Assert.That(preserveMethodBody, Does.Contain("guestObject.scene.IsValid()"), "Sorting preservation should only apply to real scene objects.");
        Assert.That(preserveMethodBody, Does.Contain("guestObject.scene.isLoaded"), "Sorting preservation should only apply to loaded scene objects.");
        Assert.That(preserveMethodBody, Does.Contain("!runtimeGeneratedGuestObjects.Contains(guestObject)"), "Runtime-generated fallback guests still need generated sorting.");
        Assert.That(ensureSorterBody, Does.Match(@"authoredOrders\[i\] = authoredRenderers\[i\]\.sortingOrder[\s\S]*AddComponent<WorldYSortSpriteRenderer>[\s\S]*authoredRenderers\[i\]\.sortingOrder = authoredOrders\[i\]"), "Attaching the continuous sorter must preserve authored child-renderer offsets through AddComponent.OnEnable.");
        Assert.That(ensureSorterBody, Does.Contain("ConfigureForActor(playerMovement, FindCharacterSpriteRenderer"), "Every guest should be configured against the Butler's canonical y-sort source.");
    }

    [Test]
    public void AllChapterGuestsUseOneContinuousSharedFootDepthSorter()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string playerMovementText = File.ReadAllText(PointClickPlayerMovementPath);
        string resetMethodBody = ExtractDeclaredMethodBody(controllerText, "ResetGuestStates");
        string forceVisibleMethodBody = ExtractDeclaredMethodBody(controllerText, "ForceGuestVisibleForDoorFlow");
        string ensureSorterBody = ExtractDeclaredMethodBody(controllerText, "EnsureGuestYSorter");
        string playerSortingMethodBody = ExtractDeclaredMethodBody(playerMovementText, "ApplyPlayerSorting");
        string coatSortingMethodBody = ExtractDeclaredMethodBody(controllerText, "ConfigureAssignedCoatSorting");
        string moveMethodBody = ExtractDeclaredMethodBody(controllerText, "MoveGuestToDrawingRoom");

        Assert.That(playerMovementText, Does.Contain("public int GetSortingOrderForFootY(float footY)"), "The butler should expose the same foot-Y sorting calculation used for player occlusion.");
        Assert.That(playerSortingMethodBody, Does.Contain("GetSortingOrderForFootY(sortingY)"), "The shared sorting helper must remain the butler's authoritative ordering path.");
        Assert.That(resetMethodBody, Does.Contain("runtimeState.YSorter = EnsureGuestYSorter(runtimeState)"), "Normal play and every debug skip should install the same persistent guest sorter while rebuilding runtime state.");
        Assert.That(ensureSorterBody, Does.Contain("ConfigureForActor(playerMovement, FindCharacterSpriteRenderer"), "Guests should reuse the Butler's sorting layer and foot-Y formula.");
        Assert.That(forceVisibleMethodBody, Does.Contain("guestState.YSorter?.ApplySorting()"), "Door-flow visibility refresh should use the same persistent sorter.");
        Assert.That(coatSortingMethodBody, Does.Contain("RefreshGuestYSorter(guest)"), "Adding or moving a coat should refresh relative renderer offsets on the same sorter.");
        Assert.That(moveMethodBody, Does.Not.Contain("sortingOrder"), "The Drawing Room transition must not install a fixed banister sorting override.");
        Assert.That(controllerText, Does.Not.Contain("ApplyEntranceBanisterSafeWalkingSorting"), "The obsolete fixed-order banister path must stay deleted.");
        Assert.That(controllerText, Does.Not.Contain("ApplyDrawingRoomGuestDepthSorting"), "The broken one-shot profile path must stay deleted.");
    }

    [Test]
    public void DrawingRoomUsesContinuousYSortingWithOnlySeatedOverrides()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string completeMethodBody = ExtractMethodBody(controllerText, "CompleteGuestDrawingRoomArrival");
        string skipStageMethodBody = ExtractMethodBody(controllerText, "StageGuestInDrawingRoomForChapter2");
        string seatedMethodBody = ExtractMethodBody(controllerText, "ApplyDrawingRoomSeatedOcclusion");
        string chairMapMethodBody = ExtractMethodBody(controllerText, "GetDrawingRoomChairRenderer");
        string gameplaySceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(completeMethodBody, Does.Match(@"PlaceGuestAt\(guest, drawingRoomSpot[\s\S]*ApplyDrawingRoomSeatedOcclusion\(guest, drawingRoomSpot\)"), "Normal arrivals should use continuous Y sorting plus the narrow seated exception.");
        Assert.That(skipStageMethodBody, Does.Match(@"PlaceGuestAt\(guest, drawingRoomSpot[\s\S]*ApplyDrawingRoomSeatedOcclusion\(guest, drawingRoomSpot\)"), "Chapter 2 skip staging should use the identical sorting path.");
        Assert.That(seatedMethodBody, Does.Contain("ShouldUseStandingDrawingRoomPose(guestState)"), "Standing guests must remain on ordinary Y sorting.");
        Assert.That(seatedMethodBody, Does.Contain("ActivateForSeat"), "Only seated guests should receive the chair/table ordering bracket.");
        Assert.That(chairMapMethodBody, Does.Match(@"case 0:[\s\S]*case 1:[\s\S]*case 3:[\s\S]*drawingRoomSofaRenderer"));
        Assert.That(chairMapMethodBody, Does.Match(@"case 5:[\s\S]*drawingRoomRedChairRenderer"));
        Assert.That(chairMapMethodBody, Does.Match(@"case 7:[\s\S]*drawingRoomGreenChairRenderer"));
        Assert.That(gameplaySceneText, Does.Contain("drawingRoomTeaTableRenderer: {fileID: 2088426359}"));
        Assert.That(gameplaySceneText, Does.Contain("drawingRoomSofaRenderer: {fileID: 496480228}"));
        Assert.That(gameplaySceneText, Does.Contain("drawingRoomRedChairRenderer: {fileID: 3602000202}"));
        Assert.That(gameplaySceneText, Does.Contain("drawingRoomGreenChairRenderer: {fileID: 362573330}"));
        AssertDrawingRoomTableUsesSharedButlerYSort(gameplaySceneText);
        AssertDrawingRoomChairUsesSharedButlerYSort(gameplaySceneText);
        AssertDrawingRoomWorldYOccluder(gameplaySceneText, "drawingroomgreenchair_0", "yReference: {fileID: 1850905445}");
        AssertDrawingRoomPhysicalOccluderHasOneWriter(gameplaySceneText, "purple_armchair_back", "PlayerBlocker_purple_armchair_back");
        AssertDrawingRoomPhysicalOccluderHasOneWriter(gameplaySceneText, "drawingroomgreenchair[_0", "PlayerBlocker_drawingroomgreenchair_0");
    }

    [Test]
    public void DrawingRoomWaitingPoseKeepsGuestsThreeFiveSevenStanding()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string completeMethodBody = ExtractMethodBody(controllerText, "CompleteGuestDrawingRoomArrival");
        string groupCompletionBody = ExtractMethodBody(controllerText, "CompleteEntranceGroupDrawingRoomArrival");
        string skipStageMethodBody = ExtractMethodBody(controllerText, "StageGuestInDrawingRoomForChapter2");
        string poseMethodBody = ExtractMethodBody(controllerText, "ApplyDrawingRoomWaitingPose");
        string standingRuleBody = ExtractMethodBody(controllerText, "ShouldUseStandingDrawingRoomPose");

        Assert.That(completeMethodBody, Does.Contain("ApplyDrawingRoomWaitingPose(guest)"), "Normal arrivals should use the shared Drawing Room pose rule.");
        Assert.That(skipStageMethodBody, Does.Contain("ApplyDrawingRoomWaitingPose(guest)"), "Chapter 2 skip staging should use the same Drawing Room pose rule.");
        Assert.That(poseMethodBody, Does.Contain("SetSeated(!ShouldUseStandingDrawingRoomPose(guest))"), "Standing guests should use idle standing while other guests still sit.");
        Assert.That(standingRuleBody, Does.Contain("guest.GuestIndex == 2"), "Guest 3 should stand in the Drawing Room.");
        Assert.That(standingRuleBody, Does.Contain("guest.GuestIndex == 4"), "Guest 5 should stand in the Drawing Room.");
        Assert.That(standingRuleBody, Does.Contain("guest.GuestIndex == 6"), "Guest 7 should stand in the Drawing Room.");
        Assert.That(groupCompletionBody, Does.Contain("guest.Seated = true"), "Atomic pair completion should still mark normal Chapter 1 guests as waiting/seated.");
        Assert.That(skipStageMethodBody, Does.Contain("guest.Seated = true"), "Visual standing should not break Chapter 2 skip progression.");
    }

    private static void AssertDrawingRoomWorldYOccluder(string assetText, string objectName, string expectedYReference)
    {
        string objectBlock = ExtractObjectBlock(assetText, objectName);

        Assert.That(objectBlock, Does.Not.Contain("guid: 361e3658088b41ab98d330ae6457640b"), $"'{objectName}' must not retain the deleted projection component.");
        Assert.That(objectBlock, Does.Contain("guid: 75f090bb68ab450d9703d9581c5c543a"), $"'{objectName}' should use the same world-Y order scale as the Butler.");
        Assert.That(objectBlock, Does.Contain("sortingOrderBase: 1000"));
        Assert.That(objectBlock, Does.Contain("sortingOrderPerYUnit: 100"));
        Assert.That(objectBlock, Does.Contain(expectedYReference));
    }

    private static void AssertDrawingRoomChairUsesSharedButlerYSort(string assetText)
    {
        string chairBlock = ExtractObjectBlock(assetText, "drawing_room_red_chair_guest6");
        string blockerBlock = ExtractObjectBlock(assetText, "PlayerBlocker_drawing_room_red_chair_guest6");

        Assert.That(chairBlock, Does.Not.Contain("guid: 361e3658088b41ab98d330ae6457640b"), "The chair must not retain projection metadata.");
        Assert.That(chairBlock, Does.Not.Contain("roomLocalFootPoint:"));
        Assert.That(chairBlock, Does.Not.Contain("applySorting:"));
        Assert.That(chairBlock, Does.Not.Contain("sortingOffset:"));
        Assert.That(blockerBlock, Does.Contain("sourceObjectName: drawing_room_red_chair_guest6"));
        Assert.That(blockerBlock, Does.Contain("sortSourceRenderers: 1"), "The chair should use its lower movement footprint as the shared y-axis sort edge.");
    }

    private static void AssertDrawingRoomTableUsesSharedButlerYSort(string assetText)
    {
        string tableBlock = ExtractObjectBlock(assetText, "tea_service_table");
        string blockerBlock = ExtractObjectBlock(assetText, "PlayerBlocker_tea_service_table");

        Assert.That(tableBlock, Does.Not.Contain("guid: 361e3658088b41ab98d330ae6457640b"), "The table must not retain projection metadata.");
        Assert.That(tableBlock, Does.Not.Contain("roomLocalFootPoint:"));
        Assert.That(tableBlock, Does.Not.Contain("applySorting:"));
        Assert.That(tableBlock, Does.Not.Contain("sortingOffset:"));
        Assert.That(blockerBlock, Does.Contain("sourceObjectName: tea_service_table"));
        Assert.That(blockerBlock, Does.Contain("sortSourceRenderers: 1"), "The table should use its lower physical footprint as the shared y-axis sort edge.");
    }

    private static void AssertDrawingRoomPhysicalOccluderHasOneWriter(
        string assetText,
        string sourceObjectName,
        string blockerObjectName)
    {
        string sourceBlock = ExtractObjectBlock(assetText, sourceObjectName);
        string blockerBlock = ExtractObjectBlock(assetText, blockerObjectName);

        Assert.That(sourceBlock, Does.Not.Contain("guid: 361e3658088b41ab98d330ae6457640b"), $"'{sourceObjectName}' must not keep a competing projection component.");
        Assert.That(sourceBlock, Does.Not.Contain("applySorting:"));
        Assert.That(blockerBlock, Does.Contain($"sourceObjectName: {sourceObjectName}"));
        Assert.That(blockerBlock, Does.Contain("sortSourceRenderers: 1"), $"'{sourceObjectName}' should have exactly one physical-footprint sorting owner.");
    }

    private static string ExtractObjectBlock(string assetText, string objectName)
    {
        int nameIndex = assetText.IndexOf($"m_Name: {objectName}", StringComparison.Ordinal);
        Assert.That(nameIndex, Is.GreaterThanOrEqualTo(0), $"Could not find object '{objectName}'.");

        int blockStart = assetText.LastIndexOf("--- !u!1 &", nameIndex, StringComparison.Ordinal);
        Assert.That(blockStart, Is.GreaterThanOrEqualTo(0), $"Could not find object block start for '{objectName}'.");

        int blockEnd = assetText.IndexOf("--- !u!1 &", nameIndex + objectName.Length, StringComparison.Ordinal);
        return blockEnd >= 0
            ? assetText.Substring(blockStart, blockEnd - blockStart)
            : assetText.Substring(blockStart);
    }

    private static string ExtractDeclaredMethodBody(string sourceText, string methodName)
    {
        Match methodMatch = Regex.Match(
            sourceText,
            $@"(?m)^[ \t]*(?:(?:public|private|protected|internal|static|virtual|override|sealed|async|new)[ \t]+)*[A-Za-z_][A-Za-z0-9_<>,\[\]?]*[ \t]+{Regex.Escape(methodName)}[ \t]*\(");
        Assert.That(methodMatch.Success, Is.True, $"Could not find declaration for method '{methodName}'.");

        int bodyStart = sourceText.IndexOf('{', methodMatch.Index);
        Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"Could not find method body for '{methodName}'.");

        int depth = 0;

        for (int i = bodyStart; i < sourceText.Length; i++)
        {
            if (sourceText[i] == '{')
            {
                depth++;
            }
            else if (sourceText[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return sourceText.Substring(bodyStart, i - bodyStart + 1);
                }
            }
        }

        Assert.Fail($"Could not find end of method body for '{methodName}'.");
        return string.Empty;
    }

    private static string ExtractMethodBody(string sourceText, string methodName)
    {
        if (!methodName.Contains(" "))
        {
            return ExtractDeclaredMethodBody(sourceText, methodName);
        }

        int methodIndex = sourceText.IndexOf(methodName, StringComparison.Ordinal);
        Assert.That(methodIndex, Is.GreaterThanOrEqualTo(0), $"Could not find method '{methodName}'.");

        int bodyStart = sourceText.IndexOf('{', methodIndex);
        Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"Could not find method body for '{methodName}'.");

        int depth = 0;

        for (int i = bodyStart; i < sourceText.Length; i++)
        {
            if (sourceText[i] == '{')
            {
                depth++;
            }
            else if (sourceText[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return sourceText.Substring(bodyStart, i - bodyStart + 1);
                }
            }
        }

        Assert.Fail($"Could not find end of method body for '{methodName}'.");
        return string.Empty;
    }
}
