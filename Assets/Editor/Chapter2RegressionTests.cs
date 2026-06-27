using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class Chapter2RegressionTests
{
    private const string ChapterManagerPath = "Assets/Scripts/Story/ChapterManager.cs";
    private const string ChapterIntroUIPath = "Assets/Scripts/Story/ChapterIntroUI.cs";
    private const string GameplayRuntimeStatePath = "Assets/Scripts/Story/GameplayRuntimeState.cs";
    private const string GameAudioSettingsPath = "Assets/Scripts/Audio/GameAudioSettings.cs";
    private const string MainMenuControllerPath = "Assets/Scripts/MainMenuController.cs";
    private const string RuntimeSettingsMenuPath = "Assets/Scripts/UI/RuntimeSettingsMenu.cs";
    private const string CameraManagerPath = "Assets/Map/CameraManager.cs";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string DoorbellSystemPath = "Assets/Scripts/Story/DoorbellSystem.cs";
    private const string DoorbellClipResourcePath = "Audio/SFX/old_fashioned_door_bell_youtube_IqFKjVlaOik_48khz";
    private const string DoorbellClipAssetPath = "Assets/Resources/Audio/SFX/old_fashioned_door_bell_youtube_IqFKjVlaOik_48khz.wav";
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string Chapter2DirectoryPath = "Assets/_Chateau/Scripts/Chapter/Chapter02";
    private const string Chapter2ControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs";
    private const string Chapter2InteractionHUDPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2InteractionHUD.cs";
    private const string Chapter2MonsterStingerControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2MonsterStingerController.cs";
    private const string Chapter2GuestSearchControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs";
    private const string Chapter2GuestPanicControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs";
    private const string Chapter2PanicAnimationLibraryPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2PanicAnimationLibrary.cs";
    private const string Chapter2GuestFindActionPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestFindAction.cs";
    private const string Chapter2ScriptPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Script.md";
    private const string Chapter2PanicLibraryAssetPath = "Assets/Resources/Chapter2/PanicAnimationLibrary.asset";
    private const string Chapter2PanicScreamCatalogPath = "Assets/Resources/Audio/Chapter2PanicScreamCatalog.asset";
    private const string GuestFootstepAudioPath = "Assets/Scripts/Audio/GuestFootstepAudio.cs";
    private const string GuestFootstepCatalogScriptPath = "Assets/Scripts/Audio/GuestFootstepCatalog.cs";
    private const string GuestFootstepCatalogPath = "Assets/Resources/Audio/GuestFootstepCatalog.asset";
    private const string SubtitleLinePath = "Assets/Scripts/UI/SubtitleLine.cs";
    private const string SubtitleLineBankScriptPath = "Assets/Scripts/UI/SubtitleLineBank.cs";
    private const string SubtitleServicePath = "Assets/Scripts/UI/SubtitleService.cs";
    private const string DialogueSpeechServicePath = "Assets/Scripts/Audio/DialogueSpeechService.cs";
    private const string SpeakingCharacterIndicatorPath = "Assets/Scripts/UI/SpeakingCharacterIndicator.cs";
    private const string SpeakingCharacterIndicatorSpritePath = "Assets/Resources/UI/chat_bubble.png";
    private const string SubtitleLineBankPath = "Assets/Resources/UI/SubtitleLineBank.asset";
    private const string GuestVoiceLinePlaybackPath = "Assets/Scripts/Audio/GuestVoiceLinePlayback.cs";
    private const string GuestVoiceLineCatalogPath = "Assets/Resources/Audio/GuestVoiceLineCatalog.asset";
    private const string ButlerVoiceFolderPath = "Assets/Audio/Voice/Butler";
    private const string Chapter2PanicLibraryBuilderPath = "Assets/Editor/Chapter2PanicAnimationLibraryBuilder.cs";
    private const string Chapter2MonsterArmSwingResourcePath = "Assets/Resources/Chapter2/Monster/ArmSwing";
    private const string Chapter2MonsterArmSwingClipPath = "Assets/Animation/Monster/Ch2_Monster_ArmSwing.anim";
    private const string AnimationLibraryPath = "Assets/Art/Library/AnimationLibrary";
    private const string GuestArtRoot = "Assets/Art/Characters";
    private const string Chapter2PanicClipRoot = "Assets/Animation/Chapter2Panic";
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string DoorTriggerNavigationPath = "Assets/Scripts/Navigation/DoorTriggerNavigation.cs";
    private static readonly string[] PanicRosterCharacters =
    {
        "Lady",
        "ButlerGuest",
        "MisterFlorianKnell",
        "CountessElowenDusk",
        "BaronHectorGlass",
        "LadySabineMarrow",
        "LordAmbroseVeil",
        "MadameCoralieThread"
    };
    private static readonly string[] PanicRosterGuestFolders =
    {
        "guest1",
        "guest2",
        "guest3",
        "guest4",
        "guest5",
        "guest6",
        "guest7",
        "guest8"
    };
    private static readonly string[] PanicRosterClipFolders =
    {
        "Guest01_Lady",
        "Guest02_ButlerGuest",
        "Guest03_MisterFlorianKnell",
        "Guest04_CountessElowenDusk",
        "Guest05_BaronHectorGlass",
        "Guest06_LadySabineMarrow",
        "Guest07_LordAmbroseVeil",
        "Guest08_MadameCoralieThread"
    };
    private static readonly PanicActionSpec[] RequiredPanicActions =
    {
        new PanicActionSpec("panic_hands_up", "panicHandsUp", 4),
        new PanicActionSpec("panic_pop", "panicPop", 8),
        new PanicActionSpec("panic_run_down", "panicRunDown", 4),
        new PanicActionSpec("panic_run_left", "panicRunLeft", 4),
        new PanicActionSpec("panic_run_right", "panicRunRight", 4),
        new PanicActionSpec("panic_run_up", "panicRunUp", 4)
    };
    private const string OptionalPanicPopAction = "panic_pop";
    private static readonly string[] RemovedPanicActions =
    {
        "panic_reaction_down",
        "panic_shriek_down",
        "panic_turnaround",
        "cover_face_cower"
    };

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
        Assert.That(controllerText, Does.Match(@"(?s)\bDebugResetForChapter2Skip\s*\([^)]*\)\s*\{.*debugTeleportToDrawingRoomOnStart\s*=\s*true"), "Debug Chapter 2 skips should force the Drawing Room active without relying on normal door routing.");
        Assert.That(controllerText, Does.Match(@"\bHandleAddressGuestsPrompt\s*\("), "Chapter2Controller should expose the address prompt callback.");
        Assert.That(controllerText, Does.Contain("Chapter2InteractionHUD"), "Chapter2Controller should use the Chapter 2 interaction HUD.");
        Assert.That(controllerText, Does.Contain("Chapter2MonsterStingerController"), "Chapter2Controller should use the Chapter 2 monster stinger controller.");
        Assert.That(controllerText, Does.Contain("Chapter2GuestSearchController"), "Chapter2Controller should use the Chapter 2 guest search controller.");
        Assert.That(controllerText, Does.Contain("Chapter2GuestPanicController"), "Chapter2Controller should use the Chapter 2 guest panic controller during the monster stinger.");
        Assert.That(controllerText, Does.Match(@"\bRunOpeningSpeechRoutine\s*\("), "Chapter2Controller should run the Butler opening speech from a coroutine.");
        Assert.That(controllerText, Does.Match(@"\bHandleAllGuestsFound\s*\("), "Chapter2Controller should handle the all-guests-found transition.");
        Assert.That(controllerText, Does.Match(@"(?s)\bMoveToDrawingRoom\s*\([^)]*\)\s*\{.*debugTeleportToDrawingRoomOnStart.*DebugTeleportToRoom\(drawingRoomId\).*MoveToRoom\(drawingRoomId\)"), "Chapter 2 debug skip should teleport to the Drawing Room first, then fall back to normal room movement.");

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
        Assert.That(stingerText, Does.Contain("using UnityEngine.UI;"));
        Assert.That(stingerText, Does.Contain("monsterRunSpritesResourcePath = \"Chapter2/Monster/ArmSwing\""));
        Assert.That(stingerText, Does.Contain("MonsterRunStutterFrameOrder = { 0, 3, 1, 5, 2, 6, 4, 7 }"));
        Assert.That(stingerText, Does.Contain("monsterRunAnimationFramesPerSecond = 6f"));
        Assert.That(stingerText, Does.Contain("useMonsterRunStutterFrameOrder = true"));
        Assert.That(stingerText, Does.Contain("monsterRunShakePixels = 1.75f"));
        Assert.That(stingerText, Does.Contain("monsterRunVerticalShakeScale = 0.2f"));
        Assert.That(stingerText, Does.Contain("holdDifferentMonsterPoseOnFreeze = true"));
        Assert.That(stingerText, Does.Contain("monsterFreezePoseStep = 3"));
        Assert.That(stingerText, Does.Contain("twitchMonsterPoseWhileFrozen = true"));
        Assert.That(stingerText, Does.Contain("monsterFreezeTwitchFramesPerSecond = 2f"));
        Assert.That(stingerText, Does.Contain("Resources.LoadAll<Sprite>(monsterRunSpritesResourcePath)"));
        Assert.That(stingerText, Does.Contain("System.Array.Sort(loadedSprites, CompareSpritesByName)"));
        Assert.That(stingerText, Does.Contain("monsterImage.sprite = sprite"));
        Assert.That(stingerText, Does.Contain("UpdateMonsterRunAnimation(monsterRunAnimationElapsedSeconds)"));
        Assert.That(stingerText, Does.Contain("GetMonsterRunFrameIndex(cycleFrameIndex)"));
        Assert.That(stingerText, Does.Contain("ApplyNextMonsterFreezePose()"));
        Assert.That(stingerText, Does.Contain("UpdateMonsterFreezeAnimation(monsterRunAnimationElapsedSeconds)"));
        Assert.That(stingerText, Does.Contain("PlayViolinAudioIfVisible();"));
        Assert.That(stingerText, Does.Contain("GetMonsterRunShakeOffset(monsterRunAnimationElapsedSeconds)"));
        Assert.That(stingerText, Does.Contain("monsterObject.transform.position = basePosition + GetMonsterRunShakeOffset(monsterRunAnimationElapsedSeconds)"));
        Assert.That(stingerText, Does.Contain("monsterObject.transform.position = targetPosition"));
        Assert.That(stingerText, Does.Match(@"(?s)yield return MoveMonsterToNextFreezeTarget\(cycleTimings\[i\]\.RunSeconds\);\s*ApplyMonsterRoomVisibility\(\);\s*if"), "The violin should not stop between monster run and freeze beats.");
        Assert.That(Directory.Exists(Chapter2MonsterArmSwingResourcePath), Is.True, "Monster run sprites should be available from Resources for the runtime-created stinger component.");
        Assert.That(Directory.GetFiles(Chapter2MonsterArmSwingResourcePath, "*.png").Length, Is.GreaterThanOrEqualTo(8), "Monster arm swing animation should have at least the approved 8-frame sprite cycle.");
        Assert.That(File.Exists(Chapter2MonsterArmSwingClipPath), Is.True, "Monster arm swing animation clip should be kept with the generated frame library.");
    }

    [Test]
    public void Chapter2ControllerRunsMonsterStingerBeforeGuestSearch()
    {
        string controllerText = File.ReadAllText(Chapter2ControllerPath);

        Assert.That(controllerText, Does.Match(@"\bRunMonsterStingerRoutine\s*\("));
        Assert.That(controllerText, Does.Contain("monsterStingerTimeoutSeconds = 14f"), "Monster stinger should have a watchdog timeout so Chapter 2 cannot stall before GuestSearch.");
        Assert.That(controllerText, Does.Match(@"(?s)\bRunMonsterStingerRoutine\s*\([^)]*\)\s*\{.*BeginStinger\s*\(.*monsterStinger\.IsRunning.*Time\.unscaledDeltaTime.*StopStinger\s*\(.*RunExitToDoorsThenRestoreRoutine\s*\(\).*StartGuestSearch\s*\(\);\s*SetPhase\s*\(\s*Chapter2Phase\.GuestSearch\s*\)"), "Monster stinger should run before GuestSearch, time out if it gets stuck, then let guests flee to the doors before search placement begins.");
        Assert.That(controllerText, Does.Contain("Find the guests. Tell them dinner will be served at 7:00 PM sharp."));
    }

    [Test]
    public void Chapter2RunsGuestPanicThroughDoorExitBeforeGuestSearch()
    {
        string controllerText = File.ReadAllText(Chapter2ControllerPath);
        int routineIndex = controllerText.IndexOf("RunMonsterStingerRoutine", System.StringComparison.Ordinal);
        int beginPanicIndex = controllerText.IndexOf("guestPanic.BeginPanic()", routineIndex, System.StringComparison.Ordinal);
        int beginStingerIndex = controllerText.IndexOf("monsterStinger.BeginStinger()", routineIndex, System.StringComparison.Ordinal);
        int doorExitIndex = controllerText.IndexOf("guestPanic.RunExitToDoorsThenRestoreRoutine()", routineIndex, System.StringComparison.Ordinal);
        int startSearchIndex = controllerText.IndexOf("StartGuestSearch()", routineIndex, System.StringComparison.Ordinal);

        Assert.That(File.Exists(Chapter2GuestPanicControllerPath), Is.True, "Chapter 2 should have a dedicated panic playback controller.");
        Assert.That(routineIndex, Is.GreaterThanOrEqualTo(0), "Chapter 2 should have a monster stinger routine.");
        Assert.That(beginPanicIndex, Is.GreaterThan(routineIndex), "Guest panic should start inside the monster stinger routine.");
        Assert.That(beginStingerIndex, Is.GreaterThan(routineIndex), "Monster stinger should start inside the monster stinger routine.");
        Assert.That(beginPanicIndex, Is.GreaterThan(beginStingerIndex), "Guest panic should begin immediately after the monster stinger cut-in starts.");
        Assert.That(doorExitIndex, Is.GreaterThan(beginPanicIndex), "Guest panic should keep running while the stinger wait/timeout runs.");
        Assert.That(doorExitIndex, Is.LessThan(startSearchIndex), "Guests should flee to the Drawing Room doors before guest search placement begins.");
        Assert.That(controllerText, Does.Match(@"(?s)\bStopChapter2Coroutines\s*\([^)]*\)\s*\{.*guestPanic\.StopPanic\(\)"), "Coroutine cleanup should not leave panic running.");
        Assert.That(controllerText, Does.Match(@"(?s)\bDebugResetForChapter2Skip\s*\([^)]*\)\s*\{.*guestPanic\.StopPanic\(\)"), "Debug reset should not leave panic running.");
    }

    [Test]
    public void Chapter2GuestPanicUsesApprovedResourcesOnly()
    {
        Assert.That(File.Exists(Chapter2GuestPanicControllerPath), Is.True);
        Assert.That(File.Exists(Chapter2PanicAnimationLibraryPath), Is.True);
        Assert.That(File.Exists(Chapter2PanicLibraryBuilderPath), Is.True);

        string panicText = File.ReadAllText(Chapter2GuestPanicControllerPath);
        string libraryText = File.ReadAllText(Chapter2PanicAnimationLibraryPath);
        string builderText = File.ReadAllText(Chapter2PanicLibraryBuilderPath);
        string libraryAssetText = File.ReadAllText(Chapter2PanicLibraryAssetPath);

        Assert.That(panicText, Does.Contain("Resources.Load<Chapter2PanicAnimationLibrary>(Chapter2PanicAnimationLibrary.ResourcesPath)"));
        Assert.That(panicText, Does.Contain("GetGuestActorsInIdentityOrder()"));
        Assert.That(panicText, Does.Contain("HasRequiredFrames"));
        Assert.That(panicText, Does.Contain("Debug.LogError"));
        Assert.That(panicText, Does.Not.Contain("AssetDatabase"), "Runtime panic playback must not read editor-only project paths.");
        Assert.That(panicText, Does.Not.Contain("Sprite.Create"), "Runtime panic playback must not synthesize fallback sprites.");
        Assert.That(panicText, Does.Not.Contain("new Texture2D"), "Runtime panic playback must not synthesize fallback textures.");
        Assert.That(panicText, Does.Not.Contain("LineRenderer"), "Runtime panic playback must not draw placeholder limbs.");
        Assert.That(panicText, Does.Not.Contain("CreatePrimitive"));

        Assert.That(libraryText, Does.Contain("ResourcesPath = \"Chapter2/PanicAnimationLibrary\""));
        Assert.That(libraryText, Does.Contain("TryGetCharacterIdForGuestNumber"));

        for (int i = 0; i < PanicRosterCharacters.Length; i++)
        {
            Assert.That(libraryText, Does.Contain("\"" + PanicRosterCharacters[i] + "\""), $"Panic roster should include {PanicRosterCharacters[i]}.");
        }

        Assert.That(builderText, Does.Contain("Assets/Art/Library/AnimationLibrary"));
        Assert.That(builderText, Does.Contain("Assets/Resources/Chapter2"));
        Assert.That(builderText, Does.Contain("Assets/Art/Characters"));
        Assert.That(builderText, Does.Contain("Assets/Animation/Chapter2Panic"));
        Assert.That(builderText, Does.Contain("Assets/Art/Characters/guest7/guest7left"));
        Assert.That(builderText, Does.Contain("Assets/Art/Characters/guest7/guest7right"));
        Assert.That(builderText, Does.Contain("LoadSpritesCycled"));
        Assert.That(builderText, Does.Contain("PanicHandsUpActionId"));
        Assert.That(builderText, Does.Contain("PanicPopActionId"));
        Assert.That(builderText, Does.Contain("GetLegacyGuestPanicFramesFolder"));
        Assert.That(builderText, Does.Contain("CreateSpriteClip"));
        Assert.That(builderText, Does.Not.Contain("SyncSpritesToGuestPanicFolder"));
        Assert.That(builderText, Does.Contain("throw new InvalidOperationException"), "The editor builder should fail when approved frames are incomplete.");

        Assert.That(libraryAssetText, Does.Contain(ReadGuid(Path.Combine(GuestArtRoot, "guest7", "panic", "panic_hands_up", "frames", "01_guest7_panic_hands_up.png.meta"))), "Guest 7 panic stop should use the generated guest-local hands-up sprites.");
        Assert.That(libraryAssetText, Does.Contain(ReadGuid(Path.Combine(GuestArtRoot, "guest1", "panic", OptionalPanicPopAction, "frames", "01_guest1_panic_pop_cover_face.png.meta"))), "Guest 1 panic pop should use the generated Guest 1-style sprites.");
        Assert.That(libraryAssetText, Does.Contain(ReadGuid(Path.Combine(GuestArtRoot, "guest2", "guest2panic", "guest2_panic_01.png.meta"))), "Guest 2 panic pop should use the existing eight-frame guest panic stills.");
        Assert.That(libraryAssetText, Does.Contain(ReadGuid(Path.Combine(GuestArtRoot, "guest3", "panic", OptionalPanicPopAction, "frames", "guest3_panic_01.png.meta"))), "Guest 3 panic pop should use the selected Guest 3 panic sprites.");
        Assert.That(libraryAssetText, Does.Not.Contain("panicReactionDown:"), "The rejected multi-action panic stop set should not be serialized.");
        Assert.That(libraryAssetText, Does.Not.Contain("panicShriekDown:"), "The rejected multi-action panic stop set should not be serialized.");
        Assert.That(libraryAssetText, Does.Not.Contain("panicTurnaround:"), "The rejected multi-action panic stop set should not be serialized.");
        Assert.That(libraryAssetText, Does.Not.Contain("coverFaceCower:"), "The rejected multi-action panic stop set should not be serialized.");
    }

    [Test]
    public void Chapter2PanicScreamCatalogCoversEveryGuest()
    {
        Assert.That(File.Exists(Chapter2PanicScreamCatalogPath), Is.True, "The runtime panic scream catalog should be available through Resources.");

        string catalogText = File.ReadAllText(Chapter2PanicScreamCatalogPath);
        string[] clipPaths =
        {
            "Assets/Audio/woman panic and scream tracks/0224_female_target_hoarse_desperate_scream_candidate_seed939827_48khz.wav",
            "Assets/Audio/men panic screams/0241_male_target_strangled_quiet_panic_candidate_seed942295_48khz.wav",
            "Assets/Audio/men panic screams/0161_male_target_deep_guttural_panic_scream_candidate_seed932133_48khz.wav",
            "Assets/Audio/woman panic and scream tracks/0166_female_target_panic_scream_high_shriek_candidate_seed932805_48khz.wav",
            "Assets/Audio/men panic screams/0237_male_short_pain_fear_grunt_candidate_seed941822_48khz.wav",
            "Assets/Audio/woman panic and scream tracks/0155_female_target_restrained_low_volume_fear_candidate_seed931314_48khz.wav",
            "Assets/Audio/men panic screams/0151_male_short_pain_fear_grunt_candidate_seed930942_48khz.wav",
            "Assets/Audio/woman panic and scream tracks/0077_female_target_restrained_low_volume_fear_candidate_seed921983_48khz.wav"
        };

        for (int i = 0; i < clipPaths.Length; i++)
        {
            string clipPath = clipPaths[i];
            string clipGuid = ReadGuid(clipPath + ".meta");
            int guestNumber = i + 1;

            Assert.That(File.Exists(clipPath), Is.True, $"Guest {guestNumber} panic scream clip should exist at {clipPath}.");
            Assert.That(
                catalogText,
                Does.Match($@"(?s)- guestNumber: {guestNumber}\s+clip: \{{fileID: 8300000, guid: {clipGuid}, type: 3\}}\s+volume: "),
                $"Guest {guestNumber} should be assigned to {Path.GetFileName(clipPath)}.");
        }
    }

    [Test]
    public void GuestFootstepCatalogCoversEveryGuestAndUsesHighPassFiltering()
    {
        Assert.That(File.Exists(GuestFootstepAudioPath), Is.True, "Guest footsteps need a reusable audio source component.");
        Assert.That(File.Exists(GuestFootstepCatalogScriptPath), Is.True, "Guest footsteps need an explicit per-guest catalog type.");
        Assert.That(File.Exists(GuestFootstepCatalogPath), Is.True, "The runtime footstep catalog should be available through Resources.");

        string audioText = File.ReadAllText(GuestFootstepAudioPath);
        string catalogScriptText = File.ReadAllText(GuestFootstepCatalogScriptPath);
        string catalogText = File.ReadAllText(GuestFootstepCatalogPath);
        string[] clipPaths =
        {
            "Assets/Audio/SFX/Footsteps/Wood/Guest/FS_Wood_Guest_Soft_01.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Guest/FS_Wood_Guest_Soft_02.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Guest/FS_Wood_Guest_Soft_03.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Guest/FS_Wood_Guest_Soft_04.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Guest/FS_Wood_Guest_Soft_05.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Guest/FS_Wood_Guest_Soft_06.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Guest/FS_Wood_Guest_Soft_07.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Guest/FS_Wood_Guest_Soft_08.wav"
        };
        string[] butlerClipPaths =
        {
            "Assets/Audio/SFX/Footsteps/Wood/Butler/FS_Wood_Butler_Soft_01.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Butler/FS_Wood_Butler_Soft_02.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Butler/FS_Wood_Butler_Soft_03.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Butler/FS_Wood_Butler_Soft_04.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Butler/FS_Wood_Butler_Soft_05.wav",
            "Assets/Audio/SFX/Footsteps/Wood/Butler/FS_Wood_Butler_Soft_06.wav"
        };

        Assert.That(audioText, Does.Contain("AudioHighPassFilter"), "Each guest footstep source should high-pass its one-shots before they enter the mix.");
        Assert.That(audioText, Does.Contain("highpassResonanceQ"), "The high-pass resonance should be serialized through the source component.");
        Assert.That(audioText, Does.Contain("source.loop = false"), "Footsteps should trigger one-shots, not long loops.");
        Assert.That(audioText, Does.Contain("PlayOneShot"), "Footsteps should be rhythmically triggered by code.");
        Assert.That(audioText, Does.Contain("stepIntervalSeconds"), "Footstep cadence should be serialized through the source component.");
        Assert.That(audioText, Does.Contain("GameAudioChannel.GameSounds"), "Footsteps should respect the Game Sounds settings slider.");
        Assert.That(catalogScriptText, Does.Contain("TryGetFootstepVariantsForGuest"), "Chapter systems should resolve clip variants through the catalog, not filenames.");
        Assert.That(catalogScriptText, Does.Contain("TryGetButlerFootstepVariants"), "The Butler should use dedicated one-shot variants.");
        Assert.That(catalogText, Does.Contain("highPassCutoffFrequency: 180"), "The catalog should apply a mix-friendly high-pass cutoff.");
        Assert.That(catalogText, Does.Contain("guestStepIntervalSeconds: 0.54"), "Guest footsteps should default to a normal walking cadence.");
        Assert.That(catalogText, Does.Contain("butlerStepIntervalSeconds: 0.6"), "Butler footsteps should default to a calmer walking cadence.");
        Assert.That(catalogText, Does.Contain("stepIntervalJitterSeconds: 0.025"), "Footsteps should have light timing jitter.");

        for (int i = 0; i < clipPaths.Length; i++)
        {
            string clipPath = clipPaths[i];
            string clipGuid = ReadGuid(clipPath + ".meta");
            int guestNumber = i + 1;

            Assert.That(File.Exists(clipPath), Is.True, $"Guest {guestNumber} footstep clip should exist at {clipPath}.");
            Assert.That(
                catalogText,
                Does.Match($@"(?s)- guestNumber: {guestNumber}\s+clip: \{{fileID: 8300000, guid: {clipGuid}, type: 3\}}\s+clips: "),
                $"Guest {guestNumber} should be assigned to {Path.GetFileName(clipPath)}.");
        }

        for (int i = 0; i < butlerClipPaths.Length; i++)
        {
            string clipPath = butlerClipPaths[i];
            string clipGuid = ReadGuid(clipPath + ".meta");

            Assert.That(File.Exists(clipPath), Is.True, $"Butler footstep variant should exist at {clipPath}.");
            Assert.That(catalogText, Does.Contain($"guid: {clipGuid}"), $"Butler catalog should reference {Path.GetFileName(clipPath)}.");
        }
    }

    [Test]
    public void DoorbellSystemUsesImportedVictorianDoorbellClip()
    {
        Assert.That(File.Exists(DoorbellClipAssetPath), Is.True, "The doorbell should use the approved imported WAV from Resources.");
        Assert.That(File.Exists(DoorbellClipAssetPath + ".meta"), Is.True, "The imported WAV should keep its Unity importer metadata.");

        string doorbellText = File.ReadAllText(DoorbellSystemPath);
        string doorbellMetaText = File.ReadAllText(DoorbellClipAssetPath + ".meta");

        Assert.That(doorbellText, Does.Contain($"DefaultDoorbellClipResourcePath = \"{DoorbellClipResourcePath}\""), "Runtime-created doorbell systems should know where the imported clip lives.");
        Assert.That(doorbellText, Does.Contain("Resources.Load<AudioClip>(doorbellClipResourcePath)"), "The doorbell should load the imported clip before falling back to generated tones.");
        Assert.That(doorbellText, Does.Match(@"(?s)Resources\.Load<AudioClip>\(doorbellClipResourcePath\)[\s\S]*CreateDoorbellClip\(\)"), "The generated tone should remain only as a fallback after the imported clip is unavailable.");
        Assert.That(doorbellMetaText, Does.Contain("preloadAudioData: 1"), "The short doorbell one-shot should be preloaded so the first ring is immediate.");
    }

    [Test]
    public void GuestFootstepsFollowChapterMovementRoutines()
    {
        string chapter1Text = File.ReadAllText(Chapter1ArrivalControllerPath);
        string panicText = File.ReadAllText(Chapter2GuestPanicControllerPath);
        string chapter1MoveBody = ExtractMethodBody(chapter1Text, "private IEnumerator MoveGuestTo(GuestRuntimeState");
        string chapter1BeginMoveBody = ExtractMethodBody(chapter1Text, "private void BeginGuestMoveTo");
        string chapter1DisableBody = ExtractMethodBody(chapter1Text, "private void DisableGuestMovement");
        string panicStepBody = ExtractMethodBody(panicText, "private bool StepParticipantsTowardAssignedTargets(float deltaTime, float moveSpeedPixels, bool hideParticipantsOnArrival)");
        string scriptedBeginBody = ExtractMethodBody(panicText, "public bool BeginScriptedAnimatorWalk");
        string scriptedStopBody = ExtractMethodBody(panicText, "public void StopScriptedAnimatorWalk");
        string hideAfterExitBody = ExtractMethodBody(panicText, "public void HideAfterExitArrival");

        Assert.That(chapter1Text, Does.Contain("GuestFootstepCatalog"), "Chapter 1 should use the shared footstep catalog.");
        Assert.That(chapter1Text, Does.Contain("ConfigureGuestFootsteps(guestObject, i + 1)"), "Chapter 1 runtime state should bind each guest to their own footstep loop.");
        Assert.That(chapter1MoveBody, Does.Match(@"StartGuestFootsteps\(guestState\)[\s\S]*mover\.MoveTo\(target\)[\s\S]*StopGuestFootsteps\(guestState\)"), "Coroutine guest movement should play footsteps only while the waypoint mover is active.");
        Assert.That(chapter1BeginMoveBody, Does.Match(@"StartGuestFootsteps\(guestState\)[\s\S]*mover\.MoveTo\(target\)"), "Asynchronous guest movement should start footsteps before moving.");
        Assert.That(chapter1DisableBody, Does.Match(@"StopGuestFootsteps\(guestState\)[\s\S]*guestState\.Mover\.StopMoving\(\)"), "Cancelling guest movement should stop footstep audio before disabling the mover.");

        Assert.That(panicText, Does.Contain("ConfigureParticipantFootsteps(participant)"), "Chapter 2 panic participants should inherit their assigned footstep loop.");
        Assert.That(panicStepBody, Does.Contain("participant.SetFootstepsMoving(!arrived)"), "Shared panic movement should toggle each guest's footstep source based on actual movement.");
        Assert.That(panicText, Does.Contain("StopSharedParticipantFootsteps()"), "Shared panic footsteps should stop when a shared movement pass ends.");
        Assert.That(scriptedBeginBody, Does.Contain("PlayFootsteps()"), "Scripted panic guests should play footsteps while animator walking starts.");
        Assert.That(scriptedStopBody, Does.Contain("StopFootsteps()"), "Scripted panic guests should stop footsteps when animator walking stops.");
        Assert.That(hideAfterExitBody, Does.Contain("StopFootsteps()"), "Guests hidden after leaving the room should not leave footstep loops running.");
    }

    [Test]
    public void SubtitleLineBankDefinesChapter1AndChapter2DialogueOnlyLines()
    {
        Assert.That(File.Exists(SubtitleLinePath), Is.True, "Subtitle entries should have a serializable data type.");
        Assert.That(File.Exists(SubtitleLineBankScriptPath), Is.True, "Subtitle entries should be data-driven through a line bank.");
        Assert.That(File.Exists(SubtitleServicePath), Is.True, "Chapter 1 needs a reusable subtitle display service.");
        Assert.That(File.Exists(SubtitleLineBankPath), Is.True, "Subtitle lines should load from Resources.");

        string bankText = File.ReadAllText(SubtitleLineBankPath);
        string serviceText = File.ReadAllText(SubtitleServicePath);
        string combinedText = bankText + serviceText + File.ReadAllText(SubtitleLinePath) + File.ReadAllText(SubtitleLineBankScriptPath);
        string[] bannedTerms =
        {
            "Chatterbox",
            "ElevenLabs",
            "OpenAI",
            "TextToSpeech",
            "AudioGenerator",
            "generated WAV",
            "voice playback",
            "API"
        };

        Assert.That(bankText, Does.Contain("SUB_CH01_BUTLER_EMPTY_DOOR_001"));
        Assert.That(bankText, Does.Contain("No one is there."));
        Assert.That(bankText, Does.Contain("SUB_CH02_BUTLER_ADDRESS_GUESTS_001"));
        Assert.That(bankText, Does.Contain("Welcome friends and gentlemen, guests of the evening, Count and Countess of Chantilly—"));
        Assert.That(bankText, Does.Not.Contain("[Guest Name]"), "Subtitle bank text should match spoken audio and must not keep placeholder names.");
        Assert.That(bankText, Does.Contain("Good evening. I trust the house remembers its manners better than the weather does."));
        Assert.That(bankText, Does.Contain("Thank you. The drive was longer in the dark than I care to admit."));
        Assert.That(bankText, Does.Contain("Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?"));
        Assert.That(bankText, Does.Contain("Good evening, Butler. The road up here has the cheerful shape of a warning."));
        Assert.That(bankText, Does.Contain("Thank you. I nearly mistook the bell pull for a funeral cord."));
        Assert.That(bankText, Does.Contain("At last. I had begun composing my obituary in the frost."));
        Assert.That(bankText, Does.Contain("It is rather cold out there, and colder still when one is expected."));
        Assert.That(bankText, Does.Contain("We have been waiting at the door for some time. The house was listening with us."));
        Assert.That(bankText, Does.Contain("At last. A closed door should not feel so pleased with itself."));
        Assert.That(bankText, Does.Contain("I have found you, Miss Isolde Wren. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?"));
        Assert.That(bankText, Does.Contain("I have found you, Professor Lucien Vale. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?"));
        Assert.That(bankText, Does.Contain("I have found you, Mister Florian Knell. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?"));
        Assert.That(bankText, Does.Contain("I have found you, Countess Elowen Dusk. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?"));
        Assert.That(bankText, Does.Contain("I have found you, Baron Hector Glass. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?"));
        Assert.That(bankText, Does.Contain("I have found you, Lady Sabine Marrow. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?"));
        Assert.That(bankText, Does.Contain("I have found you, Lord Ambrose Veil. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?"));
        Assert.That(bankText, Does.Contain("I have found you, Madame Coralie Thread. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?"));
        Assert.That(bankText, Does.Contain("SUB_CH02_BUTLER_SPIRITS_ASK_001"));
        Assert.That(bankText, Does.Contain("And shall I see that your bottle of spirits is waiting at the table?"));
        Assert.That(Regex.Matches(bankText, @"(?m)^  - lineId: CH[12]_G\d\d_").Count, Is.EqualTo(160), "Every generated guest voice line ID should have a direct subtitle-bank entry with matching script text.");
        Assert.That(bankText, Does.Contain("CH1_G01_ENTRY"));
        Assert.That(bankText, Does.Contain("CH2_G08_DINING_REVEAL"));
        Assert.That(bankText, Does.Contain("Serve quickly, Butler. The night is not finished with us."));
        Assert.That(serviceText, Does.Contain("Queue<QueuedSubtitle>"), "Non-choice bark subtitles should queue rather than overlap.");
        Assert.That(serviceText, Does.Contain("WaitForSecondsRealtime"), "Subtitle auto-hide timing should not pause or own gameplay.");
        Assert.That(serviceText, Does.Contain("KeyCode.Escape"), "Players should be able to skip the current queued subtitle with Escape.");
        Assert.That(serviceText, Does.Contain("Missing subtitle line"), "Missing line IDs should warn and continue.");

        for (int i = 1; i <= 8; i++)
        {
            string guestPrefix = $"G{i:00}";
            Assert.That(bankText, Does.Contain($"SUB_CH01_{guestPrefix}_GREETING_001"));
            Assert.That(bankText, Does.Contain($"SUB_CH01_{guestPrefix}_ANNOYED_001"));
            Assert.That(bankText, Does.Contain($"SUB_CH01_{guestPrefix}_AMBIENT_001"));
            Assert.That(bankText, Does.Contain($"SUB_CH02_BUTLER_FOUND_{guestPrefix}"));
            Assert.That(bankText, Does.Contain($"SUB_CH02_{guestPrefix}_FINAL_ACK_001"));
        }

        for (int i = 0; i < bannedTerms.Length; i++)
        {
            Assert.That(combinedText, Does.Not.Contain(bannedTerms[i]), $"Subtitle implementation should not reference {bannedTerms[i]}.");
        }
    }

    [Test]
    public void ChapterSubtitlesDecorateExistingFlowWithoutOwningGameplay()
    {
        string chapter1Text = File.ReadAllText(Chapter1ArrivalControllerPath);
        string chapter2Text = File.ReadAllText(Chapter2ControllerPath);
        string searchText = File.ReadAllText(Chapter2GuestSearchControllerPath);
        string introText = File.ReadAllText(ChapterIntroUIPath);
        string chapterManagerText = File.ReadAllText(ChapterManagerPath);
        string chapter1AdmitGroupsBody = ExtractMethodBody(chapter1Text, "private IEnumerator AdmitQueuedGuestGroups");
        string chapter1AdmissionBody = ExtractMethodBody(chapter1Text, "private IEnumerator AdmitGuestToEntranceHall");
        string takeGuestCoatBody = ExtractMethodBody(chapter1Text, "private void TakeGuestCoat");
        string canMoveGuestBody = ExtractMethodBody(chapter1Text, "private bool CanMoveGuestToDrawingRoom");
        string guestLineIdBody = ExtractMethodBody(chapter1Text, "private static string GetChapter1GuestLineId");
        string checkActiveGroupsBody = ExtractMethodBody(chapter1Text, "private void CheckActiveGroupsReadyForDrawingRoom");
        string moveEntranceGroupBody = ExtractMethodBody(chapter1Text, "private IEnumerator MoveEntranceGroupToDrawingRoom");
        string tryCompleteGroupBody = ExtractMethodBody(chapter1Text, "private void TryCompleteEntranceGroup");
        string fastForwardDoorbellBody = ExtractMethodBody(chapter1Text, "private void TryFastForwardNextDoorbellIfEntranceClear");
        string ambientBody = ExtractMethodBody(chapter1Text, "private void StartAmbientConversation");
        string openingSpeechBody = ExtractMethodBody(chapter2Text, "private IEnumerator RunOpeningSpeechRoutine");
        string holdChoicesBody = ExtractMethodBody(chapter2Text, "private void HoldDialogueChoicesForSpeech");
        string chapter2ResolveBody = ExtractMethodBody(chapter2Text, "private void ResolveReferences");
        string startConversationBody = ExtractMethodBody(searchText, "public bool TryStartGuestConversation");
        string butlerFoundBody = ExtractMethodBody(searchText, "private void ShowButlerFoundLine");
        string mealQuestionBody = ExtractMethodBody(searchText, "private void ShowButlerMealAsk");
        string smokeQuestionBody = ExtractMethodBody(searchText, "private void ShowButlerSmokeAsk");
        string spiritsQuestionBody = ExtractMethodBody(searchText, "private void ShowButlerSpiritsAsk");
        string guestMealReplyBody = ExtractMethodBody(searchText, "private void ShowGuestMealReply");
        string guestSmokeReplyBody = ExtractMethodBody(searchText, "private void ShowGuestSmokeReply");
        string guestSpiritsReplyBody = ExtractMethodBody(searchText, "private void ShowGuestSpiritsReply");
        string exitToDiningBody = ExtractMethodBody(searchText, "private void ShowGuestExitToDining");

        Assert.That(chapter1Text, Does.Contain("ShowSubtitleLine(\"SUB_CH01_BUTLER_EMPTY_DOOR_001\")"), "Empty 6:04 doorbell should show the short Butler subtitle.");
        Assert.That(chapter1Text, Does.Contain("ShowSubtitleLine(\"SUB_CH01_BUTLER_ONE_COAT_001\")"), "Existing one-coat rejection should get a subtitle without changing coat state.");
        Assert.That(chapter1Text, Does.Contain("ShowSubtitleLine(\"SUB_CH01_BUTLER_NO_COAT_001\")"), "Existing empty-handed hanger rejection should get a subtitle without changing coat state.");
        Assert.That(chapter1AdmitGroupsBody, Does.Contain("StartCoroutine(AdmitGuestToEntranceHall"), "Answering the door should admit all waiting guests instead of serializing them behind speech.");
        Assert.That(chapter1AdmissionBody, Does.Contain("QueueGuestLine(guest, \"GREETING\""), "Guest greetings should be queued while guests continue walking.");
        Assert.That(chapter1AdmissionBody, Does.Not.Contain("yield return SpeakGuestLine"), "Guest entry movement should not wait for greeting audio.");
        Assert.That(chapter1AdmissionBody, Does.Contain("PrepareGuestCoatForArrival(guest)"), "The correct guest coat should be prepared before walking to the waiting spot.");
        Assert.That(chapter1AdmitGroupsBody, Does.Contain("QueueButlerLine(\"SUB_CH01_BUTLER_WELCOME_001\")"), "The Butler should welcome each answered door batch once.");
        Assert.That(chapter1Text, Does.Contain("chapter1SecondsPerGameMinute = 35f"), "Doorbells should be spaced about 35 seconds apart when the entrance is busy.");
        Assert.That(takeGuestCoatBody, Does.Contain("QueueButlerLine(\"SUB_CH01_BUTLER_TAKE_COAT_001\")"), "The Butler should ask for coats only after the player clicks/takes a coat.");
        Assert.That(canMoveGuestBody, Does.Contain("guest.CoatStored"), "Guest pairs should not depart before their coats have actually been stored.");
        Assert.That(guestLineIdBody, Does.Contain("CH1_G{guestNumber:00}_ENTRY"), "Entry speech should use generated voice line IDs.");
        Assert.That(guestLineIdBody, Does.Contain("CH1_G{guestNumber:00}_DELAYED"), "Delayed speech should use generated voice line IDs.");
        Assert.That(checkActiveGroupsBody, Does.Contain("CanMoveEntranceGroupToDrawingRoom(group)"), "Guests should leave the entrance by group after all coats are stored.");
        Assert.That(moveEntranceGroupBody, Does.Contain("yield return SpeakButlerLine(\"SUB_CH01_BUTLER_THIS_WAY_001\")"), "The Butler's Drawing Room line should finish before the pair starts moving.");
        Assert.That(moveEntranceGroupBody, Does.Contain("QueueGuestLine(group.Guests[i], \"TO_DRAWING_ROOM\", null)"), "Guest Drawing Room lines should be queued as the group departs.");
        Assert.That(tryCompleteGroupBody, Does.Contain("TryFastForwardNextDoorbellIfEntranceClear()"), "The next doorbell should accelerate once the entrance is clear.");
        Assert.That(fastForwardDoorbellBody, Does.Contain("HandleScheduledDoorbell(nextGroup)"), "Doorbell acceleration should reuse the normal scheduled doorbell path.");
        Assert.That(ambientBody, Does.Contain("ShowGuestSubtitle(guestState, \"AMBIENT\""), "Ambient captions should only come from the existing ambient hook.");
        Assert.That(chapter1Text, Does.Contain("Good evening. I trust the house remembers its manners better than the weather does."), "Chapter 1 fallback greetings should match the generated voice script.");
        Assert.That(chapter1Text, Does.Contain("Thank you. The drive was longer in the dark than I care to admit."), "Chapter 1 fallback greetings should match the generated voice script.");
        Assert.That(chapter1Text, Does.Contain("Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?"), "Chapter 1 fallback greetings should match the generated voice script.");
        Assert.That(chapter1Text, Does.Contain("At last. I had begun composing my obituary in the frost."), "Chapter 1 delayed fallback lines should match the generated voice script.");
        Assert.That(chapter1Text, Does.Contain("It is rather cold out there, and colder still when one is expected."), "Chapter 1 delayed fallback lines should match the generated voice script.");

        Assert.That(chapter2Text, Does.Contain("ShowGuestConversationWithSubtitle"), "Chapter 2 should preserve the existing dialogue panel and choices.");
        Assert.That(holdChoicesBody, Does.Contain("SetDialogueSkipAction(service.SkipCurrentSpeech)"), "Interactive dialog should expose skip on the dialogue panel.");
        Assert.That(holdChoicesBody, Does.Contain("showSubtitleOverlay: false"), "Interactive dialog should not also show the subtitle overlay.");
        Assert.That(openingSpeechBody, Does.Contain("const string openingSpeechLineId = \"SUB_CH02_BUTLER_ADDRESS_GUESTS_001\""), "Address Guests should keep the interrupted Butler line ID explicit.");
        Assert.That(openingSpeechBody, Does.Contain("yield return SpeakLineInDialoguePanel(openingSpeechLineId, \"Butler\", line, false, true)"), "Address Guests should use the shared speech API through the dialogue panel and block input.");
        Assert.That(openingSpeechBody, Does.Match(@"SpeakLineInDialoguePanel\(openingSpeechLineId[\s\S]*ClearDialogue\(\)[\s\S]*SetPhase\(Chapter2Phase\.MonsterStinger\)"), "The dialogue panel should clear before the monster stinger.");
        Assert.That(chapter2ResolveBody, Does.Not.Contain("ResolveSubtitleService();"), "Subtitle UI should be created lazily, not during chapter intro/reference resolution.");
        Assert.That(chapter2Text, Does.Contain("SetDialoguePanelSpeechLine"), "Chapter 2 interactive speech should render resolved subtitles in the dialogue panel.");
        Assert.That(chapter2Text, Does.Contain("onSpeechLineStarted: SetDialoguePanelSpeechLine"), "Dialogue-panel speech should show the exact line resolved by the speech service.");
        Assert.That(startConversationBody, Does.Contain("ShowButlerFoundLine(guest)"), "Hidden guest conversations should begin with the Butler found line, not an extra prompt gate.");
        Assert.That(searchText, Does.Not.Contain("ShowGuestFoundStart"), "Hidden guest conversations should not require the old found-start/Announce dinner click gate.");
        Assert.That(butlerFoundBody, Does.Contain("spec.ButlerFoundLineId"), "Found subtitles should follow the clicked guest identity.");
        Assert.That(mealQuestionBody, Does.Contain("ButlerMealAskLineId"));
        Assert.That(smokeQuestionBody, Does.Contain("ButlerSmokeAskLineId"));
        Assert.That(spiritsQuestionBody, Does.Contain("ButlerSpiritsAskLineId"));
        Assert.That(exitToDiningBody, Does.Contain("spec.ExitToDiningLineId"));

        Assert.That(searchText, Does.Not.Contain("\"Announce dinner\""));
        Assert.That(searchText, Does.Contain("\"Ask supper preference\""));
        Assert.That(searchText, Does.Contain("\"Ask drink preference\""));
        Assert.That(searchText, Does.Contain("\"Ask smoke preference\""));
        Assert.That(searchText, Does.Contain("\"Comfort and send to Dining Room\""));
        Assert.That(searchText, Does.Contain("ShowPreferenceChoices"));
        Assert.That(searchText, Does.Contain("AreAllPreferencesRecorded"));
        Assert.That(searchText, Does.Not.Contain("\"Continue\""));
        Assert.That(searchText, Does.Not.Contain("ChooseMealPreference"));
        Assert.That(searchText, Does.Not.Contain("ChooseSmokingPreference"));
        Assert.That(searchText, Does.Not.Contain("guest.mealPreference = preference"));
        Assert.That(searchText, Does.Not.Contain("guest.smokingPreference = preference"));
        Assert.That(searchText, Does.Not.Contain("\"Cigar\""));
        Assert.That(searchText, Does.Not.Contain("\"Pipe\""));
        Assert.That(searchText, Does.Not.Contain("\"No smoke\""));
        Assert.That(guestMealReplyBody, Does.Contain("guest.mealPreference = spec.FixedMealPreference"));
        Assert.That(guestSmokeReplyBody, Does.Contain("guest.smokingPreference = spec.FixedSmokingPreference"));
        Assert.That(guestSpiritsReplyBody, Does.Contain("guest.spiritBottle = spec.FixedSpiritBottle"));
        Assert.That(searchText, Does.Contain("foundGuestIdsInOrder.Add(GetGuestIdForOrderList(guest))"));
        Assert.That(introText, Does.Contain("Time.unscaledDeltaTime"), "Chapter intro fades should not freeze when gameplay is paused.");
        Assert.That(chapterManagerText, Does.Contain("WaitForSecondsRealtime(GetIntroTitleHoldSeconds())"), "Chapter title holds should not freeze when gameplay is paused.");
    }

    [Test]
    public void ButlerVoiceLinesAreCatalogedAndProtectedFromRoutineCutoff()
    {
        Assert.That(File.Exists(DialogueSpeechServicePath), Is.True, "Required dialog should use the shared speech/subtitle service.");
        Assert.That(File.Exists(SpeakingCharacterIndicatorPath), Is.True, "Voiced dialog should mark the currently speaking character in-world.");
        Assert.That(File.Exists(SpeakingCharacterIndicatorSpritePath), Is.True, "The approved transparent chat bubble cutout should be available through Resources.");
        Assert.That(File.Exists(SpeakingCharacterIndicatorSpritePath + ".meta"), Is.True, "The chat bubble cutout should import as a Sprite.");
        Assert.That(File.Exists(GuestVoiceLinePlaybackPath), Is.True, "Butler dialog should use the shared voice-line playback component.");
        Assert.That(File.Exists(GuestVoiceLineCatalogPath), Is.True, "Butler dialog clips should be registered in the voice-line catalog.");
        Assert.That(Directory.Exists(ButlerVoiceFolderPath), Is.True, "Butler dialog WAVs should live beside the guest voice assets.");

        string[] catalogedButlerLineIds =
        {
            "SUB_CH01_BUTLER_EMPTY_DOOR_001",
            "SUB_CH01_BUTLER_NO_COAT_001",
            "SUB_CH01_BUTLER_ONE_COAT_001",
            "SUB_CH01_BUTLER_TAKE_COAT_001",
            "SUB_CH01_BUTLER_THIS_WAY_001",
            "SUB_CH01_BUTLER_WELCOME_001",
            "SUB_CH02_BUTLER_ADDRESS_GUESTS_001",
            "SUB_CH02_BUTLER_FOUND_G01",
            "SUB_CH02_BUTLER_FOUND_G02",
            "SUB_CH02_BUTLER_FOUND_G03",
            "SUB_CH02_BUTLER_FOUND_G04",
            "SUB_CH02_BUTLER_FOUND_G05",
            "SUB_CH02_BUTLER_FOUND_G06",
            "SUB_CH02_BUTLER_FOUND_G07",
            "SUB_CH02_BUTLER_FOUND_G08",
            "SUB_CH02_BUTLER_MEAL_ASK_001",
            "SUB_CH02_BUTLER_SMOKE_ASK_001",
            "SUB_CH02_BUTLER_SPIRITS_ASK_001"
        };

        string catalogText = File.ReadAllText(GuestVoiceLineCatalogPath);
        string playbackText = File.ReadAllText(GuestVoiceLinePlaybackPath);
        string speechServiceText = File.ReadAllText(DialogueSpeechServicePath);
        string speakingIndicatorText = File.ReadAllText(SpeakingCharacterIndicatorPath);
        string subtitleServiceText = File.ReadAllText(SubtitleServicePath);
        string chapter2Text = File.ReadAllText(Chapter2ControllerPath);

        Assert.That(Directory.GetFiles(ButlerVoiceFolderPath, "*.wav").Length, Is.GreaterThanOrEqualTo(catalogedButlerLineIds.Length), "Cataloged Butler lines should have imported WAVs.");

        for (int i = 0; i < catalogedButlerLineIds.Length; i++)
        {
            string lineId = catalogedButlerLineIds[i];
            string wavPath = $"{ButlerVoiceFolderPath}/{lineId}.wav";
            string metaPath = $"{wavPath}.meta";

            Assert.That(File.Exists(wavPath), Is.True, $"{lineId} should have a generated Butler WAV.");
            Assert.That(File.Exists(metaPath), Is.True, $"{lineId} should have a Unity meta file.");
            Assert.That(catalogText, Does.Contain($"lineId: {lineId}"), $"{lineId} should be in the voice-line catalog.");
            Assert.That(catalogText, Does.Contain(ReadGuid(metaPath)), $"{lineId} catalog entry should reference its WAV GUID.");
        }

        Assert.That(playbackText, Does.Contain("SUB_CH01_BUTLER_"), "Chapter 1 Butler subtitle IDs should resolve directly as audio IDs.");
        Assert.That(playbackText, Does.Contain("SUB_CH02_BUTLER_"), "Chapter 2 Butler subtitle IDs should resolve directly as audio IDs.");
        Assert.That(playbackText, Does.Contain("PlayForDialogue(string lineId, string speaker, string text, bool allowOverlap)"), "Speech playback should support explicit overlap only when requested.");
        Assert.That(speechServiceText, Does.Contain("while (!allowOverlap && normalSpeechActive)"), "Normal dialog should serialize through one active speech line.");
        Assert.That(speechServiceText, Does.Contain("subtitleService.ShowSpeechLine"), "Speech playback should display the matching subtitle at voice start.");
        Assert.That(speechServiceText, Does.Contain("voicePlayback.PlayForDialogue(lineId, speaker, text, allowOverlap)"), "Speech playback should use the resolved voice clip for the same line.");
        Assert.That(speechServiceText, Does.Contain("SpeakingCharacterIndicator.FindOrCreate()"), "Speech playback should lazily create the speaker marker with the other dialogue services.");
        Assert.That(speechServiceText, Does.Contain("speakingIndicator.ShowForSpeechLine(speechToken, lineId, speaker, text)"), "The speaker marker should appear when the resolved speech line starts.");
        Assert.That(speechServiceText, Does.Contain("speakingIndicator.HideForSpeechToken(speechToken)"), "The speaker marker should clear only for the speech line that owns it.");
        Assert.That(speechServiceText, Does.Contain("Input.GetKeyDown(KeyCode.Escape)"), "Escape should skip the active speech line without advancing the next line.");
        Assert.That(subtitleServiceText, Does.Contain("Button_SubtitleSkip"), "Subtitle UI should expose a small skip button during active speech.");
        Assert.That(subtitleServiceText, Does.Not.Contain("PlayForDialogue("), "Subtitle-only paths must not bypass DialogueSpeechService voice serialization.");
        Assert.That(subtitleServiceText, Does.Match(@"(?s)\bClearAll\s*\([^)]*\)\s*\{.*GuestVoiceLinePlayback\.StopAnyCurrentLine\(\)"), "Room, teleport, and chapter clears should stop active dialog audio.");
        Assert.That(subtitleServiceText, Does.Match(@"(?s)\bClearAll\s*\([^)]*\)\s*\{.*SpeakingCharacterIndicator\.HideAnyCurrent\(\)"), "Room, teleport, and chapter clears should also remove the speaker marker.");
        Assert.That(speakingIndicatorText, Does.Contain("DefaultSpriteResourcePath = \"UI/chat_bubble\""), "The marker should load the approved cutout chat bubble sprite from Resources.");
        Assert.That(speakingIndicatorText, Does.Contain("FindObjectsByType<ActorRoomState>(FindObjectsInactive.Include)"), "Guest speech should resolve to the visible ActorRoomState instead of a fixed screen coordinate.");
        Assert.That(speakingIndicatorText, Does.Contain("GuestDisplayNames"), "Display-name dialogue should map back to the correct guest actor.");
        Assert.That(speakingIndicatorText, Does.Contain("bounds.max.y + verticalDistance"), "The marker should sit above the actor bounds without covering the head.");
        Assert.That(speakingIndicatorText, Does.Contain("sortingOrder + sortingOrderOffset"), "The marker should render above the speaking actor.");
        Assert.That(chapter2Text, Does.Contain("ShowGuestConversation(\"Butler\", string.Empty, \"Address Guests\", HandleAddressGuestsPrompt)"), "Address Guests should appear as a dialogue-panel choice, not a separate primary action.");
        Assert.That(chapter2Text, Does.Contain("yield return SpeakLineInDialoguePanel(openingSpeechLineId, \"Butler\", line, false, true)"), "The Butler opening speech should show subtitles in the dialogue panel and wait on the shared speech API.");
        Assert.That(chapter2Text, Does.Contain("interactionHUD.ClearDialogue();"), "The Chapter 2 dialogue panel should clear before the monster stinger takes over.");
    }

    [Test]
    public void NewGameAndGameplayStartResetStickyPauseState()
    {
        Assert.That(File.Exists(GameplayRuntimeStatePath), Is.True, "Gameplay should have a single reset point for sticky editor/runtime pause state.");

        string gameplayRuntimeText = File.ReadAllText(GameplayRuntimeStatePath);
        string gameAudioSettingsText = File.ReadAllText(GameAudioSettingsPath);
        string mainMenuText = File.ReadAllText(MainMenuControllerPath);
        string chapterManagerText = File.ReadAllText(ChapterManagerPath);
        string runtimeSettingsText = File.ReadAllText(RuntimeSettingsMenuPath);
        string loadGameBody = ExtractMethodBody(mainMenuText, "private void LoadGameScene");
        string chapterManagerAwakeBody = ExtractMethodBody(chapterManagerText, "private void Awake");

        Assert.That(gameplayRuntimeText, Does.Contain("Time.timeScale = 1f"), "New game should never inherit Time.timeScale = 0 from another machine/editor session.");
        Assert.That(gameplayRuntimeText, Does.Contain("GameAudioSettings.ResetUnityAudioState()"), "New game should reset Unity's global audio listener state.");
        Assert.That(gameAudioSettingsText, Does.Contain("AudioListener.pause = false"), "New game should never inherit a paused audio listener.");
        Assert.That(gameAudioSettingsText, Does.Contain("AudioListener.volume = 1f"), "New game should never inherit a muted global audio listener volume.");
        Assert.That(gameplayRuntimeText, Does.Contain("EditorApplication.isPaused = false"), "Editor play-mode pause should be cleared at gameplay scene boundaries.");
        Assert.That(gameplayRuntimeText, Does.Contain("SceneManager.sceneLoaded"), "Scene loads should clear sticky pause state even when Gameplay is opened directly.");
        Assert.That(gameplayRuntimeText, Does.Contain("RuntimeSettingsMenu.ResetGlobalModalState()"), "Settings input-block state should be reset at gameplay boundaries.");
        Assert.That(runtimeSettingsText, Does.Contain("public static void ResetGlobalModalState()"));
        Assert.That(runtimeSettingsText, Does.Contain("BlocksGameInput = false"));
        Assert.That(loadGameBody, Does.Contain("GameplayRuntimeState.ResetForNewGame()"));
        Assert.That(chapterManagerAwakeBody, Does.Contain("GameplayRuntimeState.ResetForGameplayStart()"));
    }

    [Test]
    public void Chapter2GuestPanicRunsBackAndForthImmediately()
    {
        Assert.That(File.Exists(Chapter2GuestPanicControllerPath), Is.True);

        string panicText = File.ReadAllText(Chapter2GuestPanicControllerPath);
        int routineIndex = panicText.IndexOf("private IEnumerator RunPanicRoutine()", System.StringComparison.Ordinal);
        Assert.That(routineIndex, Is.GreaterThanOrEqualTo(0), "Panic playback should have a dedicated stinger routine.");

        int nextMethodIndex = panicText.IndexOf("private IEnumerator MoveParticipantsToward", routineIndex, System.StringComparison.Ordinal);
        int whileIndex = panicText.IndexOf("while (isRunning)", routineIndex, System.StringComparison.Ordinal);
        int firstClipIndex = panicText.IndexOf("yield return MoveParticipantsToward", routineIndex, System.StringComparison.Ordinal);

        Assert.That(nextMethodIndex, Is.GreaterThan(routineIndex));
        Assert.That(whileIndex, Is.GreaterThan(routineIndex), "Panic playback should loop while the stinger is running.");
        Assert.That(firstClipIndex, Is.GreaterThan(whileIndex), "Guests should start running immediately instead of spending stinger time on a non-running pre-roll.");

        string routineText = panicText.Substring(routineIndex, nextMethodIndex - routineIndex);
        Assert.That(routineText, Does.Contain("ChooseRandomRunTargets"));
        Assert.That(routineText, Does.Contain("MoveParticipantsTowardAssignedTargets"));
        Assert.That(routineText, Does.Contain("PlayRandomStopActions"));
        Assert.That(panicText, Does.Contain("PanicAction.PanicHandsUp"));
        Assert.That(panicText, Does.Contain("PanicAction.PanicPop"));
        Assert.That(panicText, Does.Contain("PanicAction.PanicRunDown"));
        Assert.That(panicText, Does.Contain("PanicAction.PanicRunLeft"));
        Assert.That(panicText, Does.Contain("PanicAction.PanicRunRight"));
        Assert.That(panicText, Does.Contain("PanicAction.PanicRunUp"));
        Assert.That(panicText, Does.Contain("panicRoamRadiusPixels = 190f"));
        Assert.That(panicText, Does.Contain("verticalRunDistanceScale = 0.55f"));
        Assert.That(panicText, Does.Contain("TryBuildReachableWorldPath"));
        Assert.That(panicText, Does.Contain("currentRouteOffsets"));
        Assert.That(panicText, Does.Contain("GetRandomScatterDirection"));
        Assert.That(panicText, Does.Not.Contain("GetRandomCardinalDirection"));
        Assert.That(panicText, Does.Contain("CurrentRunAction"));
        Assert.That(panicText, Does.Contain("SetStopAction"));
        Assert.That(panicText, Does.Contain("randomStopActionChance = 1f"));
        Assert.That(panicText, Does.Contain("randomPopActionChance = 0.45f"));
        Assert.That(panicText, Does.Not.Contain("ScriptedGuestNumber"), "Scripted panic should be data-driven, not hardwired to Guest 1.");
        Assert.That(panicText, Does.Contain("useScriptedGuestPanic = true"));
        Assert.That(panicText, Does.Contain("FormerlySerializedAs(\"useScriptedGuest1Panic\")"), "Existing scene data should survive the field rename.");
        Assert.That(panicText, Does.Contain("StartScriptedGuestPanicRoutines"));
        Assert.That(panicText, Does.Contain("panicFrames.Length < 8"), "Any guest with eight panic_pop frames should use the scripted panic routine.");
        Assert.That(panicText, Does.Contain("scriptedGuestMinRunSeconds = 0.5f"));
        Assert.That(panicText, Does.Contain("scriptedGuestMaxRunSeconds = 1.25f"));
        Assert.That(panicText, Does.Contain("scriptedGuestHoldSeconds = 0.25f"));
        Assert.That(panicText, Does.Contain("scriptedGuestRunDistancePixels = 500f"));
        Assert.That(panicText, Does.Contain("scriptedGuestMoveSpeedPixels = 560f"));
        Assert.That(panicText, Does.Contain("scriptedGuestWalkAnimationSpeed = 2f"));
        Assert.That(panicText, Does.Contain("scriptedGuestPanicSpriteScaleMultiplier = 1f"));
        Assert.That(panicText, Does.Contain("RunScriptedGuestPanicRoutine"));
        Assert.That(panicText, Does.Contain("RunScriptedGuestDirectionalRun"));
        Assert.That(panicText, Does.Contain("ChooseRandomScriptedGuestRunAction"), "Scripted guests should choose their next panic direction independently.");
        Assert.That(panicText, Does.Contain("GetRandomScriptedGuestRunSeconds"), "Scripted guests should randomize how long each direction lasts.");
        Assert.That(panicText, Does.Contain("GetScriptedGuestTransitionSeconds"), "Scripted panic stills should use the short transition beat.");
        Assert.That(panicText, Does.Contain("UnityEngine.Random.Range(0, 4)"), "Scripted guests should choose from all four cardinal directions.");
        Assert.That(panicText, Does.Contain("Vector2.down"));
        Assert.That(panicText, Does.Contain("Vector2.left"));
        Assert.That(panicText, Does.Contain("Vector2.right"));
        Assert.That(panicText, Does.Contain("Vector2.up"));
        Assert.That(panicText, Does.Contain("RunScriptedGuestMoveForSeconds(participant, durationSeconds, moveSpeedPixels, true, runAction)"), "Scripted guest runs should lock to each randomly requested four-direction beat.");
        Assert.That(panicText, Does.Contain("BeginScriptedAnimatorWalk(lockedRunAction, scriptedGuestWalkAnimationSpeed)"), "Scripted run beats should use the existing Animator walk clips, not panic still sprites.");
        Assert.That(panicText, Does.Contain("UpdateScriptedAnimatorWalk(lockedRunAction, scriptedGuestWalkAnimationSpeed)"), "Scripted run beats should keep the Animator walking in the scripted direction.");
        Assert.That(panicText, Does.Contain("panicFrames[UnityEngine.Random.Range(0, panicFrames.Length)]"), "Each transition should choose one random panic still instead of playing a coordinated sequence.");
        Assert.That(panicText, Does.Contain("StopScriptedAnimatorWalk(participant.CurrentRunAction)"), "Scripted panic holds should stop the Animator before showing one panic still.");
        Assert.That(panicText, Does.Contain("SetSprite(panicSprite, scriptedGuestPanicSpriteScaleMultiplier)"), "Scripted panic stills should stay sized against each guest's authored body scale.");
        Assert.That(panicText, Does.Contain("HoldScriptedGuestPanicFrame"));
        Assert.That(panicText, Does.Not.Contain("SetInputEnabled(false)"), "Guest panic must not lock the global point-click input/cursor state.");
        Assert.That(panicText, Does.Contain("ReleaseScriptedGuestParticipantsForSharedMotion"));
        Assert.That(panicText, Does.Contain("IsControlledByScript"));
        Assert.That(panicText, Does.Contain("HasSharedPanicParticipants"));
        Assert.That(panicText, Does.Not.Contain("PanicAction.PanicShriekDown"));
        Assert.That(panicText, Does.Not.Contain("PanicAction.PanicReactionDown"));
        Assert.That(panicText, Does.Not.Contain("PanicAction.CoverFaceCower"));
        Assert.That(panicText, Does.Not.Contain("PanicAction.PanicTurnaround"));
        Assert.That(panicText, Does.Contain("ChooseRandomStopActions"));
        Assert.That(panicText, Does.Contain("ApplyRandomStopActionFrame"));
        Assert.That(panicText, Does.Contain("runDistancePixels = 150f"));
        Assert.That(panicText, Does.Contain("panicMoveSpeedPixels = 300f"));
        Assert.That(panicText, Does.Contain("DefaultExecutionOrder(10000)"));
        Assert.That(panicText, Does.Not.Contain("turnaroundDistanceScale"));
        Assert.That(panicText, Does.Not.Contain("GetTurnaroundOffset"));
        Assert.That(panicText, Does.Contain("MoveParticipantsTowardAssignedTargets"));
        Assert.That(panicText, Does.Contain("StepParticipantsTowardAssignedTargets"));
        Assert.That(panicText, Does.Contain("MovePanicOffsetTowardCurrentTarget"));
        Assert.That(panicText, Does.Contain("Vector2.MoveTowards"));
        Assert.That(panicText, Does.Contain("private void LateUpdate()"));
        Assert.That(panicText, Does.Contain("ReapplyParticipantVisualOffsets"));
        Assert.That(panicText, Does.Contain("Rigidbody2D"));
        Assert.That(panicText, Does.Contain("MoveRigidbodyTo"));
        Assert.That(panicText, Does.Contain("Physics2D.SyncTransforms"));
        Assert.That(panicText, Does.Contain("CaptureOriginalSpriteLocalSize"));
        Assert.That(panicText, Does.Contain("GetSpriteScaleMultiplier"));
        Assert.That(panicText, Does.Contain("ApplySpriteScale(currentPanicSprite)"));
        Assert.That(panicText, Does.Contain("float frameProgress"));
        Assert.That(panicText, Does.Contain("float motionFrame = frameIndex + frameProgress"));
        Assert.That(panicText, Does.Contain("ApplyAssignedRunFrame(frameIndex, motionFrame, jitter)"));
        Assert.That(panicText, Does.Not.Contain("Vector2.LerpUnclamped"));
        Assert.That(panicText, Does.Contain("ConfigureRunMotion"));
        Assert.That(panicText, Does.Contain("GetPanicOffset"));
        Assert.That(panicText, Does.Contain("FindMotionDrivers"));
        Assert.That(panicText, Does.Contain("RoomPersonWalker2D"));
        Assert.That(panicText, Does.Contain("NPCWaypointMover"));
        Assert.That(panicText, Does.Contain("motionDrivers[i].enabled = false"));
    }

    [Test]
    public void Chapter2GuestPanicExitsHalfTheGuestsThroughEachDrawingRoomDoor()
    {
        Assert.That(File.Exists(Chapter2GuestPanicControllerPath), Is.True);

        string panicText = File.ReadAllText(Chapter2GuestPanicControllerPath);
        Assert.That(panicText, Does.Contain("RunExitToDoorsThenRestoreRoutine"), "Panic should expose an exit beat that Chapter2Controller can await.");
        Assert.That(panicText, Does.Contain("BeginExitToDoors"), "Panic should switch from roaming to a directed door exit.");
        Assert.That(panicText, Does.Contain("StopPanicRoutineOnly(true)"), "Door exit should reclaim per-guest panic routines before assigning shared door targets.");
        Assert.That(panicText, Does.Contain("MoveParticipantsTowardAssignedTargets(false, exitMoveSpeedPixels, true)"), "Door exit should hide each guest as soon as that guest reaches its assigned door point.");
        Assert.That(panicText, Does.Contain("TryChooseExitTarget(exitTarget, routePlanner, worldUnitsPerRoomPixel, 0f)"), "Guests should disappear at the door point itself instead of running past it before hiding.");
        Assert.That(panicText, Does.Contain("HideAfterExitArrival"), "Door exit should make each arrived guest disappear independently.");
        Assert.That(panicText, Does.Contain("IsHiddenAfterExitArrival"), "Arrived guests should stop receiving shared run frames while other guests keep fleeing.");
        Assert.That(panicText, Does.Contain("ShouldHideAfterCurrentTargetArrival"), "Only guests with assigned door targets should disappear on arrival.");
        Assert.That(panicText, Does.Contain("SetVisibleByChapterState(false)"), "Arrived guests should disappear through ActorRoomState visibility.");
        Assert.That(panicText, Does.Contain("PointClickPlayerMovement routePlanner"), "Guest panic should reuse the butler movement route planner instead of inventing a separate floor system.");
        Assert.That(panicText, Does.Contain("FindRoutePlanner"), "Guest panic should resolve the named Player movement component for walkable-floor route queries.");
        Assert.That(panicText, Does.Contain("TryChooseRoutedRunTarget"), "Random panic movement should prefer butler route-space targets before falling back to raw room offsets.");
        Assert.That(panicText, Does.Contain("TryChooseRoutedDirectionalRunTarget"), "Guest 1's scripted panic runs should also prefer butler route-space targets.");
        Assert.That(panicText, Does.Contain("leftExitTargetName = \"DoorTrigger_DrawingRoom_MusicRoom\""));
        Assert.That(panicText, Does.Contain("rightExitTargetName = \"DoorTrigger_DrawingRoom_GEH\""));
        Assert.That(panicText, Does.Contain("ChooseDoorExitTargets"));
        Assert.That(panicText, Does.Contain("sortedParticipants.Count / 2"), "Door exit should split the visible guests evenly between the two exits.");
        Assert.That(panicText, Does.Contain("TryChooseExitTarget"));
        Assert.That(panicText, Does.Contain("TryChooseRoutedExitTarget"), "Door exits should clamp through the same walkable-floor route logic as point-click movement.");
        Assert.That(panicText, Does.Contain("MoveLogicalPointToward"), "Door exits should step in butler logical coordinates, including vertical movement scaling.");
        Assert.That(panicText, Does.Contain("TryGetLogicalPositionFromWorldPoint"), "Door exits should ask the player movement boundary to clamp targets onto the floor collider.");
        Assert.That(panicText, Does.Contain("TryGetRoomPixelOffsetFromWorldPoint"), "Guest route positions should be converted back into RoomProjectedEntity room-local points.");
        Assert.That(panicText, Does.Contain("TryGetActiveRoomStageLocalPoint"), "Persistent projected guests should convert routed world points back through the active room stage.");
        Assert.That(panicText, Does.Contain("GetExitFootWorldPosition"), "Guests should run toward the door floor, not the center of the door trigger.");
        Assert.That(panicText, Does.Contain("GetExitWaitTimeoutSeconds"), "Guest search handoff should wait long enough for Guest 1's scripted panic sequence to finish.");
        Assert.That(panicText, Does.Contain("StopPanic()"), "The exit beat should restore normal sprites/animators before guest search stages actors at hide anchors.");

        string movementText = File.ReadAllText(PointClickPlayerMovementPath);
        Assert.That(movementText, Does.Contain("TryGetLogicalPositionFromWorldPoint"), "Guest panic needs public access to the same world-to-logical walkable-floor conversion used by the butler.");
        Assert.That(movementText, Does.Contain("TryGetWorldPointFromLogicalPosition"), "Guest panic needs public access to the same logical-to-world conversion used by the butler.");
        Assert.That(movementText, Does.Contain("MoveLogicalPointToward"), "Guest panic should step using the butler's vertical movement scaling.");

        string cameraManagerText = File.ReadAllText(CameraManagerPath);
        Assert.That(cameraManagerText, Does.Contain("TryGetActiveRoomStageLocalPoint"), "Projected guest panic needs the inverse of CameraManager's active room-stage world conversion.");
    }

    [Test]
    public void Chapter2PanicApprovedFrameLibraryIsComplete()
    {
        Assert.That(File.Exists(Chapter2PanicLibraryAssetPath), Is.True, "The runtime Resources panic library asset should be built.");

        string assetText = File.ReadAllText(Chapter2PanicLibraryAssetPath);
        Assert.That(Regex.Matches(assetText, @"\bcharacterId:").Count, Is.EqualTo(PanicRosterCharacters.Length));

        for (int characterIndex = 0; characterIndex < PanicRosterCharacters.Length; characterIndex++)
        {
            string character = PanicRosterCharacters[characterIndex];
            Assert.That(assetText, Does.Contain("characterId: " + character), $"Runtime asset should contain {character}.");
            Assert.That(File.Exists(Path.Combine(AnimationLibraryPath, character, "manifest.json")), Is.True, $"{character} should have a manifest.");

            for (int actionIndex = 0; actionIndex < RequiredPanicActions.Length; actionIndex++)
            {
                PanicActionSpec action = RequiredPanicActions[actionIndex];
                Assert.That(assetText, Does.Contain(action.AssetField + ":"), $"Runtime asset should contain {action.AssetField} arrays.");
            }

            string handsUpFramesFolder = Path.Combine(GuestArtRoot, PanicRosterGuestFolders[characterIndex], "panic", "panic_hands_up", "frames");
            Assert.That(Directory.Exists(handsUpFramesFolder), Is.True, $"{PanicRosterGuestFolders[characterIndex]} should have generated panic_hands_up frames.");
            Assert.That(Directory.GetFiles(handsUpFramesFolder, "*.png").Length, Is.EqualTo(4), $"{PanicRosterGuestFolders[characterIndex]} should have exactly four generated hands-up frames.");

            string firstGuestFrameMeta = Path.Combine(handsUpFramesFolder, $"01_{PanicRosterGuestFolders[characterIndex]}_panic_hands_up.png.meta");
            Assert.That(File.Exists(firstGuestFrameMeta), Is.True, $"{PanicRosterGuestFolders[characterIndex]} should have imported generated hands-up sprite metadata.");
            Assert.That(assetText, Does.Contain(ReadGuid(firstGuestFrameMeta)), $"Runtime asset should reference the generated {PanicRosterGuestFolders[characterIndex]} panic_hands_up sprites.");

            string clipPath = Path.Combine(Chapter2PanicClipRoot, PanicRosterClipFolders[characterIndex], $"{PanicRosterClipFolders[characterIndex]}_PanicHandsUp.anim");
            Assert.That(File.Exists(clipPath), Is.True, $"{PanicRosterClipFolders[characterIndex]} should have one saved PanicHandsUp AnimationClip.");

            string clipText = File.ReadAllText(clipPath);
            Assert.That(clipText, Does.Contain("m_SampleRate: 12"), $"{clipPath} should play at the panic frame rate.");
            Assert.That(clipText, Does.Contain("classID: 212"), $"{clipPath} should animate SpriteRenderer sprites like normal character clips.");
            Assert.That(clipText, Does.Not.Contain("classID: 114"), $"{clipPath} should not animate an extra UI Image curve over the SpriteRenderer frames.");
            Assert.That(clipText, Does.Contain(ReadGuid(firstGuestFrameMeta)), $"{clipPath} should reference generated guest-local frame sprites.");

            string guestFolder = PanicRosterGuestFolders[characterIndex];
            string clipFolder = PanicRosterClipFolders[characterIndex];
            string generatedPopFramesFolder = Path.Combine(GuestArtRoot, guestFolder, "panic", OptionalPanicPopAction, "frames");
            string legacyPopFramesFolder = Path.Combine(GuestArtRoot, guestFolder, guestFolder + "panic");
            string popFramesFolder = Directory.Exists(generatedPopFramesFolder)
                ? generatedPopFramesFolder
                : legacyPopFramesFolder;
            Assert.That(Directory.Exists(popFramesFolder), Is.True, $"{guestFolder} should have eight panic_pop source frames.");
            Assert.That(Directory.GetFiles(popFramesFolder, "*.png").Length, Is.EqualTo(8), $"{guestFolder} should have exactly eight panic_pop source frames.");

            string[] popFrameMetas = Directory.GetFiles(popFramesFolder, "*.png.meta");
            System.Array.Sort(popFrameMetas, System.StringComparer.OrdinalIgnoreCase);
            string firstPopFrameMeta = popFrameMetas[0];
            Assert.That(File.Exists(firstPopFrameMeta), Is.True, $"{guestFolder} panic_pop should have imported sprite metadata.");
            Assert.That(assetText, Does.Contain("panicPop:"), "Runtime asset should serialize panicPop arrays.");
            Assert.That(assetText, Does.Contain(ReadGuid(firstPopFrameMeta)), $"Runtime asset should reference {guestFolder} panic_pop sprites.");

            string popClipPath = Path.Combine(Chapter2PanicClipRoot, clipFolder, $"{clipFolder}_PanicPop.anim");

            if (File.Exists(popClipPath))
            {
                string popClipText = File.ReadAllText(popClipPath);
                Assert.That(popClipText, Does.Contain("m_SampleRate: 12"), $"{popClipPath} should play at the panic frame rate.");
                Assert.That(popClipText, Does.Contain("classID: 212"), $"{popClipPath} should animate SpriteRenderer sprites like normal character clips.");
                Assert.That(popClipText, Does.Not.Contain("classID: 114"), $"{popClipPath} should not animate an extra UI Image curve over the SpriteRenderer frames.");
                Assert.That(popClipText, Does.Contain(ReadGuid(firstPopFrameMeta)), $"{popClipPath} should reference generated {guestFolder} panic_pop sprites.");
            }

            string[] runClipSuffixes =
            {
                "PanicRunDown",
                "PanicRunLeft",
                "PanicRunRight",
                "PanicRunUp"
            };

            for (int clipIndex = 0; clipIndex < runClipSuffixes.Length; clipIndex++)
            {
                string runClipPath = Path.Combine(Chapter2PanicClipRoot, PanicRosterClipFolders[characterIndex], $"{PanicRosterClipFolders[characterIndex]}_{runClipSuffixes[clipIndex]}.anim");
                Assert.That(File.Exists(runClipPath), Is.True, $"{PanicRosterClipFolders[characterIndex]} should have a saved {runClipSuffixes[clipIndex]} clip.");

                string runClipText = File.ReadAllText(runClipPath);
                Assert.That(runClipText, Does.Contain("m_SampleRate: 12"), $"{runClipPath} should play at the panic frame rate.");
                Assert.That(runClipText, Does.Contain("classID: 212"), $"{runClipPath} should animate SpriteRenderer sprites like normal character clips.");
                Assert.That(runClipText, Does.Not.Contain("classID: 114"), $"{runClipPath} should not animate an extra UI Image curve over the SpriteRenderer frames.");
                Assert.That(runClipText, Does.Contain("m_LoopTime: 1"), $"{runClipPath} should loop while a guest runs in that direction.");
            }

            for (int actionIndex = 0; actionIndex < RemovedPanicActions.Length; actionIndex++)
            {
                string removedFolder = Path.Combine(GuestArtRoot, PanicRosterGuestFolders[characterIndex], "panic", RemovedPanicActions[actionIndex]);
                Assert.That(Directory.Exists(removedFolder), Is.False, $"{removedFolder} should not be kept after replacing the bad generated panic actions.");
            }
        }
    }

    [Test]
    public void Chapter2GuestPanicBeginStopRestoresActorState()
    {
        GameObject root = new GameObject("Chapter2PanicTestRoot");
        GameObject actor = new GameObject("Guest1");
        Texture2D texture = null;
        Sprite originalSprite = null;

        try
        {
            actor.transform.SetParent(root.transform);
            actor.transform.position = new Vector3(1.25f, 2.5f, -0.75f);
            actor.transform.localScale = new Vector3(1.2f, 0.9f, 1f);

            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            });
            texture.Apply();
            originalSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0f));

            SpriteRenderer renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sprite = originalSprite;
            Animator animator = actor.AddComponent<Animator>();
            animator.enabled = true;
            NPCWaypointMover waypointMover = actor.AddComponent<NPCWaypointMover>();
            waypointMover.enabled = true;
            Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1f;
            body.linearVelocity = new Vector2(0.5f, -0.25f);

            ActorRoomState actorState = actor.AddComponent<ActorRoomState>();
            actorState.SetCurrentRoom("Drawing Room");
            actorState.SetAvailableInCurrentChapter(true);
            actorState.SetVisibleByChapterState(true);
            actorState.SetInteractable(true);
            actorState.SetSeated(true);

            root.AddComponent<Chapter2GuestSearchController>();
            Chapter2GuestPanicController panic = root.AddComponent<Chapter2GuestPanicController>();

            Vector3 originalPosition = actor.transform.position;
            Vector3 originalLocalScale = actor.transform.localScale;
            RigidbodyType2D originalBodyType = body.bodyType;
            float originalGravityScale = body.gravityScale;
            Vector2 originalBodyVelocity = body.linearVelocity;
            Vector2 originalBodyPosition = body.position;

            panic.BeginPanic();

            Assert.That(panic.IsRunning, Is.True, "BeginPanic should start playback when approved Resources frames exist.");
            Assert.That(actorState.IsInteractable, Is.False, "Panic should make guests non-interactable.");
            Assert.That(actorState.IsSeated, Is.False, "Panic should stand guests up temporarily.");
            Assert.That(animator.enabled, Is.False, "Panic should disable authored animators while sprite clips play.");
            Assert.That(waypointMover.enabled, Is.False, "Panic should disable guest movement drivers that can overwrite panic offsets.");
            Assert.That(actor.transform.position, Is.Not.EqualTo(originalPosition), "Panic should translate the guest left/right while the run animation sequence is active.");
            Assert.That(actor.transform.localScale, Is.Not.EqualTo(originalLocalScale), "Panic should normalize replacement sprite scale against the original guest sprite.");
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Kinematic), "Panic should take Rigidbody2D authority away from normal physics.");
            Assert.That(body.gravityScale, Is.Zero, "Panic should prevent Rigidbody2D gravity from fighting panic movement.");
            Assert.That(body.position, Is.Not.EqualTo(originalBodyPosition), "Panic should move Rigidbody2D-backed guest actors, not just their sprite.");

            panic.StopPanic();

            Assert.That(panic.IsRunning, Is.False);
            Assert.That(renderer.sprite, Is.EqualTo(originalSprite));
            Assert.That(animator.enabled, Is.True);
            Assert.That(waypointMover.enabled, Is.True);
            Assert.That(actorState.CurrentRoomId, Is.EqualTo("Drawing Room"));
            Assert.That(actorState.IsVisibleByChapterState, Is.True);
            Assert.That(actorState.IsInteractable, Is.True);
            Assert.That(actorState.IsSeated, Is.True);
            Assert.That(actor.transform.position, Is.EqualTo(originalPosition));
            Assert.That(actor.transform.localScale, Is.EqualTo(originalLocalScale));
            Assert.That(body.bodyType, Is.EqualTo(originalBodyType));
            Assert.That(body.gravityScale, Is.EqualTo(originalGravityScale));
            Assert.That(body.linearVelocity, Is.EqualTo(originalBodyVelocity));
            Assert.That(body.position, Is.EqualTo(originalBodyPosition));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(originalSprite);
            Object.DestroyImmediate(texture);
        }
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
        Assert.That(guestSearchText, Does.Contain("HiddenGuestConversationSpec"));
        Assert.That(guestSearchText, Does.Contain("Miss Isolde Wren"));
        Assert.That(guestSearchText, Does.Contain("Professor Lucien Vale"));
        Assert.That(guestSearchText, Does.Contain("Ask supper preference"));
        Assert.That(guestSearchText, Does.Contain("Ask drink preference"));
        Assert.That(guestSearchText, Does.Contain("Ask smoke preference"));
        Assert.That(guestSearchText, Does.Contain("Comfort and send to Dining Room"));
        Assert.That(guestSearchText, Does.Contain("AreAllPreferencesRecorded"));
        Assert.That(guestSearchText, Does.Match(@"\bShowButlerMealAsk\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bShowGuestMealReply\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bShowButlerSmokeAsk\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bShowGuestSmokeReply\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bShowButlerSpiritsAsk\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bShowGuestSpiritsReply\s*\("));
        Assert.That(guestSearchText, Does.Not.Match(@"\bChooseMealPreference\s*\("));
        Assert.That(guestSearchText, Does.Not.Match(@"\bChooseSmokingPreference\s*\("));
        Assert.That(guestSearchText, Does.Match(@"\bFinishGuestConversation\s*\("));
        Assert.That(guestSearchText, Does.Contain("MarkGuestFound(GetGuestIdForOrderList(guest))"));
        Assert.That(guestSearchText, Does.Contain("HandleGuestSearchProgressChanged()"));
        Assert.That(guestSearchText, Does.Contain("HandleAllGuestsFound()"));
    }

    [Test]
    public void Chapter2GuestConversationsResumeAfterInterruptedLineWithoutRepeatingAudio()
    {
        string controllerText = File.ReadAllText(Chapter2ControllerPath);
        string guestSearchText = File.ReadAllText(Chapter2GuestSearchControllerPath);
        string showConversationBody = ExtractMethodBody(controllerText, "public void ShowGuestConversation");
        string controllerRoomChangedBody = ExtractMethodBody(controllerText, "private void HandleCurrentRoomChanged");
        string startConversationBody = ExtractMethodBody(guestSearchText, "public bool TryStartGuestConversation");
        string roomChangedBody = ExtractMethodBody(guestSearchText, "private void HandleCurrentRoomChanged");
        string resumeAfterRoomChangeBody = ExtractMethodBody(guestSearchText, "private IEnumerator ResumeActiveConversationAfterRoomChange");
        string resumeForRoomBody = ExtractMethodBody(guestSearchText, "private bool TryResumeActiveConversationForRoom");
        string showResumeBody = ExtractMethodBody(guestSearchText, "private bool TryShowActiveConversationResumeState");
        string finishBody = ExtractMethodBody(guestSearchText, "private void FinishGuestConversation");

        Assert.That(guestSearchText, Does.Contain("enum GuestConversationResumeStep"), "Guest conversations need an explicit cursor for interrupted lines.");
        Assert.That(guestSearchText, Does.Contain("AwaitPreferencePrompt"));
        Assert.That(guestSearchText, Does.Contain("AwaitMealReply"));
        Assert.That(guestSearchText, Does.Contain("AwaitSmokeReply"));
        Assert.That(guestSearchText, Does.Contain("AwaitSpiritsReply"));
        Assert.That(guestSearchText, Does.Contain("AwaitSendToDiningPrompt"));
        Assert.That(guestSearchText, Does.Contain("AwaitExitToDiningCompletion"));
        Assert.That(guestSearchText, Does.Contain("ShowPreferenceChoices"));
        Assert.That(guestSearchText, Does.Contain("AreAllPreferencesRecorded"));
        Assert.That(startConversationBody, Does.Contain("TryShowActiveConversationResumeState(guest)"), "Clicking the same active guest should resume after the interrupted line instead of restarting.");
        Assert.That(roomChangedBody, Does.Contain("StartCoroutine(ResumeActiveConversationAfterRoomChange(roomName))"), "Room re-entry should resume active conversation state automatically.");
        Assert.That(resumeAfterRoomChangeBody, Does.Contain("yield return null"), "Resume should wait one frame so subtitle/audio room cleanup runs first.");
        Assert.That(resumeAfterRoomChangeBody, Does.Contain("TryResumeActiveConversationForRoom(roomName)"));
        Assert.That(resumeForRoomBody, Does.Contain("SameRoom(actorState.CurrentRoomId, roomName)"), "Conversation should only resume when the Butler re-enters the active guest's room.");
        Assert.That(resumeForRoomBody, Does.Contain("SetGuestConversationInputEnabled(false)"), "Restored choices should keep normal movement paused while the player answers.");
        Assert.That(showResumeBody, Does.Not.Contain("ShowGuestConversationWithSubtitle"), "Interrupted lines must not replay audio when resuming.");
        Assert.That(showResumeBody, Does.Not.Contain("ShowResumeChoice"));
        Assert.That(showResumeBody, Does.Contain("ShowPreferenceChoices"), "Preference interruptions should restore the remaining choice menu without replaying old lines.");
        Assert.That(finishBody, Does.Contain("activeConversationResumeStep = GuestConversationResumeStep.None"), "Finishing a guest conversation should clear the resume cursor.");
        Assert.That(controllerRoomChangedBody, Does.Contain("currentPhase == Chapter2Phase.GuestSearch"));
        Assert.That(controllerRoomChangedBody, Does.Contain("SetGuestConversationInputEnabled(true)"), "Leaving a guest-search room should restore movement until a visible interrupted conversation resumes.");
        Assert.That(showConversationBody, Does.Contain("SetDialogueChoicesInteractable(true)"), "Non-audio resume states should restore clickable choices immediately.");
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
        Assert.That(guestSearchText, Does.Match(@"(?s)\bStageGuestForDiningRoomReveal\s*\([^)]*\)\s*\{.*SetCurrentRoom\(targetRoom\).*SetVisibleByChapterState\(false\).*SetSeated\(true\).*ApplyState\(\)"), "Spoken-to guests should leave the search room and wait hidden/seated for the Dining Room reveal.");
        Assert.That(guestSearchText, Does.Match(@"(?s)\bHideGuestForDiningRoomTransfer\s*\([^)]*\)\s*\{.*SetVisibleByChapterState\(false\).*ApplyState\(\)"), "Guests should be hidden from their search rooms before being moved to Dining Room seats.");
        Assert.That(guestSearchText, Does.Match(@"(?s)\bGetGuestsInDiningSeatOrder\s*\([^)]*\)\s*\{.*new List<GuestSearchEntry>\(\).*orderedGuests\.Add\(guests\[i\]\).*orderedGuests\.Sort\(CompareGuestIdentity\)"), "Dining Room seats must use canonical guest identity order so normal playthrough matches the Chapter 3 debug skip regardless of discovery order.");
        Assert.That(guestSearchText, Does.Not.Match(@"(?s)\bGetGuestsInDiningSeatOrder\s*\([^)]*\)\s*\{.*GetFoundGuestsInOrder\(\)"), "Dining seating should not depend on the order the player found guests.");
        Assert.That(guestSearchText, Does.Match(@"(?s)\bSeatGuestsInDiningRoom\s*\(\s*List<GuestSearchEntry>\s+guestsToSeat\s*\)\s*\{.*HideGuestForDiningRoomTransfer\(guest\).*SetCurrentRoom\(diningSeat\.RoomId\).*PlaceAt\(diningSeat\.transform\).*SetVisibleByChapterState\(true\).*ResetAnimatorToAuthoredState\(\).*SetSeated\(true\).*ApplyState\(\)"), "Dining seating should hide, assign Dining Room, move, reset any search/panic facing state, mark seated, then restore visibility through ActorRoomState.");
        Assert.That(File.ReadAllText("Assets/Scripts/Story/ActorRoomState.cs"), Does.Match(@"(?s)\bResetAnimatorToAuthoredState\s*\([^)]*\)\s*\{.*animator\.Rebind\(\).*animator\.Update\(0f\)"), "Normal playthrough Dining Room reveal should reset guests to the same authored/default animator pose used by debug skip before applying seated.");
        Assert.That(guestSearchText, Does.Not.Contain("ApplyDiningRoomGuestSorting"), "Dining seating should preserve the scene-authored guest/chair/table sorting instead of overriding it at runtime.");
        Assert.That(guestSearchText, Does.Not.Contain("diningGuestSortingOrderBase"), "Dining guest sorting should stay authored on the scene sprites.");
        Assert.That(guestSearchText, Does.Contain("SetCurrentRoom(diningSeat.RoomId)"));
        Assert.That(guestSearchText, Does.Contain("SetSeated(false)"), "Guests should still stand while being searched/clicked before the dining reveal.");
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
    public void Chapter3DebugSkipStagesFoundGuestsInDiningRoom()
    {
        string managerText = File.ReadAllText(ChapterManagerPath);
        string controllerText = File.ReadAllText(Chapter2ControllerPath);
        string guestSearchText = File.ReadAllText(Chapter2GuestSearchControllerPath);
        string managerSkipBody = ExtractMethodBody(managerText, "public void SkipToChapter3ForTesting");
        string completeRoutineBody = ExtractMethodBody(managerText, "private IEnumerator CompleteChapterRoutine");
        string debugStageBody = ExtractMethodBody(guestSearchText, "public void DebugStageAllGuestsFoundForChapter3Skip");

        Assert.That(managerSkipBody, Does.Contain("currentChapterId = Chapter3PendingId"), "The Chapter 3 debug skip should preserve the known-good direct Chapter 3 staging path.");
        Assert.That(completeRoutineBody, Does.Match(@"(?s)currentChapterId\s*=\s*cleanNextChapterId.*displayedTitle\s*=\s*GetChapterTitle\(cleanNextChapterId\)"), "The normal chapter handoff should apply the same Chapter 3 pending id that debug skip uses.");
        Assert.That(controllerText, Does.Match(@"(?s)\bDebugSkipToChapter3ForTesting\s*\([^)]*\)\s*\{.*MoveToDiningRoomForDebugSkip\s*\(\).*guestSearch\.Initialize\(this\).*guestSearch\.DebugStageAllGuestsFoundForChapter3Skip\(\)"), "Chapter 3 debug skip should keep using the direct staging path that has correct dining orientations.");
        Assert.That(debugStageBody, Does.Match(@"(?s)AutoDiscoverGuestsIfNeeded\(\).*AutoAssignHideAnchorsIfNeeded\(\).*guests\.Sort\(CompareGuestIdentity\).*foundOrderCounter\+\+.*guest\.found\s*=\s*true.*SeatGuestsInDiningRoom\(GetFoundGuestsInOrder\(\)\)"), "Chapter 3 debug skip should discover guests, mark them found in canonical identity order, then seat them.");
        Assert.That(guestSearchText, Does.Match(@"(?s)\bSeatGuestsInDiningRoom\s*\(\s*List<GuestSearchEntry>\s+guestsToSeat\s*\)\s*\{.*SetCurrentRoom\(diningSeat\.RoomId\).*SetVisibleByChapterState\(true\).*ResetAnimatorToAuthoredState\(\).*SetSeated\(true\).*ApplyState\(\)"), "Chapter 3 debug skip seating should make guests visible while preserving authored Dining Room sorting.");
    }

    [Test]
    public void Chapter2SevenPmDebugSkipRunsClockStrikeObjectiveBeforeDiningReveal()
    {
        string settingsText = File.ReadAllText(RuntimeSettingsMenuPath);
        string managerText = File.ReadAllText(ChapterManagerPath);
        string controllerText = File.ReadAllText(Chapter2ControllerPath);
        string guestSearchText = File.ReadAllText(Chapter2GuestSearchControllerPath);
        string managerSkipBody = ExtractMethodBody(managerText, "public void SkipToSevenPMForTesting");
        string controllerSkipBody = ExtractMethodBody(controllerText, "public void DebugSkipToSevenPMForTesting");
        string debugStageBody = ExtractMethodBody(guestSearchText, "public void DebugStageAllGuestsFoundForSevenPMSkip");

        Assert.That(settingsText, Does.Contain("Button_SkipToSevenPM"));
        Assert.That(settingsText, Does.Contain("\"Skip to 7:00 PM\""));
        Assert.That(settingsText, Does.Contain("manager.SkipToSevenPMForTesting()"));
        Assert.That(managerSkipBody, Does.Contain("currentChapterId = Chapter2Id"), "The 7:00 PM skip should stay in Chapter 2, not jump to Chapter 3.");
        Assert.That(managerSkipBody, Does.Contain("displayedTitle = Chapter2Title"));
        Assert.That(managerSkipBody, Does.Contain("chapter2Controller.DebugSkipToSevenPMForTesting(this)"));
        Assert.That(managerSkipBody, Does.Not.Contain("Chapter3PendingId"), "The 7:00 PM skip must not use the Chapter 3 pending id.");
        Assert.That(controllerSkipBody, Does.Contain("SetDinnerClockAndStop()"));
        Assert.That(controllerSkipBody, Does.Contain("guestSearch.DebugStageAllGuestsFoundForSevenPMSkip()"));
        Assert.That(controllerSkipBody, Does.Contain("BeginDiningRoomObjective()"), "The skip should run the same clock-strike / go-to-Dining-Room objective sequence as normal play.");
        Assert.That(controllerSkipBody, Does.Not.Contain("MoveToDiningRoomForDebugSkip"), "The 7:00 PM skip should not teleport directly to the dining table.");
        Assert.That(controllerSkipBody, Does.Not.Contain("DebugStageAllGuestsFoundForChapter3Skip"), "The 7:00 PM skip should not use the direct Chapter 3 seating path.");
        Assert.That(debugStageBody, Does.Match(@"(?s)guests\.Sort\(CompareGuestIdentity\).*guest\.found\s*=\s*true.*FillDefaultPreferences\(guest\).*StageGuestForDiningRoomReveal\(guest\)"), "The 7:00 PM skip should mark every guest found and stage them hidden for the Dining Room reveal.");
        Assert.That(debugStageBody, Does.Not.Contain("SeatGuestsInDiningRoom"), "Guests should only be seated after the player reaches the Dining Room objective.");
        Assert.That(controllerText, Does.Contain("The clock strikes 7:00. Go to the Dining Room."));
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
        Assert.That(hudText, Does.Contain("ClockStrikeSecondHandName"), "The 7:00 clock-strike close-up should draw a runtime second hand.");
        Assert.That(hudText, Does.Contain("ClockStrikeSecondTailName"), "The second hand should have a visible counterweight so it reads as a designed hand, not a plain debug line.");
        Assert.That(hudText, Does.Contain("ClockStrikeSecondTicksPerSecond"), "The strike close-up second hand should tick independently of gameplay clock alignment.");
        Assert.That(hudText, Does.Match(@"(?s)\bUpdate\s*\([^)]*\)\s*\{.*RefreshClockStrikeHands\(\)"), "The second hand must keep ticking while the strike graphic is visible.");
        Assert.That(hudText, Does.Match(@"(?s)\bShowClockStrike\s*\([^)]*\)\s*\{.*clockStrikeStartedAt\s*=\s*Time\.unscaledTime.*RefreshClockStrikeHands\(true\)"), "Showing the strike graphic should reset and immediately render the ticking second hand.");
        Assert.That(hudText, Does.Match(@"(?s)clockStrikeRect\.sizeDelta\s*=\s*new Vector2\(880f,\s*900f\)"), "The clock-strike graphic should be much larger and closer than the old 660x560 panel.");
        Assert.That(hudText, Does.Match(@"(?s)clockStrikeImageRect\.sizeDelta\s*=\s*new Vector2\(760f,\s*1350f\)"), "The grandfather clock sprite should be scaled up so the face reads as a close-up.");
        Assert.That(hudText, Does.Contain("RectMask2D"), "The enlarged clock sprite should be clipped by the close-up panel.");
        Assert.That(hudText, Does.Contain("SetClockStrikeHand(clockStrikeHourHand"), "The strike close-up should draw the stationary thicker hour hand.");
        Assert.That(hudText, Does.Contain("SetClockStrikeHand(clockStrikeMinuteHand"), "The strike close-up should draw the stationary thicker minute hand.");
        Assert.That(hudText, Does.Contain("SetClockStrikeHand(clockStrikeSecondHand"), "The strike close-up should animate the second hand around the clock.");
        Assert.That(hudText, Does.Contain("ClockStrikeFaceCenterYOffset = 0.267f"), "The close-up hands should be centered on the baked clock-face pivot.");
        Assert.That(hudText, Does.Contain("ClockStrikeHandsDiameterScale = 0.22f"), "The close-up hands should stay inside the clock-face circle.");
        Assert.That(hudText, Does.Contain("radius * 0.45f"), "The minute hand should fit within the clock-face circle.");
        Assert.That(hudText, Does.Contain("radius * 0.52f"), "The ticking second hand should fit within the clock-face circle.");
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

    private static string ReadGuid(string metaPath)
    {
        string metaText = File.ReadAllText(metaPath);
        Match match = Regex.Match(metaText, @"(?m)^guid: ([0-9a-f]{32})$");

        Assert.That(match.Success, Is.True, $"{metaPath} should contain a Unity GUID.");
        return match.Groups[1].Value;
    }

    private static string ExtractMethodBody(string sourceText, string methodName)
    {
        int methodIndex = sourceText.IndexOf(methodName, System.StringComparison.Ordinal);
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

    private readonly struct PanicActionSpec
    {
        public readonly string ActionId;
        public readonly string AssetField;
        public readonly int ExpectedFrameCount;

        public PanicActionSpec(string actionId, string assetField, int expectedFrameCount)
        {
            ActionId = actionId;
            AssetField = assetField;
            ExpectedFrameCount = expectedFrameCount;
        }
    }
}
