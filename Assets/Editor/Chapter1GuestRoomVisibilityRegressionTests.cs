using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class Chapter1GuestRoomVisibilityRegressionTests
{
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string Chapter1SceneActionPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string DrawingRoomPrefabPath = "Assets/Prefabs/Room_Drawing_Room.prefab";
    private const string DrawingRoomPerspectivePrefabPath = "Assets/Prefabs/Room_Drawing_Room_Perspective.prefab";
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
        string entryMethodBody = ExtractMethodBody(controllerText, "private Vector3 GetWorldDrawingRoomEntryPosition");
        string entryBaseMethodBody = ExtractMethodBody(controllerText, "private Vector3 GetWorldDrawingRoomEntryBasePosition");
        string editableTargetMethodBody = ExtractMethodBody(controllerText, "private bool TryGetGrandEntranceDrawingRoomGuestTargetPosition");
        string spotMethodBody = ExtractMethodBody(controllerText, "private Transform ResolveDrawingRoomSpotForGuest");

        Assert.That(entryMethodBody, Does.Contain("GetWorldDrawingRoomEntryBasePosition(guestState)"), "World-space guest exit movement should use a converted visible entrance-hall doorway target.");
        Assert.That(entryBaseMethodBody, Does.Contain("TryGetGrandEntranceDrawingRoomGuestTargetPosition"), "Guest movement should prefer the hand-authored straight-line target.");
        Assert.That(editableTargetMethodBody, Does.Contain("drawingRoomDoorTarget"), "The Drawing Room door walking target should use its serialized Entrance Hall anchor.");
        Assert.That(editableTargetMethodBody, Does.Not.Contain("FindAnchor"), "The serialized Drawing Room door target must not be rediscovered.");
        Assert.That(entryBaseMethodBody, Does.Not.Contain("TryGetGrandEntranceDrawingRoomDoorPosition"), "Guest movement must not globally scan duplicate door triggers.");
        Assert.That(entryBaseMethodBody, Does.Contain("GetWorldVisibleAnchorPosition"), "World-space guest movement should convert room-stage anchors into guest world coordinates.");
        Assert.That(entryMethodBody, Does.Not.Contain("GetEntranceDrawingRoomExitPosition"), "World-space guest movement must not chase a UI/RectTransform Drawing Room door coordinate.");
        Assert.That(spotMethodBody, Does.Contain("GetDrawingRoomGuestPoint"), "World-space guests should use their ordered authored Drawing Room point.");
        Assert.That(spotMethodBody, Does.Not.Contain("GetWorldDrawingRoomSeatPosition"), "World-space guests must not synthesize replacement seats.");
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
        string waitBody = ExtractMethodBody(controllerText, "private Vector3 GetWorldEntranceWaitPosition(GuestRuntimeState guestState, int fallbackIndex, int fallbackCount)");
        string waitIndexBody = ExtractMethodBody(controllerText, "private Vector3 GetEntranceWaitPosition(int indexInBatch, int batchCount)");
        string worldWaitIndexBody = ExtractMethodBody(controllerText, "private Vector3 GetWorldEntranceWaitPosition(int indexInBatch, int batchCount)");
        string worldEntranceOffsetBody = ExtractMethodBody(controllerText, "private Vector2 GetWorldEntranceGroupOffset");
        string uiEntranceOffsetBody = ExtractMethodBody(
            controllerText,
            "private Vector2 GetEntranceGroupOffset(\n" +
            "        GuestRuntimeState guestState,\n" +
            "        int fallbackIndex,\n" +
            "        int fallbackCount,\n" +
            "        float spacing,\n" +
            "        float baseY)");
        string entranceCenterBody = ExtractMethodBody(controllerText, "private Vector3 GetWorldEntranceCenterPosition(GuestRuntimeState guestState)");
        string interactionTargetBody = ExtractMethodBody(controllerText, "private Transform GetFrontDoorInteractionTransform");
        string conversionBody = ExtractMethodBody(controllerText, "private bool TryGetWorldPositionForGuestTarget");
        string guestArrivalDoorBlock = ExtractObjectBlock(sceneText, "GuestArrival_Door");
        string guestEntrancePlacemarkBlock = ExtractObjectBlock(sceneText, "Placemark_guests_entrance");
        string doorAnswerTriggerBlock = ExtractObjectBlock(sceneText, "Door_answer_trigger");
        string drawingRoomSideSpotBlock = ExtractObjectBlock(sceneText, "DrawingRoomSideButlerSpot");
        string drawingRoomDoorTriggerBlock = ExtractObjectBlock(sceneText, "DoorTrigger_GEH_DrawingRoom");

        Assert.That(controllerText, Does.Contain("[SerializeField] private Transform entranceHallGuestAnchor;"), "Chapter 1 should own the editable Entrance Hall guest anchor directly.");
        Assert.That(controllerText, Does.Contain("[SerializeField] private Transform guestEntranceSpawnPlacemark;"), "Guest entrance spawning should own the draggable scene placemark directly.");
        Assert.That(controllerText, Does.Contain("[SerializeField] private Transform frontDoorArrivalPoint;"), "Front-door guest spawning should own a separate front entrance anchor.");
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
        Assert.That(doorArrivalTargetBody, Does.Contain("guestEntranceSpawnPlacemark"), "Front-door spawning should first use the serialized movable Placemark_guests_entrance anchor.");
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
        Assert.That(controllerText, Does.Not.Contain("private Transform GetEntranceHallGuestAnchor("), "The serialized entrance wait point must not have a discovery wrapper.");
        Assert.That(controllerText, Does.Not.Contain("FindAnchor("), "Chapter 1 must not scan RoomAnchor instances for immutable scene data.");
        Assert.That(doorArrivalBody, Does.Not.Contain("GetWorldVisibleAnchorPosition"), "Door-answer spawning should not project the high-Z GuestArrival_Door stage anchor off camera.");
        Assert.That(waitBody, Does.Not.Contain("GetWorldVisibleAnchorPosition"), "Entrance waiting should not project high-Z stage anchors off camera.");
        Assert.That(interactionTargetBody, Does.Match(@"frontDoorArrivalPoint[\s\S]*return frontDoorArrivalPoint[\s\S]*frontDoorSceneAction != null[\s\S]*frontDoorSceneAction.transform"), "The butler should walk to the centered front-door arrival point before answering the door.");
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
        string spotMethodBody = ExtractMethodBody(controllerText, "private Transform ResolveDrawingRoomSpotForGuest");
        string seatMethodBody = ExtractMethodBody(controllerText, "private Transform ResolveSeatForGuest");
        string placeMethodBody = ExtractMethodBody(controllerText, "private void PlaceGuestAt");

        Assert.That(controllerText, Does.Contain("[SerializeField] private Transform[] drawingRoomGuestPoints"), "Chapter 1 should own its ordered editable Drawing Room guest points.");
        Assert.That(spotMethodBody, Does.Contain("GetDrawingRoomGuestPoint(guest.GuestIndex)"), "Guests should use their direct authored Drawing Room point.");
        Assert.That(seatMethodBody, Does.Contain("return GetDrawingRoomGuestPoint(index);"), "Assigned seats should use the same ordered authored point graph.");
        Assert.That(controllerText, Does.Not.Contain("FindDrawingRoomGuestPoint"), "Editable guest points must not be rediscovered by RoomAnchor or object name.");
        Assert.That(controllerText, Does.Not.Contain("DrawingRoomSeat_Runtime_"), "Chapter 1 must not synthesize replacement seat anchors.");
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
        string handleClosetBody = ExtractMethodBody(controllerText, "public void HandleClosetClicked");
        string walkClosetBody = ExtractMethodBody(controllerText, "private void WalkButlerToCloset");
        string completeClosetBody = ExtractMethodBody(controllerText, "private void CompletePendingClosetStorage");
        string closetDestinationBody = ExtractMethodBody(controllerText, "private bool TryGetClosetApproachDestination");
        string closetScreenBody = ExtractMethodBody(controllerText, "private bool TryGetClosetApproachScreenPosition");
        string walkCoatBody = ExtractMethodBody(controllerText, "private void WalkButlerToCoat");
        string closeCoatBody = ExtractMethodBody(controllerText, "private bool IsButlerCloseToCoat");
        string actionBoundsBody = ExtractMethodBody(actionText, "private bool IsPointerInsideActionBounds");
        string screenBoundsBody = ExtractMethodBody(actionText, "private bool IsPointerInsideScreenBounds");
        string actionUpdateBody = ExtractMethodBody(actionText, "private void Update");
        string performActionBody = ExtractMethodBody(actionText, "private void PerformAction");
        string resolveReferencesBody = ExtractMethodBody(controllerText, "ResolveReferences(bool createFallbacks)");

        Assert.That(sceneText, Does.Contain("m_Name: entrance_coat_hanger_0"), "Gameplay should contain the authored entrance coat hanger object.");
        Assert.That(sceneText, Does.Contain("- component: {fileID: 1592234996}"), "The authored hanger should own its serialized trigger collider.");
        Assert.That(sceneText, Does.Contain("- component: {fileID: 1592234995}"), "The authored hanger should own its serialized scene action.");
        Assert.That(sceneText, Does.Contain("- component: {fileID: 3303000001}"), "The authored hanger should own its serialized coat storage.");
        Assert.That(sceneText, Does.Contain("closetPoint: {fileID: 1592234993}"), "The controller should approach the authored hanger transform directly.");
        Assert.That(controllerText, Does.Not.Contain("EntranceCoatHangerName"), "Chapter 1 should not discover the serialized hanger by name.");
        Assert.That(controllerText, Does.Not.Contain("EnsureEntranceCoatHanger"), "Chapter 1 should not repair the serialized hanger at runtime.");
        Assert.That(controllerText, Does.Not.Contain("ConfigureAuthoredCoatHangerObject"));
        Assert.That(controllerText, Does.Not.Contain("EnsureCoatHangerCollider"));
        Assert.That(controllerText, Does.Not.Contain("GetCoatHangerColliderSize"));
        Assert.That(controllerText, Does.Not.Contain("AddComponent<CoatCloset>"));
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
        Assert.That(resolveReferencesBody, Does.Not.Contain("EnsureEntranceCoatHanger"), "Runtime reference resolution should retain the serialized hanger owner.");
        Assert.That(controllerText, Does.Not.Contain("ResolveAnchors"), "Serialized Entrance anchors must not be repaired at runtime.");
        Assert.That(controllerText, Does.Not.Contain("FindPropAnchor"), "The dead pantry-prop anchor lookup must stay removed.");
        Assert.That(controllerText, Does.Not.Contain("IsUnderNamedTransform"), "The dead pantry hierarchy-name scan must stay removed.");
        Assert.That(controllerText, Does.Contain("Chapter1ArrivalController requires its serialized Entrance coat closet."));
        Assert.That(controllerText, Does.Contain("Chapter1ArrivalController requires its serialized Entrance closet approach point."));
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
    public void SceneGuestsPreserveAuthoredSortingInPlayMode()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string playerMovementText = File.ReadAllText(PointClickPlayerMovementPath);
        string awakeMethodBody = ExtractMethodBody(playerMovementText, "private void Awake");
        string playerSortingMethodBody = ExtractMethodBody(playerMovementText, "private void ApplyPlayerSorting");
        string playerSortingSetterBody = ExtractMethodBody(playerMovementText, "public void SetPlayerSortingEnabled");
        string prepareMethodBody = ExtractMethodBody(controllerText, "PrepareSceneGuestObject");
        string disablePlayerMethodBody = ExtractMethodBody(controllerText, "DisablePlayerOnlyComponents");
        string preserveMethodBody = ExtractMethodBody(controllerText, "ShouldPreserveAuthoredGuestSorting");

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
    }

    [Test]
    public void EntranceHallTemporarilySortsGuestsByArrivalPair()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string resetMethodBody = ExtractMethodBody(controllerText, "ResetChapterRuntime");
        string prepareMethodBody = ExtractMethodBody(controllerText, "PrepareSceneGuestObject");
        string activateMethodBody = ExtractMethodBody(controllerText, "ActivateAuthoredChapterGuestObject");
        string forceVisibleMethodBody = ExtractMethodBody(controllerText, "ForceGuestVisibleForDoorFlow");
        string applyMethodBody = ExtractMethodBody(controllerText, "ApplyEntranceHallGuestSorting");
        string coatSortingMethodBody = ExtractMethodBody(controllerText, "ConfigureAssignedCoatSorting");
        string cacheCoatSortingMethodBody = ExtractMethodBody(controllerText, "CacheConfiguredCoatSorting");
        string moveMethodBody = ExtractMethodBody(controllerText, "MoveGuestToDrawingRoom");
        string banisterSafeWalkMethodBody = ExtractMethodBody(controllerText, "ApplyEntranceBanisterSafeWalkingSorting");
        string completeMethodBody = ExtractMethodBody(controllerText, "CompleteGuestDrawingRoomArrival");
        string skipStageMethodBody = ExtractMethodBody(controllerText, "StageGuestInDrawingRoomForChapter2");

        Assert.That(controllerText, Does.Contain("[Header(\"Entrance Sorting\")]"), "Entrance hall should have its own temporary sorting controls.");
        Assert.That(controllerText, Does.Contain("entranceGuestSortingOrderGroupStep"), "Entrance sorting should step each arrival pair above the previous pair.");
        Assert.That(controllerText, Does.Contain("authoredGuestRendererSorting"), "The temporary entrance override should preserve drawing-room authored sorting.");
        Assert.That(resetMethodBody, Does.Contain("authoredGuestRendererSorting.Clear()"), "A fresh Chapter 1 run should not reuse stale renderer sorting cache entries.");
        Assert.That(prepareMethodBody, Does.Contain("CacheGuestAuthoredSorting(guestObject)"), "Guest preparation should cache the drawing-room-authored sort order before hallway overrides.");
        Assert.That(activateMethodBody, Does.Contain("ApplyEntranceHallGuestSorting(guestState)"), "Authored scene guests should receive the entrance hallway pair order when admitted.");
        Assert.That(forceVisibleMethodBody, Does.Contain("ApplyEntranceHallGuestSorting(guestState)"), "Door-flow visibility refreshes should reapply the entrance pair order after movement/state changes.");
        Assert.That(applyMethodBody, Does.Contain("groupIndex * Mathf.Max(1, entranceGuestSortingOrderGroupStep)"), "Later arrival pairs should render above earlier pairs.");
        Assert.That(applyMethodBody, Does.Contain("slotIndex * Mathf.Max(1, entranceGuestSortingOrderSlotStep)"), "Guests inside a pair should still have a stable left/right order.");
        Assert.That(applyMethodBody, Does.Contain("GetCachedSortingOrder(renderer) - authoredReferenceOrder"), "Coat/body renderer offsets should survive the temporary hallway override.");
        Assert.That(coatSortingMethodBody, Does.Contain("CacheConfiguredCoatSorting(coatRenderer)"), "Coat sorting should refresh the cached renderer order after being moved in front of the guest.");
        Assert.That(cacheCoatSortingMethodBody, Does.Contain("authoredGuestRendererSorting[coatRenderer]"), "Coat cache refresh should replace stale scene coat ordering, not preserve order zero behind the guest.");
        Assert.That(applyMethodBody, Does.Contain("guestState.MovingToDrawingRoom"), "Guests walking to the Drawing Room should not regain the high entrance-hall sorting override.");
        Assert.That(moveMethodBody, Does.Match(@"RestoreGuestAuthoredSorting\(guest\)[\s\S]*ApplyEntranceBanisterSafeWalkingSorting\(guest\)[\s\S]*BeginGuestMoveTo\(guest, drawingRoomEntry"), "Guests should switch from the high entrance override to banister-safe sorting before walking to the Drawing Room door.");
        Assert.That(banisterSafeWalkMethodBody, Does.Contain("EntranceBanisterSafeWalkingSortingOrder"), "The drawing-room walk should use a transition-only order below the front banister.");
        Assert.That(banisterSafeWalkMethodBody, Does.Contain("GetCachedSortingOrder(renderer) - referenceOrder"), "The banister-safe walk should preserve local body/coat renderer offsets.");
        Assert.That(completeMethodBody, Does.Match(@"RestoreGuestAuthoredSorting\(guest\)[\s\S]*PlaceGuestAt\(guest, drawingRoomSpot"), "Normal Drawing Room entry should restore authored sort order before placement.");
        Assert.That(skipStageMethodBody, Does.Match(@"RestoreGuestAuthoredSorting\(guest\)[\s\S]*PlaceGuestAt\(guest, drawingRoomSpot"), "Chapter 2 skip staging should also restore authored sort order before Drawing Room placement.");
    }

    [Test]
    public void DrawingRoomGuestsAndFurnitureUsePerspectiveDepthSorting()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string completeMethodBody = ExtractMethodBody(controllerText, "CompleteGuestDrawingRoomArrival");
        string skipStageMethodBody = ExtractMethodBody(controllerText, "StageGuestInDrawingRoomForChapter2");
        string depthSortMethodBody = ExtractMethodBody(controllerText, "private void ApplyDrawingRoomGuestDepthSorting");
        string gameplaySceneText = File.ReadAllText(GameplayScenePath);
        string drawingRoomPrefabText = File.ReadAllText(DrawingRoomPrefabPath);
        string drawingRoomPerspectivePrefabText = File.ReadAllText(DrawingRoomPerspectivePrefabPath);

        Assert.That(completeMethodBody, Does.Match(@"PlaceGuestAt\(guest, drawingRoomSpot[\s\S]*ApplyDrawingRoomGuestDepthSorting\(guest, drawingRoomSpot\)"), "Normal Drawing Room arrivals should sort guests from their actual room-local foot anchor.");
        Assert.That(skipStageMethodBody, Does.Match(@"PlaceGuestAt\(guest, drawingRoomSpot[\s\S]*ApplyDrawingRoomGuestDepthSorting\(guest, drawingRoomSpot\)"), "Chapter 2 skip staging should use the same Drawing Room depth sorting as normal play.");
        Assert.That(depthSortMethodBody, Does.Contain("TryGetRoomLocalFootPoint(drawingRoomSpot"), "Guest sorting should use the Drawing Room anchor's local foot point.");
        Assert.That(depthSortMethodBody, Does.Contain("TryGetPerspectiveProfileForTarget(drawingRoomSpot"), "Guest sorting should come from the target room profile, not a fixed order.");
        Assert.That(depthSortMethodBody, Does.Contain("profile.GetSortingOrder(roomLocalFootPoint)"), "Guest sorting should use y-axis depth from the room profile.");
        Assert.That(depthSortMethodBody, Does.Contain("GetCachedSortingOrder(renderer) - referenceOrder"), "Depth sorting should preserve local renderer offsets such as coats or layered bodies.");
        Assert.That(depthSortMethodBody, Does.Not.Contain("9000"), "Drawing Room depth sorting should not reuse the entrance fallback sorting band.");

        AssertDrawingRoomSetPieceDepth(gameplaySceneText, "tea_service_table", "roomLocalOcclusionAnchor: {x: -80.26, y: -211.67}", "m_SortingOrder: 6627", "cutoutRenderer: {fileID: 2088426359}", "m_Father: {fileID: 3930000001}");
        AssertDrawingRoomProjectedOccluderDepth(gameplaySceneText, "drawing_room_red_chair_guest6", "roomLocalFootPoint: {x: 59, y: -208.5}", "m_SortingOrder: 800", "sortingOffset: -5776");
        AssertDrawingRoomProjectedOccluderDepth(gameplaySceneText, "purple_armchair_back", "roomLocalFootPoint: {x: 243.62, y: -315.58}", "m_SortingOrder: 8289");
        AssertDrawingRoomProjectedOccluderDepth(gameplaySceneText, "drawingroomgreenchair_0", "roomLocalFootPoint: {x: -479.54, y: -281.56}", "m_SortingOrder: 7745");
        AssertDrawingRoomProjectedOccluderDepth(gameplaySceneText, "drawingroomgreenchair[_0", "roomLocalFootPoint: {x: -408.72, y: -261.91}", "m_SortingOrder: 7431");

        AssertDrawingRoomSetPieceDepth(drawingRoomPrefabText, "tea_service_table", "roomLocalOcclusionAnchor: {x: -77.23, y: -208.14}", "m_SortingOrder: 6570", "cutoutRenderer: {fileID: 4469554848413931009}", "m_Father: {fileID: 3931000001}");
        AssertDrawingRoomProjectedOccluderDepth(drawingRoomPrefabText, "drawing_room_red_chair_guest6", "roomLocalFootPoint: {x: 59, y: -208.5}", "m_SortingOrder: 800", "sortingOffset: -5776");
        AssertDrawingRoomProjectedOccluderDepth(drawingRoomPrefabText, "purple_armchair_back", "roomLocalFootPoint: {x: 243.62, y: -315.58}", "m_SortingOrder: 8289");
        AssertDrawingRoomProjectedOccluderDepth(drawingRoomPrefabText, "drawingroomgreenchair_0", "roomLocalFootPoint: {x: -490.46, y: -282.58}", "m_SortingOrder: 7761");

        AssertDrawingRoomSetPieceDepth(drawingRoomPerspectivePrefabText, "tea_service_table", "roomLocalOcclusionAnchor: {x: -77.23, y: -208.14}", "m_SortingOrder: 6570", "cutoutRenderer: {fileID: 7736515036983942028}", "m_Father: {fileID: 3932000001}");
        AssertDrawingRoomProjectedOccluderDepth(drawingRoomPerspectivePrefabText, "drawing_room_red_chair_guest6", "roomLocalFootPoint: {x: 59, y: -208.5}", "m_SortingOrder: 800", "sortingOffset: -5776");
        AssertDrawingRoomProjectedOccluderDepth(drawingRoomPerspectivePrefabText, "purple_armchair_back", "roomLocalFootPoint: {x: 243.62, y: -315.58}", "m_SortingOrder: 8289");
        AssertDrawingRoomProjectedOccluderDepth(drawingRoomPerspectivePrefabText, "drawingroomgreenchair_0", "roomLocalFootPoint: {x: -490.46, y: -282.58}", "m_SortingOrder: 7761");
    }

    [Test]
    public void DrawingRoomTeaTableUsesOneSetPieceDepthOwnerAndKeepsCollisionFootprint()
    {
        string gameplaySceneText = File.ReadAllText(GameplayScenePath);
        string tableBlock = ExtractGameObjectBundle(gameplaySceneText, "tea_service_table");
        string blockerBlock = ExtractGameObjectBundle(gameplaySceneText, "PlayerBlocker_tea_service_table");
        string setPieceText = File.ReadAllText("Assets/_Chateau/Runtime/World/Rooms/Props/SetPieces/SetPieceView.cs");
        string blockerText = File.ReadAllText("Assets/Scripts/Navigation/ObjectMovementBlocker2D.cs");

        Assert.That(tableBlock, Does.Contain("m_Sprite: {fileID: -7836622596164935206, guid: c9c9711a41d82097fbae9cb69d6b7e6d, type: 3}"));
        Assert.That(tableBlock, Does.Contain("m_Materials:\n  - {fileID: 2100000, guid: a97c105638bdf8b4a8650670310a4cd3, type: 2}"));
        Assert.That(tableBlock, Does.Contain("m_LocalPosition: {x: -80.26, y: -211.67, z: -6570.105}"));
        Assert.That(tableBlock, Does.Contain("m_LocalScale: {x: 99.52793, y: 99.40213, z: 73.00117}"));
        Assert.That(tableBlock, Does.Contain("guid: 5e7a11c7d4b24c68a1f9e2d3c4b5a607"));
        Assert.That(tableBlock, Does.Not.Contain("guid: 361e3658088b41ab98d330ae6457640b"));
        Assert.That(tableBlock, Does.Contain("cutoutRenderer: {fileID: 2088426359}"));
        Assert.That(tableBlock, Does.Contain("roomLocalOcclusionAnchor: {x: -80.26, y: -211.67}"));
        Assert.That(tableBlock, Does.Contain("m_SortingOrder: 6627"));
        Assert.That(tableBlock, Does.Contain("m_Father: {fileID: 3930000001}"));
        Assert.That(gameplaySceneText, Does.Contain("m_Name: Set Pieces"));
        Assert.That(gameplaySceneText, Does.Contain("m_Father: {fileID: 3502000003}"));
        Assert.That(gameplaySceneText, Does.Contain("- {fileID: 2088426361}"), "GameRoot should bind the inactive set-piece view.");

        Assert.That(blockerBlock, Does.Contain("guid: b95469e02af64fee8b29689edb9b583a"));
        Assert.That(blockerBlock, Does.Contain("sourceObject: {fileID: 2088426358}"));
        Assert.That(blockerBlock, Does.Contain("sourceObjectName: tea_service_table"));
        Assert.That(blockerBlock, Does.Contain("sourceRoomName: Drawing Room"));
        Assert.That(blockerBlock, Does.Contain("category: Table"));
        Assert.That(blockerBlock, Does.Contain("sortSourceRenderers: 0"));
        Assert.That(blockerBlock, Does.Contain("m_IsTrigger: 1"));
        Assert.That(blockerBlock, Does.Contain("- - {x: -214.44357, y: -357.79114}"));
        Assert.That(blockerBlock, Does.Contain("- {x: 53.923557, y: -357.79114}"));
        Assert.That(blockerBlock, Does.Contain("- {x: 53.923557, y: -270.11847}"));
        Assert.That(blockerBlock, Does.Contain("- {x: -214.44357, y: -270.11847}"));

        Assert.That(setPieceText, Does.Not.Contain("LateUpdate()"));
        Assert.That(setPieceText, Does.Not.Contain(".bounds"));
        Assert.That(setPieceText, Does.Contain("cutoutRenderer.sortingOrder = CurrentSortingOrder"));
        Assert.That(blockerText, Does.Match(@"public void ApplySourceSortingNow\(\)[\s\S]*if \(!sortSourceRenderers\)[\s\S]*return"));
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

    private static void AssertDrawingRoomProjectedOccluderDepth(string assetText, string objectName, string expectedFootPoint, string expectedSortingOrder, string expectedSortingOffset = null)
    {
        string objectBlock = ExtractObjectBlock(assetText, objectName);

        Assert.That(objectBlock, Does.Contain("guid: 361e3658088b41ab98d330ae6457640b"), $"The Drawing Room object '{objectName}' should use RoomProjectedEntity for depth sorting.");
        Assert.That(objectBlock, Does.Contain("roomProfile: {fileID: 11400000, guid: 426f8e326a60b3a0eaeb540d7d670267"), $"The Drawing Room object '{objectName}' should sort against the Drawing Room perspective profile.");
        Assert.That(objectBlock, Does.Contain(expectedFootPoint), $"The Drawing Room object '{objectName}' sort point should match the authored floor/occlusion point.");
        Assert.That(objectBlock, Does.Contain("applyPosition: 0"), $"The Drawing Room object '{objectName}' projection must not move authored art.");
        Assert.That(objectBlock, Does.Contain("applyScale: 0"), $"The Drawing Room object '{objectName}' projection must not resize authored art.");
        Assert.That(objectBlock, Does.Contain("applyTint: 0"), $"The Drawing Room object '{objectName}' projection must not recolor authored art.");
        Assert.That(objectBlock, Does.Contain("applySorting: 1"), $"The Drawing Room object '{objectName}' projection should only own sorting.");
        Assert.That(objectBlock, Does.Contain(expectedSortingOrder), $"The Drawing Room object '{objectName}' serialized order should match its profile-derived y depth.");
        if (!string.IsNullOrEmpty(expectedSortingOffset))
        {
            Assert.That(objectBlock, Does.Contain(expectedSortingOffset), $"The Drawing Room object '{objectName}' should keep its authored projection sorting offset.");
        }
    }

    private static void AssertDrawingRoomSetPieceDepth(
        string assetText,
        string objectName,
        string expectedAnchor,
        string expectedSortingOrder,
        string expectedRendererReference,
        string expectedParentReference)
    {
        string objectBlock = ExtractGameObjectBundle(assetText, objectName);

        Assert.That(objectBlock, Does.Contain("guid: 5e7a11c7d4b24c68a1f9e2d3c4b5a607"), $"The Drawing Room object '{objectName}' should use the target SetPieceView.");
        Assert.That(objectBlock, Does.Not.Contain("guid: 361e3658088b41ab98d330ae6457640b"), $"The Drawing Room object '{objectName}' should no longer use the actor projection helper.");
        Assert.That(objectBlock, Does.Contain("depthProfile: {fileID: 11400000, guid: 426f8e326a60b3a0eaeb540d7d670267"));
        Assert.That(objectBlock, Does.Contain(expectedRendererReference));
        Assert.That(objectBlock, Does.Contain(expectedAnchor));
        Assert.That(objectBlock, Does.Contain("sortingOffset: 0"));
        Assert.That(objectBlock, Does.Contain(expectedSortingOrder));
        Assert.That(objectBlock, Does.Contain(expectedParentReference));
        Assert.That(assetText, Does.Contain("m_Name: Set Pieces"));
    }

    private static string ExtractObjectBlock(string assetText, string objectName)
    {
        int nameIndex = assetText.IndexOf($"m_Name: {objectName}", StringComparison.Ordinal);
        Assert.That(nameIndex, Is.GreaterThanOrEqualTo(0), $"Could not find object '{objectName}'.");

        const string gameObjectDocumentPrefix = "--- !u!1 &";
        int blockStart = assetText.LastIndexOf(gameObjectDocumentPrefix, nameIndex, StringComparison.Ordinal);
        Assert.That(blockStart, Is.GreaterThanOrEqualTo(0), $"Could not find object block start for '{objectName}'.");

        int blockEnd = assetText.IndexOf(gameObjectDocumentPrefix, nameIndex + objectName.Length, StringComparison.Ordinal);
        return blockEnd >= 0
            ? assetText.Substring(blockStart, blockEnd - blockStart)
            : assetText.Substring(blockStart);
    }

    private static string ExtractGameObjectBundle(string assetText, string objectName)
    {
        MatchCollection bundles = Regex.Matches(
            assetText,
            @"(?ms)^--- !u!1 &[^\r\n]+\r?\nGameObject:.*?(?=^--- !u!1 &[^\r\n]+\r?\nGameObject:|\z)");
        string nameLine = $"\n  m_Name: {objectName}\n";

        foreach (Match bundle in bundles)
        {
            if (bundle.Value.Contains(nameLine))
            {
                return bundle.Value;
            }
        }

        Assert.Fail($"Could not find serialized GameObject bundle '{objectName}'.");
        return string.Empty;
    }

    private static string ExtractMethodBody(string sourceText, string methodName)
    {
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
