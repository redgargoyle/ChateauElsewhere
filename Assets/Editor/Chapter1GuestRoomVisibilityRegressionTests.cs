using System;
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

        Assert.That(methodBody, Does.Contain("SetCurrentRoom(drawingRoomId)"), "Guests should logically move to the Drawing Room.");
        Assert.That(methodBody, Does.Contain("SetAvailableInCurrentChapter(true)"), "Guests in the Drawing Room should remain available in Chapter 1.");
        Assert.That(methodBody, Does.Contain("SetVisibleByChapterState(true)"), "Room visibility, not chapter invisibility, should decide whether Drawing Room guests render.");
        Assert.That(methodBody, Does.Contain("ApplyDrawingRoomWaitingPose(guest)"), "Guests should get their drawing-room waiting pose when they enter.");
        Assert.That(methodBody, Does.Contain("guest.Seated = true"), "Guests should still be marked as waiting/seated for chapter progression.");
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
        string moveGuestObjectBody = ExtractMethodBody(controllerText, "MoveGuestObjectToRoomContent");
        string suspiciousGuestReparentPattern =
            @"(?s)(?:Move|Place|Reparent|Parent)[A-Za-z0-9_]*(?:Guest|Guests)[A-Za-z0-9_]*(?:Room_Drawing|Room_DrawingRoom|DrawingRoom)[A-Za-z0-9_]*\s*\([^)]*\)\s*\{.*?SetParent";

        Assert.That(controllerText, Does.Not.Match(suspiciousGuestReparentPattern), "Guests should not be reparented under the Drawing Room presentation hierarchy.");
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
        Assert.That(spotMethodBody, Does.Contain("GetWorldDrawingRoomSeatPosition"), "World-space guests should receive world-space Drawing Room waiting spots.");
        Assert.That(sceneText, Does.Contain("m_Name: GuestDrawingRoomDoorTarget"), "Gameplay should expose an editable Entrance Hall target for the Drawing Room guest walk path.");
        Assert.That(sceneText, Does.Contain("anchorId: GuestDrawingRoomDoorTarget"), "The editable Drawing Room guest walk target should have a RoomAnchor id.");
        Assert.That(sceneText, Does.Contain("roomId: Grand Entrance Hall"), "The editable Drawing Room guest walk target should belong to the Entrance Hall.");
    }

    [Test]
    public void LiveDoorAnswerUsesStableEntranceWorldPositions()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string sceneText = File.ReadAllText(GameplayScenePath);
        string admitBody = ExtractMethodBody(controllerText, "private IEnumerator AdmitGuestToEntranceHall");
        string placeDoorBody = ExtractMethodBody(controllerText, "private void PlaceGuestAtDoorArrival");
        string projectedDoorBody = ExtractMethodBody(controllerText, "private bool TryPlaceProjectedGuestFeetAtTarget");
        string placeFeetBody = ExtractMethodBody(controllerText, "private void PlaceGuestFeetAtPosition");
        string doorArrivalBody = ExtractMethodBody(controllerText, "private Vector3 GetWorldDoorArrivalPosition(GuestRuntimeState guestState, int fallbackIndex, int fallbackCount)");
        string doorArrivalIndexBody = ExtractMethodBody(controllerText, "private Vector3 GetWorldDoorArrivalPosition(int indexInBatch, int batchCount)");
        string doorPairOffsetBody = ExtractMethodBody(controllerText, "private Vector2 GetDoorArrivalPairSlotOffset");
        string doorArrivalBaseBody = ExtractMethodBody(controllerText, "private Vector3 GetWorldDoorArrivalBasePosition");
        string doorArrivalTargetBody = ExtractMethodBody(controllerText, "private Transform GetWorldDoorArrivalTarget");
        string answerSpotBody = ExtractMethodBody(controllerText, "private bool TryGetWorldFrontDoorAnswerSpot");
        string waitBody = ExtractMethodBody(controllerText, "GetWorldEntranceWaitPosition");
        string waitIndexBody = ExtractMethodBody(controllerText, "private Vector3 GetEntranceWaitPosition(int indexInBatch, int batchCount)");
        string worldWaitIndexBody = ExtractMethodBody(controllerText, "private Vector3 GetWorldEntranceWaitPosition(int indexInBatch, int batchCount)");
        string worldEntranceOffsetBody = ExtractMethodBody(controllerText, "private Vector2 GetWorldEntranceGroupOffset");
        string uiEntranceOffsetBody = ExtractMethodBody(controllerText, "private Vector2 GetEntranceGroupOffset");
        string entranceCenterBody = ExtractMethodBody(controllerText, "GetWorldEntranceCenterPosition(GuestRuntimeState guestState)");
        string anchorLookupBody = ExtractMethodBody(controllerText, "GetEntranceHallGuestAnchor");
        string interactionTargetBody = ExtractMethodBody(controllerText, "GetFrontDoorInteractionTransform");
        string conversionBody = ExtractMethodBody(controllerText, "TryGetWorldPositionForGuestTarget");
        string guestArrivalDoorBlock = ExtractObjectBlock(sceneText, "GuestArrival_Door");
        string guestEntrancePlacemarkBlock = ExtractObjectBlock(sceneText, "Placemark_guests_entrance");
        string doorAnswerTriggerBlock = ExtractObjectBlock(sceneText, "Door_answer_trigger");
        string drawingRoomSideSpotBlock = ExtractObjectBlock(sceneText, "DrawingRoomSideButlerSpot");
        string drawingRoomDoorTriggerBlock = ExtractObjectBlock(sceneText, "DoorTrigger_GEH_DrawingRoom");

        Assert.That(controllerText, Does.Contain("EntranceHallGuestAnchorId"), "Chapter 1 should name the editable Entrance Hall guest anchor consistently.");
        Assert.That(controllerText, Does.Contain("GuestEntranceSpawnPlacemarkId = \"Placemark_guests_entrance\""), "Guest entrance spawning should use the draggable scene placemark.");
        Assert.That(controllerText, Does.Contain("FrontDoorGuestSpawnAnchorId"), "Front-door guest spawning should have a named anchor separate from drawing-room door targets.");
        Assert.That(admitBody, Does.Contain("PlaceGuestAtDoorArrival"), "The initial door spawn should use a feet-aware door-arrival placement path.");
        Assert.That(admitBody, Does.Not.Contain("PlaceGuestAt(guest, arrivalPoint"), "UI guests should not bypass feet-aware door spawning by placing their transform directly on the front-door anchor.");
        Assert.That(placeDoorBody, Does.Contain("PlaceGuestFeetAtPosition"), "Door arrival should place the guest by feet, not by transform center.");
        Assert.That(placeDoorBody, Does.Contain("TryPlaceProjectedGuestFeetAtTarget"), "Projected door arrival should set the projection foot point directly from the door-base target.");
        Assert.That(projectedDoorBody, Does.Contain("TryGetRoomLocalFootPointForTarget(target"), "Projected guests should spawn from the door-base anchor's room-local foot point.");
        Assert.That(projectedDoorBody, Does.Contain("SetRoomLocalFootPoint(footPoint + roomLocalPairOffset)"), "Projected door spawning should split each pair side-by-side without moving them off the door.");
        Assert.That(placeFeetBody, Does.Contain("TryGetGuestFeetWorldPoint"), "Door arrival should compensate for the guest's visible foot offset.");
        Assert.That(placeFeetBody, Does.Contain("targetPosition.x -= feetOffset.x"), "Door arrival should align the guest feet horizontally with the door answer spot.");
        Assert.That(placeFeetBody, Does.Contain("targetPosition.y -= feetOffset.y"), "Door arrival should align the guest feet vertically with the door answer spot.");
        Assert.That(doorArrivalBody, Does.Contain("GetWorldDoorArrivalBasePosition(guestState)"), "Door-answer spawning should begin at the front-door point before guests walk inward.");
        Assert.That(doorArrivalBody, Does.Contain("GetDoorArrivalPairSlotOffset"), "Door-answer spawning should split guests in the same pair so they do not stack.");
        Assert.That(doorArrivalBody, Does.Not.Contain("GetWorldEntranceGroupOffset"), "Door-answer spawning should not stagger later arrival pairs away from the front door.");
        Assert.That(doorArrivalBody, Does.Not.Contain("GetWorldGuestGridOffset"), "Door-answer spawning should not use batch/grid offsets.");
        Assert.That(doorArrivalIndexBody, Does.Contain("GetWorldDoorArrivalBasePosition(null)"), "Index-based door-answer spawning should also use the same front-door point.");
        Assert.That(doorArrivalIndexBody, Does.Contain("GetDoorArrivalPairSlotOffset"), "Index-based door-answer spawning should split guests only within their pair.");
        Assert.That(doorArrivalIndexBody, Does.Not.Contain("GetWorldEntranceGroupOffset"), "Index-based door-answer spawning should not stagger later arrival pairs away from the front door.");
        Assert.That(doorArrivalIndexBody, Does.Not.Contain("GetWorldGuestGridOffset"), "Index-based door-answer spawning should not use batch/grid offsets.");
        Assert.That(doorPairOffsetBody, Does.Contain("out _"), "Door pair offsets should intentionally discard the group index.");
        Assert.That(doorPairOffsetBody, Does.Contain("return new Vector2(centeredSlot * spacing, 0f)"), "Door pair offsets should only split guests horizontally at the same door base.");
        Assert.That(doorPairOffsetBody, Does.Not.Contain("groupIndex"), "Door pair offsets must not move later arrival groups progressively away from the front door.");
        Assert.That(doorArrivalBaseBody, Does.Match(@"GetWorldDoorArrivalTarget[\s\S]*TryGetWorldPositionForGuestTarget[\s\S]*TryGetWorldFrontDoorAnswerSpot[\s\S]*GetWorldEntranceCenterPosition\(guestState\)"), "Front-door spawning should prefer the draggable door placemark, then the Butler's cached answer spot, then the stable entrance fallback.");
        Assert.That(doorArrivalTargetBody, Does.Contain("GetGuestEntranceSpawnPlacemark()"), "Front-door spawning should first use the movable Placemark_guests_entrance anchor.");
        Assert.That(doorArrivalTargetBody, Does.Contain("GetFrontDoorArrivalPoint(frontDoorArrivalPoint)"), "Front-door spawning should only fall back to the configured GuestArrival_Door front entrance anchor.");
        Assert.That(doorArrivalTargetBody, Does.Not.Contain("drawingRoomSideButlerSpot"), "Front-door spawning must not use the left Drawing Room side-door marker.");
        Assert.That(answerSpotBody, Does.Contain("TryGetWorldPointFromLogicalPosition(frontDoorAnswerSpot"), "The cached Butler door-answer floor point should be converted back to world space for guest feet.");
        Assert.That(waitBody, Does.Contain("GetWorldEntranceCenterPosition(guestState)"), "Entrance wait spots should use the guest-depth-aware editable entrance anchor.");
        Assert.That(waitBody, Does.Contain("GetWorldEntranceGroupOffset"), "Entrance wait spots should keep group/slot offsets after guests spawn at the shared doorway.");
        Assert.That(controllerText, Does.Contain("EntranceWaitDepthStepMultiplier = 0.32f"), "Later arrival pairs should only step slightly toward the camera so coats remain reachable near the bottom of the screen.");
        Assert.That(controllerText, Does.Contain("EntranceWaitSlotSpacingMultiplier = 1.3f"), "Guests in the same pair should stand far enough apart after the larger entrance scale is applied.");
        Assert.That(controllerText, Does.Contain("EntranceWaitGroupSideStepMultiplier = -0.32f"), "Later entrance pairs should fan sideways instead of standing directly in front of each other.");
        Assert.That(worldEntranceOffsetBody, Does.Contain("EntranceWaitDepthStepMultiplier"), "World-space entrance guests should use the shallow depth step.");
        Assert.That(worldEntranceOffsetBody, Does.Contain("EntranceWaitSlotSpacingMultiplier"), "World-space entrance pair spacing should use the wider slot step.");
        Assert.That(worldEntranceOffsetBody, Does.Contain("EntranceWaitGroupSideStepMultiplier"), "World-space entrance groups should fan sideways.");
        Assert.That(uiEntranceOffsetBody, Does.Contain("EntranceWaitDepthStepMultiplier"), "UI entrance guests should use the same shallow depth step.");
        Assert.That(uiEntranceOffsetBody, Does.Contain("EntranceWaitSlotSpacingMultiplier"), "UI entrance pair spacing should use the wider slot step.");
        Assert.That(uiEntranceOffsetBody, Does.Contain("EntranceWaitGroupSideStepMultiplier"), "UI entrance groups should fan sideways.");
        Assert.That(waitIndexBody, Does.Contain("GetEntranceGroupOffset"), "Index-only entrance wait fallback should use the same group formation as live guests.");
        Assert.That(worldWaitIndexBody, Does.Contain("GetWorldEntranceGroupOffset"), "World-space index fallback should use the same group formation as live guests.");
        Assert.That(worldWaitIndexBody, Does.Not.Contain("GetWorldGuestGridOffset"), "World-space index fallback should not reintroduce the old tight grid formation.");
        Assert.That(entranceCenterBody, Does.Match(@"TryGetEntranceHallGuestAnchorWorldPosition\(guestState[\s\S]*TryGetAverageAuthoredChapterGuestPosition"), "Entrance waiting should prefer the scene anchor before falling back to authored guest averages.");
        Assert.That(anchorLookupBody, Does.Contain("FindAnchor(EntranceHallGuestAnchorId, entryRoomId)"), "The entrance wait point should be discoverable through RoomAnchor data.");
        Assert.That(anchorLookupBody, Does.Contain("FindSceneObjectByExactName(EntranceHallGuestAnchorId)"), "The entrance wait point should still resolve if RoomAnchor data is stale.");
        Assert.That(doorArrivalBody, Does.Not.Contain("GetWorldVisibleAnchorPosition"), "Door-answer spawning should not project the high-Z GuestArrival_Door stage anchor off camera.");
        Assert.That(waitBody, Does.Not.Contain("GetWorldVisibleAnchorPosition"), "Entrance waiting should not project high-Z stage anchors off camera.");
        Assert.That(interactionTargetBody, Does.Match(@"frontDoorArrivalPoint[\s\S]*return frontDoorArrivalPoint[\s\S]*FindDoorAnswerTriggerObject"), "The butler should walk to the centered front-door arrival point before answering the door.");
        Assert.That(interactionTargetBody, Does.Not.Contain("drawingRoomSideButlerSpot"), "Front-door click/approach logic must not fall back to the left Drawing Room side-door marker.");
        Assert.That(conversionBody, Does.Not.Contain("target.GetComponentInParent<Canvas>(true) == null"), "Visible anchor conversion must work for non-Canvas room-stage anchors as well as UI anchors.");
        Assert.That(conversionBody, Does.Contain("TryGetTargetScreenPosition"), "Visible anchor conversion should preserve what the player sees on screen.");
        Assert.That(conversionBody, Does.Contain("mainCamera.ScreenToWorldPoint"), "Drawing Room anchor conversion should land on the guest world plane instead of raw room-stage coordinates.");
        Assert.That(guestArrivalDoorBlock, Does.Contain("m_LocalPosition: {x: -7.216162, y: -94"), "GuestArrival_Door should be a separate front-door threshold anchor for guest feet, not the Butler's answer interaction point.");
        Assert.That(guestEntrancePlacemarkBlock, Does.Contain("m_Name: Placemark_guests_entrance"), "Gameplay should expose a draggable guest entrance spawn placemark.");
        Assert.That(guestEntrancePlacemarkBlock, Does.Contain("anchorId: Placemark_guests_entrance"), "The guest entrance placemark should be a RoomAnchor target.");
        Assert.That(guestEntrancePlacemarkBlock, Does.Contain("roomId: Grand Entrance Hall"), "The guest entrance placemark should belong to the Grand Entrance Hall.");
        Assert.That(guestEntrancePlacemarkBlock, Does.Contain("showSceneGizmo: 1"), "The guest entrance placemark should be visible and easy to move in Edit Mode.");
        Assert.That(guestEntrancePlacemarkBlock, Does.Contain("sceneGizmoColor: {r: 0.05, g: 0.35, b: 1"), "The guest entrance placemark should use a blue editor gizmo.");
        Assert.That(doorAnswerTriggerBlock, Does.Contain("m_LocalPosition: {x: -7.216162, y: -13.4132805"), "The clickable door-answer trigger should remain separate from the guest spawn threshold.");
        Assert.That(drawingRoomSideSpotBlock, Does.Contain("m_LocalPosition: {x: -684"), "The old ButlerGreetingSpot marker is actually beside the left Drawing Room door and must stay labeled that way.");
        Assert.That(drawingRoomDoorTriggerBlock, Does.Contain("m_AnchoredPosition: {x: -687.8042"), "The Drawing Room trigger proves the side marker is not the front entrance.");
        Assert.That(sceneText, Does.Contain("drawingRoomSideButlerSpot: {fileID: 140767560}"), "The serialized scene reference should label the left-door marker as a Drawing Room side spot.");
        Assert.That(sceneText, Does.Not.Contain("butlerDoorSpot:"), "The scene should not serialize the left-door marker under the old front-door-adjacent name.");
        Assert.That(sceneText, Does.Not.Contain("anchorId: ButlerGreetingSpot"), "The scene anchor id should not label the left Drawing Room side marker as the Butler greeting/front-door spot.");
        Assert.That(sceneText, Does.Contain("m_Name: EntranceHallGuestAnchor"), "Gameplay should expose a movable Entrance Hall guest anchor.");
        Assert.That(sceneText, Does.Contain("anchorId: EntranceHallGuestAnchor"), "The Entrance Hall guest anchor should have a RoomAnchor id.");
        Assert.That(sceneText, Does.Contain("m_LocalPosition: {x: 143, y: -98"), "Entrance Hall guests should wait near the start of the red carpet, not down at the bottom edge.");
        Assert.That(sceneText, Does.Contain("roomId: Grand Entrance Hall"), "The Entrance Hall guest anchor should belong to the entry room hierarchy.");
        Assert.That(sceneText, Does.Contain("entranceHallGuestAnchor: {fileID: 3501000024}"), "The Chapter 1 controller should serialize the editable Entrance Hall guest anchor.");
        Assert.That(sceneText, Does.Contain("showSceneGizmo: 1"), "The Entrance Hall guest anchor should be color-coded in Edit Mode.");
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
        string seatMethodBody = ExtractMethodBody(controllerText, "ResolveSeatForGuest");
        string placeMethodBody = ExtractMethodBody(controllerText, "PlaceGuestAt");

        Assert.That(controllerText, Does.Contain("DrawingRoomGuestPointPrefix"), "Chapter 1 should name editable Drawing Room guest points consistently.");
        Assert.That(spotMethodBody, Does.Match(@"FindDrawingRoomGuestPoint\(guest\.GuestIndex\)[\s\S]*IsWorldSpaceGuestObject"), "Guests should prefer editable Drawing Room points before generated world-space seats.");
        Assert.That(seatMethodBody, Does.Match(@"FindDrawingRoomGuestPoint\(index\)[\s\S]*drawingRoomSeat01"), "Assigned seats should prefer editable Drawing Room points before old fallback seats.");
        Assert.That(controllerText, Does.Match(@"FindDrawingRoomGuestPoint\s*\([^)]*\)\s*\{[\s\S]*FindAnchor\(pointName, drawingRoomId\)[\s\S]*FindSceneObjectByExactName\(pointName\)"), "Editable guest points should fall back to the physical scene object name if RoomAnchor data is stale.");
        Assert.That(placeMethodBody, Does.Match(@"TryGetWorldPositionForGuestTarget[\s\S]*ActorState\.PlaceAt"), "World-space guests should convert UI room-stage points before falling back to raw Transform placement.");
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
        string screenBoundsBody = ExtractMethodBody(actionText, "IsPointerInsideScreenBounds");
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
    public void Chapter1GuestsUseAuthoredScaleAsRoomZoomBaseline()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string playerMovementText = File.ReadAllText(PointClickPlayerMovementPath);
        string actorRoomStateText = File.ReadAllText(ActorRoomStatePath);
        string playerPerspectiveScaleBody = ExtractMethodBody(playerMovementText, "private void ApplyPerspectiveScale");
        string prepareMethodBody = ExtractMethodBody(controllerText, "PrepareSceneGuestObject");
        string disablePlayerMethodBody = ExtractMethodBody(controllerText, "DisablePlayerOnlyComponents");
        string placeMethodBody = ExtractMethodBody(controllerText, "PlaceGuestAt");

        Assert.That(playerMovementText, Does.Contain("applyPerspectiveScale"), "Player movement should have an explicit switch for runtime perspective scale.");
        Assert.That(playerMovementText, Does.Contain("SetPerspectiveScaleEnabled"), "Guests cloned from the player prefab need a public way to opt out of player depth scaling.");
        Assert.That(playerMovementText, Does.Match(@"if \(!applyPerspectiveScale\)[\s\S]*return;"), "Disabled perspective scale should stop PointClickPlayerMovement from writing transform.localScale.");
        Assert.That(playerMovementText, Does.Contain("authoredPerspectiveScaleReference"), "The butler should keep the Edit Mode transform scale as the baseline at its authored depth.");
        Assert.That(playerMovementText, Does.Contain("GetPerspectiveScaleForY"), "Perspective scaling should compare the current room depth against the authored depth.");
        Assert.That(playerPerspectiveScaleBody, Does.Contain("depthScale / Mathf.Max(0.0001f, authoredPerspectiveScaleReference)"), "Play Mode perspective scaling should be relative to the Edit Mode depth, not an absolute replacement.");
        Assert.That(playerPerspectiveScaleBody, Does.Contain("authoredLocalScale.x * scale"), "Play Mode should multiply the artist-authored butler X scale instead of replacing it.");
        Assert.That(playerPerspectiveScaleBody, Does.Contain("authoredLocalScale.y * scale"), "Play Mode should multiply the artist-authored butler Y scale instead of replacing it.");
        Assert.That(playerPerspectiveScaleBody, Does.Contain("currentRoomStageScaleRatio"), "The authored-scale fix must keep room-stage zoom scaling.");
        Assert.That(actorRoomStateText, Does.Contain("scaleWithRoomStageMotion"), "ActorRoomState should be able to scale a bound actor from its authored base scale.");
        Assert.That(prepareMethodBody, Does.Contain("authoredGuestScale"), "Scene guest preparation should capture the scale restored from player movement before other setup.");
        Assert.That(prepareMethodBody, Does.Contain("SetScaleWithRoomStageMotion(true)"), "Chapter 1 guests should follow room-stage zoom from their authored base scale, matching the butler.");
        Assert.That(disablePlayerMethodBody, Does.Contain("SetPerspectiveScaleEnabled(false)"), "Scene guests should turn off inherited player perspective scaling before disabling player-only movement.");
        Assert.That(placeMethodBody, Does.Contain("PreserveGuestAuthoredScale(guestState)"), "Drawing room placement and skip staging should preserve the scale artists set in Edit Mode before room zoom is applied.");
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
        string activateMethodBody = ExtractDeclaredMethodBody(controllerText, "ActivateAuthoredChapterGuestObject");
        string forceVisibleMethodBody = ExtractDeclaredMethodBody(controllerText, "ForceGuestVisibleForDoorFlow");
        string ensureSorterBody = ExtractDeclaredMethodBody(controllerText, "EnsureGuestYSorter");
        string playerSortingMethodBody = ExtractDeclaredMethodBody(playerMovementText, "ApplyPlayerSorting");
        string coatSortingMethodBody = ExtractDeclaredMethodBody(controllerText, "ConfigureAssignedCoatSorting");
        string moveMethodBody = ExtractDeclaredMethodBody(controllerText, "MoveGuestToDrawingRoom");

        Assert.That(playerMovementText, Does.Contain("public int GetSortingOrderForFootY(float footY)"), "The butler should expose the same foot-Y sorting calculation used for player occlusion.");
        Assert.That(playerSortingMethodBody, Does.Contain("GetSortingOrderForFootY(sortingY)"), "The shared sorting helper must remain the butler's authoritative ordering path.");
        Assert.That(resetMethodBody, Does.Contain("runtimeState.YSorter = EnsureGuestYSorter(runtimeState)"), "Normal play and every debug skip should install the same persistent guest sorter while rebuilding runtime state.");
        Assert.That(ensureSorterBody, Does.Contain("ConfigureForActor(playerMovement, FindCharacterSpriteRenderer"), "Guests should reuse the Butler's sorting layer and foot-Y formula.");
        Assert.That(activateMethodBody, Does.Contain("guestState.YSorter?.ApplySorting()"), "Entrance activation should apply the persistent sorter immediately.");
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
        string skipStageMethodBody = ExtractMethodBody(controllerText, "StageGuestInDrawingRoomForChapter2");
        string poseMethodBody = ExtractMethodBody(controllerText, "ApplyDrawingRoomWaitingPose");
        string standingRuleBody = ExtractMethodBody(controllerText, "ShouldUseStandingDrawingRoomPose");

        Assert.That(completeMethodBody, Does.Contain("ApplyDrawingRoomWaitingPose(guest)"), "Normal arrivals should use the shared Drawing Room pose rule.");
        Assert.That(skipStageMethodBody, Does.Contain("ApplyDrawingRoomWaitingPose(guest)"), "Chapter 2 skip staging should use the same Drawing Room pose rule.");
        Assert.That(poseMethodBody, Does.Contain("SetSeated(!ShouldUseStandingDrawingRoomPose(guest))"), "Standing guests should use idle standing while other guests still sit.");
        Assert.That(standingRuleBody, Does.Contain("guest.GuestIndex == 2"), "Guest 3 should stand in the Drawing Room.");
        Assert.That(standingRuleBody, Does.Contain("guest.GuestIndex == 4"), "Guest 5 should stand in the Drawing Room.");
        Assert.That(standingRuleBody, Does.Contain("guest.GuestIndex == 6"), "Guest 7 should stand in the Drawing Room.");
        Assert.That(completeMethodBody, Does.Contain("guest.Seated = true"), "Visual standing should not break normal Chapter 1 progression.");
        Assert.That(skipStageMethodBody, Does.Contain("guest.Seated = true"), "Visual standing should not break Chapter 2 skip progression.");
    }

    private static void AssertDrawingRoomWorldYOccluder(string assetText, string objectName, string expectedYReference)
    {
        string objectBlock = ExtractObjectBlock(assetText, objectName);

        Assert.That(objectBlock, Does.Contain("guid: 361e3658088b41ab98d330ae6457640b"));
        Assert.That(objectBlock, Does.Contain("applySorting: 0"), $"'{objectName}' must not retain the incompatible perspective-profile order scale.");
        Assert.That(objectBlock, Does.Contain("guid: 75f090bb68ab450d9703d9581c5c543a"), $"'{objectName}' should use the same world-Y order scale as the Butler.");
        Assert.That(objectBlock, Does.Contain("sortingOrderBase: 1000"));
        Assert.That(objectBlock, Does.Contain("sortingOrderPerYUnit: 100"));
        Assert.That(objectBlock, Does.Contain(expectedYReference));
    }

    private static void AssertDrawingRoomChairUsesSharedButlerYSort(string assetText)
    {
        string chairBlock = ExtractObjectBlock(assetText, "drawing_room_red_chair_guest6");
        string blockerBlock = ExtractObjectBlock(assetText, "PlayerBlocker_drawing_room_red_chair_guest6");

        Assert.That(chairBlock, Does.Contain("roomLocalFootPoint: {x: 59, y: -208.5}"));
        Assert.That(chairBlock, Does.Contain("applySorting: 0"), "The chair must not compete with the shared Butler y-axis sorter.");
        Assert.That(chairBlock, Does.Contain("sortingOffset: 0"), "The old forced-low chair order must remain removed.");
        Assert.That(chairBlock, Does.Not.Contain("sortingOffset: -5776"));
        Assert.That(blockerBlock, Does.Contain("sourceObjectName: drawing_room_red_chair_guest6"));
        Assert.That(blockerBlock, Does.Contain("sortSourceRenderers: 1"), "The chair should use its lower movement footprint as the shared y-axis sort edge.");
    }

    private static void AssertDrawingRoomTableUsesSharedButlerYSort(string assetText)
    {
        string tableBlock = ExtractObjectBlock(assetText, "tea_service_table");
        string blockerBlock = ExtractObjectBlock(assetText, "PlayerBlocker_tea_service_table");

        Assert.That(tableBlock, Does.Contain("roomLocalFootPoint: {x: -80.26, y: -211.67}"));
        Assert.That(tableBlock, Does.Contain("applySorting: 0"), "The table must not compete with the shared Butler y-axis sorter.");
        Assert.That(tableBlock, Does.Contain("sortingOffset: 0"));
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

        Assert.That(sourceBlock, Does.Contain("applySorting: 0"), $"'{sourceObjectName}' must not keep a competing projection sorter.");
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
            $@"(?m)^\s*(?:(?:public|private|protected|internal|static|virtual|override|sealed|async|new)\s+)*[A-Za-z_][A-Za-z0-9_<>,\[\]?]*\s+{Regex.Escape(methodName)}\s*\(");
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
