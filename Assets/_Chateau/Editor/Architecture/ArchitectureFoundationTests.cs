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
    public void ProvenDeadStarterAndPickupScriptsStayPruned()
    {
        Assert.That(File.Exists("Assets/Scripts/NewBehaviourScript.cs"), Is.False);
        Assert.That(File.Exists("Assets/Scripts/PickupObject.cs"), Is.False);
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

        Assert.That(managerText, Does.Not.Contain("BootstrapChapterManagerForGameplay"));
        Assert.That(managerText, Does.Not.Contain("ChapterManager_Runtime"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterClock>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterEventScheduler>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterIntroUI>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<Chapter1ArrivalController>"));
        Assert.That(managerText, Does.Not.Contain("managerObject.AddComponent<ChapterManager>"));
        Assert.That(managerText, Does.Contain("ResolveChapter2Controller()"));
        Assert.That(managerText, Does.Not.Contain("ResolveChapter2Controller(true)"));
        Assert.That(managerText, Does.Not.Contain("ResolveChapter2Controller(false)"));
        Assert.That(managerText, Does.Not.Contain("AddComponent<Chapter2Controller>"));

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
    public void DialogueCoreServicesAreSerializedAndBound()
    {
        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");

        Assert.That(sceneText, Does.Contain("subtitleService: {fileID: 1878886995}"));
        Assert.That(sceneText, Does.Contain("speechService: {fileID: 1878886994}"));
        Assert.That(sceneText, Does.Contain("lineBank: {fileID: 11400000, guid: 47d20ba9660546050951e9ea07a0b3da, type: 2}"));
        Assert.That(sceneText, Does.Contain("navigationManager: {fileID: 1878886997}"));

        string speechText = File.ReadAllText("Assets/Scripts/Audio/DialogueSpeechService.cs");
        string subtitleText = File.ReadAllText("Assets/Scripts/UI/SubtitleService.cs");
        string chapter1Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs");
        string chapter2Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs");
        Assert.That(speechText, Does.Not.Contain("DialogueSpeechService FindOrCreate"));
        Assert.That(subtitleText, Does.Not.Contain("SubtitleService FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("SubtitleService.FindOrCreate"));
        Assert.That(chapter1Text, Does.Not.Contain("DialogueSpeechService.FindOrCreate"));
        Assert.That(chapter1Text, Does.Not.Contain("SubtitleService.FindOrCreate"));
        Assert.That(chapter2Text, Does.Not.Contain("DialogueSpeechService.FindOrCreate"));
        Assert.That(chapter2Text, Does.Not.Contain("SubtitleService.FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("GuestVoiceLinePlayback.FindOrCreate"));
        Assert.That(speechText, Does.Not.Contain("SpeakingCharacterIndicator.FindOrCreate"));
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
        Assert.That(sceneText, Does.Contain("catalog: {fileID: 11400000, guid: 147a8473c4c849c9908200b092d13691, type: 2}"));
        Assert.That(sceneText, Does.Contain("bubbleSprite: {fileID: 21300000, guid: b40c2d5917304c3e822fad1b6f3e5960, type: 3}"));

        string playbackText = File.ReadAllText("Assets/Scripts/Audio/GuestVoiceLinePlayback.cs");
        string indicatorText = File.ReadAllText("Assets/Scripts/UI/SpeakingCharacterIndicator.cs");
        string dialogueText = File.ReadAllText("Assets/Scripts/Audio/DialogueSpeechService.cs");
        string subtitleText = File.ReadAllText("Assets/Scripts/UI/SubtitleService.cs");
        string chapter2Text = File.ReadAllText("Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs");
        Assert.That(playbackText, Does.Not.Contain("GuestVoiceLinePlayback FindOrCreate"));
        Assert.That(indicatorText, Does.Not.Contain("SpeakingCharacterIndicator FindOrCreate"));
        Assert.That(indicatorText, Does.Not.Contain("HideAnyCurrent"));
        Assert.That(dialogueText, Does.Not.Contain("StopAnyCurrentSpeech"));
        Assert.That(subtitleText, Does.Not.Contain("HideAnyCurrent"));
        Assert.That(subtitleText, Does.Not.Contain("GuestVoiceLinePlayback.StopAnyCurrentLine"));
        Assert.That(chapter2Text, Does.Not.Contain("StopAnyCurrentSpeech"));
        Assert.That(sceneText, Does.Contain("speakingIndicator: {fileID: 1878887002}"));
        Assert.That(indicatorText, Does.Contain("new GameObject(SpriteObjectName)"), "Only the indicator's nested presentation child should remain lazy.");
    }

    private static int CountOccurrences(string text, string value)
    {
        return text.Split(new[] { value }, StringSplitOptions.None).Length - 1;
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
