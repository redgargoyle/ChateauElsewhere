#if UNITY_EDITOR
using System;
using System.IO;
using Chateau.Architecture;
using NUnit.Framework;

public sealed class ArchitectureFoundationTests
{
    private enum TestState
    {
        Idle,
        Running,
        Complete
    }

    [Test]
    public void StateMachineRejectsUndeclaredTransition()
    {
        StateMachine<TestState> machine = new StateMachine<TestState>(TestState.Idle)
            .Allow(TestState.Idle, TestState.Running)
            .Allow(TestState.Running, TestState.Complete);

        Assert.That(machine.TryTransition(TestState.Complete), Is.False);
        Assert.That(machine.Current, Is.EqualTo(TestState.Idle));
    }

    [Test]
    public void StateMachinePublishesValidTransition()
    {
        StateMachine<TestState> machine = new StateMachine<TestState>(TestState.Idle)
            .Allow(TestState.Idle, TestState.Running);
        TestState observedFrom = TestState.Complete;
        TestState observedTo = TestState.Complete;
        string observedReason = null;

        machine.Transitioned += (from, to, reason) =>
        {
            observedFrom = from;
            observedTo = to;
            observedReason = reason;
        };

        machine.TransitionOrThrow(TestState.Running, "test");

        Assert.That(machine.Current, Is.EqualTo(TestState.Running));
        Assert.That(observedFrom, Is.EqualTo(TestState.Idle));
        Assert.That(observedTo, Is.EqualTo(TestState.Running));
        Assert.That(observedReason, Is.EqualTo("test"));
    }

    [Test]
    public void ValidationReportTracksErrorsAndWarnings()
    {
        ValidationReport report = new ValidationReport();
        report.AddWarning("warning");
        report.AddError("error");

        Assert.That(report.HasErrors, Is.True);
        Assert.That(report.ErrorCount, Is.EqualTo(1));
        Assert.That(report.WarningCount, Is.EqualTo(1));
        Assert.That(report.Messages.Count, Is.EqualTo(2));
    }

    [Test]
    public void GameRootNeverRepairsDependenciesAtRuntime()
    {
        string rootText = File.ReadAllText("Assets/_Chateau/Runtime/Core/GameRoot.cs");

        Assert.That(rootText, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(rootText, Does.Not.Contain("FindFirstObjectByType"));
        Assert.That(rootText, Does.Not.Contain("GameObject.Find"));
        Assert.That(rootText, Does.Not.Contain("new GameObject"));
        Assert.That(rootText, Does.Not.Contain("AddComponent<"));
        Assert.That(rootText, Does.Not.Contain("Resources.Load"));
        Assert.That(rootText, Does.Not.Contain("RuntimeInitializeOnLoadMethod"));
    }

    [Test]
    public void MajorManagersEnterTheArchitectureFamiliesWithoutChangingScriptFiles()
    {
        Assert.That(File.ReadAllText("Assets/Scripts/Story/ChapterManager.cs"), Does.Contain("Chateau.Architecture.GameServiceBase"));
        Assert.That(File.ReadAllText("Assets/Scripts/Story/ChapterClock.cs"), Does.Contain("Chateau.Architecture.GameServiceBase"));
        Assert.That(File.ReadAllText("Assets/Scripts/Navigation/RoomNavigationManager.cs"), Does.Contain("Chateau.Architecture.GameServiceBase"));
        Assert.That(File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs"), Does.Contain("Chateau.Architecture.ChapterControllerBase"));
        Assert.That(File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs"), Does.Contain("Chateau.Architecture.ChapterControllerBase"));
    }

    [Test]
    public void ProvenDeadRuntimeScriptsStayPruned()
    {
        Assert.That(File.Exists("Assets/Scripts/NewBehaviourScript.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/PickupObject.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/Story/GameClockHandsDisplay.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/Story/GameClockHandsDisplay.cs.meta"), Is.False);
    }

    [Test]
    public void RuntimeNavigationBootstrapStaysPruned()
    {
        Assert.That(File.Exists("Assets/Scripts/Navigation/RoomNavigationBootstrap.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/Navigation/RoomNavigationBootstrap.cs.meta"), Is.False);
    }

    [Test]
    public void ChapterStackIsSerializedInsteadOfRepairedAtRuntime()
    {
        string managerText = File.ReadAllText("Assets/Scripts/Story/ChapterManager.cs");
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        string playerInstanceDocument = ExtractDocument(sceneText, "--- !u!1001 &81962841");

        Assert.That(managerText, Does.Not.Contain("BootstrapChapterManagerForGameplay"));
        Assert.That(managerText, Does.Not.Contain("ChapterManager_Runtime"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterClock>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterEventScheduler>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterIntroUI>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<Chapter1ArrivalController>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterManager>"));
        Assert.That(managerText, Does.Not.Contain("AddComponent<Chapter2Controller>"));
        Assert.That(managerText, Does.Not.Contain("ResolveChapter2Controller"));
        Assert.That(managerText, Does.Not.Contain("ResolveReferences"));
        Assert.That(managerText, Does.Not.Contain("ResolvePlayerReference"));
        Assert.That(managerText, Does.Not.Contain("FindPlayerInput"));
        Assert.That(managerText, Does.Not.Contain("GameObject.Find"));
        Assert.That(managerText, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(managerText, Does.Not.Contain("FindObjectsByType"));
        Assert.That(managerText, Does.Not.Contain("Canvas_ChapterDebug"));
        Assert.That(sceneText, Does.Contain("playerInput: {fileID: 81962842}"));
        Assert.That(sceneText, Does.Contain("chapter2Controller: {fileID: 3301000006}"));
        string legacyControllerOverride = "target: {fileID: 7110128061864666233, guid: 3c2a23f8d68b2d05cace0338fba9a1d1, type: 3}\n      propertyPath: m_Enabled\n      value: 0";
        string legacyMovementOverride = "target: {fileID: 7656683542599176262, guid: 3c2a23f8d68b2d05cace0338fba9a1d1, type: 3}\n      propertyPath: m_Enabled\n      value: 0";
        Assert.That(playerInstanceDocument, Does.Contain(legacyControllerOverride));
        Assert.That(playerInstanceDocument, Does.Contain(legacyMovementOverride));
        Assert.That(CountOccurrences(sceneText, legacyControllerOverride), Is.EqualTo(1));
        Assert.That(CountOccurrences(sceneText, legacyMovementOverride), Is.EqualTo(1));
        Assert.That(managerText, Does.Contain("ChapterManager requires its serialized PointClickPlayerMovement."));
        Assert.That(managerText, Does.Contain("ChapterManager requires its serialized Chapter2Controller."));

        string chapter2Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs");
        Assert.That(chapter2Text, Does.Not.Contain("AddComponent<Chapter2InteractionHUD>"));
        Assert.That(chapter2Text, Does.Not.Contain("AddComponent<Chapter2MonsterStingerController>"));
        Assert.That(chapter2Text, Does.Not.Contain("AddComponent<Chapter2GuestPanicController>"));
        Assert.That(chapter2Text, Does.Not.Contain("AddComponent<Chapter2GuestSearchController>"));
    }

    [Test]
    public void Chapter2FeatureControllersAreSerializedOnce()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(CountOccurrences(sceneText, "guid: 684198ee76c12a66cb4335c3ab64b1bc"), Is.EqualTo(1));
        Assert.That(CountOccurrences(sceneText, "guid: aa4143ddf6de4b6b9b8c1edc0f9e2a31"), Is.EqualTo(1));
        Assert.That(CountOccurrences(sceneText, "guid: 5daaf625b50c2b1048154975a147950a"), Is.EqualTo(1));
        Assert.That(sceneText, Does.Contain("monsterStinger: {fileID: 3301000007}"));
        Assert.That(sceneText, Does.Contain("guestPanic: {fileID: 3301000008}"));
        Assert.That(sceneText, Does.Contain("guestSearch: {fileID: 3301000009}"));
    }

    [Test]
    public void Chapter2DependenciesAreSerializedWithoutRepairSearches()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        string chapter2Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs");
        string chapter2Document = ExtractDocument(sceneText, "--- !u!114 &3301000006");
        string hostGameObjectDocument = ExtractDocument(sceneText, "--- !u!1 &2099709257");
        string hostTransformDocument = ExtractDocument(sceneText, "--- !u!4 &2099709258");
        string clockGameObjectDocument = ExtractDocument(sceneText, "--- !u!1 &3301000010");
        string clockTransformDocument = ExtractDocument(sceneText, "--- !u!4 &3301000011");
        string clockSourceDocument = ExtractDocument(sceneText, "--- !u!82 &3301000012");
        string clockBindingDocument = ExtractDocument(sceneText, "--- !u!114 &3301000013");

        Assert.That(sceneText, Does.Contain("chapterManager: {fileID: 3301000004}"));
        Assert.That(sceneText, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(sceneText, Does.Contain("introUI: {fileID: 3301000003}"));
        Assert.That(sceneText, Does.Contain("chapterClock: {fileID: 3301000001}"));
        Assert.That(sceneText, Does.Contain("playerMovement: {fileID: 81962842}"));
        Assert.That(sceneText, Does.Contain("interactionHUD: {fileID: 3301000005}"));
        Assert.That(sceneText, Does.Contain("monsterStinger: {fileID: 3301000007}"));
        Assert.That(sceneText, Does.Contain("guestPanic: {fileID: 3301000008}"));
        Assert.That(sceneText, Does.Contain("guestSearch: {fileID: 3301000009}"));
        Assert.That(sceneText, Does.Contain("subtitleService: {fileID: 1878886995}"));
        Assert.That(sceneText, Does.Contain("speechService: {fileID: 1878886994}"));
        Assert.That(chapter2Document, Does.Contain("clockStrikeAudioSource: {fileID: 3301000012}"));
        Assert.That(chapter2Document, Does.Contain("clockStrikeVolumeBinding: {fileID: 3301000013}"));
        Assert.That(chapter2Document, Does.Contain("clockStrikeClip: {fileID: 8300000, guid: d7084eafa9124afcbcbf12529e08bc70, type: 3}"));
        Assert.That(hostGameObjectDocument, Does.Not.Contain("3301000012"), "The shared ChapterManager host must not own the clock source that the violin fallback can discover.");
        Assert.That(hostGameObjectDocument, Does.Not.Contain("3301000013"));
        Assert.That(CountOccurrences(hostTransformDocument, "- {fileID: 3301000011}"), Is.EqualTo(1));
        Assert.That(clockGameObjectDocument, Does.Contain("m_Name: Audio_Chapter2ClockStrike"));
        Assert.That(clockGameObjectDocument, Does.Contain("- component: {fileID: 3301000011}"));
        Assert.That(clockGameObjectDocument, Does.Contain("- component: {fileID: 3301000012}"));
        Assert.That(clockGameObjectDocument, Does.Contain("- component: {fileID: 3301000013}"));
        Assert.That(clockTransformDocument, Does.Contain("m_Father: {fileID: 2099709258}"));
        Assert.That(clockSourceDocument, Does.Contain("m_Resource: {fileID: 8300000, guid: d7084eafa9124afcbcbf12529e08bc70, type: 3}"));
        Assert.That(clockSourceDocument, Does.Contain("m_PlayOnAwake: 0"));
        Assert.That(clockSourceDocument, Does.Contain("m_Volume: 0.4"));
        Assert.That(clockSourceDocument, Does.Contain("Loop: 0"));
        Assert.That(clockBindingDocument, Does.Contain("audioSource: {fileID: 3301000012}"));
        Assert.That(clockBindingDocument, Does.Contain("channel: 1"));
        Assert.That(clockBindingDocument, Does.Contain("baseVolume: 0.4"));
        Assert.That(chapter2Text, Does.Not.Contain("ResolveReferences"));
        Assert.That(chapter2Text, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(chapter2Text, Does.Not.Contain("GameObject.Find(\"Player\")"));
        Assert.That(chapter2Text, Does.Not.Contain("GetComponent<Chapter"));
        Assert.That(chapter2Text, Does.Not.Contain("GetComponent<PointClickPlayerMovement>"));
        Assert.That(chapter2Text, Does.Contain("public override void ValidateConfiguration"));
        Assert.That(chapter2Text, Does.Contain("RegisterRoomChangeHandler();"));
    }

    [Test]
    public void Chapter1BindsSerializedGuestScaleOwners()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(sceneText, Does.Contain("guestRoomScaleApplier: {fileID: 86244178}"));
        Assert.That(sceneText, Does.Contain("calibration: {fileID: 1844861547}"));
        Assert.That(sceneText, Does.Contain("butlerScaleSource: {fileID: 81962842}"));

        string chapter1Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs");
        string applierText = File.ReadAllText("Assets/Scripts/Characters/GuestRoomScaleApplier.cs");
        Assert.That(chapter1Text, Does.Not.Contain("GuestRoomScaleApplier.EnsureInScene"));
        Assert.That(chapter1Text, Does.Not.Contain("new GameObject(\"GuestRoomScaleCalibration\")"));
        Assert.That(chapter1Text, Does.Not.Contain("AddComponent<GuestRoomScaleCalibration>"));
        Assert.That(applierText, Does.Not.Contain("EnsureInScene"));
        Assert.That(applierText, Does.Not.Contain("AddComponent<GuestRoomScaleApplier>"));
        Assert.That(applierText, Does.Not.Contain("FindAnyObjectByType<GuestRoomScaleCalibration>"));
        Assert.That(applierText, Does.Contain("AddComponent<GuestScaleParticipant>"));
    }

    [Test]
    public void Chapter1HudOwnerIsSerializedOnce()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(CountOccurrences(sceneText, "guid: a7a7a747ac7ae2fb48c9d60608ca3dc9"), Is.EqualTo(1));
        Assert.That(sceneText, Does.Contain("interactionHUD: {fileID: 3302000002}"));
        Assert.That(sceneText, Does.Contain("- component: {fileID: 3302000002}"));

        string chapter1Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs");
        Assert.That(chapter1Text, Does.Not.Contain("FindAnyObjectByType<Chapter1InteractionHUD>"));
        Assert.That(chapter1Text, Does.Not.Contain("AddComponent<Chapter1InteractionHUD>"));
        Assert.That(chapter1Text, Does.Not.Contain("createRuntimeHud"));
    }

    [Test]
    public void DialogueCoreServicesAreSerializedAndBound()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(sceneText, Does.Contain("subtitleService: {fileID: 1878886995}"));
        Assert.That(sceneText, Does.Contain("speechService: {fileID: 1878886994}"));
        Assert.That(sceneText, Does.Contain("lineBank: {fileID: 11400000, guid: 47d20ba9660546050951e9ea07a0b3da, type: 2}"));
        Assert.That(sceneText, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(sceneText, Does.Contain("playerMovement: {fileID: 81962842}"));

        string speechText = File.ReadAllText("Assets/Scripts/Audio/DialogueSpeechService.cs");
        string subtitleText = File.ReadAllText("Assets/Scripts/UI/SubtitleService.cs");
        string chapter1Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs");
        string chapter2Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs");
        Assert.That(speechText, Does.Not.Contain("DialogueSpeechService FindOrCreate"));
        Assert.That(subtitleText, Does.Not.Contain("SubtitleService FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("SubtitleService.FindOrCreate"));
        Assert.That(subtitleText, Does.Not.Contain("Resources.Load<SubtitleLineBank>"));
        Assert.That(subtitleText, Does.Not.Contain("lineBankResourcePath"));
        Assert.That(subtitleText, Does.Not.Contain("FindAnyObjectByType<RoomNavigationManager>"));
        Assert.That(subtitleText, Does.Not.Contain("ResolveReferences"));
        Assert.That(subtitleText, Does.Contain("PreparePresentation"));
        Assert.That(chapter1Text, Does.Not.Contain("DialogueSpeechService.FindOrCreate"));
        Assert.That(chapter1Text, Does.Not.Contain("SubtitleService.FindOrCreate"));
        Assert.That(chapter2Text, Does.Not.Contain("DialogueSpeechService.FindOrCreate"));
        Assert.That(chapter2Text, Does.Not.Contain("SubtitleService.FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("GuestVoiceLinePlayback.FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("SpeakingCharacterIndicator.FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("FindAnyObjectByType<PointClickPlayerMovement>"));
        Assert.That(speechText, Does.Not.Contain("previousInputEnabled"));
        Assert.That(speechText, Does.Contain("AcquireBlockedPlayerInput(speechToken)"));
        Assert.That(speechText, Does.Contain("ReleaseBlockedPlayerInput();"));
        Assert.That(CountOccurrences(sceneText, "speechService: {fileID: 1878886994}"), Is.EqualTo(3));
        Assert.That(CountOccurrences(sceneText, "subtitleService: {fileID: 1878886995}"), Is.EqualTo(4));
    }

    [Test]
    public void DialogueAuxiliaryOwnersAreSerializedOnce()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(CountOccurrences(sceneText, "guid: 9e13b0cd7a5f44a69fe0b75a2cb76123"), Is.EqualTo(1));
        Assert.That(CountOccurrences(sceneText, "guid: 9963bb0aa9d84cc7a8cb801c668a92ee"), Is.EqualTo(1));
        Assert.That(sceneText, Does.Contain("voicePlayback: {fileID: 1878887001}"));
        Assert.That(sceneText, Does.Contain("speakingIndicator: {fileID: 1878887002}"));
        Assert.That(CountOccurrences(sceneText, "speakingIndicator: {fileID: 1878887002}"), Is.EqualTo(2));
        Assert.That(sceneText, Does.Contain("audioSource: {fileID: 1878887000}"));
        Assert.That(sceneText, Does.Contain("audioVolumeBinding: {fileID: 1878887003}"));
        Assert.That(CountOccurrences(sceneText, "guid: 5161da2d2e1b408d859e3792f47407f4"), Is.EqualTo(3));
        Assert.That(sceneText, Does.Match(@"--- !u!114 &1878887003[\s\S]*?m_GameObject: \{fileID: 1878886993\}[\s\S]*?audioSource: \{fileID: 1878887000\}[\s\S]*?channel: 0[\s\S]*?baseVolume: 1"));
        Assert.That(sceneText, Does.Contain("catalog: {fileID: 11400000, guid: 147a8473c4c849c9908200b092d13691, type: 2}"));
        Assert.That(sceneText, Does.Contain("bubbleSprite: {fileID: 21300000, guid: b40c2d5917304c3e822fad1b6f3e5960, type: 3}"));

        string playbackText = File.ReadAllText("Assets/Scripts/Audio/GuestVoiceLinePlayback.cs");
        string indicatorText = File.ReadAllText("Assets/Scripts/UI/SpeakingCharacterIndicator.cs");
        string dialogueText = File.ReadAllText("Assets/Scripts/Audio/DialogueSpeechService.cs");
        string subtitleText = File.ReadAllText("Assets/Scripts/UI/SubtitleService.cs");
        string chapter2Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs");
        string chapterManagerText = File.ReadAllText("Assets/Scripts/Story/ChapterManager.cs");
        string settingsText = File.ReadAllText("Assets/Scripts/UI/RuntimeSettingsMenu.cs");
        Assert.That(playbackText, Does.Not.Contain("GuestVoiceLinePlayback FindOrCreate"));
        Assert.That(playbackText, Does.Not.Contain("EnsureAudioSource"));
        Assert.That(playbackText, Does.Not.Contain("GameAudioSettings.EnsureBinding(audioSource"));
        Assert.That(playbackText, Does.Contain("audioVolumeBinding.Configure(audioSource, GameAudioChannel.Dialogue, sourceBaseVolume)"));
        Assert.That(playbackText, Does.Not.Contain("Resources.Load<GuestVoiceLineCatalog>"));
        Assert.That(playbackText, Does.Not.Contain("catalogResourcePath"));
        Assert.That(playbackText, Does.Not.Contain("FindAnyObjectByType<RoomNavigationManager>"));
        Assert.That(playbackText, Does.Not.Contain("ResolveReferences"));
        Assert.That(indicatorText, Does.Not.Contain("SpeakingCharacterIndicator FindOrCreate"));
        Assert.That(indicatorText, Does.Not.Contain("HideAnyCurrent"));
        Assert.That(dialogueText, Does.Not.Contain("StopAnyCurrentSpeech"));
        Assert.That(subtitleText, Does.Not.Contain("HideAnyCurrent"));
        Assert.That(subtitleText, Does.Not.Contain("GuestVoiceLinePlayback.StopAnyCurrentLine"));
        Assert.That(chapter2Text, Does.Not.Contain("StopAnyCurrentSpeech"));
        Assert.That(playbackText, Does.Not.Contain("StopAnyCurrentLine"));
        Assert.That(chapterManagerText, Does.Not.Contain("FindAnyObjectByType<SubtitleService>"));
        Assert.That(chapterManagerText, Does.Not.Contain("GuestVoiceLinePlayback.StopAnyCurrentLine"));
        Assert.That(settingsText, Does.Not.Contain("GuestVoiceLinePlayback.StopAnyCurrentLine"));
        Assert.That(settingsText, Does.Not.Contain("FindAnyObjectByType<SubtitleService>"));
        Assert.That(chapterManagerText, Does.Match(@"StopActiveDialogueForDebugTransition\s*\(\s*\)[\s\S]*CancelQueuedSpeech\s*\(\s*\)[\s\S]*ClearAll\s*\(\s*\)"));
        Assert.That(sceneText, Does.Contain("speakingIndicator: {fileID: 1878887002}"));
        Assert.That(indicatorText, Does.Contain("new GameObject(SpriteObjectName)"), "Only the indicator's nested presentation child should remain lazy.");
    }

    [Test]
    public void RuntimeSettingsOwnerIsSerializedOnce()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(CountOccurrences(sceneText, "guid: 06d3a7eb4f7d428f9bc3e64b6c47f0b6"), Is.EqualTo(1));
        Assert.That(sceneText, Does.Contain("runtimeSettingsMenu: {fileID: 1878887112}"));
        Assert.That(sceneText, Does.Contain("m_Name: Canvas_RuntimeSettingsMenu"));
        Assert.That(sceneText, Does.Contain("m_SortingOrder: 10050"));
        Assert.That(sceneText, Does.Contain("m_ReferenceResolution: {x: 1366, y: 768}"));
        Assert.That(sceneText, Does.Contain("chapterManager: {fileID: 3301000004}"));
        Assert.That(sceneText, Does.Contain("chapterClock: {fileID: 3301000001}"));
        Assert.That(sceneText, Does.Contain("explorationMusicSource: {fileID: 2201000003}"));
        Assert.That(sceneText, Does.Contain("explorationMusicVolumeBinding: {fileID: 2201000004}"));
        Assert.That(sceneText, Does.Match(@"--- !u!114 &2201000004[\s\S]*?m_GameObject: \{fileID: 2201000001\}[\s\S]*?audioSource: \{fileID: 2201000003\}[\s\S]*?channel: 3[\s\S]*?baseVolume: 0\.125"));

        string settingsText = File.ReadAllText("Assets/Scripts/UI/RuntimeSettingsMenu.cs");
        string navigationText = File.ReadAllText("Assets/Scripts/Navigation/RoomNavigationManager.cs");
        Assert.That(settingsText, Does.Not.Contain("RuntimeSettingsMenu FindOrCreate"));
        Assert.That(settingsText, Does.Not.Contain("GetOrCreateMenuCanvas"));
        Assert.That(settingsText, Does.Not.Contain("new GameObject(MenuObjectName"));
        Assert.That(settingsText, Does.Not.Contain("GameAudioSettings.EnsureBinding(musicSource"));
        Assert.That(settingsText, Does.Not.Contain("FindAnyObjectByType"));
        Assert.That(settingsText, Does.Not.Contain("FindObjectsByType"));
        Assert.That(settingsText, Does.Not.Contain("GameObject.Find"));
        Assert.That(settingsText, Does.Not.Contain("ResolveChapterManager"));
        Assert.That(settingsText, Does.Not.Contain("ResolveChapterClock"));
        Assert.That(settingsText, Does.Not.Contain("ResolveExplorationMusicSource"));
        Assert.That(settingsText, Does.Not.Contain("EnsureEventSystem"));
        Assert.That(settingsText, Does.Not.Contain("AddComponent<RectTransform>"));
        Assert.That(settingsText, Does.Contain("public void ValidateConfiguration"));
        Assert.That(navigationText, Does.Contain("runtimeSettingsMenu.ValidateConfiguration(report)"));
        Assert.That(navigationText, Does.Not.Contain("RuntimeSettingsMenu.FindOrCreate"));
        Assert.That(navigationText, Does.Contain("runtimeSettingsMenu?.Initialize(this)"));
        Assert.That(settingsText, Does.Contain("FindOrCreateSettingsOverlay"), "Nested settings controls remain deliberate owner-scoped view construction.");
    }

    [Test]
    public void RoomAmbienceOwnersAreSerializedOnce()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(CountOccurrences(sceneText, "guid: c5d8eebb18904780a5d77a1c9da6ce6f"), Is.EqualTo(1));
        Assert.That(CountOccurrences(sceneText, "guid: 65e29c4687b6bad242fac7bcb6849828"), Is.EqualTo(1));
        Assert.That(sceneText, Does.Contain("fireplaceAmbienceController: {fileID: 2201000025}"));
        Assert.That(sceneText, Does.Contain("clockTickingAmbienceController: {fileID: 2201000034}"));
        Assert.That(sceneText, Does.Contain("catalog: {fileID: 11400000, guid: 950e4008c31a44739c468b6ccd0efd68, type: 2}"));
        Assert.That(sceneText, Does.Contain("catalog: {fileID: 11400000, guid: d1c5479f74b94514cdf7a37d49f95fbe, type: 2}"));
        Assert.That(sceneText, Does.Contain("audioSource: {fileID: 2201000023}"));
        Assert.That(sceneText, Does.Contain("highPassFilter: {fileID: 2201000024}"));
        Assert.That(sceneText, Does.Contain("audioSource: {fileID: 2201000033}"));
        Assert.That(sceneText, Does.Match(@"--- !u!4 &2201000022[\s\S]*?m_GameObject: \{fileID: 2201000021\}[\s\S]*?m_Father: \{fileID: 1878886999\}"));
        Assert.That(sceneText, Does.Match(@"--- !u!4 &2201000032[\s\S]*?m_GameObject: \{fileID: 2201000031\}[\s\S]*?m_Father: \{fileID: 1878886999\}"));

        string fireplaceText = File.ReadAllText("Assets/Scripts/Audio/FireplaceAmbienceController.cs");
        string clockText = File.ReadAllText("Assets/Scripts/Audio/ClockTickingAmbienceController.cs");
        string navigationText = File.ReadAllText("Assets/Scripts/Navigation/RoomNavigationManager.cs");
        string[] ownerTexts = { fireplaceText, clockText };

        for (int i = 0; i < ownerTexts.Length; i++)
        {
            Assert.That(ownerTexts[i], Does.Not.Contain("FindOrCreate"));
            Assert.That(ownerTexts[i], Does.Not.Contain("FindAnyObjectByType"));
            Assert.That(ownerTexts[i], Does.Not.Contain("Resources.Load"));
            Assert.That(ownerTexts[i], Does.Not.Contain("new GameObject"));
            Assert.That(ownerTexts[i], Does.Not.Contain("AddComponent<"));
            Assert.That(ownerTexts[i], Does.Not.Contain("GetComponent<"));
        }

        Assert.That(sceneText, Does.Not.Contain("catalogResourcePath: Audio/FireplaceAmbienceCatalog"));
        Assert.That(sceneText, Does.Not.Contain("catalogResourcePath: Audio/ClockTickingAmbienceCatalog"));
        Assert.That(navigationText, Does.Not.Contain("FireplaceAmbienceController.FindOrCreate"));
        Assert.That(navigationText, Does.Not.Contain("ClockTickingAmbienceController.FindOrCreate"));
        Assert.That(navigationText, Does.Contain("fireplaceAmbienceController?.Initialize(this)"));
        Assert.That(navigationText, Does.Contain("clockTickingAmbienceController?.Initialize(this)"));
    }

    private static int CountOccurrences(string text, string value)
    {
        return text.Split(new[] { value }, StringSplitOptions.None).Length - 1;
    }

    private static string ExtractDocument(string assetText, string header)
    {
        int start = assetText.IndexOf(header, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing document '{header}'.");
        int end = assetText.IndexOf("\n--- !u!", start + header.Length, StringComparison.Ordinal);
        return end >= 0 ? assetText.Substring(start, end - start) : assetText.Substring(start);
    }

    [Test]
    public void InvalidTransitionCanThrowWithUsefulMessage()
    {
        StateMachine<TestState> machine = new StateMachine<TestState>(TestState.Idle);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            machine.TransitionOrThrow(TestState.Complete));

        Assert.That(exception.Message, Does.Contain("Idle"));
        Assert.That(exception.Message, Does.Contain("Complete"));
    }
}
#endif
