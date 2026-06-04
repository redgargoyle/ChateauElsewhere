using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class Chapter2RegressionTests
{
    private const string ChapterManagerPath = "Assets/Scripts/Story/ChapterManager.cs";
    private const string ChapterIntroUIPath = "Assets/Scripts/Story/ChapterIntroUI.cs";
    private const string CameraManagerPath = "Assets/Map/CameraManager.cs";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string Chapter2DirectoryPath = "Assets/_Chateau/Scripts/Chapter/Chapter02";
    private const string Chapter2ControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs";
    private const string Chapter2InteractionHUDPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2InteractionHUD.cs";
    private const string Chapter2MonsterStingerControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2MonsterStingerController.cs";
    private const string Chapter2GuestSearchControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs";
    private const string Chapter2GuestFindActionPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestFindAction.cs";
    private const string Chapter2ScriptPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Script.md";
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string DoorTriggerNavigationPath = "Assets/Scripts/Navigation/DoorTriggerNavigation.cs";

    [Test]
    public void Chapter2ScriptSpecExists()
    {
        Assert.That(File.Exists(Chapter2ScriptPath), Is.True, "Chapter 2 should have a markdown implementation script.");
    }

    [Test]
    public void ChapterTitlesUseChapterLabels()
    {
        string managerText = File.ReadAllText(ChapterManagerPath);
        string introText = File.ReadAllText(ChapterIntroUIPath);
        string chapter2Text = File.ReadAllText(Chapter2ControllerPath);

        Assert.That(managerText, Does.Contain("Chapter1Title = \"Chapter 1\""));
        Assert.That(managerText, Does.Contain("displayedTitle = Chapter1Title"));
        Assert.That(introText, Does.Contain("defaultTitle = \"Chapter 1\""));
        Assert.That(chapter2Text, Does.Contain("chapterTitle = \"Chapter 2\""));
        Assert.That(chapter2Text, Does.Contain("ShowTitle(chapterTitle)"));
    }

    [Test]
    public void Chapter1ArrivalControllerDoesNotOwnChapter2()
    {
        string chapter1Text = File.ReadAllText(Chapter1ArrivalControllerPath);

        Assert.That(chapter1Text, Does.Not.Contain("Chapter2Controller"));
        Assert.That(chapter1Text, Does.Not.Contain("Chapter2GuestSearchController"));
        Assert.That(chapter1Text, Does.Not.Contain("Chapter2MonsterStingerController"));
        Assert.That(chapter1Text, Does.Not.Contain("Chapter2InteractionHUD"));
        Assert.That(chapter1Text, Does.Not.Contain("chapter_03_dinner_pending"));
    }

    [Test]
    public void Chapter2ControllerExistsAndDeclaresExpectedHandoff()
    {
        Assert.That(File.Exists(Chapter2ControllerPath), Is.True, "Chapter 2 should have a dedicated controller.");

        string controllerText = File.ReadAllText(Chapter2ControllerPath);
        string[] expectedPhases =
        {
            "NotStarted",
            "FadeInDrawingRoom",
            "AwaitingAddressPrompt",
            "ButlerSpeech",
            "MonsterStinger",
            "GuestSearch",
            "DiningRoomObjective",
            "DiningRoomReveal",
            "Complete"
        };

        Assert.That(controllerText, Does.Match(@"\bBeginChapter2\s*\("), "Chapter2Controller should expose BeginChapter2.");
        Assert.That(controllerText, Does.Match(@"\bDebugResetForChapter2Skip\s*\("), "Chapter2Controller should expose a reset path for repeated debug skips.");
        Assert.That(controllerText, Does.Match(@"(?s)\bDebugResetForChapter2Skip\s*\([^)]*\)\s*\{.*StopChapter2Coroutines\s*\(\).*StopStinger\s*\(.*currentPhase\s*=\s*Chapter2Phase\.NotStarted"), "Debug skip reset should stop stale Chapter 2 routines before allowing BeginChapter2 to run again.");
        Assert.That(controllerText, Does.Match(@"\bHandleAddressGuestsPrompt\s*\("), "Chapter2Controller should expose the address prompt callback.");
        Assert.That(controllerText, Does.Contain("Chapter2InteractionHUD"), "Chapter2Controller should use the Chapter 2 interaction HUD.");
        Assert.That(controllerText, Does.Contain("Chapter2MonsterStingerController"), "Chapter2Controller should use the Chapter 2 monster stinger controller.");
        Assert.That(controllerText, Does.Contain("Chapter2GuestSearchController"), "Chapter2Controller should use the Chapter 2 guest search controller.");
        Assert.That(controllerText, Does.Match(@"\bRunOpeningSpeechRoutine\s*\("), "Chapter2Controller should run the Butler opening speech from a coroutine.");
        Assert.That(controllerText, Does.Match(@"\bHandleAllGuestsFound\s*\("), "Chapter2Controller should handle the all-guests-found transition.");

        for (int i = 0; i < expectedPhases.Length; i++)
        {
            Assert.That(controllerText, Does.Match(@"\b" + Regex.Escape(expectedPhases[i]) + @"\b"), $"Missing Chapter 2 phase: {expectedPhases[i]}.");
        }
    }

    [Test]
    public void Chapter2OpeningSpeechStopsAtMonsterStinger()
    {
        string controllerText = File.ReadAllText(Chapter2ControllerPath);

        Assert.That(controllerText, Does.Contain("Welcome friends and gentlemen, guests of the evening, Count and Countess of Chantilly—"));
        Assert.That(controllerText, Does.Contain("speechLineSeconds = 1.75f"));
        Assert.That(controllerText, Does.Match(@"(?s)\bRunOpeningSpeechRoutine\s*\([^)]*\)\s*\{.*SetPhase\s*\(\s*Chapter2Phase\.MonsterStinger\s*\)"), "Opening speech should advance to the MonsterStinger phase.");
        Assert.That(controllerText, Does.Contain("A terrible sound cuts through the room..."));
    }

    [Test]
    public void Chapter2MonsterStingerControllerExists()
    {
        Assert.That(File.Exists(Chapter2MonsterStingerControllerPath), Is.True, "Chapter 2 should have a dedicated scripted monster stinger controller.");

        string stingerText = File.ReadAllText(Chapter2MonsterStingerControllerPath);
        Assert.That(stingerText, Does.Contain("DisallowMultipleComponent"));
        Assert.That(stingerText, Does.Contain("AudioSource"));
        Assert.That(stingerText, Does.Match(@"\bBeginStinger\s*\("));
        Assert.That(stingerText, Does.Match(@"\bStopStinger\s*\("));
        Assert.That(stingerText, Does.Contain("public bool IsRunning => isRunning || stingerRoutine != null"));
        Assert.That(stingerText, Does.Contain("minimumRunSeconds = 1f"));
        Assert.That(stingerText, Does.Contain("maximumRunSeconds = 2f"));
        Assert.That(stingerText, Does.Contain("minimumFreezeSeconds = 1f"));
        Assert.That(stingerText, Does.Contain("maximumFreezeSeconds = 2f"));
        Assert.That(stingerText, Does.Contain("RunFreezeCycleCount = 3"));
        Assert.That(stingerText, Does.Contain("BuildCycleTimings"));
        Assert.That(stingerText, Does.Contain("new StingerCycleTiming[RunFreezeCycleCount]"));
        Assert.That(stingerText, Does.Contain("MoveMonsterToNextFreezeTarget"));
        Assert.That(stingerText, Does.Match(@"(?s)StingerCycleTiming\[\]\s+cycleTimings\s*=\s*BuildCycleTimings\(\);\s*for\s*\([^)]*cycleTimings\.Length[^)]*\)\s*\{\s*ApplyMonsterRoomVisibility\(\);\s*PlayViolinAudioIfVisible\(true\);\s*yield return MoveMonsterToNextFreezeTarget"), "Monster runs should continue from the current frozen position instead of resetting to runStart inside the cycle loop.");
        Assert.That(stingerText, Does.Contain("Vector3 startPosition = monsterObject.transform.position"));
        Assert.That(stingerText, Does.Contain("GetForwardRunTargetPosition"));
        Assert.That(stingerText, Does.Contain("GetRunSegmentDistance"));
        Assert.That(stingerText, Does.Contain("runSegmentDistanceScale = 0.65f"));
        Assert.That(stingerText, Does.Contain("rightDistance * runSegmentDistanceScale"));
        Assert.That(stingerText, Does.Contain("return startPosition + Vector3.right * GetRunSegmentDistance(startPosition);"));
        Assert.That(stingerText, Does.Contain("Vector3.right"));
        Assert.That(stingerText, Does.Contain("PlayViolinAudioIfVisible(true)"));
        Assert.That(stingerText, Does.Not.Contain("minimumCyclesBeforeComplete"));
        Assert.That(stingerText, Does.Not.Contain("maximumCyclesBeforeComplete"));
        Assert.That(stingerText, Does.Not.Contain("GetRandomCycleCount"));
        Assert.That(stingerText, Does.Not.Contain("TrimCycleTimingsToVisibleBudget"));
        Assert.That(stingerText, Does.Contain("violinscreech"));
        Assert.That(stingerText, Does.Contain("loopViolinAudio = true"));
        Assert.That(stingerText, Does.Contain(".loop = loopViolinAudio"));
        Assert.That(stingerText, Does.Contain("drawingRoomId = \"Drawing Room\""));
        Assert.That(stingerText, Does.Contain("maxVisibleSeconds = 12f"));
        Assert.That(stingerText, Does.Contain("OnCurrentRoomChanged"));
        Assert.That(stingerText, Does.Contain("SetActive(false)"));
        Assert.That(stingerText, Does.Contain("SetAsLastSibling"));
        Assert.That(stingerText, Does.Contain("monsterSortingOrder = 9999"));
        Assert.That(stingerText, Does.Contain("monsterOverlaySortingOrder = 10000"));
        Assert.That(stingerText, Does.Contain("overrideSorting = true"));
    }

    [Test]
    public void Chapter2ControllerRunsMonsterStingerBeforeGuestSearch()
    {
        string controllerText = File.ReadAllText(Chapter2ControllerPath);

        Assert.That(controllerText, Does.Match(@"\bRunMonsterStingerRoutine\s*\("));
        Assert.That(controllerText, Does.Contain("monsterStingerTimeoutSeconds = 14f"), "Monster stinger should have a watchdog timeout so Chapter 2 cannot stall before GuestSearch.");
        Assert.That(controllerText, Does.Match(@"(?s)\bRunMonsterStingerRoutine\s*\([^)]*\)\s*\{.*BeginStinger\s*\(.*monsterStinger\.IsRunning.*Time\.unscaledDeltaTime.*StopStinger\s*\(.*StartGuestSearch\s*\(\);\s*SetPhase\s*\(\s*Chapter2Phase\.GuestSearch\s*\)"), "Monster stinger should run before GuestSearch, but time out and continue if it gets stuck.");
        Assert.That(controllerText, Does.Contain("Find the guests. Tell them dinner will be served at 7:00 PM sharp."));
    }

    [Test]
    public void Chapter2GuestSearchControllerExistsAndUsesRoomState()
    {
        Assert.That(File.Exists(Chapter2GuestSearchControllerPath), Is.True, "Chapter 2 should have a dedicated guest search controller.");

        string guestSearchText = File.ReadAllText(Chapter2GuestSearchControllerPath);
        Assert.That(guestSearchText, Does.Contain("DisallowMultipleComponent"));
        Assert.That(guestSearchText, Does.Contain("ActorRoomState"));
        Assert.That(guestSearchText, Does.Contain("RoomAnchor"));
        Assert.That(guestSearchText, Does.Contain("ChapterActors_Runtime"));
        Assert.That(guestSearchText, Does.Contain("Ch2_Hide_"));
        Assert.That(guestSearchText, Does.Not.Contain("hideRoomId = \"Ballroom\""));
        Assert.That(guestSearchText, Does.Contain("SetCurrentRoom(guest.hideAnchor.RoomId)"));
        Assert.That(guestSearchText, Does.Contain("guests.Sort(CompareGuestIdentity)"));
        Assert.That(guestSearchText, Does.Match(@"(?s)\bBeginSearch\s*\([^)]*\)\s*\{.*activeConversationGuest\s*=\s*null"), "Beginning a fresh Chapter 2 guest search should clear any previous active conversation.");
        Assert.That(guestSearchText, Does.Contain("Ch2_DiningSeat_"));
        Assert.That(guestSearchText, Does.Contain("GuestCount"));
        Assert.That(guestSearchText, Does.Contain("FoundGuestCount"));
        Assert.That(guestSearchText, Does.Contain("GetFoundGuestDisplayNamesInOrder"));
        Assert.That(guestSearchText, Does.Contain("PrepareGuestsForDiningTransfer"));
        Assert.That(guestSearchText, Does.Match(@"\bBeginSearch\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bMarkGuestFound\s*\("));
    }

    [Test]
    public void Chapter2GuestFindActionMarksGuestsFound()
    {
        Assert.That(File.Exists(Chapter2GuestFindActionPath), Is.True, "Chapter 2 should have a dedicated guest find click action.");

        string actionText = File.ReadAllText(Chapter2GuestFindActionPath);
        string cameraText = File.ReadAllText(CameraManagerPath);
        Assert.That(actionText, Does.Contain("DisallowMultipleComponent"));
        Assert.That(actionText, Does.Contain("IPointerClickHandler"));
        Assert.That(actionText, Does.Contain("IPointerEnterHandler"));
        Assert.That(actionText, Does.Contain("IPointerExitHandler"));
        Assert.That(actionText, Does.Contain("OnMouseDown"));
        Assert.That(actionText, Does.Contain("TryStartGuestConversation(guestId)"));
        Assert.That(actionText, Does.Not.Contain("MarkGuestFound(guestId)"), "Clicking a hidden guest should start dialogue, not immediately increment the found count.");
        Assert.That(actionText, Does.Contain("HoverIcon.Talk"));
        Assert.That(cameraText, Does.Contain("Talk"));
        Assert.That(cameraText, Does.Contain("CreateTalkCursor"));
    }

    [Test]
    public void Chapter2GuestClickPriorityStartsConversationOnFirstClick()
    {
        string actionText = File.ReadAllText(Chapter2GuestFindActionPath);
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        string doorTriggerText = File.ReadAllText(DoorTriggerNavigationPath);

        Assert.That(actionText, Does.Contain("IsPointerOverAvailableGuestAction"), "Guest actions should expose a shared priority helper.");
        Assert.That(actionText, Does.Contain("Physics2D.OverlapPointAll"), "The priority helper should check 2D colliders under the pointer.");
        Assert.That(actionText, Does.Contain("Physics2D.SyncTransforms()"), "Guest pointer checks should sync moved 2D colliders before querying.");
        Assert.That(actionText, Does.Contain("GetComponentInParent<Chapter2GuestFindAction>()"), "Child colliders should resolve to their guest action parent.");
        Assert.That(actionText, Does.Contain("GetComponentInChildren<Chapter2GuestFindAction>(true)"), "Existing actor colliders should be able to resolve to the dedicated child action.");
        Assert.That(actionText, Does.Contain("lastSuccessfulClickFrame"), "Duplicate-frame suppression should only track successful starts.");
        Assert.That(actionText, Does.Contain("UpdateManualPointerHandling"), "Guest actions should have a manual pointer fallback when Unity OnMouse callbacks miss moved 2D colliders.");
        Assert.That(actionText, Does.Contain("ManualPointerClick"), "The manual pointer fallback should start dialogue on the first pointer-down frame.");
        Assert.That(actionText, Does.Contain("SetManualHoveredAction"), "The manual pointer fallback should own talk cursor hover state.");
        Assert.That(actionText, Does.Not.Contain("lastClickFrame"), "Rejected callbacks must not poison duplicate-frame handling.");
        Assert.That(actionText, Does.Match(@"(?s)if\s*\(\s*searchController\.TryStartGuestConversation\(guestId\)\s*\)\s*\{.*lastSuccessfulClickFrame\s*=\s*Time\.frameCount"), "Duplicate-frame state should be set only after the search controller accepts the conversation.");

        Assert.That(movementText, Does.Match(@"(?s)\bTryGetFloorClick\s*\([^)]*\)\s*\{.*IsPointerOverAvailableGuestAction\(screenPosition\).*return false;.*TryEvaluateMovementAtScreenPoint"), "Floor clicks should defer to available hidden guests before movement evaluation.");
        Assert.That(movementText, Does.Match(@"(?s)\bUpdateWalkCursor\s*\([^)]*\)\s*\{.*IsPointerOverAvailableGuestAction\(screenPosition\).*ClearWalkHover\(this\).*return;.*TryEvaluateMovementAtScreenPoint"), "Walk hover should clear instead of overriding the talk cursor over available hidden guests.");

        Assert.That(doorTriggerText, Does.Match(@"(?s)\bOnPointerClick\s*\(\s*PointerEventData\s+eventData\s*\)\s*\{.*IsPointerOverAvailableGuestAction\(eventData\).*return;.*ActivateDoor\(\)"), "Door UI callbacks should defer to available hidden guests before activating.");
        Assert.That(doorTriggerText, Does.Match(@"(?s)\bUpdateFallbackPointerHoverAndClick\s*\([^)]*\)\s*\{.*TryGetPointerPosition\(out Vector2 screenPosition\).*IsPointerOverAvailableGuestAction\(screenPosition\).*ClearActiveDoorHover\(fallbackHoveredTrigger\).*return;.*FindTopmostTriggerAtScreenPoint"), "Door fallback hover/click should defer to available hidden guests before setting door hover or activating a trigger.");
    }

    [Test]
    public void Chapter2GuestSearchCreatesDedicatedRuntimeClickTargets()
    {
        string guestSearchText = File.ReadAllText(Chapter2GuestSearchControllerPath);
        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        string doorTriggerText = File.ReadAllText(DoorTriggerNavigationPath);

        Assert.That(guestSearchText, Does.Contain("ClickTargetName = \"Ch2_ClickTarget\""), "Hidden guests should use a named dedicated child click target.");
        Assert.That(guestSearchText, Does.Match(@"(?s)\bFindClickTargetTransform\s*\([^)]*\).*GetComponentsInChildren<Transform>\(true\).*childTransform\.name == ClickTargetName"), "Guest setup should reuse an existing Ch2_ClickTarget child.");
        Assert.That(guestSearchText, Does.Contain("new GameObject(ClickTargetName)"), "Guest setup should create Ch2_ClickTarget when it is missing.");
        Assert.That(guestSearchText, Does.Contain("targetTransform.SetParent(actorObject.transform, false)"), "The click target should be parented directly under the guest actor root.");
        Assert.That(guestSearchText, Does.Contain("targetTransform.localPosition = Vector3.zero"));
        Assert.That(guestSearchText, Does.Contain("targetTransform.localRotation = Quaternion.identity"));
        Assert.That(guestSearchText, Does.Contain("targetTransform.localScale = Vector3.one"));

        Assert.That(guestSearchText, Does.Contain("targetTransform.GetComponent<BoxCollider2D>()"), "The click target should own the BoxCollider2D.");
        Assert.That(guestSearchText, Does.Contain("targetTransform.gameObject.AddComponent<BoxCollider2D>()"));
        Assert.That(guestSearchText, Does.Contain("clickCollider.isTrigger = true"));
        Assert.That(guestSearchText, Does.Contain("targetTransform.GetComponent<Chapter2GuestFindAction>()"), "The click target should own the guest action.");
        Assert.That(guestSearchText, Does.Contain("targetTransform.gameObject.AddComponent<Chapter2GuestFindAction>()"));
        Assert.That(guestSearchText, Does.Contain("findAction.Initialize(guestId, this)"));

        Assert.That(guestSearchText, Does.Not.Contain("GetComponentInChildren<Collider>(true) != null"), "Existing 3D colliders must not make EnsureRuntimeClickTarget return early.");
        Assert.That(guestSearchText, Does.Not.Contain("GetComponentInChildren<Collider2D>(true) != null"), "Existing 2D colliders must not make EnsureRuntimeClickTarget return early.");
        Assert.That(guestSearchText, Does.Not.Contain("GetComponentInChildren<Graphic>(true) != null"), "Existing graphics must not make EnsureRuntimeClickTarget return early.");

        Assert.That(guestSearchText, Does.Match(@"(?s)\bDisableGuestFindAction\s*\([^)]*\)\s*\{.*GetComponentsInChildren<Chapter2GuestFindAction>\(true\).*SetAvailable\(false\).*enabled = false"), "Disabling a guest should disable child click-target actions too.");
        Assert.That(guestSearchText, Does.Match(@"(?s)\bDisableCompetingGuestFindActions\s*\([^)]*\)\s*\{.*GetComponentsInChildren<Chapter2GuestFindAction>\(true\).*findAction == activeAction.*SetAvailable\(false\).*enabled = false"), "Legacy/root guest actions should be left unavailable when the child target is active.");

        Assert.That(guestSearchText, Does.Contain("TryGetGuestRendererBounds"), "Collider sizing should inspect the guest renderers.");
        Assert.That(guestSearchText, Does.Contain("GetComponentsInChildren<SpriteRenderer>(true)"));
        Assert.That(guestSearchText, Does.Contain("GetComponentsInChildren<Renderer>(true)"));
        Assert.That(guestSearchText, Does.Contain("targetTransform.InverseTransformPoint(rendererBounds.center)"));
        Assert.That(guestSearchText, Does.Contain("GetLocalBoundsSize(targetTransform, rendererBounds)"));
        Assert.That(guestSearchText, Does.Contain("ClickTargetWidthPadding"));
        Assert.That(guestSearchText, Does.Contain("ClickTargetHeightPadding"));
        Assert.That(guestSearchText, Does.Contain("MinimumClickTargetSize"));
        Assert.That(guestSearchText, Does.Contain("FallbackClickTargetOffset"));
        Assert.That(guestSearchText, Does.Contain("FallbackClickTargetSize"));
        Assert.That(guestSearchText, Does.Contain("LogFallbackClickBoundsOnce"));
        Assert.That(guestSearchText, Does.Contain("clickCollider.offset = nextOffset"));
        Assert.That(guestSearchText, Does.Contain("clickCollider.size = nextSize"));

        Assert.That(guestSearchText, Does.Match(@"(?s)EnsureGuestUsesPersistentActorRoot\(guest\).*PlaceAt\(guest\.hideAnchor\.transform\).*SetCurrentRoom\(guest\.hideAnchor\.RoomId\).*SetAvailableInCurrentChapter\(true\).*SetVisibleByChapterState\(true\).*SetInteractable\(true\).*SetSeated\(false\).*EnsureGuestFindAction\(guest\).*ApplyState\(\).*Physics2D\.SyncTransforms\(\)"), "BeginSearch should create the click target before the final ApplyState/Physics2D sync.");
        Assert.That(movementText, Does.Contain("Chapter2GuestFindAction.IsPointerOverAvailableGuestAction(screenPosition)"), "Movement should keep deferring to hidden guest click targets.");
        Assert.That(doorTriggerText, Does.Contain("Chapter2GuestFindAction.IsPointerOverAvailableGuestAction(screenPosition)"), "Door fallback navigation should keep deferring to hidden guest click targets.");
        Assert.That(doorTriggerText, Does.Contain("Chapter2GuestFindAction.IsPointerOverAvailableGuestAction(eventData.position)"), "Door UI navigation should keep deferring to hidden guest click targets.");
    }

    [Test]
    public void Chapter2GuestSearchRecordsFoundOrderAndPreferences()
    {
        string guestSearchText = File.ReadAllText(Chapter2GuestSearchControllerPath);

        Assert.That(guestSearchText, Does.Contain("foundGuestIdsInOrder"));
        Assert.That(guestSearchText, Does.Contain("foundOrderCounter++"));
        Assert.That(guestSearchText, Does.Contain("guest.foundOrder = foundOrderCounter"));
        Assert.That(guestSearchText, Does.Contain("mealPreference"));
        Assert.That(guestSearchText, Does.Contain("fresh monte genellion de plink"));
        Assert.That(guestSearchText, Does.Contain("thyme with Lillums"));
        Assert.That(guestSearchText, Does.Contain("smokingPreference"));
        Assert.That(guestSearchText, Does.Contain("pipe"));
        Assert.That(guestSearchText, Does.Contain("cigar"));
        Assert.That(guestSearchText, Does.Contain("none, thank you"));
        Assert.That(guestSearchText, Does.Contain("spiritBottle"));
        Assert.That(guestSearchText, Does.Contain("bottle of spirits"));
        Assert.That(guestSearchText, Does.Match(@"\bTryStartGuestConversation\s*\("));
        Assert.That(guestSearchText, Does.Contain("Ask meal preference"));
        Assert.That(guestSearchText, Does.Match(@"\bShowMealPreferenceQuestion\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bChooseMealPreference\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bChooseSmokingPreference\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bFinishGuestConversation\s*\("));
        Assert.That(guestSearchText, Does.Contain("MarkGuestFound(GetGuestIdForOrderList(guest))"));
        Assert.That(guestSearchText, Does.Contain("HandleGuestSearchProgressChanged()"));
        Assert.That(guestSearchText, Does.Contain("HandleAllGuestsFound()"));
    }

    [Test]
    public void Chapter2HidesGuestsAtSevenThenSeatsGuestsOnDiningRoomReveal()
    {
        string controllerText = File.ReadAllText(Chapter2ControllerPath);
        string guestSearchText = File.ReadAllText(Chapter2GuestSearchControllerPath);

        Assert.That(guestSearchText, Does.Match(@"\bPrepareGuestsForDiningTransfer\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bSeatGuestsInDiningRoom\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bGetGuestsInDiningSeatOrder\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bFindDiningSeatAnchors\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bHideGuestForDiningRoomTransfer\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bRunGuestExitToDiningRoomRoutine\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bStageGuestForDiningRoomReveal\s*\("));
        Assert.That(guestSearchText, Does.Match(@"(?s)\bStageGuestForDiningRoomReveal\s*\([^)]*\)\s*\{.*SetCurrentRoom\(targetRoom\).*SetVisibleByChapterState\(false\).*ApplyState\(\)"), "Spoken-to guests should leave the search room and wait hidden for the Dining Room reveal.");
        Assert.That(guestSearchText, Does.Match(@"(?s)\bHideGuestForDiningRoomTransfer\s*\([^)]*\)\s*\{.*SetVisibleByChapterState\(false\).*ApplyState\(\)"), "Guests should be hidden from their search rooms before being moved to Dining Room seats.");
        Assert.That(guestSearchText, Does.Match(@"(?s)\bSeatGuestsInDiningRoom\s*\(\s*List<GuestSearchEntry>\s+guestsToSeat\s*\)\s*\{.*HideGuestForDiningRoomTransfer\(guest\).*PlaceAt\(diningSeat\.transform\).*SetCurrentRoom\(diningSeat\.RoomId\).*SetVisibleByChapterState\(true\).*ApplyState\(\)"), "Dining seating should hide, move, assign Dining Room, then restore visibility through ActorRoomState.");
        Assert.That(guestSearchText, Does.Contain("SetCurrentRoom(diningSeat.RoomId)"));
        Assert.That(guestSearchText, Does.Contain("SetSeated(false)"));
        Assert.That(controllerText, Does.Contain("BeginDiningRoomObjective()"));
        Assert.That(controllerText, Does.Match(@"\bRunDiningObjectiveTransitionRoutine\s*\("));
        Assert.That(controllerText, Does.Contain("SetDinnerClockAndStop()"));
        Assert.That(controllerText, Does.Contain("PrepareGuestsForDiningTransfer()"));
        Assert.That(controllerText, Does.Contain("ShowClockStrike"));
        Assert.That(controllerText, Does.Contain("PlayClockStrikeDing"));
        Assert.That(controllerText, Does.Contain("RuntimeChapter2ClockStrikeDing"));
        Assert.That(controllerText, Does.Contain("ClearClockStrike"));
        Assert.That(controllerText, Does.Contain("SeatGuestsInDiningRoom()"));
        Assert.That(controllerText, Does.Contain("guestSearch.SeatGuestsInDiningRoom()"));
        Assert.That(controllerText, Does.Contain("diningRoomRevealSeconds = 5f"));
        Assert.That(controllerText, Does.Match(@"(?s)\bHandleAllGuestsFound\s*\([^)]*\)\s*\{.*BeginDiningRoomObjective\s*\(\s*\)"), "Finding all guests should be the only path that makes the clock strike 7.");
        Assert.That(controllerText, Does.Contain("currentPhase == Chapter2Phase.DiningRoomObjective && IsCurrentRoom(diningRoomId)"));
        Assert.That(controllerText, Does.Contain("chapterClock.SetStartTime(dinnerHour, dinnerMinute)"));
        Assert.That(controllerText, Does.Not.Contain("currentPhase == Chapter2Phase.GuestSearch && HasReachedDinnerTime()"));
        Assert.That(controllerText, Does.Not.Contain("HasReachedDinnerTime()"));
        Assert.That(controllerText, Does.Not.Contain("chapterClock.HasReachedTime(dinnerHour, dinnerMinute)"));
        Assert.That(controllerText, Does.Not.Contain("StartChapter2Clock()"));
        Assert.That(controllerText, Does.Not.Contain("chapterClock.StartClock()"));
        Assert.That(controllerText, Does.Contain("IsCurrentRoom(diningRoomId)"));
        Assert.That(controllerText, Does.Match(@"(?s)\bRunDiningRoomCompletionRoutine\s*\([^)]*\)\s*\{.*SetPhase\s*\(\s*Chapter2Phase\.DiningRoomReveal\s*\).*SeatGuestsInDiningRoom\s*\(\).*WaitForSeconds\s*\(\s*GetDiningRoomRevealSeconds\s*\(\s*\)\s*\).*CompleteChapterAndTriggerNextChapter\(""chapter_03_dinner_pending""\)"), "Guests should be seated on Dining Room reveal, then fade after a short realtime hold.");
        Assert.That(controllerText, Does.Contain("CompleteChapterAndTriggerNextChapter(\"chapter_03_dinner_pending\")"));
        Assert.That(controllerText, Does.Not.Contain("diningRoomFadeDelayGameMinutes"));
        Assert.That(controllerText, Does.Not.Contain("TrySeatDinnerGuestsAtDinnerTime"));
        Assert.That(controllerText, Does.Not.Contain("HasReachedDiningRoomFadeTime"));
    }

    [Test]
    public void Chapter2HideAnchorsAreAuthoredAcrossHouse()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        AssertHideAnchor(sceneText, "Ch2_Hide_Guest01", "Room_Library", "Library");
        AssertHideAnchor(sceneText, "Ch2_Hide_Guest02", "Room_Music_Room", "Music Room");
        AssertHideAnchor(sceneText, "Ch2_Hide_Guest03", "Room_Billiard_Room", "Billiard Room");
        AssertHideAnchor(sceneText, "Ch2_Hide_Guest04", "Room_Conservatory", "Conservatory");
        AssertHideAnchor(sceneText, "Ch2_Hide_Guest05", "Room_Kitchen", "Kitchen");
        AssertHideAnchor(sceneText, "Ch2_Hide_Guest06", "Room_Chapel", "Chapel");
        AssertHideAnchor(sceneText, "Ch2_Hide_Guest07", "Room_Upper_Gallery", "Upper Gallery");
        AssertHideAnchor(sceneText, "Ch2_Hide_Guest08", "Room_Blue_Bedroom", "Blue Bedroom");
    }

    [Test]
    public void Chapter2InteractionHudExists()
    {
        Assert.That(File.Exists(Chapter2InteractionHUDPath), Is.True, "Chapter 2 should have a dedicated runtime HUD.");

        string hudText = File.ReadAllText(Chapter2InteractionHUDPath);
        Assert.That(hudText, Does.Contain("DisallowMultipleComponent"));
        Assert.That(hudText, Does.Contain("Canvas_Chapter2HUD"));
        Assert.That(hudText, Does.Match(@"\bInitialize\s*\("));
        Assert.That(hudText, Does.Match(@"\bSetObjective\s*\("));
        Assert.That(hudText, Does.Match(@"\bSetPrimaryAction\s*\("));
        Assert.That(hudText, Does.Match(@"\bClearPrimaryAction\s*\("));
        Assert.That(hudText, Does.Match(@"\bSetDialogue\s*\("));
        Assert.That(hudText, Does.Match(@"\bSetDialogueChoices\s*\("));
        Assert.That(hudText, Does.Match(@"\bClearDialogue\s*\("));
        Assert.That(hudText, Does.Match(@"\bShowClockStrike\s*\("));
        Assert.That(hudText, Does.Match(@"\bClearClockStrike\s*\("));
        Assert.That(hudText, Does.Match(@"\bSetFoundGuests\s*\("));
        Assert.That(hudText, Does.Contain("Text_Chapter2FoundList"));
        Assert.That(hudText, Does.Contain("Panel_Chapter2Dialogue"));
        Assert.That(hudText, Does.Contain("Panel_Chapter2ClockStrike"));
        Assert.That(hudText, Does.Contain("IReadOnlyList<string>"));
        Assert.That(hudText, Does.Match(@"(?s)\bSetDialogueChoices\s*\([^)]*\)\s*\{.*EnsureUI\s*\(\s*\).*dialoguePanel\.SetActive\(true\).*SetDialogueChoice\(0"), "The first visible guest dialogue should not be hidden by EnsureUI before choices are installed.");
    }

    [Test]
    public void ChapterManagerHandsOffToChapter2Controller()
    {
        string managerText = File.ReadAllText(ChapterManagerPath);

        Assert.That(managerText, Does.Contain("Chapter2Controller"), "ChapterManager should reference the Chapter 2 controller.");
        Assert.That(managerText, Does.Contain("Chapter2Id"), "ChapterManager should expose the canonical Chapter 2 id.");
        Assert.That(managerText, Does.Contain("chapter_02_guest_search"), "ChapterManager should normalize Chapter 2 requests to the guest-search chapter id.");
        Assert.That(managerText, Does.Match(@"\.BeginChapter2\s*\(\s*this\s*\)"), "ChapterManager should begin Chapter 2 after the Chapter 1 fade-to-black.");
    }

    [Test]
    public void Chapter1CannotRetriggerChapter2AfterHandoff()
    {
        string chapter1Text = File.ReadAllText(Chapter1ArrivalControllerPath);
        string managerText = File.ReadAllText(ChapterManagerPath);

        Assert.That(chapter1Text, Does.Contain("chapterCompletionRequested"), "Chapter 1 should remember that its Chapter 2 handoff already fired.");
        Assert.That(chapter1Text, Does.Match(@"(?s)\bCheckChapterCompletionGate\s*\([^)]*\)\s*\{.*!sequenceActive \|\| chapterCompletionRequested"), "Chapter 1 completion gate should not run after Chapter 1 has ended.");
        Assert.That(chapter1Text, Does.Match(@"(?s)chapterCompletionRequested = true;.*sequenceActive = false;.*UnsubscribeFromRoomChanges\(\);.*CompleteChapterAndTriggerNextChapter\(""chapter_02_pending""\)"), "Chapter 1 should unsubscribe before requesting Chapter 2.");
        Assert.That(chapter1Text, Does.Match(@"(?s)\bHandleRoomChanged\s*\([^)]*\)\s*\{.*!sequenceActive \|\| chapterCompletionRequested"), "Re-entering Drawing Room after Chapter 1 should not call the completion gate.");

        Assert.That(managerText, Does.Contain("IsDuplicateChapter2Request"), "ChapterManager should reject duplicate Chapter 2 handoff requests before fading.");
        Assert.That(managerText, Does.Contain("Chapter 2 request ignored because Chapter 2 is already active."));
    }

    [Test]
    public void Chapter2DoesNotUseHeavySystems()
    {
        if (!Directory.Exists(Chapter2DirectoryPath))
        {
            return;
        }

        string[] chapter2Files = Directory.GetFiles(Chapter2DirectoryPath, "*.cs", SearchOption.AllDirectories);
        string[] forbiddenTerms =
        {
            "NavMeshAgent",
            "UnityEngine.AI",
            "ChaseTarget",
            "BehaviorTree",
            "QuestSystem",
            "DialogueEditor",
            "InventorySystem"
        };

        for (int i = 0; i < chapter2Files.Length; i++)
        {
            string fileText = File.ReadAllText(chapter2Files[i]);

            for (int termIndex = 0; termIndex < forbiddenTerms.Length; termIndex++)
            {
                Assert.That(
                    fileText,
                    Does.Not.Contain(forbiddenTerms[termIndex]),
                    $"{chapter2Files[i]} should not use {forbiddenTerms[termIndex]}.");
            }
        }
    }

    [Test]
    public void Chapter2AnchorNamingConventionIsDocumented()
    {
        string scriptText = File.ReadAllText(Chapter2ScriptPath);

        Assert.That(scriptText, Does.Match(Regex.Escape("Ch2_ButlerSpeechSpot")));
        Assert.That(scriptText, Does.Match(Regex.Escape("Ch2_MonsterRunStart")));
        Assert.That(scriptText, Does.Match(Regex.Escape("Ch2_MonsterFreezeTarget")));
        Assert.That(scriptText, Does.Match(Regex.Escape("Ch2_Hide_")));
        Assert.That(scriptText, Does.Match(Regex.Escape("Ch2_DiningSeat_")));
    }

    private static void AssertHideAnchor(string sceneText, string anchorName, string roomObjectName, string roomId)
    {
        Match roomTransformMatch = Regex.Match(
            sceneText,
            $@"(?s)m_Name: {Regex.Escape(roomObjectName)}.*?--- !u!224 &(?<transformId>\d+)\s+RectTransform:");

        Assert.That(roomTransformMatch.Success, Is.True, $"Gameplay scene should contain a {roomObjectName} RectTransform.");

        string transformId = roomTransformMatch.Groups["transformId"].Value;
        string anchorPattern =
            $@"(?s)m_Name: {Regex.Escape(anchorName)}.*?" +
            $@"m_Father: \{{fileID: {Regex.Escape(transformId)}\}}.*?" +
            $@"anchorId: {Regex.Escape(anchorName)}\s+roomId: {Regex.Escape(roomId)}";

        Assert.That(sceneText, Does.Match(anchorPattern), $"{anchorName} should be parented under {roomObjectName} and marked as {roomId}.");
        Assert.That(sceneText, Does.Not.Match($@"anchorId: {Regex.Escape(anchorName)}\s+roomId: (Ballroom|Drawing Room|Dining Room)"));
    }
}
