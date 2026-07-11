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
