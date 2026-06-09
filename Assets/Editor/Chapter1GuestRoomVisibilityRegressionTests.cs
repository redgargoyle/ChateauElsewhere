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
        string doorArrivalBody = ExtractMethodBody(controllerText, "GetWorldDoorArrivalPosition");
        string waitBody = ExtractMethodBody(controllerText, "GetWorldEntranceWaitPosition");
        string interactionTargetBody = ExtractMethodBody(controllerText, "GetFrontDoorInteractionTransform");
        string conversionBody = ExtractMethodBody(controllerText, "TryGetWorldPositionForGuestTarget");

        Assert.That(doorArrivalBody, Does.Contain("GetWorldEntranceCenterPosition()"), "Door-answer spawning should use the stable world-space entrance cluster instead of projecting authored room-stage anchors.");
        Assert.That(waitBody, Does.Contain("GetWorldEntranceCenterPosition()"), "Entrance wait spots should stay near the visible guest cluster.");
        Assert.That(doorArrivalBody, Does.Not.Contain("GetWorldVisibleAnchorPosition"), "Door-answer spawning should not project the high-Z GuestArrival_Door stage anchor off camera.");
        Assert.That(waitBody, Does.Not.Contain("GetWorldVisibleAnchorPosition"), "Entrance waiting should not project the high-Z ButlerGreetingSpot stage anchor off camera.");
        Assert.That(interactionTargetBody, Does.Match(@"frontDoorArrivalPoint[\s\S]*return frontDoorArrivalPoint[\s\S]*butlerDoorSpot"), "The butler should walk to the front-door arrival point before answering the door.");
        Assert.That(conversionBody, Does.Not.Contain("target.GetComponentInParent<Canvas>(true) == null"), "Visible anchor conversion must work for non-Canvas room-stage anchors as well as UI anchors.");
        Assert.That(conversionBody, Does.Contain("TryGetTargetScreenPosition"), "Visible anchor conversion should preserve what the player sees on screen.");
        Assert.That(conversionBody, Does.Contain("mainCamera.ScreenToWorldPoint"), "Drawing Room anchor conversion should land on the guest world plane instead of raw room-stage coordinates.");
    }

    [Test]
    public void FrontDoorActionUsesArrivalControllerAnswerSpot()
    {
        string actionText = File.ReadAllText(Chapter1SceneActionPath);
        string startBody = ExtractMethodBody(actionText, "StartFrontDoorApproach");
        string closeBody = ExtractMethodBody(actionText, "IsPlayerCloseToFrontDoor");

        Assert.That(startBody, Does.Contain("arrivalController.TryGetFrontDoorApproachDestination(playerMovement, out Vector2 approachDestination)"), "Front-door clicks should use the arrival controller's reachable greeting spot.");
        Assert.That(startBody, Does.Contain("TryFindClosestReachableDestinationToWorldPoint(transform.position, out approachDestination)"), "The raw trigger transform should only be the fallback for front-door walking.");
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
    public void Chapter1GuestsUseAuthoredScaleAsRoomZoomBaseline()
    {
        string controllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string playerMovementText = File.ReadAllText(PointClickPlayerMovementPath);
        string actorRoomStateText = File.ReadAllText(ActorRoomStatePath);
        string prepareMethodBody = ExtractMethodBody(controllerText, "PrepareSceneGuestObject");
        string disablePlayerMethodBody = ExtractMethodBody(controllerText, "DisablePlayerOnlyComponents");
        string placeMethodBody = ExtractMethodBody(controllerText, "PlaceGuestAt");

        Assert.That(playerMovementText, Does.Contain("applyPerspectiveScale"), "Player movement should have an explicit switch for runtime perspective scale.");
        Assert.That(playerMovementText, Does.Contain("SetPerspectiveScaleEnabled"), "Guests cloned from the player prefab need a public way to opt out of player depth scaling.");
        Assert.That(playerMovementText, Does.Match(@"if \(!applyPerspectiveScale\)[\s\S]*return;"), "Disabled perspective scale should stop PointClickPlayerMovement from writing transform.localScale.");
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
