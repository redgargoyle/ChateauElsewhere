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
